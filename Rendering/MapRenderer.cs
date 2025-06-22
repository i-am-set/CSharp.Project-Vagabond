using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond
{
    #region Pathfinding System
    internal class PathfinderNode
    {
        public Vector2 Position { get; }
        public PathfinderNode Parent { get; set; }
        public float CostFromStartPoint { get; set; }
        public float EstimatedCostToEndPoint { get; set; }
        public float TotalEstimatedCost => CostFromStartPoint + EstimatedCostToEndPoint;

        public PathfinderNode(Vector2 position)
        {
            Position = position;
        }
    }

    internal static class Pathfinder
    {
        public static List<Vector2> FindPath(Vector2 start, Vector2 end, GameState gameState)
        {
            var startNode = new PathfinderNode(start);
            var endNode = new PathfinderNode(end);

            var openList = new List<PathfinderNode> { startNode };
            var closedList = new HashSet<Vector2>();

            while (openList.Count > 0)
            {
                var currentNode = openList.OrderBy(n => n.TotalEstimatedCost).First();
                openList.Remove(currentNode);
                closedList.Add(currentNode.Position);

                if (currentNode.Position == endNode.Position)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (var neighborPos in GetNeighbors(currentNode.Position))
                {
                    if (!gameState.IsPositionPassable(neighborPos) || closedList.Contains(neighborPos))
                        continue;

                    int moveCost = gameState.GetMovementEnergyCost(new PendingAction(neighborPos, isRunning: true));
                    float newGCost = currentNode.CostFromStartPoint + moveCost;

                    var neighborNode = openList.FirstOrDefault(n => n.Position == neighborPos);
                    if (neighborNode == null || newGCost < neighborNode.CostFromStartPoint)
                    {
                        if (neighborNode == null)
                        {
                            neighborNode = new PathfinderNode(neighborPos);
                            neighborNode.EstimatedCostToEndPoint = GetDistance(neighborPos, endNode.Position);
                            openList.Add(neighborNode);
                        }
                        neighborNode.CostFromStartPoint = newGCost;
                        neighborNode.Parent = currentNode;
                    }
                }
            }
            return null; // No path found
        }

        private static List<Vector2> RetracePath(PathfinderNode startNode, PathfinderNode endNode)
        {
            var path = new List<Vector2>();
            var currentNode = endNode;
            while (currentNode != startNode)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }
            path.Reverse();
            return path;
        }

        private static IEnumerable<Vector2> GetNeighbors(Vector2 pos)
        {
            yield return new Vector2(pos.X, pos.Y - 1); // Up
            yield return new Vector2(pos.X, pos.Y + 1); // Down
            yield return new Vector2(pos.X - 1, pos.Y); // Left
            yield return new Vector2(pos.X + 1, pos.Y); // Right
        }

        private static float GetDistance(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }
    #endregion

    #region Context Menu System
    public class ContextMenuItem
    {
        public string Text { get; set; }
        public Action OnClick { get; set; }
        public Func<bool> IsVisible { get; set; } = () => true;
    }

    public class ContextMenu
    {
        private List<ContextMenuItem> _allItems = new List<ContextMenuItem>();
        private List<ContextMenuItem> _visibleItems = new List<ContextMenuItem>();
        private bool _isOpen;
        private Vector2 _position;
        private Rectangle _bounds;
        private int _hoveredIndex = -1;

        public void Show(Vector2 position, List<ContextMenuItem> items)
        {
            _allItems = items;
            _visibleItems = _allItems.Where(i => i.IsVisible()).ToList();
            if (!_visibleItems.Any()) return;

            _position = position;
            _isOpen = true;
            _hoveredIndex = -1;

            float width = _visibleItems.Max(i => Global.Instance.DefaultFont.MeasureString(i.Text).Width) + 16;
            float height = (_visibleItems.Count * (Global.Instance.DefaultFont.LineHeight + 4)) + 8;
            _bounds = new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height);
        }

        public void Hide() => _isOpen = false;

        public void Update(MouseState mouseState, Vector2 virtualMousePos)
        {
            if (!_isOpen) return;

            if (mouseState.LeftButton == ButtonState.Pressed || mouseState.RightButton == ButtonState.Pressed)
            {
                if (_bounds.Contains(virtualMousePos))
                {
                    if (_hoveredIndex != -1 && mouseState.LeftButton == ButtonState.Pressed)
                    {
                        _visibleItems[_hoveredIndex].OnClick?.Invoke();
                        Hide();
                    }
                }
                else
                {
                    Hide();
                }
            }
            else
            {
                _hoveredIndex = -1;
                if (_bounds.Contains(virtualMousePos))
                {
                    float yOffset = virtualMousePos.Y - _bounds.Y - 4;
                    int itemHeight = Global.Instance.DefaultFont.LineHeight + 4;
                    int index = (int)(yOffset / itemHeight);
                    if (index >= 0 && index < _visibleItems.Count)
                    {
                        _hoveredIndex = index;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_isOpen) return;

            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            spriteBatch.Draw(pixel, _bounds, Global.Instance.ToolTipBGColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Bottom - 1, _bounds.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, 1, _bounds.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(_bounds.Right - 1, _bounds.Y, 1, _bounds.Height), Global.Instance.ToolTipBorderColor);

            float y = _bounds.Y + 4;
            for (int i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                var color = (i == _hoveredIndex) ? Global.Instance.OptionHoverColor : Global.Instance.ToolTipTextColor;
                spriteBatch.DrawString(Global.Instance.DefaultFont, item.Text, new Vector2(_bounds.X + 8, y), color);
                y += Global.Instance.DefaultFont.LineHeight + 4;
            }
        }
    }
    #endregion

    public class MapRenderer
    {
        private GameState _gameState = Core.CurrentGameState;

        private Vector2? _hoveredGridWorldPos;
        private float _hoverTimer;
        private const float HOVER_TIME_FOR_TOOLTIP = 0.5f;
        private bool _showTooltip;
        private string _tooltipText;
        private Vector2 _tooltipPosition;
        private Rectangle _mapGridBounds;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private ContextMenu _contextMenu = new ContextMenu();

        private bool _isDraggingPath = false;
        private int _originalPendingActionCount = 0;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            _contextMenu.Update(_currentMouseState, virtualMousePos);

            UpdateHover(gameTime, virtualMousePos);
            HandleMouseInput(virtualMousePos);
        }

        private void UpdateHover(GameTime gameTime, Vector2 virtualMousePos)
        {
            Vector2? currentHoveredWorldPos = null;

            if (_mapGridBounds.Contains(virtualMousePos))
            {
                int gridX = (int)((virtualMousePos.X - _mapGridBounds.X) / Global.GRID_CELL_SIZE);
                int gridY = (int)((virtualMousePos.Y - _mapGridBounds.Y) / Global.GRID_CELL_SIZE);

                if (gridX >= 0 && gridX < Global.GRID_SIZE && gridY >= 0 && gridY < Global.GRID_SIZE)
                {
                    int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
                    int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;
                    currentHoveredWorldPos = new Vector2(startX + gridX, startY + gridY);
                }
            }

            if (currentHoveredWorldPos.HasValue)
            {
                if (currentHoveredWorldPos.Equals(_hoveredGridWorldPos))
                {
                    _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_hoverTimer >= HOVER_TIME_FOR_TOOLTIP && !_showTooltip)
                    {
                        _showTooltip = true;
                        int worldX = (int)currentHoveredWorldPos.Value.X;
                        int worldY = (int)currentHoveredWorldPos.Value.Y;
                        float noise = _gameState.GetNoiseAt(worldX, worldY);
                        string terrainName = GetTerrainName(noise);

                        var stringBuilder = new StringBuilder();
                        stringBuilder.Append($"Pos: ({worldX}, {worldY})\n");
                        stringBuilder.Append($"Terrain: {terrainName}\n");
                        stringBuilder.Append($"Noise: {noise:F2}");

                        _tooltipText = stringBuilder.ToString();
                        _tooltipPosition = new Vector2(virtualMousePos.X + 15, virtualMousePos.Y);
                    }
                }
                else
                {
                    _hoveredGridWorldPos = currentHoveredWorldPos;
                    _hoverTimer = 0f;
                    _showTooltip = false;
                }
            }
            else
            {
                _hoveredGridWorldPos = null;
                _hoverTimer = 0f;
                _showTooltip = false;
            }
        }

        private void HandleMouseInput(Vector2 virtualMousePos)
        {
            bool leftClickPressed = _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = _currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = _currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightClickPressed = _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            if (_hoveredGridWorldPos.HasValue)
            {
                var targetPos = _hoveredGridWorldPos.Value;

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

        public void DrawMap()
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;

            int mapStartX = 35;
            int mapStartY = 50;
            int mapWidth = Global.MAP_WIDTH;
            int mapHeight = Global.GRID_SIZE * Global.GRID_CELL_SIZE + 30;

            // Define map grid area for hover detection //
            _mapGridBounds = new Rectangle(mapStartX, mapStartY, Global.GRID_SIZE * Global.GRID_CELL_SIZE, Global.GRID_SIZE * Global.GRID_CELL_SIZE);

            // Draw map border //
            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Border rectangle //
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), Global.Instance.Palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), Global.Instance.Palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Right

            // Draw map title //
            string posText = $"Pos: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})";
            _spriteBatch.DrawString(Global.Instance.DefaultFont, posText,
                new Vector2(mapStartX, mapStartY - 20), Global.Instance.TextColor);

            // Draw the current date and time //
            string timeText = Core.CurrentWorldClockManager.CurrentTime;
            Vector2 timeTextSize = Global.Instance.DefaultFont.MeasureString(timeText);
            Vector2 timeTextPos = new Vector2(mapStartX + mapWidth - timeTextSize.X - 15, mapStartY - 20);
            _spriteBatch.DrawString(Global.Instance.DefaultFont, timeText, timeTextPos, Global.Instance.TextColor);

            // Generate grid elements //
            var gridElements = GenerateMapGridElements(mapStartX, mapStartY);

            // Draw each grid element //
            foreach (var element in gridElements)
            {
                DrawGridElement(element);
            }

            // Draw hover indicator //
            if (_hoveredGridWorldPos.HasValue)
            {
                int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
                int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;

                int gridX = (int)_hoveredGridWorldPos.Value.X - startX;
                int gridY = (int)_hoveredGridWorldPos.Value.Y - startY;

                if (gridX >= 0 && gridX < Global.GRID_SIZE && gridY >= 0 && gridY < Global.GRID_SIZE)
                {
                    int screenX = _mapGridBounds.X + gridX * Global.GRID_CELL_SIZE;
                    int screenY = _mapGridBounds.Y + gridY * Global.GRID_CELL_SIZE;

                    Rectangle indicatorRect = new Rectangle(screenX, screenY, Global.GRID_CELL_SIZE, Global.GRID_CELL_SIZE);

                    Texture2D texture = Core.CurrentSpriteManager.MapHoverMarkerSprite;
                    _spriteBatch.Draw(texture, indicatorRect, Global.Instance.Palette_Red * 0.5f);
                }
            }

            // Draw tooltip if needed //
            if (_showTooltip)
            {
                DrawTooltip(_spriteBatch);
            }

            // Draw Context Menu on top of everything else
            _contextMenu.Draw(_spriteBatch);
        }

        private List<GridElement> GenerateMapGridElements(int mapStartX, int mapStartY)
        {
            var elements = new List<GridElement>();

            int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;

            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                for (int x = 0; x < Global.GRID_SIZE; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;

                    float noise = _gameState.GetNoiseAt(worldX, worldY);
                    Texture2D texture = GetTerrainTexture(noise);
                    Texture2D secondaryTexture = Core.CurrentSpriteManager.EmptySprite;
                    Color color = GetTerrainColor(noise);
                    Color secondaryColor = Color.White;

                    bool isPlayer = (worldX == (int)_gameState.PlayerWorldPos.X && worldY == (int)_gameState.PlayerWorldPos.Y);

                    bool isPath = false;
                    bool isPathEnd = false;
                    bool isShortRest = false;
                    bool isLongRest = false;
                    bool isRunning = false;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    var actionsAtPos = _gameState.PendingActions.Where(a => a.Position == worldPos).ToList(); // Find all actions at this position to handle overlaps (e.g., move then rest on same tile)
                    if (actionsAtPos.Any())
                    {
                        if (actionsAtPos.Any(a => a.Type == ActionType.ShortRest)) isShortRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.LongRest)) isLongRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.RunMove)) isRunning = true;

                        if (actionsAtPos.Any(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove))
                        {
                            if (actionsAtPos.Any(a => a.Type == ActionType.RunMove))
                            {
                                isRunning = true;
                            }

                            isPath = true;
                            var lastMoveAction = _gameState.PendingActions.LastOrDefault(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove);
                            if (lastMoveAction != null && lastMoveAction.Position == worldPos)
                            {
                                isPathEnd = true;
                            }
                        }
                    }

                    if (isPlayer)
                    {
                        texture = Core.CurrentSpriteManager.PlayerSprite;
                        color = Global.Instance.PlayerColor;
                    }
                    else if (isPath && !isShortRest && !isLongRest) // Don't draw path if a rest is here
                    {
                        if (!isRunning)
                        {
                            texture = Core.CurrentSpriteManager.PathSprite;
                            color = isPathEnd ? Global.Instance.PathEndColor : Global.Instance.PathColor;
                        }
                        else
                        {
                            texture = Core.CurrentSpriteManager.RunPathSprite;
                            color = isPathEnd ? Global.Instance.PathEndColor : Global.Instance.RunPathColor;
                        }
                    }

                    if (isShortRest)
                    {
                        texture = Core.CurrentSpriteManager.ShortRestSprite;
                        color = Global.Instance.ShortRestColor;
                    }
                    else if (isLongRest)
                    {
                        texture = Core.CurrentSpriteManager.LongRestSprite;
                        color = Global.Instance.LongRestColor;
                    }

                    Vector2 gridPos = new Vector2(mapStartX + x * Global.GRID_CELL_SIZE, mapStartY + y * Global.GRID_CELL_SIZE);

                    elements.Add(new GridElement(texture, color, gridPos));
                    elements.Add(new GridElement(secondaryTexture, secondaryColor, gridPos));
                }
            }

            return elements;
        }

        private void DrawGridElement(GridElement element)
        {
            Rectangle destRect = new Rectangle(
                (int)element.Position.X,
                (int)element.Position.Y,
                Global.GRID_CELL_SIZE,
                Global.GRID_CELL_SIZE
            );

            Global.Instance.CurrentSpriteBatch.Draw(element.Texture, destRect, element.Color);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(_tooltipText)) return;

            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(_tooltipText);

            const int paddingX = 8;
            const int paddingY = 4;
            float tooltipWidth = textSize.X + paddingX;
            float tooltipHeight = textSize.Y + paddingY;

            Vector2 finalTopLeftPosition;

            if (_tooltipPosition.Y > _mapGridBounds.Y + _mapGridBounds.Height / 2)
            {
                finalTopLeftPosition = new Vector2(
                    _tooltipPosition.X,
                    _tooltipPosition.Y - tooltipHeight - 5
                );
            }
            else
            {
                finalTopLeftPosition = new Vector2(
                    _tooltipPosition.X,
                    _tooltipPosition.Y + 15
                );
            }

            Rectangle tooltipBg = new Rectangle(
                (int)finalTopLeftPosition.X,
                (int)finalTopLeftPosition.Y,
                (int)tooltipWidth,
                (int)tooltipHeight
            );

            Vector2 textPosition = new Vector2(
                finalTopLeftPosition.X + (paddingX / 2),
                finalTopLeftPosition.Y + (paddingY / 2)
            );

            spriteBatch.Draw(pixel, tooltipBg, Global.Instance.ToolTipBGColor * 0.8f);

            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);

            spriteBatch.DrawString(Global.Instance.DefaultFont, _tooltipText, textPosition, Global.Instance.ToolTipTextColor);
        }

        private string GetTerrainName(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return "Water";
            if (noise < Global.Instance.FlatlandsLevel) return "Flatlands";
            if (noise < Global.Instance.HillsLevel) return "Hills";
            if (noise < Global.Instance.MountainsLevel) return "Mountains";
            return "Peak";
        }

        private Texture2D GetTerrainTexture(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return Core.CurrentSpriteManager.WaterSprite;
            if (noise < Global.Instance.FlatlandsLevel) return Core.CurrentSpriteManager.FlatlandSprite;
            if (noise < Global.Instance.HillsLevel) return Core.CurrentSpriteManager.HillSprite;
            if (noise < Global.Instance.MountainsLevel) return Core.CurrentSpriteManager.MountainSprite;
            return Core.CurrentSpriteManager.PeakSprite;
        }

        private Color GetTerrainColor(float noise)
        {
            if (noise < Global.Instance.WaterLevel) return Global.Instance.WaterColor;
            if (noise < Global.Instance.FlatlandsLevel) return Global.Instance.FlatlandColor;
            if (noise < Global.Instance.HillsLevel) return Global.Instance.HillColor;
            if (noise < Global.Instance.MountainsLevel) return Global.Instance.MountainColor;
            return Global.Instance.MountainColor;
        }
    }
}