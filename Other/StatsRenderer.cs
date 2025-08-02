using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;

namespace ProjectVagabond
{
    public class StatsRenderer
    {
        private readonly GameState _gameState;
        private readonly TooltipManager _tooltipManager;
        private readonly Global _global;

        private MouseState _previousMouseState;
        private MouseState _currentMouseState;

        private Rectangle _hpBarBounds;
        private Rectangle _epBarBounds;

        public StatsRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            CheckTooltipHover();
        }

        public void DrawStats(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, int availableWidth)
        {
            if (_gameState.PlayerStats == null) return;

            var stats = _gameState.PlayerStats;

            float currentY = position.Y;

            // Health bar
            _hpBarBounds = DrawSimpleStatBar(spriteBatch, font, "HP", stats.CurrentHealthPoints, stats.MaxHealthPoints,
                new Vector2(position.X, currentY), _global.Palette_Red, _global.Palette_DarkGray, availableWidth);

            currentY += 14;
            _epBarBounds = DrawEnergyBarWithPreview(spriteBatch, font, stats, new Vector2(position.X, currentY), availableWidth);

            currentY += 16;
            string secondaryStats = $"Spd:{stats.WalkSpeed:F1} Car:{stats.CarryCapacity} Men:{stats.MentalResistance} Soc:{stats.SocialInfluence}";
            spriteBatch.DrawString(font, secondaryStats, new Vector2(position.X, currentY), _global.Palette_LightGray);
        }

        private Rectangle DrawEnergyBarWithPreview(SpriteBatch spriteBatch, BitmapFont font, StatsComponent stats, Vector2 position, int availableWidth)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            const int segmentWidth = 3;
            const int segmentGap = 3;
            const int segmentHeight = 6;
            const int barHeight = 12;
            const int horizontalPadding = 2;

            // Calculate the vertical offset to center the text with the bar
            float textOffsetY = (barHeight - font.LineHeight) / 2f;
            Vector2 textPosition = new Vector2(position.X, position.Y + textOffsetY);

            string labelText = "EP";
            spriteBatch.DrawString(font, labelText, textPosition, _global.GameTextColor);

            Vector2 textSize = font.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)position.Y;

            int maxEnergy = stats.MaxEnergyPoints;
            int segmentsAreaWidth = (maxEnergy * (segmentWidth + segmentGap)) - segmentGap;
            int barWidth = segmentsAreaWidth + (horizontalPadding * 2);
            barWidth = Math.Min(barWidth, availableWidth - (int)textSize.X - 5);
            var barBounds = new Rectangle(barX, barY, barWidth, barHeight);

            // Draw the base bar using the new primitive
            UIPrimitives.DrawSegmentedBar(
                spriteBatch,
                pixel,
                barBounds,
                (float)stats.CurrentEnergyPoints / maxEnergy,
                maxEnergy,
                _global.Palette_LightGreen,
                Color.Lerp(_global.Palette_DarkGray, _global.Palette_LightGreen, 0.3f),
                _global.Palette_DarkGray,
                segmentWidth,
                segmentGap,
                segmentHeight,
                horizontalPadding
            );

            // Draw preview segments on top
            if (_gameState.PendingActions.Count > 0)
            {
                int currentEnergy = stats.CurrentEnergyPoints;
                int finalEnergy = _gameState.PendingQueueSimulationResult.finalEnergy;
                int totalSegmentUnitWidth = segmentWidth + segmentGap;
                int segmentY = barBounds.Y + (barBounds.Height - segmentHeight) / 2;
                int segmentsStartX = barBounds.X + horizontalPadding;

                int availableWidthForSegments = barBounds.Width - (horizontalPadding * 2);
                int maxSegmentsThatFit = (availableWidthForSegments + segmentGap) / totalSegmentUnitWidth;

                if (finalEnergy < currentEnergy) // Energy loss preview
                {
                    for (int i = finalEnergy; i < currentEnergy; i++)
                    {
                        if (i >= maxSegmentsThatFit) break;
                        int segmentX = segmentsStartX + i * totalSegmentUnitWidth;
                        Rectangle segmentRect = new Rectangle(segmentX, segmentY, segmentWidth, segmentHeight);
                        spriteBatch.Draw(pixel, segmentRect, _global.Palette_Yellow);
                    }
                }
                else if (finalEnergy > currentEnergy) // Energy gain preview
                {
                    for (int i = currentEnergy; i < finalEnergy; i++)
                    {
                        if (i >= maxSegmentsThatFit) break;
                        int segmentX = segmentsStartX + i * totalSegmentUnitWidth;
                        Rectangle segmentRect = new Rectangle(segmentX, segmentY, segmentWidth, segmentHeight);
                        spriteBatch.Draw(pixel, segmentRect, _global.Palette_LightBlue);
                    }
                }
            }

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, barHeight);
        }

        private Rectangle DrawSimpleStatBar(SpriteBatch spriteBatch, BitmapFont font, string label, int current, int max, Vector2 position, Color fillColor, Color bgColor, int availableWidth)
        {
            const int segmentWidth = 3;
            const int segmentGap = 2;
            const int segmentHeight = 6;
            const int barHeight = 10;
            const int horizontalPadding = 2;

            // Calculate the vertical offset to center the text with the bar
            float textOffsetY = (barHeight - font.LineHeight) / 2f;
            Vector2 textPosition = new Vector2(position.X, position.Y + textOffsetY);

            string labelText = $"{label}";
            spriteBatch.DrawString(font, labelText, textPosition, _global.GameTextColor);

            Vector2 textSize = font.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)position.Y;

            int segmentsAreaWidth = (max * (segmentWidth + segmentGap)) - segmentGap;
            int barWidth = segmentsAreaWidth + (horizontalPadding * 2);
            barWidth = Math.Min(barWidth, availableWidth - (int)textSize.X - 5);
            var barBounds = new Rectangle(barX, barY, barWidth, barHeight);

            if (max > 0)
            {
                UIPrimitives.DrawSegmentedBar(
                    spriteBatch,
                    ServiceLocator.Get<Texture2D>(),
                    barBounds,
                    (float)current / max,
                    max,
                    fillColor,
                    Color.Lerp(bgColor, fillColor, 0.3f),
                    bgColor,
                    segmentWidth,
                    segmentGap,
                    segmentHeight,
                    horizontalPadding
                );
            }

            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, barHeight);
        }

        private void CheckTooltipHover()
        {
            Vector2 virtualMousePos = Core.TransformMouse(_currentMouseState.Position);

            if (_gameState.PlayerStats == null) return;

            var stats = _gameState.PlayerStats;

            if (_hpBarBounds.Contains(virtualMousePos))
            {
                string tooltipText = $"{stats.CurrentHealthPoints}/{stats.MaxHealthPoints}";
                _tooltipManager.RequestTooltip(_hpBarBounds, tooltipText, virtualMousePos, 0f);
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
                _tooltipManager.RequestTooltip(_epBarBounds, tooltipText, virtualMousePos, 0f);
            }
        }
    }
}