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

        private float _pathUpdateTimer = 0f;
        private const float PATH_PREVIEW_UPDATE_DELAY = 0.05f;
        private Vector2? _lastPathTargetPosition = null;

        public MapInputHandler(ContextMenu contextMenu)
        {
            _contextMenu = contextMenu;
        }

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            _pathUpdateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

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
            if (targetPos == _gameState.PlayerWorldPos && _gameState.PendingActions.Any()) // Cancel path if clicking player
            {
                _gameState.CancelPendingActions();
                _isDraggingPath = false;
                return;
            }

            if (!_gameState.IsPositionPassable(targetPos)) return;

            Vector2 startPos = (_originalPendingActionCount > 0)
                ? _gameState.PendingActions[_originalPendingActionCount - 1].Position
                : _gameState.PlayerWorldPos;

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

            var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning, mode);

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
            bool isPassable = _gameState.IsPositionPassable(targetPos);
            bool isPlayerPos = targetPos == _gameState.PlayerWorldPos;
            bool pathPending = _gameState.PendingActions.Any();

            Action<RestType> queuePathAndRest = (restType) =>
            {
                var startPos = _gameState.PendingActions.Any() ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;

                if (startPos != targetPos)
                {
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time);
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
                string restTypeName = restType == RestType.ShortRest ? "short" : "long";
                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {restTypeName} rest at ({targetPos.X},{targetPos.Y}).");
            };

            // Option: Submit Path
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

            // Option: Walk To
            menuItems.Add(new ContextMenuItem
            {
                Text = "Walk To",
                IsVisible = () => isPassable && !isPlayerPos,
                OnClick = () =>
                {
                    var startPos = pathPending ? _gameState.PendingActions.Last().Position : _gameState.PlayerWorldPos;
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time);
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
                    var path = Pathfinder.FindPath(startPos, targetPos, _gameState, isRunning: true, PathfindingMode.Time);
                    if (path != null) _gameState.AppendPath(path, isRunning: true);
                }
            });

            // Option: Reposition (Walk)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Reposition",
                IsVisible = () => isPassable && !isPlayerPos && pathPending,
                OnClick = () =>
                {
                    var path = Pathfinder.FindPath(_gameState.PlayerWorldPos, targetPos, _gameState, isRunning: false, PathfindingMode.Time);
                    if (path != null) _gameState.QueueNewPath(path, isRunning: false);
                }
            });

            //// Option: Reposition (Run)
            //menuItems.Add(new ContextMenuItem
            //{
            //    Text = "Reposition (Run)",
            //    IsVisible = () => isPassable && !isPlayerPos && pathPending,
            //    OnClick = () =>
            //    {
            //        var path = Pathfinder.FindPath(_gameState.PlayerWorldPos, targetPos, _gameState, isRunning: true, PathfindingMode.Time);
            //        if (path != null) _gameState.QueueNewPath(path, isRunning: true);
            //    }
            //});

            // Option: Short Rest (Updated)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Short Rest",
                IsVisible = () => isPassable, // Can only rest on passable tiles.
                OnClick = () => queuePathAndRest(RestType.ShortRest)
            });



            // Option: Long Rest (Updated)
            menuItems.Add(new ContextMenuItem
            {
                Text = "Long Rest",
                IsVisible = () => isPassable, // Can only rest on passable tiles.
                OnClick = () => queuePathAndRest(RestType.LongRest)
            });

            // Option: Clear Path
            menuItems.Add(new ContextMenuItem
            {
                Text = "Clear Path",
                Color = Global.Instance.Palette_Yellow,
                IsVisible = () => pathPending, // Only show if there's a path to clear.
                OnClick = () =>
                {
                    _gameState.CancelPendingActions();
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Path cleared.");
                }
            });

            _mapRenderer.RightClickedWorldPos = targetPos;

            Vector2? menuScreenPos = _mapRenderer.WorldToScreen(targetPos);
            Vector2 finalMenuPos;

            if (menuScreenPos.HasValue)
            {
                finalMenuPos = new Vector2(// Snap to the bottom-right of the grid cell
                    menuScreenPos.Value.X + Global.GRID_CELL_SIZE,
                    menuScreenPos.Value.Y + Global.GRID_CELL_SIZE
                );
            }
            else
            {
                finalMenuPos = mousePos;
            }

            _contextMenu.Show(finalMenuPos, menuItems);
        }
    }
}