using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// A robust state machine for handling UI entry, exit, and interaction animations.
    /// Abstracts the timing, delays, and math for effects like Pop, Fade, Slide, Hover, and Click.
    /// </summary>
    public class UIAnimator
    {
        public enum AnimationState
        {
            Hidden,
            InDelay,
            AnimatingIn,
            Idle,
            Hovered,
            Pressed,
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

        // --- Configuration ---
        public Utils.EntryExitStyle EntryStyle { get; set; } = Utils.EntryExitStyle.Pop;
        public Utils.EntryExitStyle ExitStyle { get; set; } = Utils.EntryExitStyle.Pop;
        public Utils.IdleAnimationType IdleStyle { get; set; } = Utils.IdleAnimationType.None;
        public Utils.HoverAnimationType HoverStyle { get; set; } = Utils.HoverAnimationType.Lift;

        public float DurationIn { get; set; } = 0.5f;
        public float DurationOut { get; set; } = 0.3f;
        public float Magnitude { get; set; } = 20f; // For slides

        // Interaction Tuning
        public float HoverScale { get; set; } = 1.05f;
        public float HoverLift { get; set; } = -4f;
        public float PressScale { get; set; } = 0.95f;
        public float InteractionSpeed { get; set; } = 15f; // Lerp speed for hover/press

        // --- State ---
        private AnimationState _state = AnimationState.Hidden;
        private float _timer = 0f;
        private float _delayTimer = 0f;
        private float _totalTime = 0f; // For continuous idle animations

        // Current interpolated values for smooth transitions
        private Vector2 _currentScale = Vector2.Zero;
        private Vector2 _currentOffset = Vector2.Zero;
        private float _currentOpacity = 0f;
        private float _currentRotation = 0f;

        // Callbacks
        public event Action OnInComplete;
        public event Action OnOutComplete;

        public bool IsVisible => _state != AnimationState.Hidden;
        public bool IsInteractive => _state == AnimationState.Idle || _state == AnimationState.Hovered || _state == AnimationState.Pressed;

        public UIAnimator() { }

        /// <summary>
        /// Starts the entry animation.
        /// </summary>
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
        public void Hide(float delay = 0f, Utils.EntryExitStyle? overrideStyle = null)
        {
            if (_state == AnimationState.Hidden) return;

            if (overrideStyle.HasValue) ExitStyle = overrideStyle.Value;

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

        public void SetHover(bool isHovered)
        {
            if (!IsInteractive) return;
            if (_state == AnimationState.Pressed) return; // Press overrides hover

            if (isHovered && _state != AnimationState.Hovered)
            {
                _state = AnimationState.Hovered;
            }
            else if (!isHovered && _state == AnimationState.Hovered)
            {
                _state = AnimationState.Idle;
            }
        }

        public void SetPress(bool isPressed)
        {
            if (!IsInteractive) return;

            if (isPressed)
            {
                _state = AnimationState.Pressed;
            }
            else if (_state == AnimationState.Pressed)
            {
                // Return to hover if released, or idle? Usually hover if mouse is still over.
                // For simplicity, we assume external logic handles the "is still hovered" check
                // and calls SetHover immediately after SetPress(false).
                _state = AnimationState.Idle;
            }
        }

        public void Reset()
        {
            _state = AnimationState.Hidden;
            _timer = 0f;
            _delayTimer = 0f;
            _currentScale = Vector2.Zero;
            _currentOpacity = 0f;
        }

        public void Update(float deltaTime)
        {
            _totalTime += deltaTime;

            // 1. Handle State Transitions & Timers
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
                    if (_timer >= DurationIn)
                    {
                        _state = AnimationState.Idle;
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
                    if (_timer >= DurationOut)
                    {
                        _state = AnimationState.Hidden;
                        OnOutComplete?.Invoke();
                    }
                    break;
            }

            // 2. Calculate Target Values based on State
            Vector2 targetScale = Vector2.One;
            Vector2 targetOffset = Vector2.Zero;
            float targetOpacity = 1f;
            float targetRotation = 0f;

            if (_state == AnimationState.Hidden || _state == AnimationState.InDelay)
            {
                // Use the "Start" state of the Entry animation as the hidden state
                var startTransform = Utils.AnimationUtils.CalculateEntryExitTransform(EntryStyle, 0f, true, Magnitude);
                targetScale = startTransform.Scale;
                targetOpacity = startTransform.Opacity;
                targetOffset = startTransform.Offset;
                targetRotation = startTransform.Rotation;
            }
            else if (_state == AnimationState.AnimatingIn)
            {
                float progress = DurationIn > 0 ? Math.Clamp(_timer / DurationIn, 0f, 1f) : 1f;
                var transform = Utils.AnimationUtils.CalculateEntryExitTransform(EntryStyle, progress, true, Magnitude);
                targetScale = transform.Scale;
                targetOpacity = transform.Opacity;
                targetOffset = transform.Offset;
                targetRotation = transform.Rotation;
            }
            else if (_state == AnimationState.AnimatingOut || _state == AnimationState.OutDelay)
            {
                float progress = DurationOut > 0 ? Math.Clamp(_timer / DurationOut, 0f, 1f) : 1f;
                var transform = Utils.AnimationUtils.CalculateEntryExitTransform(ExitStyle, progress, false, Magnitude);
                targetScale = transform.Scale;
                targetOpacity = transform.Opacity;
                targetOffset = transform.Offset;
                targetRotation = transform.Rotation;
            }
            else // Interactive States (Idle, Hover, Pressed)
            {
                // Base Idle Animation
                Vector2 idleOffset = Utils.AnimationUtils.CalculateIdleOffset(IdleStyle, _totalTime);
                Vector2 idleScale = Utils.AnimationUtils.CalculateIdleScale(IdleStyle, _totalTime);

                targetOffset = idleOffset;
                targetScale = idleScale;

                if (_state == AnimationState.Hovered)
                {
                    if (HoverStyle == Utils.HoverAnimationType.Lift) targetOffset.Y += HoverLift;
                    if (HoverStyle == Utils.HoverAnimationType.ScaleUp) targetScale *= HoverScale;
                    if (HoverStyle == Utils.HoverAnimationType.Wiggle) targetRotation = MathF.Sin(_totalTime * 10f) * 0.1f;
                }
                else if (_state == AnimationState.Pressed)
                {
                    targetScale *= PressScale;
                    targetOffset.Y += 2f; // Slight press down
                }
            }

            // 3. Interpolate Current Values towards Targets
            // For Entry/Exit, we snap directly to the calculated curve value to ensure the animation plays exactly as designed.
            // For Interaction, we lerp to smooth out state changes (e.g. Hover -> Idle).
            if (_state == AnimationState.AnimatingIn || _state == AnimationState.AnimatingOut || _state == AnimationState.Hidden)
            {
                _currentScale = targetScale;
                _currentOpacity = targetOpacity;
                _currentOffset = targetOffset;
                _currentRotation = targetRotation;
            }
            else
            {
                float lerpT = Math.Clamp(deltaTime * InteractionSpeed, 0f, 1f);
                _currentScale = Vector2.Lerp(_currentScale, targetScale, lerpT);
                _currentOpacity = MathHelper.Lerp(_currentOpacity, targetOpacity, lerpT);
                _currentOffset = Vector2.Lerp(_currentOffset, targetOffset, lerpT);
                _currentRotation = MathHelper.Lerp(_currentRotation, targetRotation, lerpT);
            }
        }

        public VisualState GetVisualState()
        {
            return new VisualState
            {
                Scale = _currentScale,
                Opacity = _currentOpacity,
                Offset = _currentOffset,
                Rotation = _currentRotation,
                IsVisible = _currentOpacity > 0.01f
            };
        }
    }
}