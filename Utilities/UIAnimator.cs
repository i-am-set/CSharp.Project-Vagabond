using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A robust state machine for handling UI entry and exit animations.
    /// Abstracts the timing, delays, and math for effects like Pop, Fade, and Slide.
    /// </summary>
    public class UIAnimator
    {
        public enum AnimationState
        {
            Hidden,
            InDelay,
            AnimatingIn,
            Visible,
            OutDelay,
            AnimatingOut
        }

        /// <summary>
        /// The current visual state of the element, calculated based on the animation progress.
        /// </summary>
        public struct VisualState
        {
            public Vector2 Scale;
            public float Opacity;
            public Vector2 Offset;
            public float Rotation;
            public bool IsVisible; // True if anything should be drawn

            public static VisualState Default => new VisualState
            {
                Scale = Vector2.One,
                Opacity = 1f,
                Offset = Vector2.Zero,
                Rotation = 0f,
                IsVisible = true
            };

            public static VisualState Hidden => new VisualState
            {
                Scale = Vector2.Zero,
                Opacity = 0f,
                Offset = Vector2.Zero,
                Rotation = 0f,
                IsVisible = false
            };
        }

        // Configuration
        public EntryExitStyle Style { get; set; } = EntryExitStyle.Pop;
        public float Duration { get; set; } = 0.5f;
        public float Magnitude { get; set; } = 20f;

        // State
        private AnimationState _state = AnimationState.Hidden;
        private float _timer = 0f;
        private float _delayTimer = 0f;

        // Callbacks
        public event Action OnInComplete;
        public event Action OnOutComplete;

        public bool IsVisible => _state != AnimationState.Hidden;
        public bool IsFullyVisible => _state == AnimationState.Visible;
        public bool IsAnimating => _state == AnimationState.AnimatingIn || _state == AnimationState.AnimatingOut || _state == AnimationState.InDelay || _state == AnimationState.OutDelay;

        public UIAnimator() { }

        /// <summary>
        /// Starts the entry animation.
        /// </summary>
        /// <param name="delay">Optional delay before the animation starts.</param>
        public void Show(float delay = 0f)
        {
            if (delay > 0)
            {
                _state = AnimationState.InDelay;
                _delayTimer = delay;
            }
            else
            {
                _state = AnimationState.AnimatingIn;
                _timer = 0f;
            }
        }

        /// <summary>
        /// Starts the exit animation.
        /// </summary>
        /// <param name="delay">Optional delay before the animation starts.</param>
        public void Hide(float delay = 0f)
        {
            if (_state == AnimationState.Hidden) return;

            if (delay > 0)
            {
                _state = AnimationState.OutDelay;
                _delayTimer = delay;
            }
            else
            {
                _state = AnimationState.AnimatingOut;
                _timer = 0f;
            }
        }

        /// <summary>
        /// Instantly resets the animator to the Hidden state.
        /// </summary>
        public void Reset()
        {
            _state = AnimationState.Hidden;
            _timer = 0f;
            _delayTimer = 0f;
        }

        /// <summary>
        /// Instantly sets the animator to the Visible state.
        /// </summary>
        public void ForceVisible()
        {
            _state = AnimationState.Visible;
            _timer = 0f;
            _delayTimer = 0f;
        }

        public void Update(float deltaTime)
        {
            switch (_state)
            {
                case AnimationState.InDelay:
                    _delayTimer -= deltaTime;
                    if (_delayTimer <= 0)
                    {
                        _state = AnimationState.AnimatingIn;
                        _timer = 0f;
                    }
                    break;

                case AnimationState.AnimatingIn:
                    _timer += deltaTime;
                    if (_timer >= Duration)
                    {
                        _state = AnimationState.Visible;
                        OnInComplete?.Invoke();
                    }
                    break;

                case AnimationState.OutDelay:
                    _delayTimer -= deltaTime;
                    if (_delayTimer <= 0)
                    {
                        _state = AnimationState.AnimatingOut;
                        _timer = 0f;
                    }
                    break;

                case AnimationState.AnimatingOut:
                    _timer += deltaTime;
                    if (_timer >= Duration)
                    {
                        _state = AnimationState.Hidden;
                        OnOutComplete?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>
        /// Calculates the current visual properties for drawing.
        /// </summary>
        public VisualState GetCurrentState()
        {
            if (_state == AnimationState.Hidden || _state == AnimationState.InDelay)
            {
                return VisualState.Hidden;
            }

            if (_state == AnimationState.Visible || _state == AnimationState.OutDelay)
            {
                return VisualState.Default;
            }

            // Handle 0 duration to prevent NaN
            float progress = Duration > 0 ? Math.Clamp(_timer / Duration, 0f, 1f) : 1f;
            bool isEntering = _state == AnimationState.AnimatingIn;

            var (scale, opacity, offset, rotation) = TextUtils.CalculateEntryExitTransform(Style, progress, isEntering, Magnitude);

            return new VisualState
            {
                Scale = scale,
                Opacity = opacity,
                Offset = offset,
                Rotation = rotation,
                IsVisible = true
            };
        }
    }
}