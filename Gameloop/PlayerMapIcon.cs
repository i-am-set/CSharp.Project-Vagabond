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
        private float _bobTimer;
        private Vector2 _currentDirection = Vector2.UnitX;
        private bool _isMoving = false;
        private float _waddleIntensity = 0f;

        // --- Tuning ---
        private const float WADDLE_SPEED = 8f;
        private const float WADDLE_AMOUNT_X = 1.5f;
        private const float WADDLE_AMOUNT_Y = 1f;
        private const float WADDLE_TRANSITION_SPEED = 8f;

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
            _bobTimer += deltaTime;

            // Smoothly interpolate the waddle intensity towards its target
            float targetIntensity = _isMoving ? 1.0f : 0.0f;
            _waddleIntensity = MathHelper.Lerp(_waddleIntensity, targetIntensity, deltaTime * WADDLE_TRANSITION_SPEED);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            InitializeTexture(); // Ensure texture is loaded before drawing.
            if (_texture == null) return; // Don't draw if the texture is still null.

            // Pillar 2: Secondary "Waddle" Motion with intensity
            float xOffset = MathF.Cos(_bobTimer * WADDLE_SPEED) * WADDLE_AMOUNT_X * _waddleIntensity;
            float yOffset = MathF.Sin(_bobTimer * WADDLE_SPEED * 2f) * WADDLE_AMOUNT_Y * _waddleIntensity; // Multiply by 2 for a faster up/down bob
            var drawPosition = new Vector2(Position.X + xOffset, Position.Y + yOffset);

            // Pillar 3: Orientation
            SpriteEffects effects = _currentDirection.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.DrawSnapped(_texture, drawPosition, null, Color.White, 0f, _origin, 1f, effects, 0.5f);
        }
    }
}