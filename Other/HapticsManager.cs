using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public enum HapticType
    {
        Shake,
        Hop,
        Pulse,
        Wobble,
        Drift,
        Bounce,
        ZoomPulse,
        DirectionalShake
    }

    public class HapticsManager
    {
        private readonly Random _random = new();
        private readonly HapticEffect _shake = new(HapticType.Shake);
        private readonly HapticEffect _hop = new(HapticType.Hop);
        private readonly HapticEffect _pulse = new(HapticType.Pulse);
        private readonly HapticEffect _wobble = new(HapticType.Wobble);
        private readonly HapticEffect _drift = new(HapticType.Drift);
        private readonly HapticEffect _bounce = new(HapticType.Bounce);
        private readonly HapticEffect _zoomPulse = new(HapticType.ZoomPulse);
        private readonly HapticEffect _directionalShake = new(HapticType.DirectionalShake);

        // --- Compound Shake State (Trauma System) ---
        private readonly SeededPerlin _perlin;
        private float _trauma = 0f;
        private float _traumaDecayRate = 1.0f; // Calculated dynamically based on duration
        private float _time = 0f;

        // --- Compound Shake Configuration ---
        /// <summary>
        /// The maximum allowed value for Trauma (0.0 to 1.0).
        /// </summary>
        public float CompoundShakeCeiling { get; set; } = 0.2f;

        /// <summary>
        /// Multiplies the intensity of every TriggerCompoundShake call.
        /// </summary>
        public float CompoundShakeMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// The exponent used to calculate shake magnitude from trauma (Shake = Trauma ^ Exponent).
        /// Higher values make low trauma feel subtle and high trauma feel explosive.
        /// </summary>
        public float CompoundShakeExponent { get; set; } = 2.0f;

        /// <summary>
        /// The minimum linear scalar for trauma.
        /// Shake = Max(Trauma^Exponent, Trauma * LinearFloor).
        /// This ensures small trauma values (like 0.1) don't vanish to 0.01, but stay around 0.05.
        /// </summary>
        public float CompoundShakeLinearFloor { get; set; } = 0.15f;

        /// <summary>
        /// The maximum pixel offset at full trauma (1.0).
        /// </summary>
        public float CompoundShakeMaxOffset { get; set; } = 20f;

        /// <summary>
        /// The maximum rotation (in radians) at full trauma (1.0).
        /// </summary>
        public float CompoundShakeMaxRotation { get; set; } = 0.3f;

        /// <summary>
        /// The frequency of the Perlin noise for the shake.
        /// </summary>
        public float CompoundShakeFrequency { get; set; } = 8f;

        private Global _global;

        public HapticsManager()
        {
            _perlin = new SeededPerlin(12345);
            StopAll();
        }

        public void TriggerShake(float magnitude, float duration, bool isDecayed = true, float frequency = 0f)
        {
            _shake.Trigger(magnitude, duration, decayed: isDecayed, frequency: frequency);
        }

        public void TriggerDirectionalShake(Vector2 direction, float intensity, float duration)
        {
            _directionalShake.Trigger(intensity, duration, direction: direction);
        }

        public void TriggerHop(float intensity, float duration)
        {
            _hop.Trigger(intensity, duration);
        }

        public void TriggerPulse(float intensity, float duration)
        {
            _pulse.Trigger(intensity, duration);
        }

        public void TriggerWobble(float intensity, float duration, float frequency = 5f)
        {
            _wobble.Trigger(intensity, duration, frequency: frequency);
        }

        public void TriggerDrift(Vector2 direction, float intensity, float duration)
        {
            _drift.Trigger(intensity, duration, direction: direction);
        }

        public void TriggerBounce(Vector2 direction, float intensity, float duration)
        {
            _bounce.Trigger(intensity, duration, direction: direction);
        }

        public void TriggerZoomPulse(float intensity, float duration)
        {
            _zoomPulse.Trigger(intensity, duration);
        }

        public void QuickZoomInPulseSmall()
        {
            TriggerZoomPulse(1.02f, 0.05f);
        }

        public void TriggerRandomHop(float intensity, float duration)
        {
            float randomIntensity = intensity * ((float)_random.NextDouble() * 0.6f + 0.4f);
            TriggerHop(randomIntensity, duration);
        }

        /// <summary>
        /// Adds trauma to the system to create a compounding shake effect.
        /// </summary>
        /// <param name="intensity">How much trauma to add (0.0 to 1.0). Small hits ~0.1, Big hits ~0.5.</param>
        /// <param name="duration">How long this specific shake should last. Adjusts the decay rate.</param>
        public void TriggerCompoundShake(float intensity, float duration)
        {
            // Apply multiplier
            float amount = intensity * CompoundShakeMultiplier;

            // Add to current trauma
            _trauma += amount;

            // Clamp to ceiling
            _trauma = Math.Clamp(_trauma, 0f, CompoundShakeCeiling);

            // Recalculate decay rate to ensure the shake lasts at least 'duration' seconds from now.
            // Decay = CurrentTrauma / Duration.
            // We take the slower of the current decay or the new required decay to prevent cutting short a big shake.
            if (duration > 0)
            {
                float requiredDecay = _trauma / duration;

                // If we are currently shaking, we want to extend the shake, not shorten it.
                // So we take the MINIMUM decay rate (which results in the LONGEST duration).
                if (_trauma > 0 && _traumaDecayRate > 0)
                {
                    _traumaDecayRate = Math.Min(_traumaDecayRate, requiredDecay);
                }
                else
                {
                    _traumaDecayRate = requiredDecay;
                }
            }
        }

        public void StopAll()
        {
            _shake.Reset();
            _hop.Reset();
            _pulse.Reset();
            _wobble.Reset();
            _drift.Reset();
            _bounce.Reset();
            _zoomPulse.Reset();
            _directionalShake.Reset();
            _trauma = 0f;
            _traumaDecayRate = 1.0f;
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            // Decay Trauma
            if (_trauma > 0)
            {
                _trauma -= _traumaDecayRate * dt;
                if (_trauma < 0) _trauma = 0;
            }

            _shake.Update(gameTime, _random);
            _hop.Update(gameTime, _random);
            _pulse.Update(gameTime, _random);
            _wobble.Update(gameTime, _random);
            _drift.Update(gameTime, _random);
            _bounce.Update(gameTime, _random);
            _zoomPulse.Update(gameTime, _random);
            _directionalShake.Update(gameTime, _random);
        }

        /// <summary>
        /// Returns the raw aggregated values for Offset, Rotation, and Scale.
        /// This allows the renderer to construct the matrix relative to the actual screen center and scale.
        /// </summary>
        public (Vector2 Offset, float Rotation, float Scale) GetTotalShakeParams()
        {
            Vector2 totalOffset = _shake.Offset + _hop.Offset + _pulse.Offset + _wobble.Offset + _drift.Offset + _bounce.Offset + _zoomPulse.Offset + _directionalShake.Offset;
            float totalRotation = _shake.Rotation + _hop.Rotation + _pulse.Rotation + _wobble.Rotation + _drift.Rotation + _bounce.Rotation + _zoomPulse.Rotation + _directionalShake.Rotation;
            float totalScale = GetCurrentScale();

            // Add Compound Shake (Trauma-based)
            if (_trauma > 0)
            {
                // Calculate shake magnitude: Max(Trauma^Exponent, Trauma * Floor)
                // This ensures small trauma values have a linear floor (visible), while high values scale exponentially.
                float exponentialShake = MathF.Pow(_trauma, CompoundShakeExponent);
                float linearShake = _trauma * CompoundShakeLinearFloor;
                float shake = Math.Max(exponentialShake, linearShake);

                // Use Perlin noise for smooth random movement
                // We use different seeds (offsets) for X, Y, and Rotation so they don't sync up
                float noiseX = _perlin.Noise(_time * CompoundShakeFrequency, 0);
                float noiseY = _perlin.Noise(0, _time * CompoundShakeFrequency);
                float noiseRot = _perlin.Noise(_time * CompoundShakeFrequency, _time * CompoundShakeFrequency);

                totalOffset.X += CompoundShakeMaxOffset * shake * noiseX;
                totalOffset.Y += CompoundShakeMaxOffset * shake * noiseY;
                totalRotation += CompoundShakeMaxRotation * shake * noiseRot;
            }

            return (totalOffset, totalRotation, totalScale);
        }

        public Matrix GetHapticsMatrix()
        {
            _global ??= ServiceLocator.Get<Global>();

            var (totalOffset, totalRotation, totalScale) = GetTotalShakeParams();

            var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

            Matrix offsetMatrix = Matrix.CreateTranslation(totalOffset.X, totalOffset.Y, 0);

            Matrix rotationMatrix = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                                     Matrix.CreateRotationZ(totalRotation) *
                                     Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);

            Matrix scaleMatrix = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                                  Matrix.CreateScale(totalScale, totalScale, 1.0f) *
                                  Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);

            // Apply effects in order: Scale, then Rotate, then Translate.
            return scaleMatrix * rotationMatrix * offsetMatrix;
        }

        public bool IsAnyHapticActive()
        {
            return _shake.Active || _hop.Active || _pulse.Active || _wobble.Active || _drift.Active || _bounce.Active || _zoomPulse.Active || _directionalShake.Active || _trauma > 0;
        }

        public Vector2 GetCurrentOffset()
        {
            return _shake.Offset + _hop.Offset + _pulse.Offset + _wobble.Offset + _drift.Offset + _bounce.Offset + _zoomPulse.Offset + _directionalShake.Offset;
        }

        public float GetCurrentScale()
        {
            // If multiple effects modify scale in the future, they should be multiplied together.
            return _zoomPulse.Scale;
        }

        private class HapticEffect
        {
            private readonly HapticType _type;
            private float _timer, _duration, _intensity, _frequency;
            private Vector2 _direction;
            private bool _decayed, _active;
            private Vector2 _offset;
            private float _rotation;
            private float _scale;
            private float _initialIntensity; // Store the starting intensity for stable decay

            public HapticEffect(HapticType type)
            {
                _type = type;
                Reset();
            }

            public bool Active => _active;
            public Vector2 Offset => _offset;
            public float Rotation => _rotation;
            public float Scale => _scale;

            public void Trigger(float intensity, float duration, bool decayed = true, float frequency = 0f, Vector2 direction = default)
            {
                _intensity = intensity;
                _initialIntensity = intensity; // Store the starting intensity
                _duration = duration;
                _timer = duration;
                _decayed = decayed;
                _frequency = frequency;
                _direction = direction != Vector2.Zero ? Vector2.Normalize(direction) : Vector2.Zero;
                _active = true;
            }

            public void Reset()
            {
                _timer = 0f;
                _duration = 0f;
                _intensity = 0f;
                _initialIntensity = 0f;
                _frequency = 0f;
                _direction = Vector2.Zero;
                _offset = Vector2.Zero;
                _rotation = 0f;
                _scale = 1.0f;
                _active = false;
                _decayed = true;
            }

            public void Update(GameTime gameTime, Random random)
            {
                if (!_active) return;

                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = _duration > 0 ? 1.0f - (_timer / _duration) : 1.0f;

                switch (_type)
                {
                    case HapticType.Shake:
                        if (_timer > 0)
                        {
                            float currentMagnitude = _intensity;
                            if (_decayed)
                            {
                                currentMagnitude = _initialIntensity * (1.0f - Easing.EaseOutQuad(progress));
                            }

                            if (_frequency > 0)
                            {
                                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                                float offsetX = (float)Math.Sin(time * _frequency) * currentMagnitude;
                                float offsetY = (float)Math.Cos(time * _frequency * 1.2f) * currentMagnitude;
                                _offset = new Vector2(offsetX, offsetY);
                            }
                            else
                            {
                                float offsetX = (float)(random.NextDouble() * 2 - 1) * currentMagnitude;
                                float offsetY = (float)(random.NextDouble() * 2 - 1) * currentMagnitude;
                                _offset = new Vector2(offsetX, offsetY);
                            }
                            _rotation = 0f;
                        }
                        break;
                    case HapticType.DirectionalShake:
                        if (_timer > 0)
                        {
                            // A violent shake along a specific vector.
                            // Uses a high-frequency sine wave to oscillate back and forth along the direction vector.
                            float decay = 1.0f - Easing.EaseOutQuad(progress);
                            float oscillation = (float)Math.Sin(_timer * 60f); // High frequency shake
                            _offset = _direction * oscillation * _intensity * decay;
                        }
                        break;
                    case HapticType.Hop:
                        if (_timer > 0)
                        {
                            float easeOut = 1.0f - (float)Math.Pow(1.0f - progress, 3);
                            _offset = new Vector2(0, _intensity * (1.0f - easeOut));
                        }
                        break;
                    case HapticType.Pulse:
                        if (_timer > 0)
                        {
                            float pulseValue = (float)Math.Sin(progress * Math.PI) * _intensity;
                            float angle = (float)random.NextDouble() * MathHelper.TwoPi;
                            _offset = new Vector2(
                                (float)Math.Cos(angle) * pulseValue,
                                (float)Math.Sin(angle) * pulseValue
                            );
                        }
                        break;
                    case HapticType.Wobble:
                        if (_timer > 0)
                        {
                            float time = (float)gameTime.TotalGameTime.TotalSeconds;
                            float decayedIntensity = _intensity * (1.0f - progress);
                            _offset = new Vector2(
                                (float)Math.Sin(time * _frequency * MathHelper.TwoPi) * decayedIntensity,
                                (float)Math.Cos(time * _frequency * MathHelper.TwoPi * 0.7f) * decayedIntensity * 0.5f
                            );
                        }
                        break;
                    case HapticType.Drift:
                        if (_timer > 0)
                        {
                            float smoothProgress = progress < 0.5f
                                ? 2.0f * progress * progress
                                : 1.0f - 2.0f * (1.0f - progress) * (1.0f - progress);
                            float driftAmount = progress < 0.5f
                                ? smoothProgress * _intensity
                                : _intensity * (1.0f - smoothProgress);
                            _offset = _direction * driftAmount;
                        }
                        break;
                    case HapticType.Bounce:
                        if (_timer > 0)
                        {
                            float bounceValue = (float)(Math.Sin(progress * Math.PI * 6) * Math.Exp(-progress * 4)) * _intensity;
                            _offset = _direction * bounceValue;
                        }
                        break;
                    case HapticType.ZoomPulse:
                        if (_timer > 0)
                        {
                            // A pulse that goes from 1.0 -> intensity -> 1.0 using a sine wave.
                            float pulseValue = (float)Math.Sin(progress * Math.PI);
                            _scale = 1.0f + (_intensity - 1.0f) * pulseValue;
                        }
                        break;
                }

                _timer -= deltaTime;
                if (_timer <= 0)
                {
                    Reset();
                }
            }
        }
    }
}