﻿using Microsoft.Xna.Framework;
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

        private string _tooltipText = "";
        private Vector2 _tooltipPosition;
        private bool _showTooltip = false;
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

            // Draw tooltip if needed
            if (_showTooltip)
            {
                DrawTooltip(_spriteBatch);
            }
        }

        private Rectangle DrawEnergyBarWithPreview(SpriteBatch spriteBatch, PlayerStats stats, Vector2 position, int width)
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
            Texture2D pixel = Core.Pixel;
            pixel.SetData(new[] { Color.White });

            string labelText = $"{label}"; // Draw label first
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.GameTextColor);

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

        private void CheckTooltipHover()
        {
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);
            _showTooltip = false;

            if (_gameState.PlayerStats == null) return;

            var stats = _gameState.PlayerStats;
            
            if (_hpBarBounds.Contains(virtualMousePos)) // Check HP bar hover using virtual coordinates
            {
                _tooltipText = $"{stats.CurrentHealthPoints}/{stats.MaxHealthPoints}";
                _tooltipPosition = new Vector2(virtualMousePos.X + 10, virtualMousePos.Y - 20);
                _showTooltip = true;
            }
            else if (_epBarBounds.Contains(virtualMousePos)) // Check EP bar hover using virtual coordinates
            {
                if (_gameState.PendingActions.Count > 0)
                {
                    var simResult = _gameState.PendingQueueSimulationResult;
                    int finalEnergy = simResult.finalEnergy;
                    _tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints} -> {finalEnergy}/{stats.MaxEnergyPoints}";
                }
                else
                {
                    _tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}";
                }
                _tooltipPosition = new Vector2(virtualMousePos.X + 10, virtualMousePos.Y - 20);
                _showTooltip = true;
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(_tooltipText)) return;

            Texture2D pixel = Core.Pixel;
            pixel.SetData(new[] { Color.White });

            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(_tooltipText);

            Rectangle tooltipBg = new Rectangle( // Create tooltip background with padding
                (int)_tooltipPosition.X - 4,
                (int)_tooltipPosition.Y - 2,
                (int)textSize.X + 8,
                (int)textSize.Y + 4
            );

            spriteBatch.Draw(pixel, tooltipBg, Global.Instance.ToolTipBGColor * 0.8f); // Draw tooltip background

            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor); // Draw tooltip border
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);

            spriteBatch.DrawString(Global.Instance.DefaultFont, _tooltipText, _tooltipPosition, Global.Instance.ToolTipTextColor); // Draw tooltip text
        }
    }
}