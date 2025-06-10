using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ProjectVagabond
{
    public class StatsRenderer
    {
        private GameState _gameState = Core.CurrentGameState;
        private MouseState _previousMouseState;
        private MouseState _currentMouseState;

        // Tooltip state
        private string _tooltipText = "";
        private Vector2 _tooltipPosition;
        private bool _showTooltip = false;
        private Rectangle _hpBarBounds;
        private Rectangle _epBarBounds;

        public void Update(GameTime gameTime)
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Check for tooltip display
            CheckTooltipHover();
        }

        public void DrawStats()
        {
            if (_gameState.PlayerStats == null) return;

            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            var stats = _gameState.PlayerStats;

            // Base positioning - adjust these values to move the entire stats display
            int baseX = 50;
            int baseY = 50 + Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10; // Closer to map

            int currentY = baseY;

            // Health bar
            currentY = baseY;
            _hpBarBounds = DrawSimpleStatBar(_spriteBatch, "HP", stats.CurrentHealthPoints, stats.MaxHealthPoints,
                new Vector2(baseX, currentY), Global.Instance.palette_Red, Global.Instance.palette_DarkGray, Global.MAP_WIDTH - baseX);

            // Energy bar with pending preview
            currentY += 14;
            _epBarBounds = DrawEnergyBarWithPreview(_spriteBatch, stats, new Vector2(baseX, currentY), Global.MAP_WIDTH - baseX);

            // Secondary stats in compact format
            currentY += 16;
            string secondaryStats = $"Spd:{stats.MoveSpeed:F1} Car:{stats.CarryCapacity} Men:{stats.MentalResistance} Soc:{stats.SocialInfluence}";
            _spriteBatch.DrawString(Global.Instance.DefaultFont, secondaryStats, new Vector2(baseX, currentY), Global.Instance.palette_LightGray);

            // Draw tooltip if needed
            if (_showTooltip)
            {
                DrawTooltip(_spriteBatch);
            }
        }

        private Rectangle DrawEnergyBarWithPreview(SpriteBatch spriteBatch, PlayerStats stats, Vector2 position, int width)
        {
            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Draw label
            string labelText = "EP";
            spriteBatch.DrawString(Global.Instance.DefaultFont, labelText, position, Global.Instance.TextColor);

            // Calculate bar position (right after text)
            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(labelText);
            int barX = (int)(position.X + textSize.X + 5);
            int barY = (int)(position.Y + 3);

            // Bar width: each value is 3 pixels wide + 3 pixel gap = 6 pixels per value, minus the final gap
            int barWidth = (stats.MaxEnergyPoints * 6) - 3;

            // Draw bar background
            Rectangle barBg = new Rectangle(barX - 2, barY - 2, barWidth + 4, 10);
            spriteBatch.Draw(pixel, barBg, Global.Instance.palette_DarkGray);

            // Check if there's a pending path and get the energy cost
            int pendingEnergyCost = _gameState.PendingPathEnergyCost;
            int energyAfterPath = stats.CurrentEnergyPoints - pendingEnergyCost;
            bool hasPendingPath = _gameState.PendingPathPreview.Count > 0;

            // Draw alternating pattern bar
            if (stats.MaxEnergyPoints > 0)
            {
                if (hasPendingPath)
                {
                    // Calculate energy costs for the path
                    int totalPathCost = _gameState.PendingPathEnergyCost;
                    int executedSteps = _gameState.CurrentPathIndex;

                    // Calculate energy consumed by executed steps
                    int energyConsumedSoFar = 0;
                    for (int step = 0; step < executedSteps && step < _gameState.PendingPathPreview.Count; step++)
                    {
                        var stepPos = _gameState.PendingPathPreview[step];
                        energyConsumedSoFar += _gameState.GetMovementEnergyCost(stepPos);
                    }

                    // Calculate remaining pending energy cost
                    int remainingPendingCost = totalPathCost - energyConsumedSoFar;

                    // The "original" energy before any path execution
                    int originalEnergy = stats.CurrentEnergyPoints + energyConsumedSoFar;
                    int energyAfterFullPath = originalEnergy - totalPathCost;

                    // Draw 3-pixel wide segments with 3-pixel gaps
                    for (int i = 0; i < stats.MaxEnergyPoints; i++)
                    {
                        int segmentX = barX + i * 6;
                        Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);

                        if (i < energyAfterFullPath)
                        {
                            // Energy that will remain after the complete path - show in normal green
                            spriteBatch.Draw(pixel, segmentRect, Global.Instance.palette_LightGreen);
                        }
                        else if (i < energyAfterFullPath + remainingPendingCost)
                        {
                            // Remaining pending energy cost - show in yellow
                            spriteBatch.Draw(pixel, segmentRect, Global.Instance.palette_Yellow);
                        }
                        else if (i < stats.CurrentEnergyPoints)
                        {
                            // Current available energy (not part of pending cost) - show in green
                            spriteBatch.Draw(pixel, segmentRect, Global.Instance.palette_LightGreen);
                        }
                        else
                        {
                            // Empty energy - show dimmed
                            spriteBatch.Draw(pixel, segmentRect, Color.Lerp(Global.Instance.palette_DarkGray, Global.Instance.palette_LightGreen, 0.3f));
                        }
                    }
                }
                else
                {
                    // No pending path - normal display
                    for (int i = 0; i < stats.MaxEnergyPoints; i++)
                    {
                        int segmentX = barX + i * 6;
                        Rectangle segmentRect = new Rectangle(segmentX, barY, 3, 6);

                        if (i < stats.CurrentEnergyPoints)
                        {
                            spriteBatch.Draw(pixel, segmentRect, Global.Instance.palette_LightGreen);
                        }
                        else
                        {
                            spriteBatch.Draw(pixel, segmentRect, Color.Lerp(Global.Instance.palette_DarkGray, Global.Instance.palette_LightGreen, 0.3f));
                        }
                    }
                }
            }

            // Return the bounds of the entire bar area (including label) for hover detection
            return new Rectangle((int)position.X, (int)position.Y, (int)textSize.X + 5 + barWidth, 12);
        }

        private Rectangle DrawSimpleStatBar(SpriteBatch spriteBatch, string label, int current, int max, Vector2 position, Color fillColor, Color bgColor, int width)
        {
            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

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

        private void CheckTooltipHover()
        {
            Point mousePosition = _currentMouseState.Position;
            _showTooltip = false;

            if (_gameState.PlayerStats == null) return;

            var stats = _gameState.PlayerStats;

#if DEBUG
            if (_hpBarBounds.Contains(mousePosition) || _epBarBounds.Contains(mousePosition))
            {
                System.Diagnostics.Debug.WriteLine($"Hovering! Mouse: {mousePosition}");
            }
#endif

            // Check HP bar hover
            if (_hpBarBounds.Contains(mousePosition))
            {
                _tooltipText = $"{stats.CurrentHealthPoints}/{stats.MaxHealthPoints}";
                _tooltipPosition = new Vector2(mousePosition.X + 10, mousePosition.Y - 20);
                _showTooltip = true;
            }
            // Check EP bar hover
            else if (_epBarBounds.Contains(mousePosition))
            {
                // Show different tooltip text based on whether there's a pending path
                if (_gameState.PendingPathPreview.Count > 0)
                {
                    int totalPathCost = _gameState.PendingPathEnergyCost;

                    int executedSteps = _gameState.CurrentPathIndex;



                    // Calculate energy consumed by executed steps

                    int energyConsumedSoFar = 0;

                    for (int step = 0; step < executedSteps && step < _gameState.PendingPathPreview.Count; step++)

                    {

                        var stepPos = _gameState.PendingPathPreview[step];

                        energyConsumedSoFar += _gameState.GetMovementEnergyCost(stepPos);

                    }



                    int remainingPendingCost = totalPathCost - energyConsumedSoFar;

                    int originalEnergy = stats.CurrentEnergyPoints + energyConsumedSoFar;

                    int finalEnergy = originalEnergy - totalPathCost;



                    if (_gameState.IsExecutingPath)

                    {

                        _tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints} <{remainingPendingCost} remaining>";

                    }

                    else

                    {

                        _tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints} <{remainingPendingCost} pending>";

                    }
                }
                else
                {
                    _tooltipText = $"{stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}";
                }
                _tooltipPosition = new Vector2(mousePosition.X + 10, mousePosition.Y - 20);
                _showTooltip = true;
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(_tooltipText)) return;

            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Measure tooltip text
            Vector2 textSize = Global.Instance.DefaultFont.MeasureString(_tooltipText);

            // Create tooltip background with padding
            Rectangle tooltipBg = new Rectangle(
                (int)_tooltipPosition.X - 4,
                (int)_tooltipPosition.Y - 2,
                (int)textSize.X + 8,
                (int)textSize.Y + 4
            );

            // Draw tooltip background (dark with slight transparency)
            spriteBatch.Draw(pixel, tooltipBg, Color.Black * 0.8f);

            // Draw tooltip border
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Color.White);

            // Draw tooltip text
            spriteBatch.DrawString(Global.Instance.DefaultFont, _tooltipText, _tooltipPosition, Color.White);
        }
    }
}