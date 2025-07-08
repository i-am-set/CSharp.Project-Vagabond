using Microsoft.Xna.Framework;
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
        private Rectangle _mapGridBounds;
        private ContextMenu _contextMenu = new ContextMenu();
        private string _cachedTimeText;
        private Vector2 _timeTextPos;
        private int _cachedMapStartX, _cachedMapWidth;
        private MapView _cachedMapView;
        private Dictionary<string, Button> _buttonMap;

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;
        public Vector2? RightClickedWorldPos { get; set; }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        public MapRenderer()
        {
            _headerButtons.Add(new Button(Rectangle.Empty, "Clear") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "World Map", "map"));

            _buttonMap = _headerButtons.ToDictionary(b => b.Function.ToLowerInvariant(), b => b);
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

            _hoveredGridPos = currentHoveredPos;

            if (currentHoveredPos.HasValue)
            {
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

                string tooltipText = stringBuilder.ToString();
                Core.CurrentTooltipManager.RequestTooltip(currentHoveredPos.Value, tooltipText, virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
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
                    _spriteBatch.Draw(Core.CurrentSpriteManager.WorldMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (RightClickedWorldPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(RightClickedWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle markerRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    _spriteBatch.Draw(Core.CurrentSpriteManager.WorldMapHoverSelectorSprite, markerRect, Color.Cyan * 0.6f);
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(_spriteBatch);
            _contextMenu.Draw(_spriteBatch);
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
                    _spriteBatch.Draw(Core.CurrentSpriteManager.LocalMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            // --- In-Combat Visuals ---
            if (_gameState.IsInCombat)
            {
                foreach (var entityId in _gameState.Combatants)
                {
                    var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (localPosComp == null) continue;

                    Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                    if (!screenPos.HasValue) continue;

                    var cellRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);

                    // Draw Bounding Box
                    Color boxColor = (entityId == _gameState.SelectedTargetId) ? Global.Instance.Palette_Pink : Global.Instance.Palette_Teal;
                    DrawHollowRectangle(_spriteBatch, cellRect, boxColor, 1);

                    // Draw Turn Indicator
                    if (entityId == _gameState.CurrentTurnEntityId)
                    {
                        int indicatorSize = 8;
                        var indicatorRect = new Rectangle(
                            cellRect.Center.X - indicatorSize / 2,
                            cellRect.Y - indicatorSize - 2,
                            indicatorSize,
                            indicatorSize
                        );
                        _spriteBatch.Draw(Core.Pixel, indicatorRect, Global.Instance.Palette_Teal);
                    }
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(_spriteBatch);
            _contextMenu.Draw(_spriteBatch);
        }

        private void DrawMapFrame(int mapStartX, int mapStartY, int mapWidth, int mapHeight, GameTime gameTime)
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            Texture2D pixel = Core.Pixel;
            BitmapFont font = Global.Instance.DefaultFont;

            // Draw Frame //
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), Global.Instance.Palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), Global.Instance.Palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), Global.Instance.Palette_White); // Right
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 5, mapWidth, 2), Global.Instance.Palette_White); // Separator

            // Draw Time //
            string timeText = Core.CurrentWorldClockManager.CurrentTime;
            if (timeText != _cachedTimeText)
            {
                _timeTextPos = new Vector2(mapStartX, mapStartY - 20);
                _cachedTimeText = timeText;
            }
            _spriteBatch.DrawString(font, _cachedTimeText, _timeTextPos, Global.Instance.GameTextColor);

            bool viewChanged = _cachedMapView != _gameState.CurrentMapView;
            if (mapStartX != _cachedMapStartX || mapWidth != _cachedMapWidth || viewChanged)
            {
                LayoutHeaderButtons(mapStartX, mapWidth, mapStartY);
                _cachedMapStartX = mapStartX;
                _cachedMapWidth = mapWidth;
                _cachedMapView = _gameState.CurrentMapView;
            }

            foreach (var b in _headerButtons)
            {
                b.Draw(_spriteBatch, font, gameTime);
            }
        }

        private void LayoutHeaderButtons(int mapStartX, int mapWidth, int mapStartY)
        {
            const int buttonHeight = 16;
            const int buttonSpacing = 5;
            const int goStopButtonWidth = 45;
            const int toggleButtonWidth = 85;

            int headerContentRightEdge = mapStartX + mapWidth - 12;
            int buttonY = mapStartY - 22;

            if (_buttonMap.TryGetValue("map", out Button toggleMapButton))
            {
                toggleMapButton.Text =
                    _gameState.CurrentMapView == MapView.World
                        ? "Local Map"
                        : "World Map";

                toggleMapButton.Bounds = new Rectangle(
                    headerContentRightEdge - toggleButtonWidth,
                    buttonY,
                    toggleButtonWidth,
                    buttonHeight
                );
            }

            if (_buttonMap.TryGetValue("stop", out Button stopButton)
             && _buttonMap.TryGetValue("map", out toggleMapButton))
            {
                stopButton.Bounds = new Rectangle(
                    toggleMapButton.Bounds.X - buttonSpacing - goStopButtonWidth,
                    buttonY,
                    goStopButtonWidth,
                    buttonHeight
                );
            }

            if (_buttonMap.TryGetValue("go", out Button goButton)
             && _buttonMap.TryGetValue("stop", out stopButton))
            {
                goButton.Bounds = new Rectangle(
                    stopButton.Bounds.X - buttonSpacing - goStopButtonWidth,
                    buttonY,
                    goStopButtonWidth,
                    buttonHeight
                );
            }

            if (_buttonMap.TryGetValue("clear", out Button clearButton)
             && _buttonMap.TryGetValue("go", out goButton))
            {
                clearButton.Bounds = new Rectangle(
                    goButton.Bounds.X - buttonSpacing - goStopButtonWidth,
                    buttonY,
                    goStopButtonWidth,
                    buttonHeight
                );
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

            // Layer 1: Base Terrain
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;
                    float noise = _gameState.GetNoiseAt(worldX, worldY);
                    Vector2 gridPos = new Vector2(mapStartX + x * cellSize, mapStartY + y * cellSize);
                    elements.Add(new GridElement(GetTerrainTexture(noise), GetTerrainColor(noise), gridPos));
                }
            }

            // Layer 2: Player Path/Action Markers
            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = Core.ComponentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId) ?? (IAction)Core.ComponentStore.GetComponent<RestAction>(_gameState.PlayerEntityId);
            if (activePlayerAction != null)
            {
                allPlayerActions.Add(activePlayerAction);
            }

            foreach (var action in allPlayerActions)
            {
                Vector2 actionPos = Vector2.Zero;
                Texture2D actionTexture = null;
                Color actionColor = Color.Transparent;

                if (action is MoveAction moveAction)
                {
                    actionPos = moveAction.Destination;
                    actionTexture = moveAction.IsRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;
                    actionColor = moveAction.IsRunning ? Global.Instance.RunPathColor : Global.Instance.PathColor;
                }
                else if (action is RestAction restAction)
                {
                    actionPos = restAction.Position;
                    actionTexture = restAction.RestType == RestType.ShortRest ? Core.CurrentSpriteManager.ShortRestSprite : Core.CurrentSpriteManager.LongRestSprite;
                    actionColor = Global.Instance.ShortRestColor;
                }

                Vector2? screenPos = MapCoordsToScreen(actionPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(actionTexture, actionColor, screenPos.Value));
                }
            }

            // Layer 3: Player Entity (drawn last to be on top)
            var playerRenderComp = Core.ComponentStore.GetComponent<RenderableComponent>(_gameState.PlayerEntityId);
            if (playerRenderComp != null)
            {
                Vector2? screenPos = MapCoordsToScreen(_gameState.PlayerWorldPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(playerRenderComp.Texture, playerRenderComp.Color, screenPos.Value));
                }
            }

            return elements;
        }

        private List<GridElement> GenerateLocalMapGridElements(int mapStartX, int mapStartY, int gridSize, int cellSize)
        {
            var elements = new List<GridElement>();
            Color bgColor = Global.Instance.Palette_DarkGray;
            Texture2D pixel = Core.Pixel;

            // Layer 1: Background
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Vector2 gridPos = new Vector2(mapStartX + x * cellSize, mapStartY + y * cellSize);
                    elements.Add(new GridElement(pixel, bgColor, gridPos));
                }
            }

            // Layer 2: Player Path
            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = Core.ComponentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId);
            if (activePlayerAction != null)
            {
                allPlayerActions.Add(activePlayerAction);
            }

            foreach (var action in allPlayerActions.OfType<MoveAction>())
            {
                Vector2? screenPos = MapCoordsToScreen(action.Destination);
                if (screenPos.HasValue)
                {
                    Texture2D texture = action.IsRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;
                    Color color = action.IsRunning ? Global.Instance.RunPathColor : Global.Instance.PathColor;
                    elements.Add(new GridElement(texture, color, screenPos.Value));
                }
            }

            // Layer 3: Entities
            foreach (var entityId in _gameState.ActiveEntities)
            {
                var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
                var renderComp = Core.ComponentStore.GetComponent<RenderableComponent>(entityId);

                if (localPosComp != null && renderComp != null)
                {
                    Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                    if (screenPos.HasValue)
                    {
                        // Use a pixel for the player in local view, but their assigned texture/color otherwise
                        Texture2D textureToDraw = renderComp.Texture;
                        if (entityId == _gameState.PlayerEntityId)
                        {
                            textureToDraw = Core.Pixel;
                        }
                        elements.Add(new GridElement(textureToDraw, renderComp.Color, screenPos.Value));
                    }
                }
            }

            return elements;
        }

        private void DrawGridElement(GridElement element, int cellSize)
        {
            Rectangle destRect = new Rectangle((int)element.Position.X, (int)element.Position.Y, cellSize, cellSize);
            Global.Instance.CurrentSpriteBatch.Draw(element.Texture, destRect, element.Color);
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

        /// <summary>
        /// Gets the ID of the entity at a specific mouse position on the local map.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor in virtual screen coordinates.</param>
        /// <returns>The entity ID of the combatant, or null if no combatant is at that position.</returns>
        public int? GetEntityIdAt(Point mousePosition)
        {
            if (!_mapGridBounds.Contains(mousePosition) || !_gameState.IsInCombat) return null;

            int cellSize = Global.LOCAL_GRID_CELL_SIZE;

            foreach (var entityId in _gameState.Combatants)
            {
                if (entityId == _gameState.PlayerEntityId) continue; // Can't target self

                var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp == null) continue;

                Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                if (screenPos.HasValue)
                {
                    var cellRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    if (cellRect.Contains(mousePosition))
                    {
                        return entityId;
                    }
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

        /// <summary>
        /// Draws a hollow rectangle.
        /// </summary>
        private void DrawHollowRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(Core.Pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color); // Top
            spriteBatch.Draw(Core.Pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color); // Bottom
            spriteBatch.Draw(Core.Pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color); // Left
            spriteBatch.Draw(Core.Pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color); // Right
        }
    }
}