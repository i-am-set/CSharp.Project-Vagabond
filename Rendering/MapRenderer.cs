using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class MapRenderer
    {
        private GameState _gameState = Core.CurrentGameState;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void DrawMap()
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;

            int mapStartX = 50;
            int mapStartY = 50;
            int mapWidth = Global.MAP_WIDTH;
            int mapHeight = Global.GRID_SIZE * Global.GRID_CELL_SIZE + 30;

            // Draw map border //
            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Border rectangle //
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), Global.Instance.palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), Global.Instance.palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), Global.Instance.palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), Global.Instance.palette_White); // Right

            // Draw map title //
            string posText = $"Pos: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})";
            _spriteBatch.DrawString(Global.Instance.DefaultFont, posText,
                new Vector2(mapStartX, mapStartY - 20), Global.Instance.TextColor);

            // Draw the current date and time ---
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

                    // Check action queue for rendering
                    bool isPath = false;
                    bool isPathEnd = false;
                    bool isShortRest = false;
                    bool isLongRest = false;
                    bool isRunning = false;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    // Find all actions at this position to handle overlaps (e.g., move then rest on same tile)
                    var actionsAtPos = _gameState.PendingActions.Where(a => a.Position == worldPos).ToList();
                    if (actionsAtPos.Any())
                    {
                        if (actionsAtPos.Any(a => a.Type == ActionType.ShortRest)) isShortRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.LongRest)) isLongRest = true;
                        if (actionsAtPos.Any(a => a.Type == ActionType.RunMove)) isRunning = true;
                        
                        if (actionsAtPos.Any(a => a.Type == ActionType.Move || a.Type == ActionType.RunMove))
                        {
                            if (actionsAtPos.Any(a => a.Type == ActionType.RunMove))
                            {
                                isRunning = true;
                            }

                            isPath = true;
                            var lastMoveAction = _gameState.PendingActions.LastOrDefault(a => a.Type == ActionType.Move || a.Type == ActionType.RunMove);
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