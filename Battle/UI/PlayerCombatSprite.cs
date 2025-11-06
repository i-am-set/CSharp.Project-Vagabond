#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages the state, animation, and rendering of the player's heart sprite in combat.
    /// </summary>
    public class PlayerCombatSprite
    {
        private Texture2D? _texture;
        private Vector2 _position;
        private Vector2 _origin;

        // Animation
        private float _frameTimer;
        private int _frameIndex;
        private int _frameCount;
        private const int FRAME_WIDTH = 32;
        private const int FRAME_HEIGHT = 32;
        private const float FRAME_DURATION = 0.4f; // ~2.5 FPS

        public PlayerCombatSprite()
        {
            // Texture is lazy-loaded to ensure SpriteManager is ready.
        }

        private void Initialize()
        {
            if (_texture == null)
            {
                _texture = ServiceLocator.Get<SpriteManager>().PlayerHeartSpriteSheet;
                if (_texture != null)
                {
                    _origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
                    _frameCount = _texture.Width / FRAME_WIDTH;
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            _position = newPosition;
        }

        public void Update(GameTime gameTime)
        {
            Initialize();
            if (_texture == null || _frameCount <= 1) return;

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= FRAME_DURATION)
            {
                _frameTimer -= FRAME_DURATION;
                _frameIndex = (_frameIndex + 1) % _frameCount;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BattleAnimationManager animationManager, BattleCombatant player, Color? tintColorOverride = null)
        {
            Initialize();
            if (_texture == null || player == null) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var silhouette = spriteManager.PlayerHeartSpriteSheetSilhouette;
            var global = ServiceLocator.Get<Global>();

            var sourceRectangle = new Rectangle(_frameIndex * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);

            // Get hit flash state and apply shake
            var hitFlashState = animationManager.GetHitFlashState(player.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;
            bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;

            var topLeftPosition = new Point(
                (int)MathF.Round(_position.X - _origin.X + shakeOffset.X),
                (int)MathF.Round(_position.Y - _origin.Y + shakeOffset.Y)
            );

            Color mainColor = tintColorOverride ?? Color.White;
            Color outlineColor = tintColorOverride ?? global.Palette_DarkGray;


            if (silhouette != null)
            {
                // Draw outline using integer-based rectangles offset from the snapped top-left position.
                var rect = new Rectangle(0, 0, FRAME_WIDTH, FRAME_HEIGHT);

                rect.Location = new Point(topLeftPosition.X - 1, topLeftPosition.Y);
                spriteBatch.Draw(silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X + 1, topLeftPosition.Y);
                spriteBatch.Draw(silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y - 1);
                spriteBatch.Draw(silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);

                rect.Location = new Point(topLeftPosition.X, topLeftPosition.Y + 1);
                spriteBatch.Draw(silhouette, rect, sourceRectangle, outlineColor, 0f, Vector2.Zero, SpriteEffects.None, 0.49f);
            }

            // Draw main sprite
            var mainRect = new Rectangle(topLeftPosition.X, topLeftPosition.Y, FRAME_WIDTH, FRAME_HEIGHT);
            spriteBatch.Draw(_texture, mainRect, sourceRectangle, mainColor, 0f, Vector2.Zero, SpriteEffects.None, 0.5f);

            // Draw flash overlay
            if (isFlashingWhite && silhouette != null)
            {
                spriteBatch.Draw(silhouette, mainRect, sourceRectangle, Color.White * 0.8f, 0f, Vector2.Zero, SpriteEffects.None, 0.51f);
            }
        }
    }
}
#nullable restore