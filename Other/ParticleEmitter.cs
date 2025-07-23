using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond.Particles
{
    public class ParticleEmitter
    {
        public ParticleEmitterSettings Settings { get; }
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;

        private readonly Particle[] _particles;
        private int _nextParticleIndex = 0;
        private float _emissionTimer = 0f;
        public float BurstTimer { get; set; } = 0f;
        private readonly Random _random;

        public ParticleEmitter(ParticleEmitterSettings settings)
        {
            Settings = settings;
            _particles = new Particle[settings.MaxParticles];
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Reset();
            }
            _random = new Random();
        }

        // NEW: Helper to get a reference to a particle for modification
        public ref Particle GetParticle(int index)
        {
            return ref _particles[index];
        }

        public void Update(float deltaTime)
        {
            if (!IsActive) return;

            deltaTime *= Settings.TimeScale;

            // Handle continuous emission
            if (Settings.EmissionRate > 0)
            {
                _emissionTimer += deltaTime;
                float timePerParticle = 1.0f / Settings.EmissionRate;
                while (_emissionTimer > timePerParticle)
                {
                    EmitParticle();
                    _emissionTimer -= timePerParticle;
                }
            }

            // Update existing particles
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsAlive) continue;

                ref var p = ref _particles[i];
                p.Age += deltaTime;

                if (p.Age >= p.Lifetime)
                {
                    p.IsAlive = false;
                    continue;
                }

                float lifeRatio = p.Age / p.Lifetime;

                // Physics
                p.Velocity += (p.Acceleration + Settings.Gravity) * deltaTime;

                // Apply drag to slow the particle down
                if (Settings.Drag > 0)
                {
                    p.Velocity *= Math.Max(0, 1.0f - Settings.Drag * deltaTime);
                }

                p.Position += p.Velocity * deltaTime;
                p.Rotation += p.RotationSpeed * deltaTime;

                // Over-lifetime changes
                p.Color = Color.Lerp(Settings.StartColor, Settings.EndColor, lifeRatio);

                if (Settings.AlphaFadeInAndOut)
                {
                    // Parabolic curve: y = 1 - (2x - 1)^2, peaks at 1 when x is 0.5
                    float curve = 1.0f - MathF.Pow(2.0f * lifeRatio - 1.0f, 2.0f);
                    p.Alpha = MathHelper.Lerp(0, Settings.StartAlpha, curve); // Use StartAlpha as the peak
                }
                else
                {
                    p.Alpha = MathHelper.Lerp(Settings.StartAlpha, Settings.EndAlpha, lifeRatio);
                }
            }
        }

        public void EmitBurst(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EmitParticle();
            }
        }

        // MODIFIED: This now returns the index of the emitted particle
        public int EmitParticleAndGetIndex()
        {
            int particleIndex = -1;
            for (int i = 0; i < Settings.MaxParticles; i++)
            {
                _nextParticleIndex = (_nextParticleIndex + 1) % Settings.MaxParticles;
                if (!_particles[_nextParticleIndex].IsAlive)
                {
                    particleIndex = _nextParticleIndex;
                    break;
                }
            }

            if (particleIndex == -1) return -1;

            ref var p = ref _particles[particleIndex];

            p.IsAlive = true;
            p.Age = 0;
            p.Lifetime = Settings.Lifetime.GetValue(_random);

            p.Position = Position; // Start at emitter center
            switch (Settings.Shape)
            {
                case EmitterShape.Circle:
                    float radius = Settings.EmitterSize.X / 2f;
                    if (radius > 0)
                    {
                        float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                        float distance = radius;
                        if (Settings.EmitFrom == EmissionSource.Volume)
                        {
                            distance *= (float)_random.NextDouble();
                        }
                        p.Position += new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                    }
                    break;
            }

            p.Velocity = new Vector2(Settings.InitialVelocityX.GetValue(_random), Settings.InitialVelocityY.GetValue(_random));
            p.Acceleration = new Vector2(Settings.InitialAccelerationX.GetValue(_random), Settings.InitialAccelerationY.GetValue(_random));
            p.Size = Settings.InitialSize.GetValue(_random);
            p.Rotation = Settings.InitialRotation.GetValue(_random);
            p.RotationSpeed = Settings.InitialRotationSpeed.GetValue(_random);
            p.Color = Settings.StartColor;
            p.Alpha = Settings.StartAlpha;

            return particleIndex;
        }

        public void EmitParticle()
        {
            EmitParticleAndGetIndex();
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Settings.Texture == null) return;

            var origin = new Vector2(Settings.Texture.Width / 2f, Settings.Texture.Height / 2f);

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsAlive) continue;

                ref var p = ref _particles[i];
                var color = p.Color * p.Alpha;
                var scale = p.Size / Settings.Texture.Width;

                spriteBatch.Draw(
                    Settings.Texture,
                    p.Position,
                    null,
                    color,
                    p.Rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    Settings.LayerDepth
                );
            }
        }
    }
}