﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the rendering of all enemies in combat, including their sprites and health bars.
    /// </summary>
    public class EnemyDisplayPanel
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly Global _global;

        private readonly Rectangle _bounds;
        private const int PADDING = 10;
        private const int BORDER_THICKNESS = 2;
        private const int MAX_COLS = 7;
        private const int MAX_ROWS = 2;

        public EnemyDisplayPanel(Rectangle bounds)
        {
            _bounds = bounds;
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Draws the enemy display panel, including a grid of enemies with their health bars.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Draw the border and background
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg);

            if (_gameState.Combatants.Count == 0) return;

            int cellWidth = _bounds.Width / MAX_COLS;
            int cellHeight = _bounds.Height / MAX_ROWS;
            int enemyIndex = 0;

            foreach (var entityId in _gameState.Combatants)
            {
                // Skip the player
                if (entityId == _gameState.PlayerEntityId) continue;

                // Stop if we've drawn the max number of enemies
                if (enemyIndex >= MAX_COLS * MAX_ROWS) break;

                var renderable = _componentStore.GetComponent<RenderableComponent>(entityId);
                var health = _componentStore.GetComponent<HealthComponent>(entityId);

                if (renderable == null || health == null)
                {
                    enemyIndex++;
                    continue; // Skip entities without necessary components
                }

                // Calculate grid position
                int col = enemyIndex % MAX_COLS;
                int row = enemyIndex / MAX_COLS;
                var cellRect = new Rectangle(_bounds.X + col * cellWidth, _bounds.Y + row * cellHeight, cellWidth, cellHeight);

                // Draw sprite (or placeholder)
                var spriteTexture = renderable.Texture ?? pixel;
                var spriteSize = 32;
                var spriteRect = new Rectangle(
                    cellRect.Center.X - spriteSize / 2,
                    cellRect.Y + PADDING,
                    spriteSize,
                    spriteSize
                );
                spriteBatch.Draw(spriteTexture, spriteRect, renderable.Color);

                // Draw health bar
                int barHeight = 8;
                int barWidth = cellWidth - (PADDING * 2);
                var barPosition = new Vector2(
                    cellRect.X + PADDING,
                    spriteRect.Bottom + 5 // 5 pixels below the sprite
                );

                // Background bar
                var bgBarRect = new Rectangle((int)barPosition.X, (int)barPosition.Y, barWidth, barHeight);
                spriteBatch.Draw(pixel, bgBarRect, _global.Palette_Red);

                // Foreground (current health) bar
                if (health.CurrentHealth > 0)
                {
                    float healthPercentage = (float)health.CurrentHealth / health.MaxHealth;
                    int fgBarWidth = (int)(barWidth * healthPercentage);
                    var fgBarRect = new Rectangle((int)barPosition.X, (int)barPosition.Y, fgBarWidth, barHeight);
                    spriteBatch.Draw(pixel, fgBarRect, Color.LawnGreen);
                }

                enemyIndex++;
            }
        }

        /// <summary>
        /// Gets the ID of the enemy at a specific mouse position. Placeholder for now.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor.</param>
        /// <returns>The entity ID of the enemy, or null if no enemy is at that position.</returns>
        public int? GetEnemyIdAt(Point mousePosition)
        {
            // TODO: Implement logic to check which grid cell the mouse is in
            // and return the corresponding enemy ID.
            return null;
        }
    }
}