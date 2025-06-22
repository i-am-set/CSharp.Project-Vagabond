using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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

        public MapInputHandler(ContextMenu contextMenu)
        {
            _contextMenu = contextMenu;
        }

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState(); // Get keyboard state for shift-clicks
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

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

        private void HandleMapInteraction(Vector2 virtualMousePos, KeyboardState keyboardState)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            Vector2? hoveredGridWorldPos = _mapRenderer.HoveredGridWorldPos;

            if (hoveredGridWorldPos.HasValue)
            {
                var targetPos = hoveredGridWorldPos.Value;

                if (leftClickPressed)
                {
                    _isDraggingPath = true;
                    _originalPendingActionCount = _gameState.PendingActions.Count;
                    HandlePathUpdate(targetPos, keyboardState);
                }
                else if (leftClickHeld && _isDraggingPath)
                {
                    HandlePathUpdate(targetPos, keyboardState);
                }

                if (rightClickPressed)
                {
                    HandleRightClickOnMap(targetPos, virtualMousePos);
                }
            }

            if (leftClickReleased)
            {
                _isDraggingPath = false;
            }
        }

        private void HandlePathUpdate(Vector2 targetPos, KeyboardState keyboardState)
        {
            // Cancel path if clicking player
            if (targetPos == _gameState.PlayerWorldPos && _gameState.PendingActions.Any())
            {
                _gameState.CancelPendingActions();
                Core.CurrentTerminalRenderer.AddOutputToHistory("Path cancelled.");
                _isDraggingPath = false;
                return;
            }

            if (!_gameState.IsPositionPassable(targetPos)) return;

            // Determine start position for pathfinding
            Vector2 startPos = (_originalPendingActionCount > 0)
                ? _gameState.PendingActions[_originalPendingActionCount - 1].Position
                : _gameState.PlayerWorldPos;

            // Don't find a path to the same spot
            if (startPos == targetPos)
            {
                // Clear any path segments added during this drag
                if (_gameState.PendingActions.Count > _originalPendingActionCount)
                {
                    _gameState.PendingActions.RemoveRange(_originalPendingActionCount, _gameState.PendingActions.Count - _originalPendingActionCount);
                }
                return;
            }

            var path = Pathfinder.FindPath(startPos, targetPos, _gameState);

            // Clear any path segments added during this drag
            if (_gameState.PendingActions.Count > _originalPendingActionCount)
            {
                _gameState.PendingActions.RemoveRange(_originalPendingActionCount, _gameState.PendingActions.Count - _originalPendingActionCount);
            }

            // Add the new path segment
            if (path != null)
            {
                bool isRunning = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
                _gameState.AppendPath(path, isRunning: isRunning);
            }
        }

        private void HandleRightClickOnMap(Vector2 targetPos, Vector2 mousePos)
        {
            var menuItems = new List<ContextMenuItem>();
            bool isPassable = _gameState.IsPositionPassable(targetPos);
            bool isPlayerPos = targetPos == _gameState.PlayerWorldPos;
            bool pathPending = _gameState.PendingActions.Any();

            // Helper action to queue a path and then a rest action.
            Action<RestType> queuePathAndRest = (restType) =>
            {
                var startPos = _gameState.PendingActions.Any() ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;

                // If we are not already at the target position, find and queue a path there.
                if (startPos != targetPos)
                {
                    // We'll queue a walk path to the rest location.
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState);
                    if (path != null)
                    {
                        _gameState.AppendPath(path, isRunning: false);
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot find a path to ({targetPos.X},{targetPos.Y}).");
                        return; // Exit the OnClick action if no path is found.
                    }
                }

                // Now, queue the rest action itself at the target position.
                _gameState.PendingActions.Add(new PendingAction(restType, targetPos));
                string restTypeName = restType == RestType.ShortRest ? "short" : "long";
                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {restTypeName} rest at ({targetPos.X},{targetPos.Y}).");
            };

            // Option: Walk To
            menuItems.Add(new ContextMenuItem
            {
                Text = "Walk To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState);
                    if (path != null) _gameState.AppendPath(path, isRunning: false);
                }
            });

            // Option: Run To
            menuItems.Add(new ContextMenuItem
            {
                Text = "Run To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState);
                    if (path != null) _gameState.AppendPath(path, isRunning: true);
                }
            });

            // Option: Reposition (Walk)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Reposition (Walk)",
                IsVisible = () => isPassable && !isPlayerPos && pathPending,
                OnClick = () =>
                {
                    var path = Pathfinder.FindPath(_gameState.PlayerWorldPos, targetPos, _gameState);
                    if (path != null) _gameState.QueueNewPath(path, isRunning: false);
                }
            });

            // Option: Reposition (Run)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Reposition (Run)",
                IsVisible = () => isPassable && !isPlayerPos && pathPending,
                OnClick = () =>
                {
                    var path = Pathfinder.FindPath(_gameState.PlayerWorldPos, targetPos, _gameState);
                    if (path != null) _gameState.QueueNewPath(path, isRunning: true);
                }
            });

            // Option: Short Rest (Updated)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Queue Short Rest",
                IsVisible = () => isPassable, // Can only rest on passable tiles.
                OnClick = () => queuePathAndRest(RestType.ShortRest)
            });

            // Option: Long Rest (Updated)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Queue Long Rest",
                IsVisible = () => isPassable, // Can only rest on passable tiles.
                OnClick = () => queuePathAndRest(RestType.LongRest)
            });

            // Option: Clear Path
            menuItems.Add(new ContextMenuItem
            {
                Text = "Clear Path",
                IsVisible = () => pathPending, // Only show if there's a path to clear.
                OnClick = () =>
                {
                    _gameState.CancelPendingActions();
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Path cleared.");
                }
            });

            // Set the marker for the right-clicked tile
            _mapRenderer.RightClickedWorldPos = targetPos;

            // Get the screen position of the tile to snap the menu to it
            Vector2? menuScreenPos = _mapRenderer.WorldToScreen(targetPos);
            Vector2 finalMenuPos;

            if (menuScreenPos.HasValue)
            {
                // Snap to the bottom-right of the grid cell
                finalMenuPos = new Vector2(
                    menuScreenPos.Value.X + Global.GRID_CELL_SIZE,
                    menuScreenPos.Value.Y + Global.GRID_CELL_SIZE
                );
            }
            else
            {
                // Fallback to the current mouse position if the tile isn't on screen
                finalMenuPos = mousePos;
            }

            _contextMenu.Show(finalMenuPos, menuItems);
        }
    }
}