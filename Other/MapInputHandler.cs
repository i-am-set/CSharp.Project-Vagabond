using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class MapInputHandler
    {
        private readonly GameState _gameState;
        private readonly MapRenderer _mapRenderer;
        private readonly PlayerInputSystem _playerInputSystem;
        private readonly ContextMenu _contextMenu;
        private readonly Global _global;
        private readonly ComponentStore _componentStore;
        private BitmapFont _font;

        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        private bool _isDraggingPath = false;
        private bool _isAppendModeDrag = false;
        private int _originalPendingActionCount = 0;

        private float _pathUpdateTimer = 0f;
        private const float PATH_PREVIEW_UPDATE_DELAY = 0.01f;
        private Vector2? _lastPathTargetPosition = null;

        // Camera Panning State
        private bool _isPanning = false;
        private Point _panStartMousePosition;
        private Vector2 _panStartCameraOffset;

        public MapInputHandler(ContextMenu contextMenu, MapRenderer mapRenderer)
        {
            _contextMenu = contextMenu;
            _mapRenderer = mapRenderer;
            _gameState = ServiceLocator.Get<GameState>();
            _playerInputSystem = ServiceLocator.Get<PlayerInputSystem>();
            _global = ServiceLocator.Get<Global>();
            _componentStore = ServiceLocator.Get<ComponentStore>();

            _previousKeyboardState = Keyboard.GetState();

            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (Button button in _mapRenderer.HeaderButtons)
            {
                count++;
                var function = button.Function;
                if (seen.TryGetValue(function, out var firstIndex))
                {
                    throw new InvalidOperationException($"Duplicate button function '{function}' found at indices {firstIndex} and {count}.");
                }
                seen[function] = count;
            }

            count = 0;
            foreach (var button in _mapRenderer.HeaderButtons)
            {
                count++;
                switch (button.Function.ToLowerInvariant())
                {
                    case "go": button.OnClick += HandleGoClick; break;
                    case "stop": button.OnClick += HandleStopClick; break;
                    case "clear": button.OnClick += () => _playerInputSystem.CancelPendingActions(_gameState); break;
                    default: throw new InvalidOperationException($"ERROR! No click handler defined for button with function '{button.Function}' at index {count}.");
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            _font ??= ServiceLocator.Get<BitmapFont>();

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            var keyboardState = Keyboard.GetState();
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            _pathUpdateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var button in _mapRenderer.HeaderButtons)
            {
                switch (button.Function.ToLower())
                {
                    case "go": button.IsEnabled = _gameState.PendingActions.Count > 0 && !_gameState.IsExecutingActions; break;
                    case "stop": button.IsEnabled = _gameState.IsExecutingActions; break;
                    case "clear": button.IsEnabled = _gameState.PendingActions.Count > 0 && !_gameState.IsExecutingActions; break;
                }
                button.Update(_currentMouseState);
            }

            bool menuWasOpen = _contextMenu.IsOpen;
            _contextMenu.Update(_currentMouseState, _previousMouseState, virtualMousePos, _font);

            if (menuWasOpen && !_contextMenu.IsOpen)
            {
                return;
            }
            if (menuWasOpen) return;

            HandleCameraPan(virtualMousePos);

            // Don't handle other map interactions if we are currently panning the camera.
            if (!_isPanning)
            {
                HandleMapInteraction(virtualMousePos, keyboardState);
            }

            _previousKeyboardState = keyboardState;
        }

        private void HandleGoClick()
        {
            _gameState.ToggleExecutingActions(true);
        }

        private void HandleStopClick()
        {
            _gameState.CancelExecutingActions();
        }

        private void HandleCameraPan(Vector2 virtualMousePos)
        {
            bool middleClickPressed = _currentMouseState.MiddleButton == ButtonState.Pressed && _previousMouseState.MiddleButton == ButtonState.Released;
            bool middleClickHeld = _currentMouseState.MiddleButton == ButtonState.Pressed;
            bool middleClickReleased = _currentMouseState.MiddleButton == ButtonState.Released && _previousMouseState.MiddleButton == ButtonState.Pressed;

            if (middleClickPressed && _mapRenderer.MapScreenBounds.Contains(virtualMousePos))
            {
                _isPanning = true;
                _panStartMousePosition = _currentMouseState.Position;
                _panStartCameraOffset = _mapRenderer.CameraOffset;
            }

            if (middleClickReleased)
            {
                _isPanning = false;
            }

            if (_isPanning && middleClickHeld)
            {
                Vector2 virtualPanStart = Core.TransformMouse(_panStartMousePosition);
                Vector2 virtualCurrentPos = Core.TransformMouse(_currentMouseState.Position);
                Vector2 virtualDelta = virtualCurrentPos - virtualPanStart;

                // Convert the virtual pixel delta to a world grid delta.
                // The delta is negative because dragging the screen right should move the world left.
                Vector2 worldDelta = -virtualDelta / _mapRenderer.CellSize;

                _mapRenderer.SetCameraOffset(_panStartCameraOffset + worldDelta);
            }
        }

        private void HandleMapInteraction(Vector2 virtualMousePos, KeyboardState keyboardState)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            if (_gameState.IsExecutingActions) return;

            Vector2? hoveredGridPos = _mapRenderer.HoveredGridPos;

            if (hoveredGridPos.HasValue)
            {
                var targetPos = hoveredGridPos.Value;
                if (leftClickPressed)
                {
                    if (_mapRenderer.IsCameraDetached || _mapRenderer.IsZoomedOut)
                    {
                        _mapRenderer.ResetCamera();
                        _mapRenderer.ResetZoom();

                        // Recalculate the target position AFTER resetting the view
                        Vector2 currentVirtualMousePos = Core.TransformMouse(_currentMouseState.Position);
                        targetPos = _mapRenderer.ScreenToWorldGrid(currentVirtualMousePos);
                    }

                    _isDraggingPath = true;
                    _isAppendModeDrag = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
                    _originalPendingActionCount = _gameState.PendingActions.Count;
                    HandlePathUpdate(targetPos, keyboardState);
                    _pathUpdateTimer = 0f;
                    _lastPathTargetPosition = targetPos;
                }
                else if (leftClickHeld && _isDraggingPath)
                {
                    bool mouseMoved = targetPos != _lastPathTargetPosition;
                    bool altChanged = (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt)) != (_previousKeyboardState.IsKeyDown(Keys.LeftAlt) || _previousKeyboardState.IsKeyDown(Keys.RightAlt));
                    bool shiftChanged = (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)) != (_previousKeyboardState.IsKeyDown(Keys.LeftShift) || _previousKeyboardState.IsKeyDown(Keys.RightShift));
                    bool modifiersChanged = altChanged || shiftChanged;

                    if ((mouseMoved && _pathUpdateTimer >= PATH_PREVIEW_UPDATE_DELAY) || modifiersChanged)
                    {
                        HandlePathUpdate(targetPos, keyboardState);
                        _pathUpdateTimer = 0f;
                        _lastPathTargetPosition = targetPos;
                    }
                }
            }

            if (rightClickPressed)
            {
                if (_gameState.PendingActions.Any())
                {
                    _playerInputSystem.CancelPendingActions(_gameState);
                }
            }

            if (leftClickReleased)
            {
                _isDraggingPath = false;
                _lastPathTargetPosition = null;
            }
        }

        private void HandlePathUpdate(Vector2 targetPos, KeyboardState keyboardState)
        {
            Vector2 playerPos = _gameState.PlayerWorldPos;
            if (!_gameState.IsPositionPassable(targetPos, MapView.World)) return;

            bool isAltHeld = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
            var pathfindingMode = isAltHeld ? PathfindingMode.Moves : PathfindingMode.Time;
            var movementMode = MovementMode.Jog;

            if (_isAppendModeDrag)
            {
                if (_gameState.PendingActions.Count > _originalPendingActionCount)
                {
                    _playerInputSystem.RemovePendingActionsFrom(_gameState, _originalPendingActionCount);
                }
                Vector2 startPos = (_originalPendingActionCount > 0) ? (_gameState.PendingActions.Last() as MoveAction)?.Destination ?? playerPos : playerPos;
                if (startPos == targetPos) return;
                var path = Pathfinder.FindPath(_gameState.PlayerEntityId, startPos, targetPos, _gameState, movementMode, pathfindingMode);
                if (path != null) _playerInputSystem.AppendPath(_gameState, path, movementMode);
            }
            else
            {
                _playerInputSystem.ClearPendingActions(_gameState);
                if (playerPos == targetPos) return;
                var path = Pathfinder.FindPath(_gameState.PlayerEntityId, playerPos, targetPos, _gameState, movementMode, pathfindingMode);
                if (path != null) _playerInputSystem.AppendPath(_gameState, path, movementMode);
            }
        }
    }
}