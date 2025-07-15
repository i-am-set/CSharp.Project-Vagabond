using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;

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
        private int? _hoveredEnemyId;

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
        /// Updates the panel to track mouse hover state.
        /// </summary>
        /// <param name="currentMouseState">The current state of the mouse.</param>
        public void Update(MouseState currentMouseState)
        {
            if (!_gameState.IsInCombat)
            {
                _hoveredEnemyId = null;
                return;
            }
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();
            _hoveredEnemyId = GetEnemyIdAt(virtualMousePos);
        }

        /// <summary>
        /// Draws the enemy display panel, including a grid of enemies with their health bars.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GameTime gameTime, MouseState currentMouseState)
        {
            if (!_gameState.IsInCombat) return;

            var animationManager = ServiceLocator.Get<CombatUIAnimationManager>();
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

            var enemies = _gameState.Combatants.Where(id => id != _gameState.PlayerEntityId).ToList();
            if (enemies.Count == 0) return;

            int cellWidth = _bounds.Width / MAX_COLS;
            int cellHeight = _bounds.Height / MAX_ROWS;
            int enemyIndex = 0;

            foreach (var entityId in enemies)
            {
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
                int spriteWidth = 32;
                int spriteHeight = 48;
                var spriteRect = new Rectangle(
                    cellRect.Center.X - spriteWidth / 2,
                    cellRect.Y + (cellHeight - spriteHeight) / 2, // Center vertically in the cell
                    spriteWidth,
                    spriteHeight
                );

                // Draw selection effects
                if (_gameState.UIState == CombatUIState.SelectTarget)
                {
                    if (_hoveredEnemyId.HasValue && _hoveredEnemyId.Value == entityId)
                    {
                        DrawCornerBrackets(spriteBatch, spriteRect, Color.Red, 2);
                    }
                    else
                    {
                        bool isPulsing = animationManager.IsPulsing("TargetSelector");
                        if (isPulsing)
                        {
                            DrawDottedRectangle(spriteBatch, spriteRect, Color.White);
                        }
                    }
                }

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
        /// Gets the ID of the enemy at a specific mouse position.
        /// </summary>
        /// <param name="mousePosition">The position of the mouse cursor.</param>
        /// <returns>The entity ID of the enemy, or null if no enemy is at that position.</returns>
        public int? GetEnemyIdAt(Point mousePosition)
        {
            if (!_gameState.IsInCombat) return null;

            var enemies = _gameState.Combatants.Where(id => id != _gameState.PlayerEntityId).ToList();
            if (enemies.Count == 0) return null;

            int cellWidth = _bounds.Width / MAX_COLS;
            int cellHeight = _bounds.Height / MAX_ROWS;
            int enemyIndex = 0;

            foreach (var entityId in enemies)
            {
                if (enemyIndex >= MAX_COLS * MAX_ROWS) break;

                int col = enemyIndex % MAX_COLS;
                int row = enemyIndex / MAX_COLS;
                var cellRect = new Rectangle(_bounds.X + col * cellWidth, _bounds.Y + row * cellHeight, cellWidth, cellHeight);

                if (cellRect.Contains(mousePosition))
                {
                    return entityId;
                }

                enemyIndex++;
            }

            return null;
        }

        private void DrawCornerBrackets(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            int armLength = (int)(Math.Min(rect.Width, rect.Height) * 0.25f);
            armLength = Math.Clamp(armLength, 5, 20);

            // Top-left
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, armLength), color);
            // Top-right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, armLength), color);
            // Bottom-left
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - armLength, thickness, armLength), color);
            // Bottom-right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - armLength, thickness, armLength), color);
        }

        private void DrawDottedRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            int dotSize = 1;
            int dotGap = 3; // 1 pixel dot, 2 pixels gap

            // Top and Bottom borders
            for (int x = rect.Left; x < rect.Right; x += dotGap)
            {
                spriteBatch.Draw(pixel, new Rectangle(x, rect.Top, dotSize, dotSize), color);
                spriteBatch.Draw(pixel, new Rectangle(x, rect.Bottom - dotSize, dotSize, dotSize), color);
            }
            // Left and Right borders
            for (int y = rect.Top; y < rect.Bottom; y += dotGap)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.Left, y, dotSize, dotSize), color);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - dotSize, y, dotSize, dotSize), color);
            }
        }
    }
}