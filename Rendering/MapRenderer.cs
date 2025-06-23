using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond
{
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
        private ContextMenu _contextMenu = new ContextMenu();

        public Vector2? HoveredGridWorldPos => _hoveredGridWorldPos;
        public ContextMenu MapContextMenu => _contextMenu;
        public Vector2? RightClickedWorldPos { get; set; }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void Update(GameTime gameTime)
        {
            UpdateHover(gameTime);
        }

        private void UpdateHover(GameTime gameTime)
        {
            Vector2 virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
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

            // Draw divider line //
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 5, mapWidth, 2), Global.Instance.Palette_White);

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
                Vector2? screenPos = WorldToScreen(_hoveredGridWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, Global.GRID_CELL_SIZE, Global.GRID_CELL_SIZE);
                    Texture2D texture = Core.CurrentSpriteManager.MapHoverMarkerSprite;
                    _spriteBatch.Draw(texture, indicatorRect, Color.Lime * 0.5f);
                }
            }

            // Draw right-click marker //
            if (RightClickedWorldPos.HasValue)
            {
                Vector2? screenPos = WorldToScreen(RightClickedWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle markerRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, Global.GRID_CELL_SIZE, Global.GRID_CELL_SIZE);
                    Texture2D texture = Core.CurrentSpriteManager.MapHoverMarkerSprite;
                    _spriteBatch.Draw(texture, markerRect, Color.Cyan * 0.6f);
                }
            }

            // Draw Context Menu on top of everything else //
            _contextMenu.Draw(_spriteBatch);

            // Draw tooltip if needed //
            if (_showTooltip)
            {
                DrawTooltip(_spriteBatch);
            }

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
                    Color color = GetTerrainColor(noise);

                    bool isPlayer = (worldX == (int)_gameState.PlayerWorldPos.X && worldY == (int)_gameState.PlayerWorldPos.Y);

                    bool isPath = false;
                    bool isPathEnd = false;
                    bool isShortRest = false;
                    bool isLongRest = false;
                    bool isRunning = false;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    var actionsAtPos = _gameState.PendingActions.Where(a => a.Position == worldPos).ToList();
                    if (actionsAtPos.Any())
                    {
                        if (actionsAtPos.Any(a => a.Type == ActionType.ShortRest)) isShortRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.LongRest)) isLongRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.RunMove)) isRunning = true;

                        if (actionsAtPos.Any(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove))
                        {
                            isRunning = actionsAtPos.Any(a => a.Type == ActionType.RunMove);
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
                    else if (isPath && !isShortRest && !isLongRest)
                    {
                        texture = isRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;
                        if (isPathEnd)
                        {
                            color = Global.Instance.PathEndColor;
                        }
                        else
                        {
                            color = isRunning ? Global.Instance.RunPathColor : Global.Instance.PathColor;
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
                finalTopLeftPosition = new Vector2(_tooltipPosition.X, _tooltipPosition.Y - tooltipHeight - 5);
            }
            else
            {
                finalTopLeftPosition = new Vector2(_tooltipPosition.X, _tooltipPosition.Y + 15);
            }

            Rectangle tooltipBg = new Rectangle((int)finalTopLeftPosition.X, (int)finalTopLeftPosition.Y, (int)tooltipWidth, (int)tooltipHeight);
            Vector2 textPosition = new Vector2(finalTopLeftPosition.X + (paddingX / 2), finalTopLeftPosition.Y + (paddingY / 2));

            spriteBatch.Draw(pixel, tooltipBg, Global.Instance.ToolTipBGColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.DrawString(Global.Instance.DefaultFont, _tooltipText, textPosition, Global.Instance.ToolTipTextColor);
        }

        public Vector2? WorldToScreen(Vector2 worldPos)
        {
            if (_mapGridBounds.IsEmpty) return null;

            int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;

            int gridX = (int)worldPos.X - startX;
            int gridY = (int)worldPos.Y - startY;

            if (gridX >= 0 && gridX < Global.GRID_SIZE && gridY >= 0 && gridY < Global.GRID_SIZE)
            {
                int screenX = _mapGridBounds.X + gridX * Global.GRID_CELL_SIZE;
                int screenY = _mapGridBounds.Y + gridY * Global.GRID_CELL_SIZE;
                return new Vector2(screenX, screenY);
            }

            return null;
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