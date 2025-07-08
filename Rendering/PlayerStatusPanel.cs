using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel that draws the player's combat information like health and action points.
    /// </summary>
    public class PlayerStatusPanel
    {
        private readonly Rectangle _bounds;
        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;

        public PlayerStatusPanel(Rectangle bounds)
        {
            _bounds = bounds;
        }

        /// <summary>
        /// Draws the player status panel if in combat.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            var health = Core.ComponentStore.GetComponent<HealthComponent>(gameState.PlayerEntityId);
            var combatStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(gameState.PlayerEntityId);

            if (health == null || combatStats == null) return;

            // Draw border and background
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);
            spriteBatch.Draw(Core.Pixel, _bounds, Global.Instance.TerminalBg);

            // Draw content
            float currentY = _bounds.Y + PADDING;

            // Player Name
            spriteBatch.DrawString(font, "Player", new Vector2(_bounds.X + PADDING, currentY), Color.Yellow);
            currentY += font.LineHeight + PADDING;

            // Health Bar
            string hpText = $"HP: {health.CurrentHealth} / {health.MaxHealth}";
            spriteBatch.DrawString(font, hpText, new Vector2(_bounds.X + PADDING, currentY), Global.Instance.GameTextColor);
            currentY += font.LineHeight;

            int barWidth = _bounds.Width - (PADDING * 2);
            int barHeight = 10;
            var bgBarRect = new Rectangle(_bounds.X + PADDING, (int)currentY, barWidth, barHeight);
            spriteBatch.Draw(Core.Pixel, bgBarRect, Global.Instance.Palette_Red);

            if (health.CurrentHealth > 0)
            {
                float healthPercentage = (float)health.CurrentHealth / health.MaxHealth;
                int fgBarWidth = (int)(barWidth * healthPercentage);
                var fgBarRect = new Rectangle(_bounds.X + PADDING, (int)currentY, fgBarWidth, barHeight);
                spriteBatch.Draw(Core.Pixel, fgBarRect, Color.LawnGreen);
            }
            currentY += barHeight + PADDING;

            // Action Points
            string apText = $"AP: {combatStats.ActionPoints}";
            spriteBatch.DrawString(font, apText, new Vector2(_bounds.X + PADDING, currentY), Global.Instance.GameTextColor);
        }
    }
}