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
            if (!gameState.IsInCombat) return; // Only draw the panel at all during combat

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            // --- Draw the panel frame regardless of whether a target is selected ---
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);
            spriteBatch.Draw(Core.Pixel, _bounds, Global.Instance.TerminalBg);

            // --- Now, draw the content based on whether a target is selected ---
            if (gameState.SelectedTargetId.HasValue)
            {
                DrawTargetDetails(spriteBatch, gameState.SelectedTargetId.Value, font);
            }
            else
            {
                // Draw a placeholder message if no target is selected
                string noTargetText = "[No Target Selected]";
                Vector2 textSize = font.MeasureString(noTargetText);
                Vector2 textPosition = new Vector2(
                    _bounds.Center.X - textSize.X / 2,
                    _bounds.Center.Y - textSize.Y / 2
                );
                spriteBatch.DrawString(font, noTargetText, textPosition, Global.Instance.Palette_Gray);
            }
        }

        private void DrawTargetDetails(SpriteBatch spriteBatch, int targetId, BitmapFont font)
        {
            var gameState = Core.CurrentGameState;
            var health = Core.ComponentStore.GetComponent<HealthComponent>(targetId);
            var archetypeIdComp = Core.ComponentStore.GetComponent<ArchetypeIdComponent>(targetId);
            var archetype = ArchetypeManager.Instance.GetArchetype(archetypeIdComp?.ArchetypeId ?? "Unknown");
            var targetPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(targetId);
            var playerPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId);

            if (health == null || archetype == null || targetPosComp == null || playerPosComp == null) return;

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
            currentY += barHeight + PADDING;

            // Distance
            float distance = Vector2.Distance(playerPosComp.LocalPosition, targetPosComp.LocalPosition);
            string distanceText = $"Distance: {distance:F1}m";
            spriteBatch.DrawString(font, distanceText, new Vector2(_bounds.X + PADDING, currentY), Global.Instance.GameTextColor);
        }
    }
}