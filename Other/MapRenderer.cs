using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
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
        private readonly CombatUIAnimationManager _animationManager;

        private Vector2? _hoveredGridPos;
        private Rectangle _mapGridBounds;
        private readonly ContextMenu _contextMenu;
        private string _cachedTimeText;
        private Vector2 _timeTextPos;
        private readonly Dictionary<string, Button> _buttonMap;

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;
        public Vector2? RightClickedWorldPos { get; set; }

        // New public properties for dynamic layout
        public Rectangle MapScreenBounds { get; private set; }
        public int GridSizeX { get; private set; }
        public int GridSizeY { get; private set; }
        public int CellSize { get; private set; }

        public MapRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _global = ServiceLocator.Get<Global>();
            _animationManager = ServiceLocator.Get<CombatUIAnimationManager>();
            _contextMenu = new ContextMenu();

            _headerButtons.Add(new Button(Rectangle.Empty, "Clear") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "World Map", "map"));

            _buttonMap = _headerButtons.ToDictionary(b => b.Function.ToLowerInvariant(), b => b);
        }

        public void Update(GameTime gameTime, BitmapFont font)
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            UpdateHover(virtualMousePos);
            _contextMenu.Update(mouseState, mouseState, virtualMousePos, font); // Note: prevMouseState is same as current here, might need adjustment if complex logic is added
        }

        private void CalculateMapLayout(Rectangle? overrideBounds = null)
        {
            if (overrideBounds.HasValue)
            {
                MapScreenBounds = overrideBounds.Value;
            }
            else
            {
                // --- UNIFIED SIZE CALCULATION ---
                // Always calculate the map's pixel dimensions based on the world map's cell size for consistency.
                const int worldCellSize = Global.GRID_CELL_SIZE;

                int maxWidth = (int)(Global.VIRTUAL_WIDTH * Global.MAP_AREA_WIDTH_PERCENT);
                int maxHeight = Global.VIRTUAL_HEIGHT - Global.MAP_TOP_PADDING - Global.TERMINAL_AREA_HEIGHT - 10;
                int baseMapSize = Math.Min(maxWidth, maxHeight);

                // Calculate grid size based on the base size and world cell size, then add the expansion
                int baseGridSize = baseMapSize / worldCellSize;
                int worldGridSize = baseGridSize + 4;

                // This is the final, uniform pixel size for all maps.
                int finalMapSize = worldGridSize * worldCellSize;

                int mapX = (Global.VIRTUAL_WIDTH - finalMapSize) / 2;
                int mapY = Global.MAP_TOP_PADDING + 7; // Move map and header down by 7 pixels
                MapScreenBounds = new Rectangle(mapX, mapY, finalMapSize, finalMapSize);
            }

            // --- VIEW-SPECIFIC GRID SETUP ---
            // Now, determine the cell size and grid dimensions for the CURRENT view.
            CellSize = _gameState.CurrentMapView == MapView.World ? Global.GRID_CELL_SIZE : Global.LOCAL_GRID_CELL_SIZE;
            _mapGridBounds = MapScreenBounds;

            // Recalculate grid dimensions based on the final, uniform pixel size and the current view's cell size.
            GridSizeX = MapScreenBounds.Width / CellSize;
            GridSizeY = MapScreenBounds.Height / CellSize;
        }

        private void UpdateHover(Vector2 virtualMousePos)
        {
            _hoveredGridPos = null; // Reset hover state each frame
            Vector2? currentHoveredGridPos = null;

            if (_mapGridBounds.Contains(virtualMousePos))
            {
                int gridX = (int)((virtualMousePos.X - _mapGridBounds.X) / CellSize);
                int gridY = (int)((virtualMousePos.Y - _mapGridBounds.Y) / CellSize);

                if (gridX >= 0 && gridX < GridSizeX && gridY >= 0 && gridY < GridSizeY)
                {
                    if (_gameState.CurrentMapView == MapView.World)
                    {
                        int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
                        int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
                        currentHoveredGridPos = new Vector2(startX + gridX, startY + gridY);
                    }
                    else
                    {
                        int startX = (int)_gameState.PlayerLocalPos.X - GridSizeX / 2;
                        int startY = (int)_gameState.PlayerLocalPos.Y - GridSizeY / 2;
                        currentHoveredGridPos = new Vector2(startX + gridX, startY + gridY);
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

        public void DrawMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds = null)
        {
            if (_gameState.CurrentMapView == MapView.World)
            {
                DrawWorldMap(spriteBatch, font, gameTime, overrideBounds);
            }
            else
            {
                DrawLocalMap(spriteBatch, font, gameTime, overrideBounds);
            }
            _contextMenu.Draw(spriteBatch, font);
        }

        private void DrawWorldMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds)
        {
            CalculateMapLayout(overrideBounds);
            DrawMapFrame(spriteBatch, font, gameTime);

            var gridElements = GenerateWorldMapGridElements();
            foreach (var element in gridElements)
            {
                DrawGridElement(spriteBatch, element, CellSize);
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                    spriteBatch.Draw(_spriteManager.WorldMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }

            if (RightClickedWorldPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(RightClickedWorldPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle markerRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                    spriteBatch.Draw(_spriteManager.WorldMapHoverSelectorSprite, markerRect, Color.Cyan * 0.6f);
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(spriteBatch, font);
        }

        private void DrawLocalMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds)
        {
            DrawLocalMapBackground(spriteBatch, font, gameTime, overrideBounds);
            DrawLocalMapEntities(spriteBatch, font, gameTime);
        }

        public void DrawLocalMapBackground(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds = null)
        {
            CalculateMapLayout(overrideBounds);
            DrawMapFrame(spriteBatch, font, gameTime);

            var backgroundElements = GenerateLocalMapBackgroundElements();
            foreach (var element in backgroundElements)
            {
                DrawGridElement(spriteBatch, element, CellSize);
            }

            // Draw AI Preview Paths
            if (_gameState.AIPreviewPaths.Any())
            {
                foreach (var entry in _gameState.AIPreviewPaths)
                {
                    var path = entry.Value;
                    if (!path.Any()) continue;

                    // Draw path segments
                    foreach (var node in path)
                    {
                        Vector2? screenPos = MapCoordsToScreen(node.Position);
                        if (screenPos.HasValue)
                        {
                            var destRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                            Texture2D texture = node.Mode == MovementMode.Run ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                            spriteBatch.Draw(texture, destRect, _global.Palette_Red * 0.4f);
                        }
                    }

                    // Draw a distinct marker at the end of the path
                    var endNode = path.Last();
                    Vector2? endScreenPos = MapCoordsToScreen(endNode.Position);
                    if (endScreenPos.HasValue)
                    {
                        var destRect = new Rectangle((int)endScreenPos.Value.X, (int)endScreenPos.Value.Y, CellSize, CellSize);
                        bool isPulsing = _animationManager.IsPulsing("PulseFast");
                        float alpha = isPulsing ? 0.8f : 0.5f;
                        spriteBatch.Draw(_spriteManager.PathEndSprite, destRect, _global.Palette_Red * alpha);
                    }
                }
            }

            // Draw Combat Move Preview Path
            if (_gameState.IsInCombat && _gameState.CombatMovePreviewPath.Any())
            {
                foreach (var (pos, mode) in _gameState.CombatMovePreviewPath)
                {
                    Vector2? screenPos = MapCoordsToScreen(pos);
                    if (screenPos.HasValue)
                    {
                        bool isRunning = mode == MovementMode.Run;
                        Texture2D texture = isRunning ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                        Color color = (isRunning ? _global.RunPathColor : _global.PathColor) * 0.6f; // Use transparency for preview

                        var destRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                        spriteBatch.Draw(texture, destRect, color);
                    }
                }
            }

            if (_hoveredGridPos.HasValue)
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    Rectangle indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                    spriteBatch.Draw(_spriteManager.LocalMapHoverSelectorSprite, indicatorRect, Color.Lime * 0.5f);
                }
            }
        }

        public void DrawLocalMapEntities(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var entityElements = GenerateLocalMapEntityElements();
            foreach (var element in entityElements)
            {
                DrawGridElement(spriteBatch, element, CellSize);
            }

            // --- COMBAT AND AI INDICATOR DRAWING ---
            var entitiesToDrawOverlays = _gameState.IsInCombat ? _gameState.Combatants : _gameState.ActiveEntities;
            foreach (var entityId in entitiesToDrawOverlays)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var interpComp = _componentStore.GetComponent<InterpolationComponent>(entityId);
                if (localPosComp == null) continue;

                Vector2 positionToDraw = interpComp != null ? interpComp.CurrentVisualPosition : localPosComp.LocalPosition;

                Vector2? screenPos = MapCoordsToScreen(positionToDraw);
                if (!screenPos.HasValue) continue;

                var cellRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);

                // Draw AI Intent Indicators (out of combat)
                if (!_gameState.IsInCombat)
                {
                    var intentComp = _componentStore.GetComponent<AIIntentComponent>(entityId);
                    if (intentComp != null && intentComp.CurrentIntent != AIIntent.None)
                    {
                        Texture2D indicatorSprite = intentComp.CurrentIntent == AIIntent.Pursuing
                            ? _spriteManager.WarningMarkSprite
                            : _spriteManager.DoubleWarningMarkSprite;

                        if (indicatorSprite != null)
                        {
                            var indicatorRect = new Rectangle(
                                cellRect.Center.X - indicatorSprite.Width / 2,
                                cellRect.Y - indicatorSprite.Height - 1, // 1 pixel padding
                                indicatorSprite.Width,
                                indicatorSprite.Height
                            );
                            spriteBatch.Draw(indicatorSprite, indicatorRect, Color.White);
                        }
                    }
                }

                // Draw Combat Indicators
                if (_gameState.IsInCombat)
                {
                    // Only draw the selected target highlight if it's the player's turn.
                    if (_gameState.CurrentTurnEntityId == _gameState.PlayerEntityId)
                    {
                        if (entityId == _gameState.SelectedTargetId)
                        {
                            bool isInflated = _animationManager.IsPulsing("TargetSelector");
                            Rectangle highlightRect = isInflated
                                ? new Rectangle(cellRect.X - 1, cellRect.Y - 1, cellRect.Width + 2, cellRect.Height + 2)
                                : cellRect;
                            DrawHollowRectangle(spriteBatch, highlightRect, _global.Palette_Pink, 1);
                        }
                    }

                    if (entityId == _gameState.CurrentTurnEntityId)
                    {
                        float yOffset = _animationManager.GetBobbingOffset("TurnIndicator");
                        var indicatorSprite = _spriteManager.TurnIndicatorSprite;
                        if (indicatorSprite != null)
                        {
                            var indicatorRect = new Rectangle(
                                cellRect.Center.X - indicatorSprite.Width / 2,
                                (int)(cellRect.Y - indicatorSprite.Height - 2 + yOffset),
                                indicatorSprite.Width,
                                indicatorSprite.Height
                            );
                            spriteBatch.Draw(indicatorSprite, indicatorRect, Color.White);
                        }
                    }
                }
            }

            if (_gameState.IsPaused) DrawPauseIcon(spriteBatch, font);
        }

        private void DrawMapFrame(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();
            int frameHeight = MapScreenBounds.Height + 30;

            // Draw the opaque background for the map area first.
            spriteBatch.Draw(pixel, MapScreenBounds, _global.TerminalBg);

            spriteBatch.Draw(pixel, new Rectangle(MapScreenBounds.X - 5, MapScreenBounds.Y - 25, MapScreenBounds.Width + 10, 2), _global.Palette_White); // Top
            spriteBatch.Draw(pixel, new Rectangle(MapScreenBounds.X - 5, MapScreenBounds.Y + MapScreenBounds.Height + 3, MapScreenBounds.Width + 10, 2), _global.Palette_White); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(MapScreenBounds.X - 5, MapScreenBounds.Y - 25, 2, frameHeight), _global.Palette_White); // Left
            spriteBatch.Draw(pixel, new Rectangle(MapScreenBounds.Right + 3, MapScreenBounds.Y - 25, 2, frameHeight), _global.Palette_White); // Right
            spriteBatch.Draw(pixel, new Rectangle(MapScreenBounds.X - 5, MapScreenBounds.Y - 5, MapScreenBounds.Width + 10, 2), _global.Palette_White); // Separator

            string timeText = _worldClockManager.CurrentTime;
            _timeTextPos = new Vector2(MapScreenBounds.X, MapScreenBounds.Y - 12 - font.LineHeight / 2);
            spriteBatch.DrawString(font, timeText, _timeTextPos, _global.GameTextColor);

            LayoutHeaderButtons();
            foreach (var b in _headerButtons)
            {
                b.Draw(spriteBatch, font, gameTime);
            }
        }

        public void LayoutHeaderButtons()
        {
            const int buttonHeight = 16;
            const int buttonSpacing = 5;
            const int goStopButtonWidth = 45;
            const int toggleButtonWidth = 85;

            int headerContentRightEdge = MapScreenBounds.Right - 2;
            int buttonY = MapScreenBounds.Y - 12 - buttonHeight / 2;

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
            spriteBatch.DrawString(font, pauseText, textPosition, Color.White * 0.7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private List<GridElement> GenerateWorldMapGridElements()
        {
            var elements = new List<GridElement>();
            int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;

            for (int y = 0; y < GridSizeY; y++)
            {
                for (int x = 0; x < GridSizeX; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;
                    float noise = _gameState.GetNoiseAt(worldX, worldY);
                    Vector2? screenPos = MapCoordsToScreen(new Vector2(worldX, worldY));
                    if (screenPos.HasValue)
                    {
                        elements.Add(new GridElement(GetTerrainTexture(noise), GetTerrainColor(noise), screenPos.Value));
                    }
                }
            }

            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = _componentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId) ?? (IAction)_componentStore.GetComponent<RestAction>(_gameState.PlayerEntityId);
            if (activePlayerAction != null)
            {
                allPlayerActions.Add(activePlayerAction);
            }

            elements.AddRange(GeneratePlayerPathGridElements(allPlayerActions, isLocalPath: false));

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

        private List<GridElement> GenerateLocalMapBackgroundElements()
        {
            var elements = new List<GridElement>();
            Color bgColor = _global.Palette_DarkGray;
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            int startX = (int)_gameState.PlayerLocalPos.X - GridSizeX / 2;
            int startY = (int)_gameState.PlayerLocalPos.Y - GridSizeY / 2;

            for (int y = 0; y < GridSizeY; y++)
            {
                for (int x = 0; x < GridSizeX; x++)
                {
                    int localX = startX + x;
                    int localY = startY + y;

                    // Only draw if the coordinate is within the logical grid bounds
                    if (localX >= 0 && localX < Global.LOCAL_GRID_SIZE && localY >= 0 && localY < Global.LOCAL_GRID_SIZE)
                    {
                        Vector2? screenPos = MapCoordsToScreen(new Vector2(localX, localY));
                        if (screenPos.HasValue)
                        {
                            elements.Add(new GridElement(pixel, bgColor, screenPos.Value));
                        }
                    }
                }
            }

            // --- Draw Player's Pending Path (Local View) ---
            var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
            if (playerActionQueue != null && playerActionQueue.ActionQueue.Any())
            {
                elements.AddRange(GeneratePlayerPathGridElements(playerActionQueue.ActionQueue, isLocalPath: true));
            }

            return elements;
        }

        private List<GridElement> GenerateLocalMapEntityElements()
        {
            var elements = new List<GridElement>();
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var renderComp = _componentStore.GetComponent<RenderableComponent>(entityId);
                var interpComp = _componentStore.GetComponent<InterpolationComponent>(entityId);
                var corpseComp = _componentStore.GetComponent<CorpseComponent>(entityId);

                if (localPosComp != null && renderComp != null)
                {
                    Vector2 positionToDraw = interpComp != null ? interpComp.CurrentVisualPosition : localPosComp.LocalPosition;

                    Vector2? screenPos = MapCoordsToScreen(positionToDraw);
                    if (screenPos.HasValue)
                    {
                        Texture2D textureToDraw = (corpseComp != null) ? pixel : (renderComp.Texture ?? pixel);
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

        /// <summary>
        /// Generates the visual path for the player's action queue, simulating energy cost to show where they will become exhausted.
        /// </summary>
        private List<GridElement> GeneratePlayerPathGridElements(IEnumerable<IAction> actions, bool isLocalPath)
        {
            var elements = new List<GridElement>();
            var playerStats = _gameState.PlayerStats;
            if (playerStats == null || !actions.Any()) return elements;

            int simulatedEnergy = playerStats.CurrentEnergyPoints;

            foreach (var action in actions)
            {
                Vector2 actionPos = Vector2.Zero;
                Texture2D actionTexture = null;
                Color actionColor = Color.Transparent;

                if (action is MoveAction moveAction)
                {
                    actionPos = moveAction.Destination;
                    bool isRunning = moveAction.Mode == MovementMode.Run;
                    bool canRun = isRunning && simulatedEnergy >= _gameState.GetMovementEnergyCost(moveAction, isLocalPath);

                    actionTexture = canRun ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                    actionColor = canRun ? _global.RunPathColor : _global.PathColor;

                    if (isRunning)
                    {
                        simulatedEnergy -= _gameState.GetMovementEnergyCost(moveAction, isLocalPath);
                    }
                }
                else if (action is RestAction restAction)
                {
                    actionPos = restAction.Position;
                    actionTexture = restAction.RestType == RestType.ShortRest ? _spriteManager.ShortRestSprite : _spriteManager.LongRestSprite;
                    actionColor = _global.ShortRestColor;

                    // Simulate energy gain from the rest
                    switch (restAction.RestType)
                    {
                        case RestType.ShortRest: simulatedEnergy += playerStats.ShortRestEnergyRestored; break;
                        case RestType.LongRest: simulatedEnergy += playerStats.LongRestEnergyRestored; break;
                        case RestType.FullRest: simulatedEnergy += playerStats.FullRestEnergyRestored; break;
                    }
                    simulatedEnergy = System.Math.Min(simulatedEnergy, playerStats.MaxEnergyPoints);
                }
                else
                {
                    // If the action is not a Move or Rest action (e.g., EndTurnAction),
                    // we don't have a visual for it, so we skip it.
                    continue;
                }

                Vector2? screenPos = MapCoordsToScreen(actionPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(actionTexture, actionColor, screenPos.Value));
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
                int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
                int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
                int gridX = (int)mapPos.X - startX;
                int gridY = (int)mapPos.Y - startY;

                if (gridX >= 0 && gridX < GridSizeX && gridY >= 0 && gridY < GridSizeY)
                {
                    return new Vector2(_mapGridBounds.X + gridX * CellSize, _mapGridBounds.Y + gridY * CellSize);
                }
            }
            else
            {
                int startX = (int)_gameState.PlayerLocalPos.X - GridSizeX / 2;
                int startY = (int)_gameState.PlayerLocalPos.Y - GridSizeY / 2;
                float gridX = mapPos.X - startX;
                float gridY = mapPos.Y - startY;

                if (gridX >= 0 && gridX < GridSizeX && gridY >= 0 && gridY < GridSizeY)
                {
                    return new Vector2(_mapGridBounds.X + gridX * CellSize, _mapGridBounds.Y + gridY * CellSize);
                }
            }
            return null;
        }

        /// <summary>
        /// Converts a screen position (e.g., mouse coordinates) to local grid coordinates.
        /// </summary>
        /// <param name="screenPosition">The position on the screen.</param>
        /// <returns>The corresponding coordinates on the local grid.</returns>
        public Vector2 ScreenToLocalGrid(Point screenPosition)
        {
            if (!_mapGridBounds.Contains(screenPosition))
            {
                // Return an invalid position if the click is outside the map area.
                return new Vector2(-1, -1);
            }

            int gridX = (screenPosition.X - _mapGridBounds.X) / CellSize;
            int gridY = (screenPosition.Y - _mapGridBounds.Y) / CellSize;

            if (_gameState.CurrentMapView == MapView.World)
            {
                int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
                int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
                return new Vector2(startX + gridX, startY + gridY);
            }
            else // Local Map
            {
                int startX = (int)_gameState.PlayerLocalPos.X - GridSizeX / 2;
                int startY = (int)_gameState.PlayerLocalPos.Y - GridSizeY / 2;
                return new Vector2(startX + gridX, startY + gridY);
            }
        }

        /// <summary>
        /// Gets the ID of the entity at a specific mouse position on the local map.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor in virtual screen coordinates.</param>
        /// <returns>The entity ID of the combatant, or null if no combatant is at that position.</returns>
        public int? GetEntityIdAt(Point mousePosition)
        {
            if (!_mapGridBounds.Contains(mousePosition) || !_gameState.IsInCombat) return null;

            foreach (var entityId in _gameState.Combatants)
            {
                if (entityId == _gameState.PlayerEntityId) continue;

                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp == null) continue;

                Vector2? screenPos = MapCoordsToScreen(localPosComp.LocalPosition);
                if (screenPos.HasValue)
                {
                    var cellRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
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