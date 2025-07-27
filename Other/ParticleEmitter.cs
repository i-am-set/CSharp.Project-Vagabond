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

        /// <summary>
        /// Gets a reference to a particle in the pool, allowing for direct modification.
        /// </summary>
        /// <param name="index">The index of the particle.</param>
        /// <returns>A reference to the particle struct.</returns>
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

                if (Settings.InterpolateSize)
                {
                    p.Size = MathHelper.Lerp(p.StartSize, p.EndSize, lifeRatio);
                }

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

        /// <summary>
        /// Finds an available particle, initializes it with the emitter's settings, and returns its index.
        /// </summary>
        /// <returns>The index of the newly emitted particle, or -1 if the pool is full.</returns>
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
            p.StartSize = Settings.InitialSize.GetValue(_random);
            p.EndSize = Settings.EndSize.GetValue(_random);
            p.Size = p.StartSize;
            p.Rotation = Settings.InitialRotation.GetValue(_random);
            p.RotationSpeed = Settings.InitialRotationSpeed.GetValue(_random);
            p.Color = Settings.StartColor;
            p.Alpha = Settings.StartAlpha;

            return particleIndex;
        }

        /// <summary>
        /// Emits a single particle using the emitter's settings.
        /// </summary>
        public void EmitParticle()
        {
            EmitParticleAndGetIndex();
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Settings.Texture == null) return;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsAlive) continue;

                ref var p = ref _particles[i];
                var color = p.Color * p.Alpha;

                // --- MODIFIED: Trail Rendering Logic ---
                if (p.Velocity.LengthSquared() > 1f && Settings.BlendMode == BlendState.Additive) // Only draw trails for sparks
                {
                    float speed = p.Velocity.Length();
                    // Trail length is based on speed, clamped to prevent extreme lengths
                    float trailLength = Math.Clamp(speed * 0.08f, p.Size, p.Size * 6);
                    float thickness = p.Size;

                    // Assuming a 1x1 pixel texture, the scale vector directly controls width and height.
                    var scale = new Vector2(trailLength, thickness);
                    var rotation = (float)Math.Atan2(p.Velocity.Y, p.Velocity.X);
                    // Rotate around the leading edge's vertical center to make it look like it's shooting forward.
                    var trailOrigin = new Vector2(0, 0.5f);

                    spriteBatch.Draw(
                        Settings.Texture,
                        p.Position,
                        null,
                        color,
                        rotation,
                        trailOrigin,
                        scale,
                        SpriteEffects.None,
                        Settings.LayerDepth
                    );
                }
                else // Fallback for other particles to draw them as points/squares
                {
                    var origin = new Vector2(0.5f, 0.5f); // Center of the 1x1 pixel
                    var scale = p.Size;

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
}