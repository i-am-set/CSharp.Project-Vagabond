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
                _mapRenderer.RightClickedWorldPos = null;
                return;
            }
            if (menuWasOpen) return;

            _mapRenderer.RightClickedWorldPos = null;
            HandleMapInteraction(virtualMousePos, keyboardState);
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

        private void HandleMapInteraction(Vector2 virtualMousePos, KeyboardState keyboardState)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            if (_gameState.IsExecutingActions || _gameState.IsInCombat) return;

            Vector2? hoveredGridPos = _mapRenderer.HoveredGridPos;

            if (hoveredGridPos.HasValue)
            {
                var targetPos = hoveredGridPos.Value;
                if (leftClickPressed)
                {
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
                if (rightClickPressed)
                {
                    int? entityIdOnTile = _gameState.GetEntityIdAtGridPos(targetPos);
                    if (entityIdOnTile.HasValue && entityIdOnTile.Value != _gameState.PlayerEntityId)
                    {
                        HandleRightClickOnEntity(entityIdOnTile.Value, targetPos, virtualMousePos);
                    }
                    else
                    {
                        HandleRightClickOnTile(targetPos, virtualMousePos);
                    }
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
            bool isShiftHeld = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            var pathfindingMode = isAltHeld ? PathfindingMode.Moves : PathfindingMode.Time;
            var movementMode = isShiftHeld ? MovementMode.Run : MovementMode.Jog;

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

        private void HandleRightClickOnEntity(int targetId, Vector2 targetPos, Vector2 mousePos)
        {
            var menuItems = new List<ContextMenuItem>();
            // The "Attack" option has been removed.
            // The context menu will now be empty for entities, but the method is kept for future expansion.
            ShowContextMenu(targetPos, mousePos, menuItems);
        }

        private void HandleRightClickOnTile(Vector2 targetPos, Vector2 mousePos)
        {
            var menuItems = new List<ContextMenuItem>();
            bool isPassable = _gameState.IsPositionPassable(targetPos, MapView.World);
            bool isPlayerPos = targetPos == _gameState.PlayerWorldPos;
            bool pathPending = _gameState.PendingActions.Any();

            Action<RestType> queuePathAndRest = (restType) =>
            {
                var lastAction = _gameState.PendingActions.LastOrDefault();
                Vector2 startPos;
                if (lastAction is MoveAction lastMove) startPos = lastMove.Destination;
                else if (lastAction is RestAction lastRest) startPos = lastRest.Position;
                else startPos = _gameState.PlayerWorldPos;

                if (startPos != targetPos)
                {
                    var path = Pathfinder.FindPath(_gameState.PlayerEntityId, startPos, targetPos, _gameState, MovementMode.Jog, PathfindingMode.Time);
                    if (path != null) _playerInputSystem.AppendPath(_gameState, path, MovementMode.Jog);
                    else
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Cannot find a path to ({targetPos.X},{targetPos.Y})." });
                        return;
                    }
                }
                _playerInputSystem.QueueAction(_gameState, new RestAction(_gameState.PlayerEntityId, restType, targetPos));
                string restTypeName = restType.ToString().Replace("Rest", "").ToLower();
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Queued a {restTypeName} rest at ({targetPos.X},{targetPos.Y})." });
            };

            menuItems.Add(new ContextMenuItem { Text = "Submit Path", IsVisible = () => pathPending && !_gameState.IsExecutingActions, OnClick = () => _gameState.ToggleExecutingActions(true) });
            menuItems.Add(new ContextMenuItem
            {
                Text = "Walk To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var lastAction = _gameState.PendingActions.LastOrDefault();
                    Vector2 startPos;
                    if (lastAction is MoveAction lastMove) startPos = lastMove.Destination;
                    else if (lastAction is RestAction lastRest) startPos = lastRest.Position;
                    else startPos = _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(_gameState.PlayerEntityId, startPos, targetPos, _gameState, MovementMode.Walk, PathfindingMode.Time);
                    if (path != null) _playerInputSystem.AppendPath(_gameState, path, MovementMode.Walk);
                }
            });
            menuItems.Add(new ContextMenuItem
            {
                Text = "Jog To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var lastAction = _gameState.PendingActions.LastOrDefault();
                    Vector2 startPos;
                    if (lastAction is MoveAction lastMove) startPos = lastMove.Destination;
                    else if (lastAction is RestAction lastRest) startPos = lastRest.Position;
                    else startPos = _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(_gameState.PlayerEntityId, startPos, targetPos, _gameState, MovementMode.Jog, PathfindingMode.Time);
                    if (path != null) _playerInputSystem.AppendPath(_gameState, path, MovementMode.Jog);
                }
            });
            menuItems.Add(new ContextMenuItem
            {
                Text = "Run To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var lastAction = _gameState.PendingActions.LastOrDefault();
                    Vector2 startPos;
                    if (lastAction is MoveAction lastMove) startPos = lastMove.Destination;
                    else if (lastAction is RestAction lastRest) startPos = lastRest.Position;
                    else startPos = _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(_gameState.PlayerEntityId, startPos, targetPos, _gameState, MovementMode.Run, PathfindingMode.Time);
                    if (path != null) _playerInputSystem.AppendPath(_gameState, path, MovementMode.Run);
                }
            });
            menuItems.Add(new ContextMenuItem { Text = "Short Rest", IsVisible = () => isPassable, OnClick = () => queuePathAndRest(RestType.ShortRest) });
            menuItems.Add(new ContextMenuItem { Text = "Long Rest", IsVisible = () => isPassable, OnClick = () => queuePathAndRest(RestType.LongRest) });
            menuItems.Add(new ContextMenuItem { Text = "Clear Path", Color = _global.Palette_Yellow, IsVisible = () => pathPending, OnClick = () => _playerInputSystem.CancelPendingActions(_gameState) });

            ShowContextMenu(targetPos, mousePos, menuItems);
        }

        private void ShowContextMenu(Vector2 targetPos, Vector2 mousePos, List<ContextMenuItem> menuItems)
        {
            _mapRenderer.RightClickedWorldPos = targetPos;

            Vector2? menuScreenPos = _mapRenderer.MapCoordsToScreen(targetPos);
            Vector2 finalMenuPos;
            int cellSize = Global.GRID_CELL_SIZE;

            if (menuScreenPos.HasValue) finalMenuPos = new Vector2(menuScreenPos.Value.X + cellSize, menuScreenPos.Value.Y + cellSize);
            else finalMenuPos = mousePos;

            _contextMenu.Show(finalMenuPos, menuItems, _font);
        }
    }
}