using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.UI
{
    public class PlayerMapIcon
    {
        private Texture2D? _texture;
        private Texture2D? _silhouette;
        private Vector2 _origin;
        private bool _isMoving = false;
        private readonly Global _global;

        // --- Tuning ---
        private const float IDLE_FRAME_DURATION = 1.0f;
        private const float MOVING_FRAME_DURATION = 0.5f;

        // Hover Animation
        private float _hoverScale = 1.0f;
        private const float HOVER_SCALE_TARGET = 1.2f;
        private const float HOVER_ANIM_SPEED = 15f;

        // Animation State
        private float _frameTimer;
        private int _frameIndex;

        public Vector2 Position { get; private set; }

        public PlayerMapIcon()
        {
            _global = ServiceLocator.Get<Global>();
        }

        private void InitializeTexture()
        {
            if (_texture == null)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                _texture = spriteManager.MapNodePlayerSprite;
                _silhouette = spriteManager.MapNodePlayerSpriteSilhouette;

                if (_texture != null)
                {
                    _origin = new Vector2(16, 16);
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            this.Position = newPosition;
        }

        public void SetIsMoving(bool isMoving)
        {
            _isMoving = isMoving;
        }

        public Rectangle GetBounds()
        {
            // 32x32 sprite centered on Position
            return new Rectangle((int)(Position.X - 16), (int)(Position.Y - 16), 32, 32);
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float currentDuration = _isMoving ? MOVING_FRAME_DURATION : IDLE_FRAME_DURATION;

            _frameTimer += deltaTime;
            if (_frameTimer >= currentDuration)
            {
                _frameTimer -= currentDuration;
                _frameIndex = (_frameIndex + 1) % 2;
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool isHovered, float dt)
        {
            InitializeTexture();
            if (_texture == null) return;

            // --- Hover Animation Logic ---
            float targetScale = isHovered ? HOVER_SCALE_TARGET : 1.0f;
            // Time-corrected damping for smooth scaling
            float damping = 1.0f - MathF.Exp(-HOVER_ANIM_SPEED * dt);
            _hoverScale = MathHelper.Lerp(_hoverScale, targetScale, damping);

            var sourceRectangle = new Rectangle(_frameIndex * 32, 0, 32, 32);

            // Highlight outline if hovered
            Color outlineColor = isHovered ? _global.ButtonHoverColor : _global.Palette_Black;

            // Apply scale
            Vector2 scaleVec = new Vector2(_hoverScale);

            if (_silhouette != null)
            {
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(-1, 0), sourceRectangle, outlineColor, 0f, _origin, scaleVec, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(1, 0), sourceRectangle, outlineColor, 0f, _origin, scaleVec, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(0, -1), sourceRectangle, outlineColor, 0f, _origin, scaleVec, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(0, 1), sourceRectangle, outlineColor, 0f, _origin, scaleVec, SpriteEffects.None, 0.5f);
            }

            spriteBatch.DrawSnapped(_texture, Position, sourceRectangle, Color.White, 0f, _origin, scaleVec, SpriteEffects.None, 0.5f);
        }
    }
}