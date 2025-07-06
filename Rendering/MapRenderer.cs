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
                        // Combine queued actions and the active action for rendering
                        var allActionsAtPos = new List<IAction>();

                        // Get actions from the queue
                        var queuedActions = _gameState.PendingActions
                            .Where(a => (a is MoveAction ma && ma.Destination == worldPos) || (a is RestAction ra && ra.Position == worldPos));
                        allActionsAtPos.AddRange(queuedActions);

                        // Get the currently executing action component
                        var activeMoveAction = Core.ComponentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId);
                        if (activeMoveAction != null && activeMoveAction.Destination == worldPos)
                        {
                            allActionsAtPos.Add(activeMoveAction);
                        }
                        var activeRestAction = Core.ComponentStore.GetComponent<RestAction>(_gameState.PlayerEntityId);
                        if (activeRestAction != null && activeRestAction.Position == worldPos)
                        {
                            allActionsAtPos.Add(activeRestAction);
                        }

                        if (allActionsAtPos.Any())
                        {
                            if (allActionsAtPos.Any(a => a is RestAction ra && ra.RestType == RestType.ShortRest))
                            {
                                texture = Core.CurrentSpriteManager.ShortRestSprite;
                                color = Global.Instance.ShortRestColor;
                            }
                            else if (allActionsAtPos.Any(a => a is RestAction ra && ra.RestType == RestType.LongRest))
                            {
                                texture = Core.CurrentSpriteManager.LongRestSprite;
                                color = Global.Instance.LongRestColor;
                            }
                            else if (allActionsAtPos.Any(a => a is MoveAction))
                            {
                                bool isRunning = allActionsAtPos.OfType<MoveAction>().Any(ma => ma.IsRunning);
                                texture = isRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;

                                // Determine if this position is the final destination in the queue
                                var lastActionInQueue = _gameState.PendingActions.LastOrDefault();
                                Vector2? lastPos = null;
                                if (lastActionInQueue is MoveAction lm) lastPos = lm.Destination;
                                if (lastActionInQueue is RestAction lr) lastPos = lr.Position;

                                if (lastPos.HasValue && lastPos.Value == worldPos)
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
                        texture = pixel;
                        color = Global.Instance.PlayerColor;
                    }
                    else
                    {
                        // Combine queued actions and the active action for rendering
                        var allActionsAtPos = new List<IAction>();

                        // Get actions from the queue
                        var queuedActions = _gameState.PendingActions
                            .OfType<MoveAction>()
                            .Where(ma => ma.Destination == localPos);
                        allActionsAtPos.AddRange(queuedActions);

                        // Get the currently executing action component
                        var activeMoveAction = Core.ComponentStore.GetComponent<MoveAction>(_gameState.PlayerEntityId);
                        if (activeMoveAction != null && activeMoveAction.Destination == localPos)
                        {
                            allActionsAtPos.Add(activeMoveAction);
                        }

                        if (allActionsAtPos.Any())
                        {
                            bool isRunning = allActionsAtPos.OfType<MoveAction>().Any(ma => ma.IsRunning);
                            texture = isRunning ? Core.CurrentSpriteManager.RunPathSprite : Core.CurrentSpriteManager.PathSprite;

                            var lastActionInQueue = _gameState.PendingActions.LastOrDefault() as MoveAction;
                            if (lastActionInQueue != null && lastActionInQueue.Destination == localPos)
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