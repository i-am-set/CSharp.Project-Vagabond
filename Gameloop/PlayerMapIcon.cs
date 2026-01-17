using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
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
        private const float IDLE_FRAME_DURATION = 1.0f;   // Slow bob when standing still
        private const float MOVING_FRAME_DURATION = 0.5f; // Fast bob when walking (2x speed)

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
            // Lazy initialization: Get the texture only when it's first needed for drawing.
            if (_texture == null)
            {
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                _texture = spriteManager.MapNodePlayerSprite;
                _silhouette = spriteManager.MapNodePlayerSpriteSilhouette; // Get the silhouette

                if (_texture != null)
                {
                    // The origin is the center of a single 32x32 frame.
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

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Determine target duration based on state
            float currentDuration = _isMoving ? MOVING_FRAME_DURATION : IDLE_FRAME_DURATION;

            _frameTimer += deltaTime;
            if (_frameTimer >= currentDuration)
            {
                _frameTimer -= currentDuration;
                _frameIndex = (_frameIndex + 1) % 2; // Cycle between frame 0 and 1
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            InitializeTexture(); // Ensure texture is loaded before drawing.
            if (_texture == null) return; // Don't draw if the texture is still null.

            // Calculate the source rectangle for the current animation frame.
            var sourceRectangle = new Rectangle(_frameIndex * 32, 0, 32, 32);
            Color outlineColor = _global.Palette_Black;

            // Draw Outline (4 directions) using the SILHOUETTE
            if (_silhouette != null)
            {
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(-1, 0), sourceRectangle, outlineColor, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(1, 0), sourceRectangle, outlineColor, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(0, -1), sourceRectangle, outlineColor, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
                spriteBatch.DrawSnapped(_silhouette, Position + new Vector2(0, 1), sourceRectangle, outlineColor, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
            }

            // Draw Main Sprite
            spriteBatch.DrawSnapped(_texture, Position, sourceRectangle, Color.White, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
        }
    }
}
