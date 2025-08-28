using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Particles
{
    public class ParticleEmitter
    {
        public ParticleEmitterSettings Settings { get; }
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;

        private readonly Particle[] _particles;
        private int _activeParticleCount = 0;
        private float _emissionTimer = 0f;
        public float BurstTimer { get; set; } = 0f;
        private readonly Random _random;

        public ParticleEmitter(ParticleEmitterSettings settings)
        {
            Settings = settings;
            _particles = new Particle[settings.MaxParticles];
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

        public void Update(float deltaTime, VectorField vectorField)
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

            // Update existing particles using a swap-and-pop technique for efficiency.
            for (int i = _activeParticleCount - 1; i >= 0; i--)
            {
                ref var p = ref _particles[i];
                p.Age += deltaTime;

                if (p.Age >= p.Lifetime)
                {
                    // Particle is dead, swap it with the last active particle and decrease the count.
                    _activeParticleCount--;
                    _particles[i] = _particles[_activeParticleCount];
                }
                else
                {
                    float lifeRatio = p.Age / p.Lifetime;

                    // Apply Vector Field influence first. This is for turbulence/flicker.
                    if (vectorField != null && Settings.VectorFieldInfluence > 0)
                    {
                        Vector2 fieldForce = vectorField.GetForceAt(p.Position);
                        p.Velocity += fieldForce * Settings.VectorFieldInfluence * deltaTime;
                    }

                    // Apply Attractor Force to pull particles towards a central line.
                    if (Settings.AttractorXPosition.HasValue && Settings.AttractorStrength > 0)
                    {
                        float distanceX = Settings.AttractorXPosition.Value - p.Position.X;
                        // The force is proportional to the distance, creating a spring-like pull to the center line.
                        p.Velocity.X += distanceX * Settings.AttractorStrength * deltaTime;
                    }

                    // Physics
                    p.Velocity += (p.Acceleration + Settings.Gravity) * deltaTime;

                    if (Settings.Drag > 0)
                    {
                        p.Velocity *= Math.Max(0, 1.0f - Settings.Drag * deltaTime);
                    }

                    p.Position += p.Velocity * deltaTime;
                    p.Rotation += p.RotationSpeed * deltaTime;

                    // Over-lifetime changes
                    if (Settings.UsesCustomShaderData)
                    {
                        // Color is handled by the shader. We only need to calculate alpha here.
                        if (Settings.AlphaFadeInAndOut)
                        {
                            float curve = 1.0f - MathF.Pow(2.0f * lifeRatio - 1.0f, 2.0f);
                            p.Alpha = MathHelper.Lerp(0, Settings.StartAlpha, curve);
                        }
                        else
                        {
                            p.Alpha = MathHelper.Lerp(Settings.StartAlpha, Settings.EndAlpha, lifeRatio);
                        }
                    }
                    else
                    {
                        // Standard CPU-based color and alpha interpolation.
                        p.Color = Color.Lerp(Settings.StartColor, Settings.EndColor, lifeRatio);

                        if (Settings.AlphaFadeInAndOut)
                        {
                            float curve = 1.0f - MathF.Pow(2.0f * lifeRatio - 1.0f, 2.0f);
                            p.Alpha = MathHelper.Lerp(0, Settings.StartAlpha, curve);
                        }
                        else
                        {
                            p.Alpha = MathHelper.Lerp(Settings.StartAlpha, Settings.EndAlpha, lifeRatio);
                        }
                    }

                    if (Settings.InterpolateSize)
                    {
                        p.Size = MathHelper.Lerp(p.StartSize, p.EndSize, lifeRatio);
                    }
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
            if (_activeParticleCount >= Settings.MaxParticles)
            {
                return -1; // Pool is full
            }

            int particleIndex = _activeParticleCount;
            ref var p = ref _particles[particleIndex];

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
                case EmitterShape.Rectangle:
                    float halfWidth = Settings.EmitterSize.X / 2f;
                    float halfHeight = Settings.EmitterSize.Y / 2f;
                    p.Position += new Vector2(
                        (float)(_random.NextDouble() * 2 - 1) * halfWidth,
                        (float)(_random.NextDouble() * 2 - 1) * halfHeight
                    );
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

            _activeParticleCount++;
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

            for (int i = 0; i < _activeParticleCount; i++)
            {
                ref var p = ref _particles[i];
                Color drawColor;

                if (Settings.UsesCustomShaderData)
                {
                    float lifeRatio = p.Age / p.Lifetime;
                    // Pack lifeRatio into the Red channel and alpha into the Alpha channel.
                    // The shader will use these values to compute the final color.
                    drawColor = new Color(lifeRatio, 0f, 0f, p.Alpha);
                }
                else
                {
                    drawColor = p.Color * p.Alpha;
                }

                var origin = new Vector2(Settings.Texture.Width / 2f, Settings.Texture.Height / 2f);
                var scale = p.Size;

                if (Settings.SnapToPixelGrid)
                {
                    spriteBatch.DrawSnapped(Settings.Texture, p.Position, null, drawColor, p.Rotation, origin, scale, SpriteEffects.None, Settings.LayerDepth);
                }
                else
                {
                    spriteBatch.Draw(Settings.Texture, p.Position, null, drawColor, p.Rotation, origin, scale, SpriteEffects.None, Settings.LayerDepth);
                }
            }
        }

        /// <summary>
        /// Immediately deactivates all particles in the emitter's pool.
        /// </summary>
        public void Clear()
        {
            _activeParticleCount = 0;
        }
    }
}