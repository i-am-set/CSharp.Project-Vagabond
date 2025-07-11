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
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly SpriteManager _spriteManager;
        private readonly TooltipManager _tooltipManager;
        private readonly ArchetypeManager _archetypeManager;
        private readonly WorldClockManager _worldClockManager;
        private readonly Global _global;
        private readonly GraphicsDevice _graphicsDevice;

        private Vector2? _hoveredGridPos;
        private Rectangle _mapGridBounds;
        private readonly ContextMenu _contextMenu;
        private string _cachedTimeText;
        private Vector2 _timeTextPos;
        private int _cachedMapStartX, _cachedMapWidth;
        private readonly Dictionary<string, Button> _buttonMap;

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;
        public Vector2? RightClickedWorldPos { get; set; }

        // Cache state
        private RenderTarget2D _mapCacheTarget;
        private bool _isMapCacheDirty = true;
        private Vector2 _cachedPlayerWorldPos = new Vector2(-1, -1);
        private MapView _cachedMapView;

        public MapRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _global = ServiceLocator.Get<Global>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            _contextMenu = new ContextMenu();

            _headerButtons.Add(new Button(Rectangle.Empty, "Clear") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "World Map", "map"));

            _buttonMap = _headerButtons.ToDictionary(b => b.Function.ToLowerInvariant(), b => b);
            _cachedMapView = _gameState.CurrentMapView; // Initialize to prevent initial dirty check mismatch
        }

        public void Update(GameTime gameTime, BitmapFont font)
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            UpdateHover(virtualMousePos);
            _contextMenu.Update(mouseState, mouseState, virtualMousePos, font); // Note: prevMouseState is same as current here, might need adjustment if complex logic is added
        }

        private void UpdateHover(Vector2 virtualMousePos)
        {
            _hoveredGridPos = null; // Reset hover state each frame
            Vector2? currentHoveredGridPos = null;
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
                        currentHoveredGridPos = new Vector2(startX + gridX, startY + gridY);
                    }
                    else
                    {
                        currentHoveredGridPos = new Vector2(gridX, gridY);
                    }
                }
            }

            if (!currentHoveredGridPos.HasValue) return;

            _hoveredGridPos = currentHoveredGridPos;
            var stringBuilder = new StringBuilder();

            // --- Tooltip Logic ---
            int? entityIdOnTile = GetEntityIdAtGridPos(currentHoveredGridPos.Value);

            if (entityIdOnTile.HasValue && _gameState.IsInCombat)
            {
                var health = _componentStore.GetComponent<HealthComponent>(entityIdOnTile.Value);
                var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(entityIdOnTile.Value);
                var archetype = _archetypeManager.GetArchetypeTemplate(archetypeIdComp?.ArchetypeId ?? "Unknown");
                var targetPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityIdOnTile.Value);
                var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);

                if (health != null && archetype != null && targetPosComp != null && playerPosComp != null)
                {
                    float distance = Vector2.Distance(playerPosComp.LocalPosition, targetPosComp.LocalPosition);
                    stringBuilder.AppendLine(archetype.Name);
                    stringBuilder.AppendLine($"Health: {health.CurrentHealth}/{health.MaxHealth}");
                    stringBuilder.Append($"Distance: {distance:F1}m");
                    _tooltipManager.RequestTooltip(entityIdOnTile.Value, stringBuilder.ToString(), virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
                }
            }
            else
            {
                int posX = (int)currentHoveredGridPos.Value.X;
                int posY = (int)currentHoveredGridPos.Value.Y;

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
                _tooltipManager.RequestTooltip(currentHoveredGridPos.Value, stringBuilder.ToString(), virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
            }
        }

        public void DrawMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_gameState.CurrentMapView != _cachedMapView ||
                (_gameState.CurrentMapView == MapView.World && _gameState.PlayerWorldPos != _cachedPlayerWorldPos))
            {
                _isMapCacheDirty = true;
            }

            if (_gameState.CurrentMapView == MapView.World)
            {
                DrawWorldMap(spriteBatch, font, gameTime);
            }
            else
            {
                DrawLocalMap(spriteBatch, font, gameTime);
            }
        }

        private void RedrawMapCache(SpriteBatch spriteBatch)
        {
            int cacheWidth, cacheHeight, cellSize, gridSize;

            if (_gameState.CurrentMapView == MapView.World)
            {
                cellSize = Global.GRID_CELL_SIZE;
                gridSize = Global.GRID_SIZE;
            }
            else
            {
                cellSize = Global.LOCAL_GRID_CELL_SIZE;
                gridSize = Global.LOCAL_GRID_SIZE;
            }
            cacheWidth = gridSize * cellSize;
            cacheHeight = gridSize * cellSize;

            if (_mapCacheTarget == null || _mapCacheTarget.Width != cacheWidth || _mapCacheTarget.Height != cacheHeight)
            {
                _mapCacheTarget?.Dispose();
                _mapCacheTarget = new RenderTarget2D(_graphicsDevice, cacheWidth, cacheHeight, false, _graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None);
            }

            spriteBatch.End();

            var originalRenderTargets = _graphicsDevice.GetRenderTargets();
            _graphicsDevice.SetRenderTarget(_mapCacheTarget);
            _graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            var staticElements = _gameState.CurrentMapView == MapView.World
                ? GenerateStaticWorldMapGridElements()
                : GenerateStaticLocalMapGridElements();

            foreach (var element in staticElements)
            {
                Rectangle destRect = new Rectangle((int)element.Position.X, (int)element.Position.Y, cellSize, cellSize);
                spriteBatch.Draw(element.Texture, destRect, element.Color);
            }

            spriteBatch.End();

            _graphicsDevice.SetRenderTargets(originalRenderTargets);

            _cachedPlayerWorldPos = _gameState.PlayerWorldPos;
            _cachedMapView = _gameState.CurrentMapView;
            _isMapCacheDirty = false;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        }

        private void DrawWorldMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            int cellSize = Global.GRID_CELL_SIZE;
            int gridSize = Global.GRID_SIZE;
            int mapStartX = 35;
            int mapStartY = 50;
            int mapWidth = gridSize * cellSize + 10;
            int mapHeight = gridSize * cellSize + 30;

            _mapGridBounds = new Rectangle(mapStartX, mapStartY, gridSize * cellSize, gridSize * cellSize);

            if (_isMapCacheDirty)
            {
                RedrawMapCache(spriteBatch);
            }

            DrawMapFrame(spriteBatch, font, mapStartX, mapStartY, mapWidth, mapHeight, gameTime);

            if (_mapCacheTarget != null)
            {
                spriteBatch.Draw(_mapCacheTarget, _mapGridBounds, Color.White);
            }

            var dynamicElements = GenerateDynamicWorldMapGridElements();
            foreach (var element in dynamicElements)
            {
                DrawGridElement(spriteBatch, element, cellSize);
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    spriteBatch.Draw(_spriteManager.WorldMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (RightClickedWorldPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(RightClickedWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle markerRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    spriteBatch.Draw(_spriteManager.WorldMapHoverSelectorSprite, markerRect, Color.Cyan * 0.6f);
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(spriteBatch, font);
            _contextMenu.Draw(spriteBatch, font);
        }

        private void DrawLocalMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            int cellSize = Global.LOCAL_GRID_CELL_SIZE;
            int gridSize = Global.LOCAL_GRID_SIZE;
            int mapStartX = 35;
            int mapStartY = 50;
            int mapWidth = gridSize * cellSize + 10;
            int mapHeight = gridSize * cellSize + 30;

            _mapGridBounds = new Rectangle(mapStartX, mapStartY, gridSize * cellSize, gridSize * cellSize);

            if (_isMapCacheDirty)
            {
                RedrawMapCache(spriteBatch);
            }

            DrawMapFrame(spriteBatch, font, mapStartX, mapStartY, mapWidth, mapHeight, gameTime);

            if (_mapCacheTarget != null)
            {
                spriteBatch.Draw(_mapCacheTarget, _mapGridBounds, Color.White);
            }

            var dynamicElements = GenerateDynamicLocalMapGridElements();
            foreach (var element in dynamicElements)
            {
                DrawGridElement(spriteBatch, element, cellSize);
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);
                    spriteBatch.Draw(_spriteManager.LocalMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (_gameState.IsInCombat)
            {
                foreach (var entityId in _gameState.Combatants)
                {
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                    if (localPosComp == null) continue;

                    Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                    if (!screenPos.HasValue) continue;

                    var cellRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, cellSize, cellSize);

                    Color boxColor = (entityId == _gameState.SelectedTargetId) ? _global.Palette_Pink : _global.Palette_Teal;
                    DrawHollowRectangle(spriteBatch, cellRect, boxColor, 1);

                    if (entityId == _gameState.CurrentTurnEntityId)
                    {
                        int indicatorSize = 8;
                        var indicatorRect = new Rectangle(cellRect.Center.X - indicatorSize / 2, cellRect.Y - indicatorSize - 2, indicatorSize, indicatorSize);
                        spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), indicatorRect, _global.Palette_Teal);
                    }
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(spriteBatch, font);
            _contextMenu.Draw(spriteBatch, font);
        }

        private void DrawMapFrame(SpriteBatch spriteBatch, BitmapFont font, int mapStartX, int mapStartY, int mapWidth, int mapHeight, GameTime gameTime)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), _global.Palette_White); // Top
            spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), _global.Palette_White); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), _global.Palette_White); // Left
            spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), _global.Palette_White); // Right
            spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 5, mapWidth, 2), _global.Palette_White); // Separator

            string timeText = _worldClockManager.CurrentTime;
            if (timeText != _cachedTimeText)
            {
                _timeTextPos = new Vector2(mapStartX, mapStartY - 20);
                _cachedTimeText = timeText;
            }
            spriteBatch.DrawString(font, _cachedTimeText, _timeTextPos, _global.GameTextColor);

            bool viewChanged = _cachedMapView != _gameState.CurrentMapView;
            if (mapStartX != _cachedMapStartX || mapWidth != _cachedMapWidth || viewChanged)
            {
                LayoutHeaderButtons(mapStartX, mapWidth, mapStartY);
                _cachedMapStartX = mapStartX;
                _cachedMapWidth = mapWidth;
            }

            foreach (var b in _headerButtons)
            {
                b.Draw(spriteBatch, font, gameTime);
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
                toggleMapButton.Text = _gameState.CurrentMapView == MapView.World ? "Local Map" : "World Map";
                toggleMapButton.Bounds = new Rectangle(headerContentRightEdge - toggleButtonWidth, buttonY, toggleButtonWidth, buttonHeight);
            }

            if (_buttonMap.TryGetValue("stop", out Button stopButton) && _buttonMap.TryGetValue("map", out toggleMapButton))
            {
                stopButton.Bounds = new Rectangle(toggleMapButton.Bounds.X - buttonSpacing - goStopButtonWidth, buttonY, goStopButtonWidth, buttonHeight);
            }

            if (_buttonMap.TryGetValue("go", out Button goButton) && _buttonMap.TryGetValue("stop", out stopButton))
            {
                goButton.Bounds = new Rectangle(stopButton.Bounds.X - buttonSpacing - goStopButtonWidth, buttonY, goStopButtonWidth, buttonHeight);
            }

            if (_buttonMap.TryGetValue("clear", out Button clearButton) && _buttonMap.TryGetValue("go", out goButton))
            {
                clearButton.Bounds = new Rectangle(goButton.Bounds.X - buttonSpacing - goStopButtonWidth, buttonY, goStopButtonWidth, buttonHeight);
            }
        }

        private void DrawPauseIcon(SpriteBatch spriteBatch, BitmapFont font)
        {
            string pauseText = "▐▐";
            Vector2 scale = new Vector2(5, 5);
            Vector2 textSize = font.MeasureString(pauseText) * scale;
            Vector2 textPosition = new Vector2(_mapGridBounds.Center.X - textSize.X / 2, _mapGridBounds.Center.Y - textSize.Y / 2);
            spriteBatch.DrawString(font, pauseText, textPosition, Color.White * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private List<GridElement> GenerateStaticWorldMapGridElements()
        {
            var elements = new List<GridElement>();
            int gridSize = Global.GRID_SIZE;
            int cellSize = Global.GRID_CELL_SIZE;
            int startX = (int)_gameState.PlayerWorldPos.X - gridSize / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - gridSize / 2;

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;
                    float noise = _gameState.GetNoiseAt(worldX, worldY);
                    Vector2 cachePos = new Vector2(x * cellSize, y * cellSize);
                    elements.Add(new GridElement(GetTerrainTexture(noise), GetTerrainColor(noise), cachePos));
                }
            }
            return elements;
        }

        private List<GridElement> GenerateDynamicWorldMapGridElements()
        {
            var elements = new List<GridElement>();
            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = _componentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId) ?? (IAction)_componentStore.GetComponent<RestAction>(_gameState.PlayerEntityId);
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
                    actionTexture = moveAction.IsRunning ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                    actionColor = moveAction.IsRunning ? _global.RunPathColor : _global.PathColor;
                }
                else if (action is RestAction restAction)
                {
                    actionPos = restAction.Position;
                    actionTexture = restAction.RestType == RestType.ShortRest ? _spriteManager.ShortRestSprite : _spriteManager.LongRestSprite;
                    actionColor = _global.ShortRestColor;
                }

                Vector2? screenPos = MapCoordsToScreen(actionPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(actionTexture, actionColor, screenPos.Value));
                }
            }

            var playerRenderComp = _componentStore.GetComponent<RenderableComponent>(_gameState.PlayerEntityId);
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

        private List<GridElement> GenerateStaticLocalMapGridElements()
        {
            var elements = new List<GridElement>();
            int gridSize = Global.LOCAL_GRID_SIZE;
            int cellSize = Global.LOCAL_GRID_CELL_SIZE;
            Color bgColor = _global.Palette_DarkGray;
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Vector2 cachePos = new Vector2(x * cellSize, y * cellSize);
                    elements.Add(new GridElement(pixel, bgColor, cachePos));
                }
            }
            return elements;
        }

        private List<GridElement> GenerateDynamicLocalMapGridElements()
        {
            var elements = new List<GridElement>();
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = _componentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId);
            if (activePlayerAction != null)
            {
                allPlayerActions.Add(activePlayerAction);
            }

            foreach (var action in allPlayerActions.OfType<MoveAction>())
            {
                Vector2? screenPos = MapCoordsToScreen(action.Destination);
                if (screenPos.HasValue)
                {
                    Texture2D texture = action.IsRunning ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                    Color color = action.IsRunning ? _global.RunPathColor : _global.PathColor;
                    elements.Add(new GridElement(texture, color, screenPos.Value));
                }
            }

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var renderComp = _componentStore.GetComponent<RenderableComponent>(entityId);

                if (localPosComp != null && renderComp != null)
                {
                    Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                    if (screenPos.HasValue)
                    {
                        Texture2D textureToDraw = renderComp.Texture ?? pixel;
                        if (entityId == _gameState.PlayerEntityId)
                        {
                            textureToDraw = pixel;
                        }
                        elements.Add(new GridElement(textureToDraw, renderComp.Color, screenPos.Value));
                    }
                }
            }

            return elements;
        }

        private void DrawGridElement(SpriteBatch spriteBatch, GridElement element, int cellSize)
        {
            Rectangle destRect = new Rectangle((int)element.Position.X, (int)element.Position.Y, cellSize, cellSize);
            spriteBatch.Draw(element.Texture, destRect, element.Color);
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
                    return new Vector2(_mapGridBounds.X + gridX * Global.GRID_CELL_SIZE, _mapGridBounds.Y + gridY * Global.GRID_CELL_SIZE);
                }
            }
            else
            {
                int gridX = (int)mapPos.X;
                int gridY = (int)mapPos.Y;
                if (gridX >= 0 && gridX < Global.LOCAL_GRID_SIZE && gridY >= 0 && gridY < Global.LOCAL_GRID_SIZE)
                {
                    return new Vector2(_mapGridBounds.X + gridX * Global.LOCAL_GRID_CELL_SIZE, _mapGridBounds.Y + gridY * Global.LOCAL_GRID_CELL_SIZE);
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
                if (entityId == _gameState.PlayerEntityId) continue;

                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
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

        /// <summary>
        /// Gets the ID of the entity at a specific grid coordinate on the local map.
        /// </summary>
        private int? GetEntityIdAtGridPos(Vector2 gridPos)
        {
            foreach (var entityId in _gameState.Combatants)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp != null && (int)localPosComp.LocalPosition.X == (int)gridPos.X && (int)localPosComp.LocalPosition.Y == (int)gridPos.Y)
                {
                    return entityId;
                }
            }
            return null;
        }

        private string GetTerrainName(float noise)
        {
            if (noise < _global.WaterLevel) return "Water";
            if (noise < _global.FlatlandsLevel) return "Flatlands";
            if (noise < _global.HillsLevel) return "Hills";
            if (noise < _global.MountainsLevel) return "Mountains";
            return "Peak";
        }

        private Texture2D GetTerrainTexture(float noise)
        {
            if (noise < _global.WaterLevel) return _spriteManager.WaterSprite;
            if (noise < _global.FlatlandsLevel) return _spriteManager.FlatlandSprite;
            if (noise < _global.HillsLevel) return _spriteManager.HillSprite;
            if (noise < _global.MountainsLevel) return _spriteManager.MountainSprite;
            return _spriteManager.PeakSprite;
        }

        private Color GetTerrainColor(float noise)
        {
            if (noise < _global.WaterLevel) return _global.WaterColor;
            if (noise < _global.FlatlandsLevel) return _global.FlatlandColor;
            if (noise < _global.HillsLevel) return _global.HillColor;
            if (noise < _global.MountainsLevel) return _global.MountainColor;
            return _global.MountainColor;
        }

        private void DrawHollowRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}