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
        private const int MAX_ROWS = 1;
        private const int HITBOX_INSET = 6; // Inset from the cell edge for hover/selection

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

                // Create a smaller hitbox inside the cell
                var hitboxRect = cellRect;
                hitboxRect.Inflate(-HITBOX_INSET, -HITBOX_INSET);

                // Define sprite and health bar dimensions
                int spriteWidth = 48;
                int spriteHeight = 80;
                int barHeight = 8;
                int barWidth = cellWidth - (PADDING * 2);
                const int spaceBetweenSpriteAndBar = 4;

                // Calculate total content height
                int totalContentHeight = spriteHeight + spaceBetweenSpriteAndBar + barHeight;

                // Calculate the starting Y position to center the content block within the hitbox
                int contentStartY = hitboxRect.Y + (hitboxRect.Height - totalContentHeight) / 2;

                // Position sprite
                var spriteRect = new Rectangle(
                    cellRect.Center.X - spriteWidth / 2,
                    contentStartY,
                    spriteWidth,
                    spriteHeight
                );

                // Position health bar
                int barY = spriteRect.Bottom + spaceBetweenSpriteAndBar;
                var barPosition = new Vector2(
                    cellRect.X + PADDING,
                    barY
                );

                // Draw sprite
                var spriteTexture = renderable.Texture ?? pixel;
                spriteBatch.Draw(spriteTexture, spriteRect, renderable.Color);

                // Draw selection effects
                if (_gameState.UIState == CombatUIState.SelectTarget)
                {
                    Rectangle baseSelectorRect = hitboxRect;

                    if (_hoveredEnemyId.HasValue && _hoveredEnemyId.Value == entityId)
                    {
                        // HOVERED: Draw pulsing corner brackets
                        bool isPulsing = animationManager.IsPulsing("TargetSelector");
                        Rectangle pulsingRect = baseSelectorRect;
                        pulsingRect.Inflate(isPulsing ? 2 : 1, isPulsing ? 2 : 1);
                        DrawCornerBrackets(spriteBatch, pulsingRect, _global.CombatSelectableColor, 2);
                    }
                    else
                    {
                        // NOT HOVERED (but selectable): Draw marching ants
                        DrawDottedRectangle(spriteBatch, baseSelectorRect, _global.CombatSelectorColor, gameTime);
                    }
                }

                // Draw health bar
                // Background bar
                var bgBarRect = new Rectangle((int)barPosition.X, (int)barPosition.Y, barWidth, barHeight);
                spriteBatch.Draw(pixel, bgBarRect, _global.Palette_Red);

                // Foreground (current health) bar
                if (health.CurrentHealth > 0)
                {
                    float healthPercentage = (float)health.CurrentHealth / health.MaxHealth;
                    int fgBarWidth = (int)(barWidth * healthPercentage);
                    var fgBarRect = new Rectangle((int)barPosition.X, (int)barPosition.Y, fgBarWidth, barHeight);
                    spriteBatch.Draw(pixel, fgBarRect, _global.Palette_DarkGreen);
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

                // Use a smaller hitbox inside the cell for hover detection
                var hitboxRect = cellRect;
                hitboxRect.Inflate(-HITBOX_INSET, -HITBOX_INSET);

                if (hitboxRect.Contains(mousePosition))
                {
                    return entityId;
                }

                enemyIndex++;
            }

            return null;
        }

        private void DrawHollowRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
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

        private void DrawDottedRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            const int dashLength = 2;
            const int dashThickness = 2;
            const int gapLength = 2;
            const int patternLength = dashLength + gapLength;
            const float speed = 15f; // pixels per second

            // Calculate a cyclical offset based on time to create the "marching ants" effect
            float offset = ((float)gameTime.TotalGameTime.TotalSeconds * speed) % patternLength;

            // Top border (moves right)
            for (float x = rect.Left - offset; x < rect.Right; x += patternLength)
            {
                float startX = Math.Max(x, rect.Left);
                float endX = Math.Min(x + dashLength, rect.Right);
                if (endX > startX)
                {
                    spriteBatch.Draw(pixel, new Rectangle((int)startX, rect.Top, (int)(endX - startX), dashThickness), color);
                }
            }

            // Bottom border (moves left)
            for (float x = rect.Right + offset; x > rect.Left; x -= patternLength)
            {
                float startX = Math.Max(x - dashLength, rect.Left);
                float endX = Math.Min(x, rect.Right);
                if (endX > startX)
                {
                    spriteBatch.Draw(pixel, new Rectangle((int)startX, rect.Bottom - dashThickness, (int)(endX - startX), dashThickness), color);
                }
            }

            // Right border (moves down)
            for (float y = rect.Top - offset; y < rect.Bottom; y += patternLength)
            {
                float startY = Math.Max(y, rect.Top);
                float endY = Math.Min(y + dashLength, rect.Bottom);
                if (endY > startY)
                {
                    spriteBatch.Draw(pixel, new Rectangle(rect.Right - dashThickness, (int)startY, dashThickness, (int)(endY - startY)), color);
                }
            }

            // Left border (moves up)
            for (float y = rect.Bottom + offset; y > rect.Top; y -= patternLength)
            {
                float startY = Math.Max(y - dashLength, rect.Top);
                float endY = Math.Min(y, rect.Bottom);
                if (endY > startY)
                {
                    spriteBatch.Draw(pixel, new Rectangle(rect.Left, (int)startY, dashThickness, (int)(endY - startY)), color);
                }
            }
        }
    }
}