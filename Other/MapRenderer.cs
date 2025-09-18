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

        private readonly List<Button> _headerButtons = new List<Button>();
        public IEnumerable<Button> HeaderButtons => _headerButtons;

        public Vector2? HoveredGridPos => _hoveredGridPos;
        public ContextMenu MapContextMenu => _contextMenu;

        // New public properties for dynamic layout
        public Rectangle MapScreenBounds { get; private set; }
        public int GridSizeX { get; private set; }
        public int GridSizeY { get; private set; }
        public int CellSize => _cellSize;

        // Camera Panning
        public Vector2 CameraOffset { get; private set; } = Vector2.Zero;
        public bool IsCameraDetached => CameraOffset != Vector2.Zero;
        public bool IsZoomedOut => _cellSize < DEFAULT_CELL_SIZE;


        // --- TUNING ---
        private const int DEFAULT_MAP_GRID_CELLS = 33;
        private const int DEFAULT_CELL_SIZE = 5;
        private const int MAP_GRID_PIXEL_WIDTH = DEFAULT_MAP_GRID_CELLS * DEFAULT_CELL_SIZE;
        private const int MAP_GRID_PIXEL_HEIGHT = DEFAULT_MAP_GRID_CELLS * DEFAULT_CELL_SIZE;
        private int _cellSize = DEFAULT_CELL_SIZE;


        // Animation state for path nodes
        private readonly Dictionary<Vector2, float> _pathNodeAnimationOffsets = new Dictionary<Vector2, float>();
        private readonly Random _pathAnimRandom = new Random();

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

            SwayAnimation = new OrganicSwayAnimation(SWAY_SPEED_X, SWAY_SPEED_Y, SWAY_AMOUNT, SWAY_AMOUNT);

            ResetHeaderState();
        }

        public void ZoomIn()
        {
            switch (_cellSize)
            {
                case 1:
                case 2:
                    _cellSize = 3;
                    break;
                case 3:
                case 4:
                    _cellSize = 5;
                    break;
            }
        }

        public void ZoomOut()
        {
            switch (_cellSize)
            {
                case 5:
                case 4:
                    _cellSize = 3;
                    break;
                case 3:
                case 2:
                    _cellSize = 1;
                    break;
            }
        }

        public void ResetZoom()
        {
            _cellSize = DEFAULT_CELL_SIZE;
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
                // --- DYNAMIC GRID & SIZE CALCULATION ---
                GridSizeX = MAP_GRID_PIXEL_WIDTH / _cellSize;
                GridSizeY = MAP_GRID_PIXEL_HEIGHT / _cellSize;

                int finalMapWidth = GridSizeX * _cellSize;
                int finalMapHeight = GridSizeY * _cellSize;

                // --- CENTERING CALCULATION ---
                int mapX = (Global.VIRTUAL_WIDTH - finalMapWidth) / 2;
                int mapY = (Global.VIRTUAL_HEIGHT - finalMapHeight) / 2;
                MapScreenBounds = new Rectangle(mapX, mapY, finalMapWidth, finalMapHeight);
            }

            // --- GRID SETUP ---
            _mapGridBounds = MapScreenBounds;
        }

        private void UpdateHover(Vector2 virtualMousePos)
        {
            _hoveredGridPos = null; // Reset hover state each frame
            Vector2? currentHoveredGridPos = null;

            if (_mapGridBounds.Contains(virtualMousePos))
            {
                // Use the robust ScreenToWorldGrid conversion to find the hovered cell.
                // This ensures the calculation uses the same reference point (the render position) as the drawing code.
                currentHoveredGridPos = ScreenToWorldGrid(virtualMousePos);
            }

            if (!currentHoveredGridPos.HasValue) return;

            _hoveredGridPos = currentHoveredGridPos;

            // Do not show tooltips while the player is auto-moving.
            if (_gameState.IsExecutingActions) return;
        }

        public void DrawMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Rectangle? overrideBounds = null)
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

            DrawWorldMap(spriteBatch, font, gameTime, transform, overrideBounds);
            _contextMenu.Draw(spriteBatch, font);
        }

        private void DrawWorldMap(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, Rectangle? overrideBounds)
        {
            CalculateMapLayout(overrideBounds);
            DrawMapFrameAndBackground(spriteBatch);

            var gridElements = GenerateWorldMapGridElements();
            foreach (var element in gridElements)
            {
                DrawGridElement(spriteBatch, element, _cellSize, gameTime);
            }

            if (_hoveredGridPos.HasValue && _gameState.ExploredCells.Contains(new Point((int)_hoveredGridPos.Value.X, (int)_hoveredGridPos.Value.Y)))
            {
                Vector2? screenPos = MapCoordsToScreen(_hoveredGridPos.Value);
                if (screenPos.HasValue)
                {
                    var markerTexture = _spriteManager.MapMarkerSprite;
                    if (markerTexture != null)
                    {
                        var indicatorRect = new Rectangle((int)screenPos.Value.X, (int)screenPos.Value.Y, _cellSize, _cellSize);
                        spriteBatch.DrawSnapped(markerTexture, indicatorRect, _global.Palette_Orange);
                    }
                }
            }

            DrawPlayerOffscreenIndicator(spriteBatch);
            DrawMapFrameOverlay(spriteBatch, font);
        }

        private void DrawMapFrameAndBackground(SpriteBatch spriteBatch)
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

            // --- Draw the main map frame and background ---
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

        private void DrawMapFrameOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
            // --- Draw the recenter prompt if needed ---
            if (IsCameraDetached)
            {
                var swayOffset = SwayAnimation.Offset;
                var swayedMapScreenBounds = new Rectangle(
                    MapScreenBounds.X + (int)swayOffset.X,
                    MapScreenBounds.Y + (int)swayOffset.Y,
                    MapScreenBounds.Width,
                    MapScreenBounds.Height
                );

                string part1 = "[SPACE]";
                string part2 = " Recenter";
                Vector2 part1Size = font.MeasureString(part1);
                Vector2 totalSize = font.MeasureString(part1 + part2);

                Vector2 startPos = new Vector2(
                    swayedMapScreenBounds.Center.X - totalSize.X / 2,
                    swayedMapScreenBounds.Bottom - totalSize.Y - 4 // 4 pixels padding from the bottom
                );

                spriteBatch.DrawStringSnapped(font, part1, startPos, _global.Palette_Orange);
                spriteBatch.DrawStringSnapped(font, part2, new Vector2(startPos.X + part1Size.X, startPos.Y), _global.Palette_Yellow);
            }
        }

        private List<GridElement> GenerateWorldMapGridElements()
        {
            var elements = new List<GridElement>();
            Vector2 viewCenter = GetPlayerRenderPosition() + CameraOffset;
            int startX = (int)viewCenter.X - GridSizeX / 2;
            int startY = (int)viewCenter.Y - GridSizeY / 2;

            // 1. Draw Terrain and Fog of War
            for (int y = 0; y < GridSizeY; y++)
            {
                for (int x = 0; x < GridSizeX; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;
                    Vector2? screenPos = MapCoordsToScreen(new Vector2(worldX, worldY));

                    if (!screenPos.HasValue) continue;

                    if (!_gameState.ExploredCells.Contains(new Point(worldX, worldY)))
                    {
                        elements.Add(new GridElement(_spriteManager.FogOfWarSprite, Color.White, screenPos.Value, new Vector2(worldX, worldY)));
                    }
                    else
                    {
                        float noise = _gameState.GetNoiseAt(worldX, worldY);
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
                var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
                if (entityId != _gameState.PlayerEntityId &&
                    posComp != null &&
                    _componentStore.HasComponent<RenderableComponent>(entityId) &&
                    _gameState.ExploredCells.Contains(new Point((int)posComp.WorldPosition.X, (int)posComp.WorldPosition.Y)))
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
            var logicalPosComp = _componentStore.GetComponent<PositionComponent>(_gameState.PlayerEntityId);
            if (playerRenderComp != null && logicalPosComp != null)
            {
                Vector2 playerDrawPos = GetPlayerRenderPosition();
                Vector2? screenPos = MapCoordsToScreen(playerDrawPos);
                if (screenPos.HasValue)
                {
                    // The GridElement's WorldPosition should be the logical one for tooltip/interaction purposes.
                    elements.Add(new GridElement(playerRenderComp.Texture, playerRenderComp.Color, screenPos.Value, logicalPosComp.WorldPosition));
                }
            }

            return elements;
        }

        /// <summary>
        /// Generates the visual path for the player's action queue.
        /// </summary>
        private List<GridElement> GeneratePlayerPathGridElements(IEnumerable<IAction> actions)
        {
            var elements = new List<GridElement>();
            if (!actions.Any()) return elements;

            foreach (var action in actions)
            {
                if (action is MoveAction moveAction)
                {
                    Vector2 actionPos = moveAction.Destination;
                    if (!_gameState.ExploredCells.Contains(new Point((int)actionPos.X, (int)actionPos.Y)))
                    {
                        continue;
                    }

                    bool isRunning = moveAction.Mode == MovementMode.Run;

                    Texture2D actionTexture = isRunning ? _spriteManager.RunPathSprite : _spriteManager.PathSprite;
                    Color actionColor = isRunning ? _global.RunPathColor : _global.PathColor;

                    Vector2? screenPos = MapCoordsToScreen(actionPos);
                    if (screenPos.HasValue)
                    {
                        elements.Add(new GridElement(actionTexture, actionColor, screenPos.Value, actionPos));
                    }
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

            // Apply a subtle flicker/jitter animation to path nodes, but only when zoomed in.
            if (_cellSize > 1 && _pathNodeAnimationOffsets.ContainsKey(element.WorldPosition) && IsPathPipTexture(element.Texture))
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
            Vector2 viewCenter = GetPlayerRenderPosition() + CameraOffset;

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
            Vector2 playerOffsetFromCenter = directionToPlayer * _cellSize;

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

            Vector2 viewCenter = GetPlayerRenderPosition() + CameraOffset;
            int startX = (int)viewCenter.X - GridSizeX / 2;
            int startY = (int)viewCenter.Y - GridSizeY / 2;

            float gridX = mapPos.X - startX;
            float gridY = mapPos.Y - startY;

            // Check if the logical center of the sprite is within the grid bounds, with a small margin.
            if (gridX >= -0.5f && gridX < GridSizeX + 0.5f && gridY >= -0.5f && gridY < GridSizeY + 0.5f)
            {
                return new Vector2(_mapGridBounds.X + gridX * _cellSize, _mapGridBounds.Y + gridY * _cellSize);
            }
            return null;
        }

        public Vector2 ScreenToWorldGrid(Vector2 virtualMousePos)
        {
            var viewCenter = GetPlayerRenderPosition() + CameraOffset;

            int cameraWorldX = (int)viewCenter.X - GridSizeX / 2;
            int cameraWorldY = (int)viewCenter.Y - GridSizeY / 2;

            // Apply the requested 1-pixel offset to the mouse position before calculation.
            float adjustedMouseX = virtualMousePos.X + 0.5f;
            float adjustedMouseY = virtualMousePos.Y + 0.5f;

            float mouseWorldX = cameraWorldX + (adjustedMouseX - _mapGridBounds.X) / _cellSize;
            float mouseWorldY = cameraWorldY + (adjustedMouseY - _mapGridBounds.Y) / _cellSize;

            return new Vector2((int)Math.Floor(mouseWorldX), (int)Math.Floor(mouseWorldY));
        }

        private Vector2 GetPlayerRenderPosition()
        {
            var renderPosComp = _componentStore.GetComponent<RenderPositionComponent>(_gameState.PlayerEntityId);
            return renderPosComp != null ? renderPosComp.WorldPosition : _gameState.PlayerWorldPos;
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
            return _global.PeakColor;
        }
    }
}