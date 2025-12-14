using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class PlayerMapIcon
    {
        private Texture2D? _texture;
        private Vector2 _origin;
        private bool _isMoving = false;

        // --- Tuning ---
        private const float FRAME_DURATION = 1f; // Time each frame is displayed

        // Animation State
        private float _frameTimer;
        private int _frameIndex;

        public Vector2 Position { get; private set; }

        public PlayerMapIcon()
        {
            // Constructor is now empty to avoid accessing assets before they are loaded.
        }

        private void InitializeTexture()
        {
            // Lazy initialization: Get the texture only when it's first needed for drawing.
            if (_texture == null)
            {
                _texture = ServiceLocator.Get<SpriteManager>().MapNodePlayerSprite;
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

            // Animate constantly, regardless of movement state.
            _frameTimer += deltaTime;
            if (_frameTimer >= FRAME_DURATION)
            {
                _frameTimer -= FRAME_DURATION;
                _frameIndex = (_frameIndex + 1) % 2; // Cycle between frame 0 and 1
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            InitializeTexture(); // Ensure texture is loaded before drawing.
            if (_texture == null) return; // Don't draw if the texture is still null.

            // Calculate the source rectangle for the current animation frame.
            var sourceRectangle = new Rectangle(_frameIndex * 32, 0, 32, 32);

            spriteBatch.DrawSnapped(_texture, Position, sourceRectangle, Color.White, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
        }
    }
}