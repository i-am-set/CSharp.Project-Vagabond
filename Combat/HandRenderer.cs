using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Responsible for rendering a single player hand in the combat scene.
    /// </summary>
    public class HandRenderer
    {
        private readonly PlayerHand _playerHand;
        private readonly SpriteManager _spriteManager;
        private readonly Random _random = new Random();

        // --- TUNING CONSTANTS ---
        private const int HAND_WIDTH = 128;
        private const int HAND_HEIGHT = 256;
        private const int IDLE_POS_Y_OFFSET = 80; // Vertical offset from the bottom of the screen
        private const int IDLE_POS_X_OFFSET = 50; // Horizontal offset from the center
        private const float ANIMATION_DURATION = 0.6f; // Duration for sliding in/out
        private const float IDLE_SWAY_SPEED_X = 0.8f;
        private const float IDLE_SWAY_SPEED_Y = 0.6f;
        private const float IDLE_SWAY_AMOUNT = 1.5f;

        private Texture2D _idleTexture;
        private Texture2D _holdTexture;

        private Vector2 _idlePosition;
        private Vector2 _offscreenPosition;

        // Animation state
        private Vector2 _currentPosition;
        private Vector2 _targetPosition;
        private Vector2 _startPosition;
        private float _animationTimer;
        private bool _isAnimating;
        private float _idleSwayTimerX;
        private float _idleSwayTimerY;

        /// <summary>
        /// The current screen bounds of the hand renderer.
        /// </summary>
        public Rectangle Bounds => new Rectangle((int)_currentPosition.X, (int)_currentPosition.Y, HAND_WIDTH, HAND_HEIGHT);

        public HandRenderer(PlayerHand playerHand)
        {
            _playerHand = playerHand;
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            CalculatePositions();

            _currentPosition = _offscreenPosition;
            _targetPosition = _offscreenPosition;
            _isAnimating = false;

            // Start timers at random points to desynchronize hand movements
            _idleSwayTimerX = (float)(_random.NextDouble() * Math.PI * 2);
            _idleSwayTimerY = (float)(_random.NextDouble() * Math.PI * 2);
        }

        public void LoadContent()
        {
            _idleTexture = _spriteManager.HandIdleSprite;
            _holdTexture = _spriteManager.HandHoldSprite;
        }

        private void CalculatePositions()
        {
            float yPos = Global.VIRTUAL_HEIGHT - HAND_HEIGHT + IDLE_POS_Y_OFFSET;
            float xPos;

            if (_playerHand.Hand == HandType.Left)
            {
                xPos = (Global.VIRTUAL_WIDTH / 2f) - HAND_WIDTH - (IDLE_POS_X_OFFSET / 2f);
            }
            else // Right Hand
            {
                xPos = (Global.VIRTUAL_WIDTH / 2f) + (IDLE_POS_X_OFFSET / 2f);
            }

            _idlePosition = new Vector2(xPos, yPos);
            _offscreenPosition = new Vector2(xPos, Global.VIRTUAL_HEIGHT);
        }

        /// <summary>
        /// Called when the combat scene is entered to trigger the intro animation.
        /// </summary>
        public void EnterScene()
        {
            _currentPosition = _offscreenPosition;
            StartAnimation(_idlePosition);
        }

        private void StartAnimation(Vector2 newTarget)
        {
            if (_targetPosition != newTarget)
            {
                _startPosition = _currentPosition;
                _targetPosition = newTarget;
                _animationTimer = 0f;
                _isAnimating = true;
            }
        }

        /// <summary>
        /// Updates the hand's animation state.
        /// </summary>
        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_isAnimating)
            {
                _animationTimer += deltaTime;
                float progress = Math.Min(1f, _animationTimer / ANIMATION_DURATION);
                float easedProgress = Easing.EaseOutCubic(progress);

                _currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

                if (progress >= 1f)
                {
                    _isAnimating = false;
                }
            }
            else
            {
                // When not doing a major animation, apply idle sway
                _idleSwayTimerX += deltaTime * IDLE_SWAY_SPEED_X;
                _idleSwayTimerY += deltaTime * IDLE_SWAY_SPEED_Y;
            }
        }

        /// <summary>
        /// Draws the hand in its current state and position.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Texture2D textureToDraw = string.IsNullOrEmpty(_playerHand.SelectedActionId)
                ? _idleTexture
                : _holdTexture;

            Vector2 finalPosition = _currentPosition;

            // Apply idle sway only when not doing a major animation and action is not selected
            if (!_isAnimating && string.IsNullOrEmpty(_playerHand.SelectedActionId))
            {
                float swayX = (float)Math.Sin(_idleSwayTimerX) * IDLE_SWAY_AMOUNT;
                float swayY = (float)Math.Cos(_idleSwayTimerY) * IDLE_SWAY_AMOUNT;
                finalPosition += new Vector2(swayX, swayY);
            }

            if (textureToDraw != null)
            {
                spriteBatch.Draw(textureToDraw, finalPosition, Color.White);
            }
        }
    }
}