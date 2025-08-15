using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProjectVagabond.Combat.UI
{
    /// <summary>
    /// Represents a single, animatable card in the action menu.
    /// It encapsulates its own state and animation logic.
    /// </summary>
    public class CombatCard
    {
        public ActionData Action { get; }
        public RectangleF CurrentBounds { get; private set; }
        public Color CurrentTint { get; private set; }
        public float CurrentScale { get; private set; }
        public float CurrentRotation { get; set; }
        public float CurrentAlpha { get; private set; }
        public float ShadowAlpha { get; private set; }
        public Vector2 ShadowOffset { get; private set; }
        public bool IsBeingDragged { get; set; }
        public bool IsTemporary { get; set; }

        // Animation targets
        private Vector2 _targetPosition;
        private float _targetScale;
        private Color _targetTint;
        private float _targetRotation;
        private float _targetAlpha;
        private float _targetShadowAlpha;

        // Animation state
        private Vector2 _startPosition;
        private float _startScale;
        private Color _startTint;
        private float _startRotation;
        private float _startAlpha;
        private float _startShadowAlpha;
        private float _animationTimer;
        private bool _isAnimating;
        private bool _isPlayAnimating = false;
        public bool IsAnimationFinished => !_isAnimating && !_isPlayAnimating;


        // Juice animations
        private OrganicSwayAnimation _dragSway;
        private float _rippleTimer;
        private Vector2 _rippleDirection;
        private const float RIPPLE_DURATION = 0.4f;
        private const float RIPPLE_DISTANCE = 15f;


        private const float ANIMATION_DURATION = 0.3f;
        private const float PLAY_ANIMATION_DURATION = 0.075f;
        private static readonly Vector2 DRAGGED_SHADOW_OFFSET = new Vector2(0, 10);

        // Drag-specific animation
        private Vector2 _lastVelocity;
        private const float DRAG_TILT_FACTOR = 0.08f; // How much to tilt based on velocity.X
        private const float MAX_DRAG_TILT_RADIANS = 0.6f; // Max tilt in either direction
        private const float DRAG_TILT_LERP_SPEED = 15f; // How quickly the tilt catches up
        private bool _isDragInPlayArea = true;
        private const float DRAG_SCALE_IN_PLAY_AREA = 0.65f; // Scale of the card when inside the targeting area.
        private const float DRAG_SCALE_OUTSIDE_PLAY_AREA = 1.0f; // Scale of the card when outside the targeting area.
        private const float DRAG_SCALE_LERP_SPEED = 10f;

        public CombatCard(ActionData action)
        {
            Action = action;
            CurrentScale = 1f;
            CurrentTint = Color.White;
            CurrentRotation = 0f;
            CurrentAlpha = 1f;
            ShadowAlpha = 0f;
            ShadowOffset = Vector2.Zero;
            IsTemporary = false;
        }

        /// <summary>
        /// Sets the initial state of the card without animation.
        /// </summary>
        public void SetInitialState(Vector2 position, float scale, Color tint, float rotation, float alpha)
        {
            // Set a bogus target to ensure the first AnimateTo call triggers an animation.
            _targetPosition = position + Vector2.One;

            CurrentBounds = new RectangleF(position.X, position.Y, ActionHandUI.CARD_SIZE.X * scale, ActionHandUI.CARD_SIZE.Y * scale);
            CurrentScale = scale;
            CurrentTint = tint;
            CurrentRotation = rotation;
            CurrentAlpha = alpha;
            ShadowAlpha = 0f;
            ShadowOffset = Vector2.Zero;
        }

        /// <summary>
        /// Starts an animation to a new target state.
        /// </summary>
        public void AnimateTo(Vector2 position, float scale, Color tint, float rotation, float alpha, float shadowAlpha)
        {
            if (_targetPosition == position && Math.Abs(_targetScale - scale) < 0.01f && _targetTint == tint && Math.Abs(_targetRotation - rotation) < 0.01f && Math.Abs(_targetAlpha - alpha) < 0.01f && Math.Abs(_targetShadowAlpha - shadowAlpha) < 0.01f)
            {
                return; // Already at/animating to the target state
            }

            _startPosition = new Vector2(CurrentBounds.X, CurrentBounds.Y);
            _startScale = CurrentScale;
            _startTint = CurrentTint;
            _startRotation = CurrentRotation;
            _startAlpha = CurrentAlpha;
            _startShadowAlpha = ShadowAlpha;

            _targetPosition = position;
            _targetScale = scale;
            _targetTint = tint;
            _targetRotation = rotation;
            _targetAlpha = alpha;
            _targetShadowAlpha = shadowAlpha;

            _animationTimer = 0f;
            _isAnimating = true;
            _isPlayAnimating = false;
        }

        /// <summary>
        /// Starts the animation for when a card is successfully played on a hand.
        /// </summary>
        /// <param name="targetPosition">The center of the hand to animate towards.</param>
        public void AnimatePlay(Vector2 targetPosition)
        {
            _startPosition = CurrentBounds.Position;
            _startScale = CurrentScale;
            _startAlpha = CurrentAlpha;

            _targetPosition = targetPosition - (ActionHandUI.CARD_SIZE.ToVector2() * 0.1f / 2f); // Center on target
            _targetScale = 0.1f;
            _targetAlpha = 0f;

            _animationTimer = 0f;
            _isAnimating = false;
            _isPlayAnimating = true;
        }


        /// <summary>
        /// Instantly moves the card to a new center position, bypassing the animation system. Used for dragging.
        /// </summary>
        public void ForcePosition(Vector2 centerPosition)
        {
            var swayOffset = _dragSway?.Offset ?? Vector2.Zero;
            var finalCenterPosition = centerPosition + swayOffset;

            var size = ActionHandUI.CARD_SIZE.ToVector2() * CurrentScale;
            var topLeftPosition = finalCenterPosition - (size / 2f);

            CurrentBounds = new RectangleF(topLeftPosition.X, topLeftPosition.Y, size.X, size.Y);
        }

        /// <summary>
        /// Called by the input handler to provide the card with its current velocity.
        /// </summary>
        public void SetDragVelocity(Vector2 velocity)
        {
            _lastVelocity = velocity;
        }

        /// <summary>
        /// Informs the card whether it is currently being dragged inside the valid play area.
        /// </summary>
        public void SetDragPlayAreaStatus(bool isInPlayArea)
        {
            _isDragInPlayArea = isInPlayArea;
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _dragSway?.Update(gameTime);

            if (IsBeingDragged)
            {
                // When dragged, the shadow is always visible and offset
                ShadowOffset = DRAGGED_SHADOW_OFFSET;

                // --- Drag tilt logic ---
                float targetTilt = _lastVelocity.X * DRAG_TILT_FACTOR;
                targetTilt = MathHelper.Clamp(targetTilt, -MAX_DRAG_TILT_RADIANS, MAX_DRAG_TILT_RADIANS);
                CurrentRotation = MathHelper.Lerp(CurrentRotation, targetTilt, DRAG_TILT_LERP_SPEED * deltaTime);

                // --- Drag scale logic ---
                float targetScale = _isDragInPlayArea ? DRAG_SCALE_IN_PLAY_AREA : DRAG_SCALE_OUTSIDE_PLAY_AREA;
                CurrentScale = MathHelper.Lerp(CurrentScale, targetScale, DRAG_SCALE_LERP_SPEED * deltaTime);
            }

            if (_isPlayAnimating)
            {
                UpdatePlayAnimation(deltaTime);
                return;
            }

            if (!_isAnimating)
            {
                UpdateRipple(deltaTime);
                return;
            }

            _animationTimer += deltaTime;
            float progress = Math.Min(1f, _animationTimer / ANIMATION_DURATION);

            // Use EaseOutBack for the scale to create an overshoot "pop" effect
            float scaleProgress = Easing.EaseOutBack(progress);
            CurrentScale = MathHelper.Lerp(_startScale, _targetScale, scaleProgress);

            float easedProgress = Easing.EaseOutCubic(progress);
            CurrentTint = Color.Lerp(_startTint, _targetTint, easedProgress);

            if (!IsBeingDragged)
            {
                CurrentRotation = MathHelper.Lerp(_startRotation, _targetRotation, easedProgress);
            }

            CurrentAlpha = MathHelper.Lerp(_startAlpha, _targetAlpha, easedProgress);
            ShadowAlpha = MathHelper.Lerp(_startShadowAlpha, _targetShadowAlpha, easedProgress);

            // Only allow the animation system to control position if the card is NOT being dragged.
            if (!IsBeingDragged)
            {
                var currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);
                UpdateRipple(deltaTime);
                currentPosition += GetRippleOffset();

                var size = new Vector2(ActionHandUI.CARD_SIZE.X * CurrentScale, ActionHandUI.CARD_SIZE.Y * CurrentScale);
                CurrentBounds = new RectangleF(currentPosition.X, currentPosition.Y, size.X, size.Y);
            }

            // When not dragged, the shadow has no offset
            if (!IsBeingDragged)
            {
                ShadowOffset = Vector2.Lerp(DRAGGED_SHADOW_OFFSET, Vector2.Zero, easedProgress);
            }

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }

        private void UpdatePlayAnimation(float deltaTime)
        {
            _animationTimer += deltaTime;
            float progress = Math.Min(1f, _animationTimer / PLAY_ANIMATION_DURATION);
            float easedProgress = Easing.EaseInCubic(progress);

            CurrentScale = MathHelper.Lerp(_startScale, _targetScale, easedProgress);
            CurrentAlpha = MathHelper.Lerp(_startAlpha, _targetAlpha, easedProgress);
            var currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

            var size = new Vector2(ActionHandUI.CARD_SIZE.X * CurrentScale, ActionHandUI.CARD_SIZE.Y * CurrentScale);
            CurrentBounds = new RectangleF(currentPosition.X, currentPosition.Y, size.X, size.Y);

            if (progress >= 1f)
            {
                _isPlayAnimating = false;
            }
        }

        public void StartDragSway()
        {
            const float SWAY_SPEED = 4f;
            const float SWAY_AMOUNT = 1f;
            _dragSway = new OrganicSwayAnimation(SWAY_SPEED, SWAY_SPEED * 1.2f, SWAY_AMOUNT, SWAY_AMOUNT);
            // Set initial drag properties here instead of using AnimateTo
            ShadowAlpha = 0.5f;
            _isDragInPlayArea = true; // Assume it starts in a valid area
        }

        public void StopDragSway()
        {
            _dragSway = null;
        }

        public void TriggerRipple(Vector2 direction)
        {
            _rippleDirection = direction;
            _rippleTimer = RIPPLE_DURATION;
        }

        private void UpdateRipple(float deltaTime)
        {
            if (_rippleTimer > 0)
            {
                _rippleTimer -= deltaTime;
            }
        }

        private Vector2 GetRippleOffset()
        {
            if (_rippleTimer <= 0) return Vector2.Zero;

            float progress = 1f - (_rippleTimer / RIPPLE_DURATION);
            // A sine wave creates a smooth out-and-back motion.
            float wave = (float)Math.Sin(progress * Math.PI);
            return _rippleDirection * wave * RIPPLE_DISTANCE;
        }
    }
}