using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;

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
        public float CurrentRotation { get; private set; }
        public float CurrentAlpha { get; private set; }
        public float ShadowAlpha { get; private set; }
        public Vector2 ShadowOffset { get; private set; }
        public bool IsBeingDragged { get; set; }

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

        private const float ANIMATION_DURATION = 0.3f;
        private static readonly Vector2 DRAGGED_SHADOW_OFFSET = new Vector2(0, 10);

        public CombatCard(ActionData action)
        {
            Action = action;
            CurrentScale = 1f;
            CurrentTint = Color.White;
            CurrentRotation = 0f;
            CurrentAlpha = 1f;
            ShadowAlpha = 0f;
            ShadowOffset = Vector2.Zero;
        }

        /// <summary>
        /// Sets the initial state of the card without animation.
        /// </summary>
        public void SetInitialState(Vector2 position, float scale, Color tint, float rotation, float alpha)
        {
            _targetPosition = position;
            _targetScale = scale;
            _targetTint = tint;
            _targetRotation = rotation;
            _targetAlpha = alpha;
            _targetShadowAlpha = 0f;

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
        }

        /// <summary>
        /// Instantly moves the card to a new position, bypassing the animation system. Used for dragging.
        /// </summary>
        public void ForcePosition(Vector2 position)
        {
            var size = new Vector2(ActionHandUI.CARD_SIZE.X * CurrentScale, ActionHandUI.CARD_SIZE.Y * CurrentScale);
            CurrentBounds = new RectangleF(position.X, position.Y, size.X, size.Y);
        }

        public void Update(GameTime gameTime)
        {
            if (IsBeingDragged)
            {
                // When dragged, the shadow is always visible and offset
                ShadowOffset = DRAGGED_SHADOW_OFFSET;
                return;
            }

            if (!_isAnimating) return;

            _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            float progress = Math.Min(1f, _animationTimer / ANIMATION_DURATION);
            float easedProgress = Easing.EaseOutCubic(progress);

            CurrentScale = MathHelper.Lerp(_startScale, _targetScale, easedProgress);
            CurrentTint = Color.Lerp(_startTint, _targetTint, easedProgress);
            CurrentRotation = MathHelper.Lerp(_startRotation, _targetRotation, easedProgress);
            CurrentAlpha = MathHelper.Lerp(_startAlpha, _targetAlpha, easedProgress);
            ShadowAlpha = MathHelper.Lerp(_startShadowAlpha, _targetShadowAlpha, easedProgress);
            var currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

            var size = new Vector2(ActionHandUI.CARD_SIZE.X * CurrentScale, ActionHandUI.CARD_SIZE.Y * CurrentScale);
            CurrentBounds = new RectangleF(currentPosition.X, currentPosition.Y, size.X, size.Y);

            // When not dragged, the shadow has no offset
            ShadowOffset = Vector2.Lerp(DRAGGED_SHADOW_OFFSET, Vector2.Zero, easedProgress);

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }
    }
}