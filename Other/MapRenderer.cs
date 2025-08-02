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

        private Vector2? _hoveredGridPos;
        private Rectangle _mapGridBounds;
        private readonly ContextMenu _contextMenu;
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

        // Animation state for path nodes
        private readonly Dictionary<Vector2, float> _pathNodeAnimationOffsets = new Dictionary<Vector2, float>();
        private readonly Random _pathAnimRandom = new Random();

        public MapRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _global = ServiceLocator.Get<Global>();
            _contextMenu = new ContextMenu();

            _headerButtons.Add(new Button(Rectangle.Empty, "Clear") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });

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
                const int worldCellSize = Global.GRID_CELL_SIZE;

                int maxWidth = (int)(Global.VIRTUAL_WIDTH * Global.MAP_AREA_WIDTH_PERCENT);
                int maxHeight = Global.VIRTUAL_HEIGHT - Global.MAP_TOP_PADDING - Global.TERMINAL_AREA_HEIGHT - 10;
                int baseMapSize = Math.Min(maxWidth, maxHeight);

                int baseGridSize = baseMapSize / worldCellSize;
                int worldGridSize = baseGridSize + 4;

                int finalMapSize = worldGridSize * worldCellSize;

                int mapX = (Global.VIRTUAL_WIDTH - finalMapSize) / 2;
                int mapY = Global.MAP_TOP_PADDING + 7; // Move map and header down by 7 pixels
                MapScreenBounds = new Rectangle(mapX, mapY, finalMapSize, finalMapSize);
            }

            // --- GRID SETUP ---
            CellSize = Global.GRID_CELL_SIZE;
            _mapGridBounds = MapScreenBounds;

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
                    int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
                    int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
                    currentHoveredGridPos = new Vector2(startX + gridX, startY + gridY);
                }
            }

            if (!currentHoveredGridPos.HasValue) return;

            _hoveredGridPos = currentHoveredGridPos;

            // Do not show tooltips while the player is auto-moving.
            if (_gameState.IsExecutingActions) return;

            var stringBuilder = new StringBuilder();

            // --- Tooltip Logic ---
            int posX = (int)currentHoveredGridPos.Value.X;
            int posY = (int)currentHoveredGridPos.Value.Y;

            float noise = _gameState.GetNoiseAt(posX, posY);
            string terrainName = GetTerrainName(noise);
            stringBuilder.Append($"Pos: ({posX}, {posY})\n");
            stringBuilder.Append($"Terrain: {terrainName}\n");
            stringBuilder.Append($"Noise: {noise:F2}");
            _tooltipManager.RequestTooltip(currentHoveredGridPos.Value, stringBuilder.ToString(), virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
        }

        public void DrawMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds = null)
        {
            if (_gameState.IsActionQueueDirty)
            {
                _pathNodeAnimationOffsets.Clear();
                foreach (var action in _gameState.PendingActions)
                {
                    Vector2 actionPos = Vector2.Zero;
                    if (action is MoveAction moveAction)
                    {
                        actionPos = moveAction.Destination;
                    }
                    else if (action is RestAction restAction)
                    {
                        actionPos = restAction.Position;
                    }

                    if (actionPos != Vector2.Zero)
                    {
                        _pathNodeAnimationOffsets[actionPos] = (float)_pathAnimRandom.NextDouble() * 10f;
                    }
                }
                _gameState.IsActionQueueDirty = false;
            }

            DrawWorldMap(spriteBatch, font, gameTime, overrideBounds);
            _contextMenu.Draw(spriteBatch, font);
        }

        private void DrawWorldMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle? overrideBounds)
        {
            CalculateMapLayout(overrideBounds);
            DrawMapFrame(spriteBatch, font, gameTime);

            var gridElements = GenerateWorldMapGridElements();
            foreach (var element in gridElements)
            {
                DrawGridElement(spriteBatch, element, CellSize, gameTime);
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

            int currentX = MapScreenBounds.Right - 2;
            int buttonY = MapScreenBounds.Y - 12 - buttonHeight / 2;

            if (_buttonMap.TryGetValue("stop", out Button stopButton))
            {
                currentX -= goStopButtonWidth;
                stopButton.Bounds = new Rectangle(currentX, buttonY, goStopButtonWidth, buttonHeight);
                currentX -= buttonSpacing;
            }

            if (_buttonMap.TryGetValue("go", out Button goButton))
            {
                currentX -= goStopButtonWidth;
                goButton.Bounds = new Rectangle(currentX, buttonY, goStopButtonWidth, buttonHeight);
                currentX -= buttonSpacing;
            }

            if (_buttonMap.TryGetValue("clear", out Button clearButton))
            {
                currentX -= goStopButtonWidth;
                clearButton.Bounds = new Rectangle(currentX, buttonY, goStopButtonWidth, buttonHeight);
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

            // 1. Draw Terrain
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
                        elements.Add(new GridElement(GetTerrainTexture(noise), GetTerrainColor(noise), screenPos.Value, new Vector2(worldX, worldY)));
                    }
                }
            }

            // 2. Draw Player Path
            var allPlayerActions = new List<IAction>(_gameState.PendingActions);
            var activePlayerAction = _componentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId) ?? (IAction)_componentStore.GetComponent<RestAction>(_gameState.PlayerEntityId);
            if (activePlayerAction != null)
            {
                allPlayerActions.Add(activePlayerAction);
            }
            elements.AddRange(GeneratePlayerPathGridElements(allPlayerActions));

            // 3. Get all important entities to draw (excluding player for now)
            var entitiesToDraw = new List<int>();
            foreach (var entityId in _gameState.ActiveEntities)
            {
                if (entityId != _gameState.PlayerEntityId &&
                    _componentStore.HasComponent<PositionComponent>(entityId) &&
                    _componentStore.HasComponent<RenderableComponent>(entityId))
                {
                    entitiesToDraw.Add(entityId);
                }
            }

            // 4. Draw the important NPCs
            foreach (var entityId in entitiesToDraw)
            {
                var renderComp = _componentStore.GetComponent<RenderableComponent>(entityId);
                var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
                Vector2? screenPos = MapCoordsToScreen(posComp.WorldPosition);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(renderComp.Texture, renderComp.Color, screenPos.Value, posComp.WorldPosition));
                }
            }

            // 5. Draw the player last (on top)
            var playerRenderComp = _componentStore.GetComponent<RenderableComponent>(_gameState.PlayerEntityId);
            if (playerRenderComp != null)
            {
                Vector2? screenPos = MapCoordsToScreen(_gameState.PlayerWorldPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(playerRenderComp.Texture, playerRenderComp.Color, screenPos.Value, _gameState.PlayerWorldPos));
                }
            }

            return elements;
        }

        /// <summary>
        /// Generates the visual path for the player's action queue, simulating energy cost to show where they will become exhausted.
        /// </summary>
        private List<GridElement> GeneratePlayerPathGridElements(IEnumerable<IAction> actions)
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
                    bool canRun = isRunning && simulatedEnergy >= _gameState.GetMovementEnergyCost(moveAction);

                    actionTexture = canRun ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                    actionColor = canRun ? _global.RunPathColor : _global.PathColor;

                    if (isRunning)
                    {
                        simulatedEnergy -= _gameState.GetMovementEnergyCost(moveAction);
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
                    continue;
                }

                Vector2? screenPos = MapCoordsToScreen(actionPos);
                if (screenPos.HasValue)
                {
                    elements.Add(new GridElement(actionTexture, actionColor, screenPos.Value, actionPos));
                }
            }
            return elements;
        }

        private bool IsPathPipTexture(Texture2D texture)
        {
            return texture == _spriteManager.PathSprite ||
                   texture == _spriteManager.RunPathSprite ||
                   texture == _spriteManager.ShortRestSprite ||
                   texture == _spriteManager.LongRestSprite;
        }

        private void DrawGridElement(SpriteBatch spriteBatch, GridElement element, int cellSize, GameTime gameTime)
        {
            Vector2 finalPosition = element.ScreenPosition;

            // Only apply the sway animation to path-related textures that are in the animation dictionary.
            if (_pathNodeAnimationOffsets.ContainsKey(element.WorldPosition) && IsPathPipTexture(element.Texture))
            {
                float offset = _pathNodeAnimationOffsets[element.WorldPosition];
                const float SWAY_SPEED = 1f;
                const float SWAY_AMOUNT_X = 1f;
                const float SWAY_AMOUNT_Y = 1f;
                float swayTimer = (float)gameTime.TotalGameTime.TotalSeconds + offset;
                float swayOffsetX = (float)Math.Sin(swayTimer * SWAY_SPEED) * SWAY_AMOUNT_X;
                float swayOffsetY = (float)Math.Sin(swayTimer * SWAY_SPEED * 2) * SWAY_AMOUNT_Y * 0.5f;
                finalPosition += new Vector2(swayOffsetX, swayOffsetY);
            }

            Rectangle destRect = new Rectangle((int)finalPosition.X, (int)finalPosition.Y, cellSize, cellSize);
            spriteBatch.Draw(element.Texture, destRect, element.Color);
        }

        public Vector2? MapCoordsToScreen(Vector2 mapPos)
        {
            if (_mapGridBounds.IsEmpty) return null;

            int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
            int gridX = (int)mapPos.X - startX;
            int gridY = (int)mapPos.Y - startY;

            if (gridX >= 0 && gridX < GridSizeX && gridY >= 0 && gridY < GridSizeY)
            {
                return new Vector2(_mapGridBounds.X + gridX * CellSize, _mapGridBounds.Y + gridY * CellSize);
            }
            return null;
        }

        public Vector2 ScreenToWorldGrid(Point screenPosition)
        {
            if (!_mapGridBounds.Contains(screenPosition))
            {
                return new Vector2(-1, -1);
            }

            int gridX = (screenPosition.X - _mapGridBounds.X) / CellSize;
            int gridY = (screenPosition.Y - _mapGridBounds.Y) / CellSize;

            int startX = (int)_gameState.PlayerWorldPos.X - GridSizeX / 2;
            int startY = (int)_gameState.PlayerWorldPos.Y - GridSizeY / 2;
            return new Vector2(startX + gridX, startY + gridY);
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