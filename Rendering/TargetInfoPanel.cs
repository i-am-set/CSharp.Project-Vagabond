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
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly ArchetypeManager _archetypeManager;
        private readonly Global _global;

        private readonly Rectangle _bounds;
        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;

        public TargetInfoPanel(Rectangle bounds)
        {
            _bounds = bounds;
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Draws the target info panel if a target is selected.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!_gameState.IsInCombat || font == null) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // --- Draw the panel frame regardless of whether a target is selected ---
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg);

            // --- Now, draw the content based on whether a target is selected ---
            if (_gameState.SelectedTargetId.HasValue)
            {
                DrawTargetDetails(spriteBatch, _gameState.SelectedTargetId.Value, font);
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
                spriteBatch.DrawString(font, noTargetText, textPosition, _global.Palette_Gray);
            }
        }

        private void DrawTargetDetails(SpriteBatch spriteBatch, int targetId, BitmapFont font)
        {
            var health = _componentStore.GetComponent<HealthComponent>(targetId);
            var archetypeIdComp = _componentStore.GetComponent<ArchetypeIdComponent>(targetId);
            var archetype = _archetypeManager.GetArchetype(archetypeIdComp?.ArchetypeId ?? "Unknown");
            var targetPosComp = _componentStore.GetComponent<LocalPositionComponent>(targetId);
            var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);

            if (health == null || archetype == null || targetPosComp == null || playerPosComp == null) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Draw content
            float currentY = _bounds.Y + PADDING;

            // Target Name
            spriteBatch.DrawString(font, archetype.Name, new Vector2(_bounds.X + PADDING, currentY), _global.Palette_Red);
            currentY += font.LineHeight + PADDING;

            // Health Bar
            string hpText = $"HP: {health.CurrentHealth} / {health.MaxHealth}";
            spriteBatch.DrawString(font, hpText, new Vector2(_bounds.X + PADDING, currentY), _global.GameTextColor);
            currentY += font.LineHeight;

            int barWidth = _bounds.Width - (PADDING * 2);
            int barHeight = 10;
            var bgBarRect = new Rectangle(_bounds.X + PADDING, (int)currentY, barWidth, barHeight);
            spriteBatch.Draw(pixel, bgBarRect, _global.Palette_Red);

            if (health.CurrentHealth > 0)
            {
                float healthPercentage = (float)health.CurrentHealth / health.MaxHealth;
                int fgBarWidth = (int)(barWidth * healthPercentage);
                var fgBarRect = new Rectangle(_bounds.X + PADDING, (int)currentY, fgBarWidth, barHeight);
                spriteBatch.Draw(pixel, fgBarRect, Color.LawnGreen);
            }
            currentY += barHeight + PADDING;

            // Distance
            float distance = Vector2.Distance(playerPosComp.LocalPosition, targetPosComp.LocalPosition);
            string distanceText = $"Distance: {distance:F1}m";
            spriteBatch.DrawString(font, distanceText, new Vector2(_bounds.X + PADDING, currentY), _global.GameTextColor);
        }
    }
}