﻿﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class MapInputHandler
    {
        private GameState _gameState = Core.CurrentGameState;
        private MapRenderer _mapRenderer = Core.CurrentMapRenderer;
        private ContextMenu _contextMenu;

        private MouseState _currentMouseState;
        private MouseState _previousMouseState;

        private bool _isDraggingPath = false;
        private int _originalPendingActionCount = 0;

        private float _pathUpdateTimer = 0f;
        private const float PATH_PREVIEW_UPDATE_DELAY = 0.025f;
        private Vector2? _lastPathTargetPosition = null;

        public MapInputHandler(ContextMenu contextMenu, MapRenderer mapRenderer)
        {
            _contextMenu = contextMenu;
            _mapRenderer = mapRenderer;

            // make sure no two buttons share the same Function
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (Button button in _mapRenderer.HeaderButtons)
            {
                count++;
                var function = button.Function;
                if (seen.TryGetValue(function, out var firstIndex))
                {
                    throw new InvalidOperationException(
                        $"Duplicate button function '{function}' found at indices {firstIndex} and {count}."
                    );
                }
                seen[function] = count;
            }

            // wire up each buttons OnClick and throw if we miss one
            count = 0;
            foreach (var button in _mapRenderer.HeaderButtons)
            {
                count++;
                switch (button.Function.ToLowerInvariant())
                {
                    case "go":
                        button.OnClick += HandleGoClick;
                        break;
                    case "stop":
                        button.OnClick += HandleStopClick;
                        break;
                    case "map":
                        button.OnClick += HandleToggleMapClick;
                        break;
                    case "clear":
                        button.OnClick += _gameState.CancelPendingActions;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"ERROR! No click handler defined for button with function '{button.Function}' at index {count}."
                        );
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            _pathUpdateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var button in _mapRenderer.HeaderButtons)
            {
                switch (button.Function.ToLower())
                {
                    case "go":
                        button.IsEnabled = _gameState.PendingActions.Count > 0 && !_gameState.IsExecutingPath;
                        break;
                    case "stop":
                        button.IsEnabled = _gameState.IsExecutingPath;
                        break;
                    case "clear":
                        button.IsEnabled = _gameState.PendingActions.Count > 0 && !_gameState.IsExecutingPath;
                        break;
                }
                button.Update(_currentMouseState);
            }

            bool menuWasOpen = _contextMenu.IsOpen;
            _contextMenu.Update(_currentMouseState, _previousMouseState, virtualMousePos);

            if (menuWasOpen)
            {
                if (!_contextMenu.IsOpen)
                {
                    _mapRenderer.RightClickedWorldPos = null;
                }
                return;
            }

            _mapRenderer.RightClickedWorldPos = null;

            HandleMapInteraction(virtualMousePos, keyboardState);
        }

        private void HandleGoClick()
        {
            _gameState.ToggleExecutingPath(true);
            Core.CurrentTerminalRenderer.AddOutputToHistory($"Executing queue of {_gameState.PendingActions.Count} action(s)...");
        }

        private void HandleStopClick()
        {
            _gameState.CancelPathExecution();
        }

        private void HandleToggleMapClick()
        {
            _gameState.ToggleMapView();
        }

        private void HandleMapInteraction(Vector2 virtualMousePos, KeyboardState keyboardState)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            Vector2? hoveredGridPos = _mapRenderer.HoveredGridPos;

            if (hoveredGridPos.HasValue)
            {
                var targetPos = hoveredGridPos.Value;

                if (leftClickPressed)
                {
                    _isDraggingPath = true;
                    _originalPendingActionCount = _gameState.PendingActions.Count;
                    HandlePathUpdate(targetPos, keyboardState);
                    _pathUpdateTimer = 0f;
                    _lastPathTargetPosition = targetPos;
                }
                else if (leftClickHeld && _isDraggingPath)
                {
                    if (targetPos != _lastPathTargetPosition && _pathUpdateTimer >= PATH_PREVIEW_UPDATE_DELAY)
                    {
                        HandlePathUpdate(targetPos, keyboardState);
                        _pathUpdateTimer = 0f;
                        _lastPathTargetPosition = targetPos;
                    }
                }

                if (rightClickPressed)
                {
                    HandleRightClickOnMap(targetPos, virtualMousePos);
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
            Vector2 playerPos = _gameState.CurrentMapView == MapView.World ? _gameState.PlayerWorldPos : _gameState.PlayerLocalPos;
            if (targetPos == playerPos && _gameState.PendingActions.Any())
            {
                _gameState.CancelPendingActions();
                _isDraggingPath = false;
                return;
            }

            if (!_gameState.IsPositionPassable(targetPos, _gameState.CurrentMapView)) return;

            bool isAltHeld = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);

            if (!isAltHeld)
            {
                int existingIndex = -1;
                for (int i = 0; i < _originalPendingActionCount; i++)
                {
                    if (i < _gameState.PendingActions.Count && _gameState.PendingActions[i].Position == targetPos)
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex != -1)
                {
                    int newCount = existingIndex + 1;
                    if (_gameState.PendingActions.Count > newCount)
                    {
                        _gameState.PendingActions.RemoveRange(newCount, _gameState.PendingActions.Count - newCount);
                    }
                    _originalPendingActionCount = _gameState.PendingActions.Count;
                }
            }

            Vector2 startPos = (_originalPendingActionCount > 0)
                ? _gameState.PendingActions[_originalPendingActionCount - 1].Position
                : playerPos;

            if (startPos == targetPos)
            {
                if (_gameState.PendingActions.Count > _originalPendingActionCount)
                {
                    _gameState.PendingActions.RemoveRange(_originalPendingActionCount, _gameState.PendingActions.Count - _originalPendingActionCount);
                }
                return;
            }

            bool isRunning = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            bool isCtrlHeld = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            var mode = isCtrlHeld ? PathfindingMode.Moves : PathfindingMode.Time;

            var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning, mode, _gameState.CurrentMapView);

            if (_gameState.PendingActions.Count > _originalPendingActionCount)
            {
                _gameState.PendingActions.RemoveRange(_originalPendingActionCount, _gameState.PendingActions.Count - _originalPendingActionCount);
            }

            if (path != null)
            {
                _gameState.AppendPath(path, isRunning: isRunning);
            }
        }

        private void HandleRightClickOnMap(Vector2 targetPos, Vector2 mousePos)
        {
            var menuItems = new List<ContextMenuItem>();
            bool isPassable = _gameState.IsPositionPassable(targetPos, _gameState.CurrentMapView);
            bool isPlayerPos = targetPos == (_gameState.CurrentMapView == MapView.World ? _gameState.PlayerWorldPos : _gameState.PlayerLocalPos);
            bool pathPending = _gameState.PendingActions.Any();
            bool isWorldMap = _gameState.CurrentMapView == MapView.World;

            Action<RestType> queuePathAndRest = (restType) =>
            {
                if (!isWorldMap)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory("[error]Cannot queue rests on the local map.");
                    return;
                }

                var startPos = _gameState.PendingActions.Any() ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;

                if (startPos != targetPos)
                {
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time, MapView.World);
                    if (path != null)
                    {
                        _gameState.AppendPath(path, isRunning: false);
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot find a path to ({targetPos.X},{targetPos.Y}).");
                        return;
                    }
                }

                _gameState.PendingActions.Add(new PendingAction(restType, targetPos));
                string restTypeName = restType.ToString().Replace("Rest", "").ToLower();
                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {restTypeName} rest at ({targetPos.X},{targetPos.Y}).");
            };

            menuItems.Add(new ContextMenuItem
            {
                Text = "Submit Path",
                IsVisible = () => pathPending && !_gameState.IsExecutingPath,
                OnClick = () =>
                {
                    _gameState.ToggleExecutingPath(true);
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"Executing queue of[undo] {Core.CurrentGameState.PendingActions.Count}[gray] action(s)...");
                }
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Walk To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : (_gameState.CurrentMapView == MapView.World ? _gameState.PlayerWorldPos : _gameState.PlayerLocalPos);
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time, _gameState.CurrentMapView);
                    if (path != null) _gameState.AppendPath(path, isRunning: false);
                }
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Run To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : (_gameState.CurrentMapView == MapView.World ? _gameState.PlayerWorldPos : _gameState.PlayerLocalPos);
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: true, PathfindingMode.Time, _gameState.CurrentMapView);
                    if (path != null) _gameState.AppendPath(path, isRunning: true);
                }
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Reposition",
                IsVisible = () => isPassable && !isPlayerPos && pathPending,
                OnClick = () =>
                {
                    var startPos = _gameState.CurrentMapView == MapView.World ? _gameState.PlayerWorldPos : _gameState.PlayerLocalPos;
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time, _gameState.CurrentMapView);
                    if (path != null) _gameState.QueueNewPath(path, isRunning: false);
                }
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Short Rest",
                IsVisible = () => isPassable && isWorldMap,
                OnClick = () => queuePathAndRest(RestType.ShortRest)
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Long Rest",
                IsVisible = () => isPassable && isWorldMap,
                OnClick = () => queuePathAndRest(RestType.LongRest)
            });

            menuItems.Add(new ContextMenuItem
            {
                Text = "Clear Path",
                Color = Global.Instance.Palette_Yellow,
                IsVisible = () => pathPending,
                OnClick = () =>
                {
                    _gameState.CancelPendingActions();
                }
            });

            if (isWorldMap) _mapRenderer.RightClickedWorldPos = targetPos;

            Vector2? menuScreenPos = _mapRenderer.MapCoordsToScreen(targetPos);
            Vector2 finalMenuPos;
            int cellSize = _gameState.CurrentMapView == MapView.World ? Global.GRID_CELL_SIZE : 5;

            if (menuScreenPos.HasValue)
            {
                finalMenuPos = new Vector2(menuScreenPos.Value.X + cellSize, menuScreenPos.Value.Y + cellSize);
            }
            else
            {
                finalMenuPos = mousePos;
            }

            _contextMenu.Show(finalMenuPos, menuItems);
        }
    }
}