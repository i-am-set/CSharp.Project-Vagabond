using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond
{
    public class StatsRenderer
    {
        private GameState _gameState = Core.CurrentGameState;
        private MouseState _previousMouseState;
        private MouseState _currentMouseState;

        private Rectangle _hpBarBounds;
        private Rectangle _epBarBounds;

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            CheckTooltipHover();
        }

        public void DrawStats()
        {
            if (_gameState.PlayerStats == null) return;

            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            var stats = _gameState.PlayerStats;

            int baseX = 50;
            int baseY = 50 + Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10;

            int currentY = baseY;

            // Health bar
            currentY = baseY;
            _hpBarBounds = DrawSimpleStatBar(_spriteBatch, "HP", stats.CurrentHealthPoints, stats.MaxHealthPoints,
                new Vector2(baseX, currentY), Global.Instance.Palette_Red, Global.Instance.Palette_DarkGray, Global.MAP_WIDTH - baseX);

            currentY += 14;
            _epBarBounds = DrawEnergyBarWithPreview(_spriteBatch, stats, new Vector2(baseX, currentY), Global.MAP_WIDTH - baseX);

            currentY += 16;
            string secondaryStats = $"Spd:{stats.WalkSpeed:F1} Car:{stats.CarryCapacity} Men:{stats.MentalResistance} Soc:{stats.SocialInfluence}";
            _spriteBatch.DrawString(Global.Instance.DefaultFont, secondaryStats, new Vector2(baseX, currentY), Global.Instance.Palette_LightGray);
        }

        private Rectangle DrawEnergyBarWithPreview(SpriteBatch spriteBatch, StatsComponent stats, Vector2 position, int width)
        {
            Texture2D pixel = Core.Pixel;
            pixel.SetData(new[] { Color.White });

            string labelText = "EP";
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.GameTextColor);

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)(position.Y + 3);
            int barWidth = (stats.MaxEnergyPoints * 6) - 3;

            Rectangle barBg = new Rectangle(barX - 2, barY - 2, barWidth + 4, 10);
            spriteBatch.Draw(pixel, barBg, Global.Instance.Palette_DarkGray);

            int currentEnergy = stats.CurrentEnergyPoints;
            int maxEnergy = stats.MaxEnergyPoints;
            bool hasPendingActions = _gameState.PendingActions.Count > 0;

            for (int i = 0; i < maxEnergy; i++)
            {
                int segmentX = barX + i * 6;
                Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);
                if (i < currentEnergy)
                    spriteBatch.Draw(pixel, segmentRect, Global.Instance.Palette_LightGreen);
                else
                    spriteBatch.Draw(pixel, segmentRect, Color.Lerp(Global.Instance.Palette_DarkGray, Global.Instance.Palette_LightGreen, 0.3f));
            }

            if (hasPendingActions)
            {
                int finalEnergy = _gameState.PendingQueueSimulationResult.finalEnergy;
                if (finalEnergy < currentEnergy)
                {
                    for (int i = finalEnergy; i < currentEnergy; i++)
                    {
                        int segmentX = barX + i * 6;
                        Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);
                        spriteBatch.Draw(pixel, segmentRect, Global.Instance.Palette_Yellow);
                    }
                }
                else if (finalEnergy > currentEnergy)
                {
                    for (int i = currentEnergy; i < finalEnergy; i++)
                    {
                        int segmentX = barX + i * 6;
                        Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);
                        spriteBatch.Draw(pixel, segmentRect, Global.Instance.Palette_Teal);
                    }
                }
            }

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, 12);
        }

        private Rectangle DrawSimpleStatBar(SpriteBatch spriteBatch, string label, int current, int max, Vector2 position, Color fillColor, Color bgColor, int width)
        {
            Texture2D pixel = Core.Pixel;
            pixel.SetData(new[] { Color.White });

            string labelText = $"{label}";
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.GameTextColor);

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)(position.Y + 3);


            int barWidth = (max * 6) - 3;

            Rectangle barBg = new Rectangle(barX - 2, barY - 2, barWidth + 4, 10);
            spriteBatch.Draw(pixel, barBg, bgColor);

            if (max > 0) // draw alternating pattern bar
            {
                for (int i = 0; i < max; i++)
                {
                    int segmentX = barX + i * 6;
                    Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);

                    if (i < current)
                    {
                        spriteBatch.Draw(pixel, segmentRect, fillColor);
                    }
                    else
                    {
                        spriteBatch.Draw(pixel, segmentRect, Color.Lerp(bgColor, fillColor, 0.3f));
                    }
                }
            }

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, 12);
        }

        private void CheckTooltipHover()
        {
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            if (_gameState.PlayerStats == null) return;

            var stats = _gameState.PlayerStats;

            if (_hpBarBounds.Contains(virtualMousePos))
            {
                string tooltipText = $"{stats.CurrentHealthPoints}/{stats.MaxHealthPoints}";
                Core.CurrentTooltipManager.RequestTooltip(_hpBarBounds, tooltipText, virtualMousePos, 0f);
            }
            else if (_epBarBounds.Contains(virtualMousePos))
            {
                string tooltipText;
                if (_gameState.PendingActions.Count > 0)
                {
                    var simResult = _gameState.PendingQueueSimulationResult;
                    int finalEnergy = simResult.finalEnergy;
                    tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints} -> {finalEnergy}/{stats.MaxEnergyPoints}";
                }
                else
                {
                    tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}";
                }
                Core.CurrentTooltipManager.RequestTooltip(_epBarBounds, tooltipText, virtualMousePos, 0f);
            }
        }
    }
}