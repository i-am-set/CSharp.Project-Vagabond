﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond
{
    public class MapRenderer
    {
        private GameState _gameState = Core.CurrentGameState;

        private Vector2? _hoveredGridPos;
        private float _hoverTimer;
        private const float HOVER_TIME_FOR_TOOLTIP = 0.5f;
        private bool _showTooltip;
        private string _tooltipText;
        private Vector2 _tooltipPosition;
        private Rectangle _mapGridBounds;
        private ContextMenu _contextMenu = new ContextMenu();

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;
        public Vector2? RightClickedWorldPos { get; set; }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        public MapRenderer()
        {
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "World Map", "map"));
        }

        public void Update(GameTime gameTime)
        {
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            UpdateHover(gameTime, virtualMousePos);
        }

        private void UpdateHover(GameTime gameTime, Vector2 virtualMousePos)
        {
            Vector2? currentHoveredPos = null;
            int cellSize = _gameState.CurrentMapView == MapView.World ? Global.GRID_CELL_SIZE : Global.LOCAL_GRID_CELL_SIZE;
            int gridSize = _gameState.CurrentMapView == MapView.World ? Global.GRID_SIZE : Global.LOCAL_GRID_SIZE;

            if (_mapGridBounds.Contains(virtualMousePos))
            {
                int gridX = (int)((virtualMousePos.X - _mapGridBounds.X) / cellSize);
                int gridY = (int)((virtualMousePos.Y - _mapGridBounds.Y) / cellSize);

                if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
                {
                    if (_gameState.CurrentMapView == MapView.World)
                    {
                        int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
                        int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;
                        currentHoveredPos = new Vector2(startX + gridX, startY + gridY);
                    }
                    else
                    {
                        currentHoveredPos = new Vector2(gridX, gridY);
                    }
                }
            }

            if (currentHoveredPos.HasValue)
            {
                if (currentHoveredPos.Equals(_hoveredGridPos))
                {
                    _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_hoverTimer >= HOVER_TIME_FOR_TOOLTIP && !_showTooltip)
                    {
                        _showTooltip = true;
                        var stringBuilder = new StringBuilder();
                        int posX = (int)currentHoveredPos.Value.X;
                        int posY = (int)currentHoveredPos.Value.Y;

                        if (_gameState.CurrentMapView == MapView.World)
                        {
                            float noise = _gameState.GetNoiseAt(posX, posY);
                            string terrainName = GetTerrainName(noise);
                            stringBuilder.Append($"Pos: ({posX}, {posY})\n");
                            stringBuilder.Append($"Terrain: {terrainName}\n");
                            stringBuilder.Append($"Noise: {noise:F2}");
                        }
                        else
                        {
                            stringBuilder.Append($"Local Pos: ({posX}, {posY})");
                        }

                        _tooltipText = stringBuilder.ToString();
                        _tooltipPosition = new Vector2(virtualMousePos.X + 15, virtualMousePos.Y);
                    }
                }
                else
                {
                    _hoveredGridPos = currentHoveredPos;
                    _hoverTimer = 0f;
                    _showTooltip = false;
                }
            }
            else
            {
                _hoveredGridPos = null;
                _hoverTimer = 0f;
                _showTooltip = false;
            }
        }

        public void DrawMap(GameTime gameTime)
        {
            if (_gameState.CurrentMapView == MapView.World)
            {
                DrawWorldMap(gameTime);
            }
            else
            {
                DrawLocalMap(gameTime);
            }
        }

        private void DrawWorldMap(GameTime gameTime)
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            int cellSize = Global.GRID_CELL_SIZE;
            int gridSize = Global.GRID_SIZE;

            int mapStartX = 35;
            int mapStartY = 50;
            int mapWidth = gridSize * cellSize + 10;
            int mapHeight = gridSize * cellSize + 30;

            _mapGridBounds = new Rectangle(mapStartX, mapStartY, gridSize * cellSize, gridSize * cellSize);

            DrawMapFrame(mapStartX, mapStartY, mapWidth, mapHeight, gameTime);

            var gridElements = GenerateWorldMapGridElements(mapStartX, mapStartY, gridSize, cellSize);
            foreach (var element in gridElements)
            {
                DrawGridElement(element, cellSize);
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    _spriteBatch.Draw(Core.CurrentSpriteManager.MapHoverMarkerSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (RightClickedWorldPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(RightClickedWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle markerRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    _spriteBatch.Draw(Core.CurrentSpriteManager.MapHoverMarkerSprite, markerRect, Color.Cyan * 0.6f);
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(_spriteBatch);
            _contextMenu.Draw(_spriteBatch);
            if (_showTooltip) DrawTooltip(_spriteBatch);
        }

        private void DrawLocalMap(GameTime gameTime)
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            int cellSize = Global.LOCAL_GRID_CELL_SIZE;
            int gridSize = Global.LOCAL_GRID_SIZE;

            int mapStartX = 35;
            int mapStartY = 50;
            int mapWidth = gridSize * cellSize + 10;
            int mapHeight = gridSize * cellSize + 30;

            _mapGridBounds = new Rectangle(mapStartX, mapStartY, gridSize * cellSize, gridSize * cellSize);

            DrawMapFrame(mapStartX, mapStartY, mapWidth, mapHeight, gameTime);

            var gridElements = GenerateLocalMapGridElements(mapStartX, mapStartY, gridSize, cellSize);
            foreach (var element in gridElements)
            {
                DrawGridElement(element, cellSize);
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    _spriteBatch.Draw(Core.CurrentSpriteManager.MapHoverMarkerSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(_spriteBatch);
            _contextMenu.Draw(_spriteBatch);
            if (_showTooltip) DrawTooltip(_spriteBatch);
        }

        private void DrawMapFrame(int mapStartX, int mapStartY, int mapWidth, int mapHeight, GameTime gameTime)
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            var pixel = Core.Pixel;
            var font = Global.Instance.DefaultFont;

            // Draw Frame //
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), Global.Instance.Palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), Global.Instance.Palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Right
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 5, mapWidth, 2), Global.Instance.Palette_White); // Separator

            // Draw Time //
            string timeText = Core.CurrentWorldClockManager.CurrentTime;
            Vector2 timeTextPos = new Vector2(mapStartX, mapStartY - 20);
            _spriteBatch.DrawString(font, timeText, timeTextPos, Global.Instance.GameTextColor);

            // Define Button Layout //
            const int buttonHeight = 16;
            const int buttonSpacing = 5;
            int goStopButtonWidth = 45;
            int toggleButtonWidth = 85;
            int headerContentRightEdge = mapStartX + mapWidth - 12;
            int buttonY = mapStartY - 22;

            // Position and Draw Buttons by iterating through the list //
            var toggleMapButton = _headerButtons.FirstOrDefault(b => b.Function.ToLower() == "map");
            var stopButton = _headerButtons.FirstOrDefault(b => b.Function.ToLower() == "stop");
            var goButton = _headerButtons.FirstOrDefault(b => b.Function.ToLower() == "go");

            if (toggleMapButton != null)
            {
                toggleMapButton.Text = _gameState.CurrentMapView == MapView.World ? "Local Map" : "World Map";
                toggleMapButton.Bounds = new Rectangle(headerContentRightEdge - toggleButtonWidth, buttonY, toggleButtonWidth, buttonHeight);
            }

            if (stopButton != null && toggleMapButton != null)
            {
                stopButton.Bounds = new Rectangle(toggleMapButton.Bounds.X - buttonSpacing - goStopButtonWidth, buttonY, goStopButtonWidth, buttonHeight);
            }

            if (goButton != null && stopButton != null)
            {
                goButton.Bounds = new Rectangle(stopButton.Bounds.X - buttonSpacing - goStopButtonWidth, buttonY, goStopButtonWidth, buttonHeight);
            }

            foreach (var button in _headerButtons)
            {
                button.Draw(_spriteBatch, font, gameTime);
            }
        }

        private void DrawPauseIcon(SpriteBatch spriteBatch)
        {
            string pauseText = "▐▐";
            Vector2 scale = new Vector2(5, 5);
            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(pauseText) * scale;
            Vector2 textPosition = new Vector2(
                _mapGridBounds.Center.X - textSize.X / 2,
                _mapGridBounds.Center.Y - textSize.Y / 2
            );

            spriteBatch.DrawString(Global.Instance.DefaultFont, pauseText, textPosition, Color.White * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private List<GridElement> GenerateWorldMapGridElements(int mapStartX, int mapStartY, int gridSize, int cellSize)
        {
            var elements = new List<GridElement>();
            int startX = (int)_gameState.PlayerWorldPos.X - gridSize / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - gridSize / 2;

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    float noise = _gameState.GetNoiseAt(worldX, worldY);
                    Texture2D texture = GetTerrainTexture(noise);
                    Color color = GetTerrainColor(noise);

                    if (worldPos == _gameState.PlayerWorldPos)
                    {
                        texture = Core.CurrentSpriteManager.PlayerSprite;
                        color = Global.Instance.PlayerColor;
                    }
                    else
                    {
                        var actionsAtPos = _gameState.PendingActions.Where(a => a.Position == worldPos).ToList();
                        if (actionsAtPos.Any())
                        {
                            if (actionsAtPos.Any(a => a.Type == ActionType.ShortRest))
                            {
                                texture = Core.CurrentSpriteManager.ShortRestSprite;
                                color = Global.Instance.ShortRestColor;
                            }
                            else if (actionsAtPos.Any(a => a.Type == ActionType.LongRest))
                            {
                                texture = Core.CurrentSpriteManager.LongRestSprite;
                                color = Global.Instance.LongRestColor;
                            }
                            else if (actionsAtPos.Any(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove))
                            {
                                bool isRunning = actionsAtPos.Any(a => a.Type == ActionType.RunMove);
                                texture = isRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;
                                var lastMoveAction = _gameState.PendingActions.LastOrDefault(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove);
                                if (lastMoveAction != null && lastMoveAction.Position == worldPos)
                                {
                                    color = Global.Instance.PathEndColor;
                                }
                                else
                                {
                                    color = isRunning ? Global.Instance.RunPathColor : Global.Instance.PathColor;
                                }
                            }
                        }
                    }
                    Vector2 gridPos = new Vector2(mapStartX + x * cellSize, mapStartY + y * cellSize);
                    elements.Add(new GridElement(texture, color, gridPos));
                }
            }
            return elements;
        }

        private List<GridElement> GenerateLocalMapGridElements(int mapStartX, int mapStartY, int gridSize, int cellSize)
        {
            var elements = new List<GridElement>();
            Color bgColor = Global.Instance.Palette_DarkGray;
            Texture2D pixel = Core.Pixel;

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Vector2 localPos = new Vector2(x, y);
                    Texture2D texture = pixel;
                    Color color = bgColor;

                    if (localPos == _gameState.PlayerLocalPos)
                    {
                        texture = pixel; // Use a solid square for player
                        color = Global.Instance.PlayerColor;
                    }
                    else
                    {
                        var actionsAtPos = _gameState.PendingActions.Where(a => a.Position == localPos).ToList();
                        if (actionsAtPos.Any())
                        {
                            bool isRunning = actionsAtPos.Any(a => a.Type == ActionType.RunMove);
                            texture = isRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;
                            var lastMoveAction = _gameState.PendingActions.LastOrDefault(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove);
                            if (lastMoveAction != null && lastMoveAction.Position == localPos)
                            {
                                color = Global.Instance.PathEndColor;
                            }
                            else
                            {
                                color = isRunning ? Global.Instance.RunPathColor : Global.Instance.PathColor;
                            }
                        }
                    }
                    Vector2 gridPos = new Vector2(mapStartX + x * cellSize, mapStartY + y * cellSize);
                    elements.Add(new GridElement(texture, color, gridPos));
                }
            }
            return elements;
        }

        private void DrawGridElement(GridElement element, int cellSize)
        {
            Rectangle destRect = new Rectangle((int)element.Position.X, (int)element.Position.Y, cellSize, cellSize);
            Global.Instance.CurrentSpriteBatch.Draw(element.Texture, destRect, element.Color);
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(_tooltipText)) return;

            var pixel = Core.Pixel;
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

        public Vector2? MapCoordsToScreen(Vector2 mapPos)
        {
            if (_mapGridBounds.IsEmpty) return null;

            if (_gameState.CurrentMapView == MapView.World)
            {
                int startX = (int)_gameState.PlayerWorldPos.X - Global.GRID_SIZE / 2;
                int startY = (int)_gameState.PlayerWorldPos.Y - Global.GRID_SIZE / 2;
                int gridX = (int)mapPos.X - startX;
                int gridY = (int)mapPos.Y - startY;

                if (gridX >= 0 && gridX < Global.GRID_SIZE && gridY >= 0 && gridY < Global.GRID_SIZE)
                {
                    int screenX = _mapGridBounds.X + gridX * Global.GRID_CELL_SIZE;
                    int screenY = _mapGridBounds.Y + gridY * Global.GRID_CELL_SIZE;
                    return new Vector2(screenX, screenY);
                }
            }
            else // Local Map
            {
                int gridX = (int)mapPos.X;
                int gridY = (int)mapPos.Y;
                int cellSize = Global.LOCAL_GRID_CELL_SIZE;
                int gridSize = Global.LOCAL_GRID_SIZE;

                if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
                {
                    int screenX = _mapGridBounds.X + gridX * cellSize;
                    int screenY = _mapGridBounds.Y + gridY * cellSize;
                    return new Vector2(screenX, screenY);
                }
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