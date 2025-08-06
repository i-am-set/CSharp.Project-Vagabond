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

        // Animation targets
        private Vector2 _targetPosition;
        private float _targetScale;
        private Color _targetTint;
        private float _targetRotation;
        private float _targetAlpha;

        // Animation state
        private Vector2 _startPosition;
        private float _startScale;
        private Color _startTint;
        private float _startRotation;
        private float _startAlpha;
        private float _animationTimer;
        private bool _isAnimating;

        private const float ANIMATION_DURATION = 0.3f;

        public CombatCard(ActionData action)
        {
            Action = action;
            CurrentScale = 1f;
            CurrentTint = Color.White;
            CurrentRotation = 0f;
            CurrentAlpha = 1f;
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

            CurrentBounds = new RectangleF(position.X, position.Y, ActionMenu.CARD_SIZE.X * scale, ActionMenu.CARD_SIZE.Y * scale);
            CurrentScale = scale;
            CurrentTint = tint;
            CurrentRotation = rotation;
            CurrentAlpha = alpha;
        }

        /// <summary>
        /// Starts an animation to a new target state.
        /// </summary>
        public void AnimateTo(Vector2 position, float scale, Color tint, float rotation, float alpha)
        {
            if (_targetPosition == position && _targetScale == scale && _targetTint == tint && _targetRotation == rotation && Math.Abs(CurrentAlpha - alpha) < 0.01f)
            {
                return; // Already at/animating to the target state
            }

            _startPosition = new Vector2(CurrentBounds.X, CurrentBounds.Y);
            _startScale = CurrentScale;
            _startTint = CurrentTint;
            _startRotation = CurrentRotation;
            _startAlpha = CurrentAlpha;

            _targetPosition = position;
            _targetScale = scale;
            _targetTint = tint;
            _targetRotation = rotation;
            _targetAlpha = alpha;

            _animationTimer = 0f;
            _isAnimating = true;
        }

        public void Update(GameTime gameTime)
        {
            if (!_isAnimating) return;

            _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            float progress = Math.Min(1f, _animationTimer / ANIMATION_DURATION);
            float easedProgress = Easing.EaseOutCubic(progress);

            CurrentScale = MathHelper.Lerp(_startScale, _targetScale, easedProgress);
            CurrentTint = Color.Lerp(_startTint, _targetTint, easedProgress);
            CurrentRotation = MathHelper.Lerp(_startRotation, _targetRotation, easedProgress);
            CurrentAlpha = MathHelper.Lerp(_startAlpha, _targetAlpha, easedProgress);
            var currentPosition = Vector2.Lerp(_startPosition, _targetPosition, easedProgress);

            var size = new Vector2(ActionMenu.CARD_SIZE.X * CurrentScale, ActionMenu.CARD_SIZE.Y * CurrentScale);
            CurrentBounds = new RectangleF(currentPosition.X, currentPosition.Y, size.X, size.Y);

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }
    }
}