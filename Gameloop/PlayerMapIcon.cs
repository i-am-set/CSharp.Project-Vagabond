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
        private Vector2 _currentDirection = Vector2.UnitX;
        private bool _isMoving = false;

        // --- Tuning ---
        private const float WADDLE_FRAME_DURATION = 0.2f; // Time each frame is displayed
        private static readonly Vector2[] _waddleAnimationFrames = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, -1),
        };

        // Animation State
        private float _waddleFrameTimer;
        private int _waddleFrameIndex;

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
                _texture = ServiceLocator.Get<SpriteManager>().PlayerSprite;
                if (_texture != null)
                {
                    _origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            Vector2 direction = newPosition - this.Position;
            // Use a small epsilon to avoid flipping from floating point noise when standing still
            if (Math.Abs(direction.X) > 0.01f)
            {
                _currentDirection = direction;
            }
            this.Position = newPosition;
        }

        public void SetIsMoving(bool isMoving)
        {
            _isMoving = isMoving;
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_isMoving)
            {
                _waddleFrameTimer += deltaTime;
                if (_waddleFrameTimer >= WADDLE_FRAME_DURATION)
                {
                    _waddleFrameTimer -= WADDLE_FRAME_DURATION;
                    _waddleFrameIndex = (_waddleFrameIndex + 1) % _waddleAnimationFrames.Length;
                }
            }
            else
            {
                // When not moving, reset to the base frame.
                _waddleFrameIndex = 0;
                _waddleFrameTimer = 0;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            InitializeTexture(); // Ensure texture is loaded before drawing.
            if (_texture == null) return; // Don't draw if the texture is still null.

            // Pillar 2: Keyframed "Waddle" Motion
            Vector2 waddleOffset = _waddleAnimationFrames[_waddleFrameIndex];
            var drawPosition = Position + waddleOffset;

            // Pillar 3: Orientation
            SpriteEffects effects = _currentDirection.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.DrawSnapped(_texture, drawPosition, null, Color.White, 0f, _origin, 1f, effects, 0.5f);
        }
    }
}