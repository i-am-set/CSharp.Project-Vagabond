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

        // Legacy/Specific effects
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
        private float _time = 0f;

        // --- Compound Shake Configuration ---

        // 1. The maximum limits of the shake at 100% Trauma
        public float MaxTranslation { get; set; } = 1.0f; // Pixels (Increased for juice)
        public float MaxRotation { get; set; } = 0.025f;  // Radians (~3 degrees)

        // 2. The "Feel" of the shake
        public float TraumaExponent { get; set; } = 2.0f; // 2.0 = Quadratic (Smooth), 3.0 = Cubic (Snappy)
        public float Frequency { get; set; } = 25f;       // How fast it vibrates
        public float RecoverySpeed { get; set; } = 2.0f;  // How fast trauma decays per second
        public float NoiseFloor { get; set; } = 0.5f;

        // 3. Global Multiplier
        public float MasterIntensity { get; set; } = 1.0f;

        private Global _global;

        public HapticsManager()
        {
            _perlin = new SeededPerlin(18020808);
            StopAll();
        }

        // --- Legacy Triggers (Specific Animations) ---

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

        // --- New Compound Shake Trigger ---

        /// <summary>
        /// Adds trauma to the system.
        /// </summary>
        /// <param name="stress">Amount of stress to add (0.0 to 1.0). 
        /// 0.2 = Subtle bump. 
        /// 0.5 = Heavy hit. 
        /// 1.0 = Catastrophic damage.</param>
        public void TriggerCompoundShake(float stress)
        {
            // Apply multiplier
            float amount = stress * MasterIntensity;

            // Add to current trauma and clamp to 1.0
            _trauma = Math.Clamp(_trauma + amount, 0f, 1.0f);
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
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            // Constant Linear Decay for Trauma
            if (_trauma > 0)
            {
                _trauma -= RecoverySpeed * dt;
                if (_trauma < 0) _trauma = 0;
            }

            // Update legacy effects
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
        /// </summary>
        public (Vector2 Offset, float Rotation, float Scale) GetTotalShakeParams()
        {
            // Sum up legacy effects
            Vector2 totalOffset = _shake.Offset + _hop.Offset + _pulse.Offset + _wobble.Offset + _drift.Offset + _bounce.Offset + _zoomPulse.Offset + _directionalShake.Offset;
            float totalRotation = _shake.Rotation + _hop.Rotation + _pulse.Rotation + _wobble.Rotation + _drift.Rotation + _bounce.Rotation + _zoomPulse.Rotation + _directionalShake.Rotation;
            float totalScale = GetCurrentScale();

            // Add Compound Shake (Trauma-based)
            if (_trauma > 0)
            {
                // Calculate shake magnitude: Trauma ^ Exponent
                // This makes low trauma values result in very small shakes, while high trauma ramps up quickly.
                float shake = MathF.Pow(_trauma, TraumaExponent);

                // Use Perlin noise for smooth random movement
                // We use distinct seeds (offsets) for X, Y, and Rotation so they don't sync up.
                float noiseX = _perlin.Noise(_time * Frequency, 0);
                float noiseY = _perlin.Noise(0, _time * Frequency + 100);
                float noiseRot = _perlin.Noise(_time * Frequency + 200, _time * Frequency + 200);

                // Apply Noise Floor to ensure shake is always perceptible if trauma is high
                noiseX = ApplyNoiseFloor(noiseX);
                noiseY = ApplyNoiseFloor(noiseY);
                noiseRot = ApplyNoiseFloor(noiseRot);

                totalOffset.X += MaxTranslation * shake * noiseX;
                totalOffset.Y += MaxTranslation * shake * noiseY;
                totalRotation += MaxRotation * shake * noiseRot;
            }

            return (totalOffset, totalRotation, totalScale);
        }

        /// <summary>
        /// Ensures the noise value is never closer to 0 than the NoiseFloor.
        /// This prevents "dead zones" in the shake where the Perlin noise crosses zero.
        /// </summary>
        private float ApplyNoiseFloor(float noise)
        {
            if (Math.Abs(noise) < NoiseFloor)
            {
                // Push to the floor value in the direction of the noise (or positive if 0)
                float sign = noise >= 0 ? 1f : -1f;
                return sign * NoiseFloor;
            }
            return noise;
        }

        public Matrix GetHapticsMatrix()
        {
            _global ??= ServiceLocator.Get<Global>();

            var (totalOffset, totalRotation, totalScale) = GetTotalShakeParams();

            var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

            // Round the offset to ensure pixel-perfect rendering
            totalOffset.X = MathF.Round(totalOffset.X);
            totalOffset.Y = MathF.Round(totalOffset.Y);

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
            private float _initialIntensity;

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
                _initialIntensity = intensity;
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
                            float decay = 1.0f - Easing.EaseOutQuad(progress);
                            float oscillation = (float)Math.Sin(_timer * 60f);
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
