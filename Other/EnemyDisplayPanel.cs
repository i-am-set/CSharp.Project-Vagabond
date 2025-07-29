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
        public int? HoveredEnemyId { get; private set; }
        private int? _previousHoveredEnemyId;
        private float _hoverAnimationStartTime;

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
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        /// <param name="currentMouseState">The current state of the mouse.</param>
        public void Update(GameTime gameTime, MouseState currentMouseState)
        {
            _previousHoveredEnemyId = HoveredEnemyId;

            if (!_gameState.IsInCombat)
            {
                HoveredEnemyId = null;
                return;
            }
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position).ToPoint();
            HoveredEnemyId = GetEnemyIdAt(virtualMousePos);

            // If the hovered enemy has changed, reset the animation start time.
            if (HoveredEnemyId.HasValue && HoveredEnemyId != _previousHoveredEnemyId)
            {
                _hoverAnimationStartTime = (float)gameTime.TotalGameTime.TotalSeconds;
            }
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

                    if (HoveredEnemyId.HasValue && HoveredEnemyId.Value == entityId)
                    {
                        // HOVERED: Draw the new animated outline effect
                        DrawAnimatedOutline(spriteBatch, baseSelectorRect, _global.CombatSelectableColor, gameTime, _hoverAnimationStartTime);
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

        private void DrawAnimatedOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, GameTime gameTime, float animationStartTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            const float duration = 1.5f;
            const int tailSegments = 120; // Much longer tail
            const float tailProgressLength = 0.4f; // The tail will cover 40% of the perimeter

            float elapsedTime = (float)gameTime.TotalGameTime.TotalSeconds - animationStartTime;
            // Add half the duration to the elapsed time to start the animation at the halfway point.
            float timeOffset = elapsedTime + (duration / 2.0f);
            float linearHeadProgress = (timeOffset % duration) / duration;

            Vector2 lastPosition = GetPositionOnRectPerimeter(rect, CalculateFinalProgress(linearHeadProgress));

            for (int i = 1; i <= tailSegments; i++)
            {
                // Calculate the progress of the current segment along the tail
                float linearSegmentProgress = linearHeadProgress - (i * (tailProgressLength / tailSegments));

                // Wrap progress if it's negative
                if (linearSegmentProgress < 0)
                {
                    linearSegmentProgress += 1.0f;
                }

                Vector2 currentPosition = GetPositionOnRectPerimeter(rect, CalculateFinalProgress(linearSegmentProgress));

                // Calculate distance and angle between the last point and the current point
                Vector2 delta = currentPosition - lastPosition;
                float distance = delta.Length();
                float angle = (float)Math.Atan2(delta.Y, delta.X);

                // Fade the tail along its length
                float alpha = 1.0f - ((float)i / tailSegments);

                // Make the tail thinner towards the end
                float thickness = 2.0f * (1.0f - ((float)i / (tailSegments * 1.5f)));

                // Draw a line segment
                if (distance > 0) // Avoid drawing zero-length lines
                {
                    spriteBatch.Draw(
                        pixel,
                        lastPosition,
                        null,
                        color * alpha,
                        angle,
                        new Vector2(0, 0.5f), // Origin at the left-center of the 1x1 pixel
                        new Vector2(distance, thickness),
                        SpriteEffects.None,
                        0
                    );
                }

                lastPosition = currentPosition;
            }
        }

        /// <summary>
        /// Calculates the final progress along the perimeter by blending linear and eased motion.
        /// </summary>
        /// <param name="linearProgress">The raw, linear progress (0 to 1).</param>
        /// <returns>The final progress value to use for positioning.</returns>
        private float CalculateFinalProgress(float linearProgress)
        {
            const float minSpeedRatio = 0.1f; // 10% minimum speed
            float easedProgress = Easing.EaseInOutCirc(linearProgress);
            // Lerp between linear and eased progress. The weight on the eased progress determines the curve's influence.
            // A weight of 1.0 would be pure easing, 0.0 would be pure linear.
            // By using 1.0 - minSpeedRatio, we ensure that at least minSpeedRatio of the linear speed is always present.
            return MathHelper.Lerp(linearProgress, easedProgress, 1.0f - minSpeedRatio);
        }

        private Vector2 GetPositionOnRectPerimeter(Rectangle rect, float progress)
        {
            // Perimeter segments (clockwise from bottom-center)
            float bottomHalf1 = rect.Width / 2f;
            float rightSide = rect.Height;
            float topSide = rect.Width;
            float leftSide = rect.Height;
            float bottomHalf2 = rect.Width / 2f;

            float perimeter = bottomHalf1 + rightSide + topSide + leftSide + bottomHalf2;
            float distance = progress * perimeter;

            // Bottom edge, center to right
            if (distance <= bottomHalf1)
            {
                return new Vector2(rect.Center.X + distance, rect.Bottom);
            }
            distance -= bottomHalf1;

            // Right edge, bottom to top
            if (distance <= rightSide)
            {
                return new Vector2(rect.Right, rect.Bottom - distance);
            }
            distance -= rightSide;

            // Top edge, right to left
            if (distance <= topSide)
            {
                return new Vector2(rect.Right - distance, rect.Top);
            }
            distance -= topSide;

            // Left edge, top to bottom
            if (distance <= leftSide)
            {
                return new Vector2(rect.Left, rect.Top + distance);
            }
            distance -= leftSide;

            // Bottom edge, left to center
            return new Vector2(rect.Left + distance, rect.Bottom);
        }
    }
}