using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Responsible for rendering a single player hand in the combat scene.
    /// </summary>
    public class HandRenderer
    {
        private readonly PlayerHand _playerHand;
        private readonly SpriteManager _spriteManager;

        // --- TUNING CONSTANTS ---
        private const int HAND_WIDTH = 128;
        private const int HAND_HEIGHT = 256;
        private const int IDLE_POS_Y_OFFSET = 10; // Vertical offset from the bottom of the screen
        private const int IDLE_POS_X_OFFSET = 180; // Horizontal offset from the center
        private const float SLIDE_ANIMATION_DURATION = 0.6f; // Duration for sliding in/out
        private const float FOCUS_ANIMATION_DURATION = 0.5f; // Duration for focus movement
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
        private float _currentAnimationDuration;
        private bool _isAnimating;
        public OrganicSwayAnimation SwayAnimation { get; }

        /// <summary>
        /// When true, the hand will render a highlight to indicate it's a valid drop target.
        /// </summary>
        public bool IsPotentialDropTarget { get; set; }

        /// <summary>
        /// The current screen bounds of the hand renderer.
        /// </summary>
        public Rectangle Bounds => new Rectangle((int)_currentPosition.X, (int)_currentPosition.Y, HAND_WIDTH, HAND_HEIGHT);

        public HandRenderer(PlayerHand playerHand)
        {
            _playerHand = playerHand;
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            RecalculateLayout();

            _currentPosition = _offscreenPosition;
            _targetPosition = _offscreenPosition;
            _isAnimating = false;
            _currentAnimationDuration = SLIDE_ANIMATION_DURATION;

            SwayAnimation = new OrganicSwayAnimation(IDLE_SWAY_SPEED_X, IDLE_SWAY_SPEED_Y, IDLE_SWAY_AMOUNT, IDLE_SWAY_AMOUNT);
        }

        public void LoadContent()
        {
            var core = ServiceLocator.Get<Core>();
            var textureFactory = ServiceLocator.Get<TextureFactory>();
            string spritePath = "";

            try
            {
                if (_playerHand.Hand == HandType.Left)
                {
                    spritePath = "Sprites/Hands/cat_hand_left_1";
                    _idleTexture = core.Content.Load<Texture2D>(spritePath);
                }
                else // Right Hand
                {
                    spritePath = "Sprites/Hands/cat_hand_right_1";
                    _idleTexture = core.Content.Load<Texture2D>(spritePath);
                }
                // For now, the "hold" state uses the same sprite as the idle state.
                _holdTexture = _idleTexture;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Could not load hand texture from '{spritePath}': {ex.Message}");
                _idleTexture = textureFactory.CreateColoredTexture(HAND_WIDTH, HAND_HEIGHT, Color.DarkGray);
                _holdTexture = textureFactory.CreateColoredTexture(HAND_WIDTH, HAND_HEIGHT, Color.CornflowerBlue);
            }
        }

        /// <summary>
        /// Recalculates the hand's screen position based on the current window size.
        /// This should be called whenever the resolution changes.
        /// </summary>
        public void RecalculateLayout()
        {
            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Anchor to the bottom of the actual screen's virtual bounds
            float yPos = actualScreenVirtualBounds.Bottom - HAND_HEIGHT + IDLE_POS_Y_OFFSET;
            float xPos;

            // The center point of the visible screen area
            float screenCenterX = actualScreenVirtualBounds.X + (actualScreenVirtualBounds.Width / 2f);

            // This shift compensates for the visual center of the hand sprites not being the geometric center of their texture.
            float centeringShift = HAND_WIDTH / 4f;

            if (_playerHand.Hand == HandType.Left)
            {
                // Position the left hand to the left of the center point
                xPos = screenCenterX - HAND_WIDTH - (IDLE_POS_X_OFFSET / 2f) - centeringShift;
            }
            else // Right Hand
            {
                // Position the right hand to the right of the center point
                xPos = screenCenterX + (IDLE_POS_X_OFFSET / 2f) - centeringShift;
            }

            _idlePosition = new Vector2(xPos, yPos);
            // Offscreen position should also be relative to the actual screen's virtual bounds
            _offscreenPosition = new Vector2(xPos, actualScreenVirtualBounds.Bottom);
        }

        /// <summary>
        /// Called when the combat scene is entered to trigger the intro animation.
        /// </summary>
        public void EnterScene()
        {
            _currentPosition = _offscreenPosition;
            StartAnimation(_idlePosition, SLIDE_ANIMATION_DURATION);
        }

        private void StartAnimation(Vector2 newTarget, float duration)
        {
            if (_targetPosition != newTarget)
            {
                _startPosition = _currentPosition;
                _targetPosition = newTarget;
                _animationTimer = 0f;
                _isAnimating = true;
                _currentAnimationDuration = duration;
            }
        }

        /// <summary>
        /// Updates the hand's animation state.
        /// </summary>
        public void Update(GameTime gameTime, CombatManager combatManager, CombatInputHandler inputHandler)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // During selection, hands are always at their idle position. Focus is now on the card hand.
            StartAnimation(_idlePosition, FOCUS_ANIMATION_DURATION);

            if (_isAnimating)
            {
                _animationTimer += deltaTime;
                float progress = Math.Min(1f, _animationTimer / _currentAnimationDuration);
                float easedProgress = Easing.EaseOutCubic(progress);

                _currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

                if (progress >= 1f)
                {
                    _isAnimating = false;
                }
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
                finalPosition += SwayAnimation.Offset;
            }

            if (textureToDraw != null)
            {
                Color tint = IsPotentialDropTarget ? Color.Lerp(Color.White, Color.Yellow, 0.5f) : Color.White;
                spriteBatch.Draw(textureToDraw, finalPosition, tint);
            }
        }
    }
}
