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
            int mapWidth = Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10;
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
            _spriteBatch.DrawString(Global.Instance.DefaultFont, $"Pos: ({(int)_gameState.PlayerWorldPos.X}, {(int)_gameState.PlayerWorldPos.Y})",
                new Vector2(mapStartX, mapStartY - 20), Global.Instance.TextColor);

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
                    Color color = GetTerrainColor(noise);

                    bool isPlayer = (worldX == (int)_gameState.PlayerWorldPos.X && worldY == (int)_gameState.PlayerWorldPos.Y); // Check if this is the player position

                    bool isPath = false; // Check if this is part of the pending path
                    bool isPathEnd = false;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    if (_gameState.PendingPathPreview.Contains(worldPos))
                    {
                        isPath = true;
                        isPathEnd = worldPos == _gameState.PendingPathPreview.Last();
                    }

                    if (isPlayer)
                    {
                        texture = Core.CurrentSpriteManager.PlayerSprite;
                        color = Color.White;
                    }
                    else if (isPathEnd)
                    {
                        texture = Core.CurrentSpriteManager.PathEndSprite;
                        color = Color.White;
                    }
                    else if (isPath)
                    {
                        texture = Core.CurrentSpriteManager.PathSprite;
                        color = Color.White;
                    }

                    Vector2 gridPos = new Vector2(mapStartX + x * Global.GRID_CELL_SIZE, mapStartY + y * Global.GRID_CELL_SIZE);

                    elements.Add(new GridElement(texture, color, gridPos));
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
