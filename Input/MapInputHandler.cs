using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            // Update the context menu first, as it might consume input
            _contextMenu.Update(_currentMouseState, _previousMouseState, virtualMousePos);

            HandleMapInteraction(virtualMousePos);
        }

        private void HandleMapInteraction(Vector2 virtualMousePos)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            // We get the hovered position from the MapRenderer
            Vector2? hoveredGridWorldPos = _mapRenderer.HoveredGridWorldPos;

            if (hoveredGridWorldPos.HasValue)
            {
                var targetPos = hoveredGridWorldPos.Value;

                if (leftClickPressed)
                {
                    _isDraggingPath = true;
                    _originalPendingActionCount = _gameState.PendingActions.Count;
                    HandlePathUpdate(targetPos);
                }
                else if (leftClickHeld && _isDraggingPath)
                {
                    HandlePathUpdate(targetPos);
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

        private void HandlePathUpdate(Vector2 targetPos)
        {
            // Cancel path if clicking player
            if (targetPos == _gameState.PlayerWorldPos && _gameState.PendingActions.Any())
            {
                _gameState.CancelPendingActions();
                Core.CurrentTerminalRenderer.AddOutputToHistory("Path cancelled.");
                _isDraggingPath = false; // Stop dragging
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
                _gameState.AppendPath(path, isRunning: true);
            }
        }

        private void HandleRightClickOnMap(Vector2 targetPos, Vector2 mousePos)
        {
            var menuItems = new List<ContextMenuItem>();
            bool isPassable = _gameState.IsPositionPassable(targetPos);
            bool isPlayerPos = targetPos == _gameState.PlayerWorldPos;
            bool pathPending = _gameState.PendingActions.Any();

            // Option: Move To
            menuItems.Add(new ContextMenuItem
            {
                Text = "Move To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState);
                    if (path != null) _gameState.AppendPath(path, true);
                }
            });

            // Option: Reposition
            menuItems.Add(new ContextMenuItem
            {
                Text = "Reposition",
                IsVisible = () => isPassable && !isPlayerPos && pathPending,
                OnClick = () =>
                {
                    var path = Pathfinder.FindPath(_gameState.PlayerWorldPos, targetPos, _gameState);
                    if (path != null) _gameState.QueueNewPath(path, true);
                }
            });

            // Option: Short Rest
            menuItems.Add(new ContextMenuItem
            {
                Text = "Queue Short Rest",
                OnClick = () => _gameState.PendingActions.Add(new PendingAction(RestType.ShortRest, pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos))
            });

            // Option: Long Rest
            menuItems.Add(new ContextMenuItem
            {
                Text = "Queue Long Rest",
                OnClick = () => _gameState.PendingActions.Add(new PendingAction(RestType.LongRest, pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos))
            });

            _contextMenu.Show(mousePos, menuItems);
        }
    }
}