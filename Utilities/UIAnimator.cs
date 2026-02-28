using Microsoft.Xna.Framework;
using ProjectVagabond.Particles;
using ProjectVagabond.Utils;
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

        // --- Tunable Speeds for Juicy Hover ---
        public float HoverSwaySpeed { get; set; } = 1.0f;
        public float HoverRotationSpeed { get; set; } = 1.0f;
        public float HoverSwayDistance { get; set; } = 3.0f; // Default distance

        // --- State ---
        private AnimationState _state = AnimationState.Hidden;
        private float _timer = 0f;
        private float _delayTimer = 0f;
        private float _totalTime = 0f; // For continuous idle animations

        // Independent timer for sway to allow speed changes without phase jumps
        private float _accumulatedSwayTime = 0f;

        // Current interpolated values for smooth transitions
        private Vector2 _currentScale = Vector2.Zero;
        private Vector2 _currentOffset = Vector2.Zero;
        private float _currentOpacity = 0f;
        private float _currentRotation = 0f;

        // Randomization for organic sway
        private static readonly Random _rng = new Random();
        private readonly float _swayOffset;

        // Callbacks
        public event Action OnInComplete;
        public event Action OnOutComplete;

        public bool IsVisible => _state != AnimationState.Hidden;
        public bool IsInteractive => _state == AnimationState.Idle || _state == AnimationState.Hovered || _state == AnimationState.Pressed;

        public UIAnimator()
        {
            // Initialize with a random offset so multiple elements don't sway in perfect unison
            _swayOffset = (float)(_rng.NextDouble() * Math.PI * 2);
        }

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
            _accumulatedSwayTime = 0f;
        }

        /// <summary>
        /// Gets the current internal offset. Used for baking positions before state changes.
        /// </summary>
        public Vector2 GetCurrentOffset() => _currentOffset;

        /// <summary>
        /// Manually forces the internal offset to a specific value.
        /// Used to reset the offset after baking it into the parent's position.
        /// </summary>
        public void ForceOffset(Vector2 offset)
        {
            _currentOffset = offset;
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
                var startTransform = Utils.AnimationUtils.CalculateEntryExitTransform(EntryStyle, 0f, true, Magnitude, _swayOffset);
                targetScale = startTransform.Scale;
                targetOpacity = startTransform.Opacity;
                targetOffset = startTransform.Offset;
                targetRotation = startTransform.Rotation;
            }
            else if (_state == AnimationState.AnimatingIn)
            {
                float progress = DurationIn > 0 ? Math.Clamp(_timer / DurationIn, 0f, 1f) : 1f;
                var transform = Utils.AnimationUtils.CalculateEntryExitTransform(EntryStyle, progress, true, Magnitude, _swayOffset);
                targetScale = transform.Scale;
                targetOpacity = transform.Opacity;
                targetOffset = transform.Offset;
                targetRotation = transform.Rotation;
            }
            else if (_state == AnimationState.AnimatingOut || _state == AnimationState.OutDelay)
            {
                float progress = DurationOut > 0 ? Math.Clamp(_timer / DurationOut, 0f, 1f) : 1f;
                var transform = Utils.AnimationUtils.CalculateEntryExitTransform(ExitStyle, progress, false, Magnitude, _swayOffset);
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

                // --- JUICY STYLE LOGIC (Always Active, Variable Intensity) ---
                if (HoverStyle == Utils.HoverAnimationType.Juicy)
                {
                    // Determine multipliers based on state
                    bool isHovered = _state == AnimationState.Hovered;
                    float speedMult = isHovered ? 1.0f : 0.5f;
                    float distMult = isHovered ? 1.0f : 0.75f;

                    // Accumulate time based on current speed multiplier to prevent phase jumps
                    _accumulatedSwayTime += deltaTime * HoverSwaySpeed * speedMult;

                    // Calculate Sway
                    float t = _accumulatedSwayTime + _swayOffset;
                    float currentDist = HoverSwayDistance * distMult;

                    // Combine two sine waves for X to make it feel less like a perfect circle
                    float swayX = (MathF.Sin(t * 1.1f) * currentDist) + (MathF.Cos(t * 0.4f) * (currentDist * 0.5f));
                    float swayY = MathF.Sin(t * 1.4f) * currentDist;

                    targetOffset.X += swayX;
                    targetOffset.Y += swayY;

                    // Apply Lift only when hovered
                    if (isHovered)
                    {
                        targetOffset.Y += HoverLift;
                    }
                }

                // --- Standard Hover Logic ---
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
            if (_state == AnimationState.AnimatingIn || _state == AnimationState.AnimatingOut || _state == AnimationState.Hidden)
            {
                _currentScale = targetScale;
                _currentOpacity = targetOpacity;
                _currentOffset = targetOffset;
                _currentRotation = targetRotation;
            }
            else
            {
                float dampingFactor = 1.0f - MathF.Exp(-InteractionSpeed * deltaTime);

                _currentScale = Vector2.Lerp(_currentScale, targetScale, dampingFactor);
                _currentOpacity = MathHelper.Lerp(_currentOpacity, targetOpacity, dampingFactor);
                _currentOffset = Vector2.Lerp(_currentOffset, targetOffset, dampingFactor);
                _currentRotation = MathHelper.Lerp(_currentRotation, targetRotation, dampingFactor);
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

    /// <summary>
    /// Provides a juicy, snappy "plink" entrance animation that can be attached to any UI element.
    /// It handles elastic scaling, a white flash, and particle debris automatically.
    /// </summary>
    public class PlinkAnimator
    {
        // --- Tunables ---
        public float MaxScale { get; set; } = 1.3f;
        public float RestScale { get; set; } = 1.0f;

        // Halved rotation variance: ~0.125 radians is approx 7 degrees
        public float MaxRotationVariance { get; set; } = 0.125f;
        public int ParticleCount { get; set; } = 12;
        public float HapticStrength { get; set; } = 0.0f;
        public float FlashMaxAlpha { get; set; } = 0.8f;
        public float PlinkTriggerThreshold { get; set; } = 0.05f;

        // --- State ---
        public bool IsActive { get; private set; }
        public float Scale { get; private set; } = 1f;
        public float Rotation { get; private set; } = 0f;
        public Color? FlashTint { get; private set; }

        private float _timer;
        private float _delay;
        private float _duration;
        private bool _hasPlinked;
        private float _startRotation;

        public void Start(float delay = 0f, float duration = 0.2f)
        {
            _delay = delay;
            _duration = duration;
            _timer = 0f;
            IsActive = true;
            _hasPlinked = false;
            Scale = 0f; // Hidden during delay
            Rotation = 0f;
            FlashTint = null;

            // Randomize starting rotation based on tunable variance
            var rng = new Random();
            _startRotation = (float)((rng.NextDouble() * 2.0 - 1.0) * MaxRotationVariance);
        }

        public void Update(GameTime gameTime, Vector2 centerPosition)
        {
            if (!IsActive) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_delay > 0)
            {
                _delay -= dt;
                Scale = 0f; // Keep hidden until delay finishes
                return;
            }

            _timer += dt;
            float progress = Math.Clamp(_timer / _duration, 0f, 1f);

            // Snappy pop: Starts huge, settles elastically to rest scale
            float ease = Easing.EaseOutBack(progress);
            Scale = MathHelper.Lerp(MaxScale, RestScale, ease);
            Rotation = MathHelper.Lerp(_startRotation, 0f, ease);

            if (!_hasPlinked && progress > PlinkTriggerThreshold)
            {
                _hasPlinked = true;

                // Spawn debris particles
                if (ParticleCount > 0)
                {
                    var psm = ServiceLocator.Get<ParticleSystemManager>();
                    var emitter = psm.CreateEmitter(ParticleEffects.CreateUIPlink());
                    emitter.Position = centerPosition;
                    emitter.EmitBurst(ParticleCount);
                }

                // Add a snappy haptic kick
                if (HapticStrength > 0)
                {
                    ServiceLocator.Get<HapticsManager>().TriggerUICompoundShake(HapticStrength);
                }
            }

            if (_hasPlinked)
            {
                float flashAlpha = 1.0f - progress;
                FlashTint = ServiceLocator.Get<Global>().Palette_Sun * (flashAlpha * FlashMaxAlpha);
            }

            if (progress >= 1.0f)
            {
                IsActive = false;
                Scale = RestScale;
                Rotation = 0f;
                FlashTint = null;
            }
        }
    }
}
