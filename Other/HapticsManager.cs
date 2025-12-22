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
        DirectionalShake // NEW
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
        private readonly HapticEffect _directionalShake = new(HapticType.DirectionalShake); // NEW
        private Global _global;

        public HapticsManager()
        {
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
        }

        public void Update(GameTime gameTime)
        {
            _shake.Update(gameTime, _random);
            _hop.Update(gameTime, _random);
            _pulse.Update(gameTime, _random);
            _wobble.Update(gameTime, _random);
            _drift.Update(gameTime, _random);
            _bounce.Update(gameTime, _random);
            _zoomPulse.Update(gameTime, _random);
            _directionalShake.Update(gameTime, _random);
        }

        public Matrix GetHapticsMatrix()
        {
            _global ??= ServiceLocator.Get<Global>();

            Vector2 totalOffset = _shake.Offset + _hop.Offset + _pulse.Offset + _wobble.Offset + _drift.Offset + _bounce.Offset + _zoomPulse.Offset + _directionalShake.Offset;
            float totalRotation = _shake.Rotation + _hop.Rotation + _pulse.Rotation + _wobble.Rotation + _drift.Rotation + _bounce.Rotation + _zoomPulse.Rotation + _directionalShake.Rotation;
            float totalScale = GetCurrentScale();

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
            return _shake.Active || _hop.Active || _pulse.Active || _wobble.Active || _drift.Active || _bounce.Active || _zoomPulse.Active || _directionalShake.Active;
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
