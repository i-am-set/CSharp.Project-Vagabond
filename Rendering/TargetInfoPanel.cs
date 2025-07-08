using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel that displays information about the currently selected enemy target.
    /// </summary>
    public class TargetInfoPanel
    {
        private readonly Rectangle _bounds;
        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;

        public TargetInfoPanel(Rectangle bounds)
        {
            _bounds = bounds;
        }

        /// <summary>
        /// Draws the target info panel if a target is selected.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat || !gameState.SelectedTargetId.HasValue) return;

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            int targetId = gameState.SelectedTargetId.Value;
            var health = Core.ComponentStore.GetComponent<HealthComponent>(targetId);
            var renderable = Core.ComponentStore.GetComponent<RenderableComponent>(targetId);
            var archetype = ArchetypeManager.Instance.GetArchetype(renderable?.Texture?.Name ?? "Unknown");

            if (health == null || archetype == null) return;

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

            // Target Name
            spriteBatch.DrawString(font, archetype.Name, new Vector2(_bounds.X + PADDING, currentY), Global.Instance.Palette_Red);
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
        }
    }
}