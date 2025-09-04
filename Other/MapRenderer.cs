using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
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
        private readonly Global _global;

        private Vector2? _hoveredGridPos;
        private Rectangle _mapGridBounds;
        private readonly ContextMenu _contextMenu;
        private readonly Dictionary<string, Button> _buttonMap;

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;

        // New public properties for dynamic layout
        public Rectangle MapScreenBounds { get; private set; }
        public int GridSizeX { get; private set; }
        public int GridSizeY { get; private set; }
        public int CellSize { get; private set; }

        // Camera Panning
        public Vector2 CameraOffset { get; private set; } = Vector2.Zero;
        public bool IsCameraDetached => CameraOffset != Vector2.Zero;


        // --- TUNING ---
        private const int MAP_GRID_WIDTH_CELLS = 29;
        private const int MAP_GRID_HEIGHT_CELLS = 29;

        // Animation state for path nodes
        private readonly Dictionary<Vector2, float> _pathNodeAnimationOffsets = new Dictionary<Vector2, float>();
        private readonly Random _pathAnimRandom = new Random();

        // Animation state for the footer tab
        private float _footerYOffset;
        private float _footerTargetYOffset;
        private const float FOOTER_ANIMATION_SPEED = 8f;
        private const float FOOTER_HIDDEN_Y_OFFSET = -20f; // Negative to move it UP, just enough to hide it

        // Animation state for map frame sway
        public OrganicSwayAnimation SwayAnimation { get; }
        private const float SWAY_SPEED_X = 0.8f;
        private const float SWAY_SPEED_Y = 0.6f;
        private const float SWAY_AMOUNT = 1.5f;

        public MapRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _global = ServiceLocator.Get<Global>();
            _contextMenu = new ContextMenu();

            _headerButtons.Add(new Button(Rectangle.Empty, "Clear") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Go") { IsEnabled = false });
            _headerButtons.Add(new Button(Rectangle.Empty, "Stop") { IsEnabled = false });

            _buttonMap = _headerButtons.ToDictionary(b => b.Function.ToLowerInvariant(), b => b);

            SwayAnimation = new OrganicSwayAnimation(SWAY_SPEED_X, SWAY_SPEED_Y, SWAY_AMOUNT, SWAY_AMOUNT);

            ResetHeaderState();
        }

        public void SetCameraOffset(Vector2 newOffset)
        {
            CameraOffset = newOffset;
        }

        public void ResetCamera()
        {
            CameraOffset = Vector2.Zero;
        }

        public void ResetHeaderState()
        {
            _footerYOffset = FOOTER_HIDDEN_Y_OFFSET;
            _footerTargetYOffset = FOOTER_HIDDEN_Y_OFFSET;
        }

        public void Update(GameTime gameTime, BitmapFont font)
        {
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            UpdateHover(virtualMousePos);
            _contextMenu.Update(mouseState, mouseState, virtualMousePos, font); // Note: prevMouseState is same as current here, might need adjustment if complex logic is added

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Footer Animation Logic ---
            _footerTargetYOffset = _gameState.PendingActions.Any() ? 0f : FOOTER_HIDDEN_Y_OFFSET;
            _footerYOffset = MathHelper.Lerp(_footerYOffset, _footerTargetYOffset, deltaTime * FOOTER_ANIMATION_SPEED);
        }

        private void CalculateMapLayout(Rectangle? overrideBounds = null)
        {
            if (overrideBounds.HasValue)
            {
                MapScreenBounds = overrideBounds.Value;
            }
            else
            {
                // --- SIZE CALCULATION ---
                // The map size is now fixed based on the desired number of cells.
                CellSize = Global.GRID_CELL_SIZE;
                int finalMapWidth = MAP_GRID_WIDTH_CELLS * CellSize;
                int finalMapHeight = MAP_GRID_HEIGHT_CELLS * CellSize;

                // --- CENTERING CALCULATION ---
                int mapX = (Global.VIRTUAL_WIDTH - finalMapWidth) / 2;
                int mapY = (Global.VIRTUAL_HEIGHT - finalMapHeight) / 2 - 7; // Shift map up by 7 pixels
                MapScreenBounds = new Rectangle(mapX, mapY, finalMapWidth, finalMapHeight);
            }

            // --- GRID SETUP ---
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
                    Vector2 viewCenter = _gameState.PlayerWorldPos + CameraOffset;
                    int startX = (int)viewCenter.X - GridSizeX / 2;
                    int startY = (int)viewCenter.Y - GridSizeY / 2;
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
                    var markerTexture = _spriteManager.MapMarkerSprite;
                    if (markerTexture != null)
                    {
                        var indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, CellSize, CellSize);
                        spriteBatch.DrawSnapped(markerTexture, indicatorRect, _global.Palette_Orange);
                    }
                }
            }

            DrawPlayerOffscreenIndicator(spriteBatch);
        }

        private void DrawMapFrame(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Calculate sway offset
            var swayOffset = SwayAnimation.Offset;

            // Create swayed versions of bounds for drawing
            var swayedMapScreenBounds = new Rectangle(
                MapScreenBounds.X + (int)swayOffset.X,
                MapScreenBounds.Y + (int)swayOffset.Y,
                MapScreenBounds.Width,
                MapScreenBounds.Height
            );

            // --- 1. Draw the animated footer FIRST ---
            // Only draw if it's not fully hidden
            if (_footerYOffset > FOOTER_HIDDEN_Y_OFFSET + 1)
            {
                int footerWidth = swayedMapScreenBounds.Width;
                int footerX = swayedMapScreenBounds.X;
                int footerY = (int)(swayedMapScreenBounds.Bottom + 4 + _footerYOffset);

                Rectangle footerBounds = new Rectangle(footerX, footerY, footerWidth, 16);

                // Draw footer background
                spriteBatch.Draw(pixel, footerBounds, _global.TerminalBg);

                // Draw footer frame lines (1px thick)
                spriteBatch.Draw(pixel, new Rectangle(footerBounds.X, footerBounds.Bottom - 1, footerBounds.Width, 1), _global.Palette_White); // Bottom
                spriteBatch.Draw(pixel, new Rectangle(footerBounds.X, footerBounds.Y, 1, footerBounds.Height), _global.Palette_White); // Left
                spriteBatch.Draw(pixel, new Rectangle(footerBounds.Right - 1, footerBounds.Y, 1, footerBounds.Height), _global.Palette_White); // Right

                LayoutAndPositionButtons(footerBounds);
                foreach (var b in _headerButtons)
                {
                    b.Draw(spriteBatch, font, gameTime);
                }
            }

            // --- 2. Draw the main map frame and background on top of the footer ---
            int framePadding = 4;
            Rectangle fullFrameArea = new Rectangle(
                swayedMapScreenBounds.X - framePadding,
                swayedMapScreenBounds.Y - framePadding,
                swayedMapScreenBounds.Width + (framePadding * 2),
                swayedMapScreenBounds.Height + (framePadding * 2)
            );

            // Draw the background for the entire framed area.
            spriteBatch.Draw(pixel, fullFrameArea, _global.TerminalBg);

            // Draw main frame lines (1px thick)
            spriteBatch.Draw(pixel, new Rectangle(fullFrameArea.X, fullFrameArea.Y, fullFrameArea.Width, 1), _global.Palette_White); // Top border
            spriteBatch.Draw(pixel, new Rectangle(fullFrameArea.X, fullFrameArea.Bottom - 1, fullFrameArea.Width, 1), _global.Palette_White); // Bottom border
            spriteBatch.Draw(pixel, new Rectangle(fullFrameArea.X, fullFrameArea.Y, 1, fullFrameArea.Height), _global.Palette_White); // Left border
            spriteBatch.Draw(pixel, new Rectangle(fullFrameArea.Right - 1, fullFrameArea.Y, 1, fullFrameArea.Height), _global.Palette_White); // Right border
        }

        public void LayoutAndPositionButtons(Rectangle footerBounds)
        {
            const int buttonHeight = 16;
            const int buttonSpacing = 5;
            const int buttonWidth = 45;

            int totalWidth = (buttonWidth * 3) + (buttonSpacing * 2);
            int groupStartX = footerBounds.X + (footerBounds.Width - totalWidth) / 2;

            int currentX = groupStartX;
            int buttonY = footerBounds.Y + (footerBounds.Height - buttonHeight) / 2;

            if (_buttonMap.TryGetValue("clear", out Button clearButton))
            {
                clearButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
                currentX += buttonWidth + buttonSpacing;
            }

            if (_buttonMap.TryGetValue("go", out Button goButton))
            {
                goButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
                currentX += buttonWidth + buttonSpacing;
            }

            if (_buttonMap.TryGetValue("stop", out Button stopButton))
            {
                stopButton.Bounds = new Rectangle(currentX, buttonY, buttonWidth, buttonHeight);
            }
        }

        private List<GridElement> GenerateWorldMapGridElements()
        {
            var elements = new List<GridElement>();
            Vector2 viewCenter = _gameState.PlayerWorldPos + CameraOffset;
            int startX = (int)viewCenter.X - GridSizeX / 2;
            int startY = (int)viewCenter.Y - GridSizeY / 2;

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
            var activePlayerAction = (IAction)_componentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId);
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
                   texture == _spriteManager.RunPathSprite;
        }

        private void DrawGridElement(SpriteBatch spriteBatch, GridElement element, int cellSize, GameTime gameTime)
        {
            Vector2 finalPosition = element.ScreenPosition;

            // Apply a subtle flicker/jitter animation to path nodes.
            if (_pathNodeAnimationOffsets.ContainsKey(element.WorldPosition) && IsPathPipTexture(element.Texture))
            {
                float timeOffset = _pathNodeAnimationOffsets[element.WorldPosition];
                float totalTime = (float)gameTime.TotalGameTime.TotalSeconds + timeOffset;
                float cycleDuration = 0.8f; // Duration of one flicker cycle (offset + return to center)
                float progressInCycle = (totalTime % cycleDuration) / cycleDuration;

                Vector2 offset = Vector2.Zero;
                if (progressInCycle < 0.5f) // First half of the cycle: apply offset
                {
                    // Use the cycle number to get a stable random direction for the duration of the offset.
                    int cycleNumber = (int)(totalTime / cycleDuration);
                    var rand = new Random(element.WorldPosition.GetHashCode() ^ cycleNumber);
                    int direction = rand.Next(4);
                    switch (direction)
                    {
                        case 0: offset = new Vector2(0, -1); break; // Up
                        case 1: offset = new Vector2(0, 1); break;  // Down
                        case 2: offset = new Vector2(-1, 0); break; // Left
                        case 3: offset = new Vector2(1, 0); break;  // Right
                    }
                }
                // Second half of the cycle: offset remains Zero (return to center).
                finalPosition += offset;
            }

            Rectangle destRect = new Rectangle((int)finalPosition.X, (int)finalPosition.Y, cellSize, cellSize);
            spriteBatch.DrawSnapped(element.Texture, destRect, element.Color);
        }

        private void DrawPlayerOffscreenIndicator(SpriteBatch spriteBatch)
        {
            Vector2 playerPos = _gameState.PlayerWorldPos;
            Vector2 viewCenter = _gameState.PlayerWorldPos + CameraOffset;

            // Check if player is on-screen. If so, do nothing.
            if (MapCoordsToScreen(playerPos).HasValue)
            {
                return;
            }

            // 1. Calculate direction from view center to player
            Vector2 directionToPlayer = playerPos - viewCenter;
            if (directionToPlayer == Vector2.Zero) return; // Should not happen if off-screen, but a good safeguard.

            // 2. Convert direction to an angle and then to a sprite index (0-7)
            float angle = MathF.Atan2(directionToPlayer.Y, directionToPlayer.X);
            int index = (int)Math.Round((angle + MathHelper.Pi) / MathHelper.TwoPi * 8) % 8;

            // 3. Calculate the arrow's position, clamped to the map border
            Rectangle arrowSourceRect = _spriteManager.ArrowIconSourceRects[index];
            Vector2 halfSize = new Vector2((MapScreenBounds.Width - arrowSourceRect.Width) / 2f, (MapScreenBounds.Height - arrowSourceRect.Height) / 2f);
            Vector2 playerOffsetFromCenter = directionToPlayer * CellSize;

            float scaleX = (playerOffsetFromCenter.X != 0) ? halfSize.X / Math.Abs(playerOffsetFromCenter.X) : float.MaxValue;
            float scaleY = (playerOffsetFromCenter.Y != 0) ? halfSize.Y / Math.Abs(playerOffsetFromCenter.Y) : float.MaxValue;
            float scale = Math.Min(scaleX, scaleY);

            Vector2 edgePosition = MapScreenBounds.Center.ToVector2() + playerOffsetFromCenter * scale;

            // 4. Draw the arrow
            var destRect = new Rectangle(
                (int)(edgePosition.X - arrowSourceRect.Width / 2f),
                (int)(edgePosition.Y - arrowSourceRect.Height / 2f),
                arrowSourceRect.Width,
                arrowSourceRect.Height
            );

            spriteBatch.DrawSnapped(
                _spriteManager.ArrowIconSpriteSheet,
                destRect,
                arrowSourceRect,
                _global.Palette_Red
            );
        }

        public Vector2? MapCoordsToScreen(Vector2 mapPos)
        {
            if (_mapGridBounds.IsEmpty) return null;

            Vector2 viewCenter = _gameState.PlayerWorldPos + CameraOffset;
            int startX = (int)viewCenter.X - GridSizeX / 2;
            int startY = (int)viewCenter.Y - GridSizeY / 2;
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

            Vector2 viewCenter = _gameState.PlayerWorldPos + CameraOffset;
            int startX = (int)viewCenter.X - GridSizeX / 2;
            int startY = (int)viewCenter.Y - GridSizeY / 2;
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
    }
}