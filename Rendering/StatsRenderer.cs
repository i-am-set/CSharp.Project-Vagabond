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
        private MouseState _currentMouseState;

        private Rectangle _hpBarBounds;
        private Rectangle _epBarBounds;

        public void Update(GameTime gameTime)
        {
            _currentMouseState = Mouse.GetState();
            HandleTooltips();
        }

        public void DrawStats()
        {
            if (_gameState.PlayerStats == null) return;

            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            var stats = _gameState.PlayerStats;

            int baseX = 50;
            int baseY = 50 + Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10; // Closer to map

            int currentY = baseY;

            // Health bar
            currentY = baseY;
            _hpBarBounds = DrawSimpleStatBar(_spriteBatch, "HP", stats.CurrentHealthPoints, stats.MaxHealthPoints,
                new Vector2(baseX, currentY), Global.Instance.Palette_Red, Global.Instance.Palette_DarkGray, Global.MAP_WIDTH - baseX);

            // Energy bar with pending preview
            currentY += 14;
            _epBarBounds = DrawEnergyBarWithPreview(_spriteBatch, stats, new Vector2(baseX, currentY), Global.MAP_WIDTH - baseX);

            // Secondary stats in compact format
            currentY += 16;
            string secondaryStats = $"Spd:{stats.WalkSpeed:F1} Car:{stats.CarryCapacity} Men:{stats.MentalResistance} Soc:{stats.SocialInfluence}";
            _spriteBatch.DrawString(Global.Instance.DefaultFont, secondaryStats, new Vector2(baseX, currentY), Global.Instance.Palette_LightGray);
        }

        private Rectangle DrawEnergyBarWithPreview(SpriteBatch spriteBatch, PlayerStats stats, Vector2 position, int width)
        {
            var pixel = Core.Pixel;

            string labelText = "EP";
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.TextColor);

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)(position.Y + 3);
            int barWidth = (stats.MaxEnergyPoints * 6) - 3;

            Rectangle barBg = new Rectangle(barX - 2, barY - 2, barWidth + 4, 10);
            spriteBatch.Draw(pixel, barBg, Global.Instance.Palette_DarkGray);

            int currentEnergy = stats.CurrentEnergyPoints;
            int maxEnergy = stats.MaxEnergyPoints;
            bool hasPendingActions = _gameState.PendingActions.Count > 0;

            for (int i = 0; i < maxEnergy; i++) // Draw the base bar (current energy in green, empty in gray)
            {
                int segmentX = barX + i * 6;
                Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);
                if (i < currentEnergy)
                    spriteBatch.Draw(pixel, segmentRect, Global.Instance.Palette_LightGreen);
                else
                    spriteBatch.Draw(pixel, segmentRect, Color.Lerp(Global.Instance.Palette_DarkGray, Global.Instance.Palette_LightGreen, 0.3f));
            }

            if (hasPendingActions) // If previewing, overlay the predicted changes
            {
                int finalEnergy = _gameState.PendingQueueSimulationResult.finalEnergy;
                if (finalEnergy < currentEnergy)
                {
                    for (int i = finalEnergy; i < currentEnergy; i++) // Draw the cost in yellow over the green part
                    {
                        int segmentX = barX + i * 6;
                        Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);
                        spriteBatch.Draw(pixel, segmentRect, Global.Instance.Palette_Yellow);
                    }
                }
                else if (finalEnergy > currentEnergy)
                {
                    for (int i = currentEnergy; i < finalEnergy; i++) // Draw the gain in teal over the empty/gray part
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
            var pixel = Core.Pixel;

            string labelText = $"{label}"; // Draw label first
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.TextColor);

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(labelText); // Calculate bar position (right after text)
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)(position.Y + 3);

            
            int barWidth = (max * 6) - 3; // Bar width: each value is 3 pixels wide + 3 pixel gap = 6 pixels per value, minus the final gap

            Rectangle barBg = new Rectangle(barX - 2, barY - 2, barWidth + 4, 10); // Draw bar background
            spriteBatch.Draw(pixel, barBg, bgColor);

            if (max > 0) // Draw alternating pattern bar
            {
                for (int i = 0; i < max; i++) // Draw 3-pixel wide segments with 3-pixel gaps
                {
                    int segmentX = barX + i * 6; // 6 pixels per segment (3 for bar + 3 for gap)
                    Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6); // 3 pixels wide

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

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, 12); // Return the bounds of the entire bar area (including label) for hover detection
        }

        private void HandleTooltips()
        {
            if (_gameState.PlayerStats == null) return;

            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);
            bool isHovering = false;

            var stats = _gameState.PlayerStats;
            
            if (_hpBarBounds.Contains(virtualMousePos))
            {
                string tooltipText = $"{stats.CurrentHealthPoints}/{stats.MaxHealthPoints}";
                Core.CurrentTooltipManager.Request(this, tooltipText, virtualMousePos);
                isHovering = true;
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
                Core.CurrentTooltipManager.Request(this, tooltipText, virtualMousePos);
                isHovering = true;
            }

            if (!isHovering)
            {
                Core.CurrentTooltipManager.CancelRequest(this);
            }
        }
    }
}