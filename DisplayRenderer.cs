using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace ProjectVagabond
{
    public enum DisplayMode
    {
        Normal,
        NoiseMap
    }

    public class DisplayRenderer
    {
        private SpriteFont _font;
        private const int GRID_SIZE = 15;
        private const int CELL_SIZE = 20;

        public void LoadContent(SpriteFont font)
        {
            _font = font;
        }

        public void Draw(SpriteBatch spriteBatch, GameState gameState, string currentInput)
        {
            // Draw output console (left side)
            DrawOutputConsole(spriteBatch, gameState);
            
            // Draw game grid (right side)
            DrawGameGrid(spriteBatch, gameState);
            
            // Draw status info
            DrawStatusInfo(spriteBatch, gameState);
            
            // Draw input line
            DrawInputLine(spriteBatch, currentInput);
        }

        private void DrawOutputConsole(SpriteBatch spriteBatch, GameState gameState)
        {
            // Draw console border
            var consoleRect = new Rectangle(10, 10, 500, 400);
            DrawBorder(spriteBatch, consoleRect, Color.White);
            
            // Draw title
            spriteBatch.DrawString(_font, "OUTPUTS", new Vector2(15, 15), Color.White);
            
            // Draw output history
            int yOffset = 40;
            foreach (var output in gameState.OutputHistory.TakeLast(15))
            {
                Color textColor = GetTextColor(output);
                spriteBatch.DrawString(_font, $"> {output}", new Vector2(15, yOffset), textColor);
                yOffset += 20;
            }
        }

        private void DrawGameGrid(SpriteBatch spriteBatch, GameState gameState)
        {
            var gridRect = new Rectangle(550, 10, GRID_SIZE * CELL_SIZE + 10, GRID_SIZE * CELL_SIZE + 10);
            DrawBorder(spriteBatch, gridRect, Color.White);
            
            for (int screenX = 0; screenX < GRID_SIZE; screenX++)
            {
                for (int screenY = 0; screenY < GRID_SIZE; screenY++)
                {
                    // Convert screen coordinates to world coordinates
                    int worldX = gameState.CameraX + screenX;
                    int worldY = gameState.CameraY + screenY;
                    
                    Vector2 position = new Vector2(555 + screenX * CELL_SIZE, 15 + screenY * CELL_SIZE);
                    
                    // Player is always at the center (7, 7) on screen
                    if (screenX == 7 && screenY == 7)
                    {
                        spriteBatch.DrawString(_font, "■", position, Color.Yellow);
                    }
                    else
                    {
                        char terrainChar = gameState.GetTerrainAt(worldX, worldY);
                        Color charColor = GetTerrainColor(terrainChar, gameState.CurrentDisplayMode, gameState, worldX, worldY);
                        spriteBatch.DrawString(_font, terrainChar.ToString(), position, charColor);
                    }
                }
            }

            foreach (var (pathX, pathY) in gameState.PreviewPath) // Draw preview path
            {
                // Convert world coordinates to screen coordinates
                int screenX = pathX - gameState.CameraX;
                int screenY = pathY - gameState.CameraY;
    
                if (screenX >= 0 && screenX < GRID_SIZE && screenY >= 0 && screenY < GRID_SIZE)
                {
                    Vector2 previewPos = new Vector2(555 + screenX * CELL_SIZE, 15 + screenY * CELL_SIZE);
                    spriteBatch.DrawString(_font, "◦", previewPos, Color.Cyan);
                }
            }
        }

        private void DrawStatusInfo(SpriteBatch spriteBatch, GameState gameState)
        {
            int yPos = 430;
            
            // Total outputs
            spriteBatch.DrawString(_font, $"Total Outputs: {gameState.TotalOutputs}", 
                new Vector2(10, yPos), Color.White);
            yPos += 25;
            
            // HP Bar
            DrawBar(spriteBatch, new Vector2(10, yPos), "HP", gameState.HP, gameState.MaxHP, Color.Red);
            yPos += 25;
            
            // AP Bar  
            DrawBar(spriteBatch, new Vector2(10, yPos), "AP", gameState.AP, gameState.MaxAP, Color.Green);
            yPos += 30;
            
            // Current terrain info (right side)
            Vector2 terrainPos = new Vector2(580, 350);
            spriteBatch.DrawString(_font, $"World Pos: ({gameState.PlayerWorldX}, {gameState.PlayerWorldY})", 
                terrainPos, Color.White);
            spriteBatch.DrawString(_font, $"Terrain Type: {gameState.CurrentTerrainType}", 
                new Vector2(terrainPos.X, terrainPos.Y + 20), Color.Yellow);
            spriteBatch.DrawString(_font, $"Vegetation  : {gameState.CurrentVegetation}", 
                new Vector2(terrainPos.X, terrainPos.Y + 40), Color.Green);
            spriteBatch.DrawString(_font, $"Difficulty  : {gameState.CurrentDifficulty:F2}", 
                new Vector2(terrainPos.X, terrainPos.Y + 60), Color.Red);
            
            // Free moving status
            if (gameState.IsFreeMoving)
            {
                spriteBatch.DrawString(_font, "You are FREE MOVING!", 
                    new Vector2(10, yPos), Color.Cyan);
                spriteBatch.DrawString(_font, "Use (W/A/S/D) to queue moves.", 
                    new Vector2(10, yPos + 20), Color.Cyan);
                spriteBatch.DrawString(_font, "Press ENTER to confirm, ESC to cancel:", 
                    new Vector2(10, yPos + 40), Color.Cyan);
            }
        }

        private void DrawInputLine(SpriteBatch spriteBatch, string currentInput)
        {
            spriteBatch.DrawString(_font, "Type 'HELP' if needed:", new Vector2(10, 550), Color.White);
            spriteBatch.DrawString(_font, $"> {currentInput}_", new Vector2(10, 575), Color.White);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            // Create a 1x1 white pixel texture for drawing borders
            Texture2D pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
            // Bottom  
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - 2, rect.Width, 2), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - 2, rect.Y, 2, rect.Height), color);
        }

        private void DrawBar(SpriteBatch spriteBatch, Vector2 position, string label, int current, int max, Color barColor)
        {
            spriteBatch.DrawString(_font, label, position, Color.White);
            
            // Draw bar background
            Texture2D pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            
            Rectangle bgRect = new Rectangle((int)position.X + 30, (int)position.Y, 200, 15);
            Rectangle fillRect = new Rectangle(bgRect.X, bgRect.Y, (int)(200 * ((float)current / max)), 15);
            
            spriteBatch.Draw(pixel, bgRect, Color.DarkGray);
            spriteBatch.Draw(pixel, fillRect, barColor);
            
            spriteBatch.DrawString(_font, $"{current}/{max}", 
                new Vector2(position.X + 240, position.Y - 3), Color.White);
        }

        private Color GetTextColor(string text)
        {
            if (text.Contains("Cannot")) return Color.Red;
            if (text.Contains("Queued")) return Color.Yellow;
            if (text.Contains("Backtracked")) return Color.Cyan;
            if (text.Contains("HELP")) return Color.Yellow;
            if (text.Contains("#")) return Color.Magenta;
            return Color.LightGray;
        }

        private Color GetTerrainColor(char terrain, DisplayMode displayMode, GameState gameState, int worldX, int worldY)
        {
            if (displayMode == DisplayMode.NoiseMap)
            {
                var mapData = gameState.NoiseManager.GetMapData(worldX, worldY);
                float value = gameState.NoiseMapType.ToLower() switch
                {
                    "terrain" => mapData.TerrainHeight,
                    "lushness" => mapData.Lushness,
                    "temperature" => mapData.Temperature,
                    "humidity" => mapData.Humidity,
                    "resources" => mapData.Resources,
                    "difficulty" => mapData.Difficulty,
                    _ => mapData.TerrainHeight
                };
        
                return value switch
                {
                    >= 0.8f => Color.Yellow,
                    >= 0.6f => Color.Orange,
                    >= 0.4f => Color.Gray,
                    >= 0.2f => Color.DarkGray,
                    _ => Color.Black
                };
            }
    
            return terrain switch
            {
                '^' or 'A' or 'M' => Color.White,
                '~' => Color.Blue,
                '.' => Color.Gray,
                '■' => Color.Yellow,
                _ => Color.White
            };
        }
    }
}
