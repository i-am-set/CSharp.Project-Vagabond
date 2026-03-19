using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;
using System.Diagnostics;

namespace ProjectVagabond.Particles
{
    public class ParticleEmitter
    {
        public ParticleEmitterSettings Settings { get; }
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;
        public float EmissionStrength { get; set; } = 1f;

        private readonly Particle[] _particles;
        private int _activeParticleCount = 0;
        private float _emissionTimer = 0f;
        public float BurstTimer { get; set; } = 0f;

        // A single, shared Random instance for all emitters to prevent seed duplication issues.
        private static readonly Random _random = new Random();

        // Auto-destruction state
        private float _durationTimer = 0f;
        public bool IsFinished { get; private set; }

        public ParticleEmitter(ParticleEmitterSettings settings)
        {
            Settings = settings;
            _particles = new Particle[settings.MaxParticles];
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
            if (Settings.Duration != float.PositiveInfinity)
            {
                _durationTimer += deltaTime;
            }

            // We must continue updating existing particles so trails fade out naturally.

            deltaTime *= Settings.TimeScale;

            // --- 1. Emission Logic (Only if Active) ---
            bool canEmit = IsActive && (Settings.Duration == float.PositiveInfinity || _durationTimer < Settings.Duration);

            if (canEmit && Settings.EmissionRate > 0)
            {
                _emissionTimer += deltaTime;
                float timePerParticle = 1.0f / (Settings.EmissionRate * EmissionStrength);
                if (timePerParticle > 0) // Avoid division by zero if strength is 0
                {
                    while (_emissionTimer > timePerParticle)
                    {
                        EmitParticle();
                        _emissionTimer -= timePerParticle;
                    }
                }
            }

            // --- 2. Particle Update Logic (Always Run) ---
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

                    if (!p.HasSettled)
                    {
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

                        if (Settings.Bounciness > 0 && p.Position.Y >= p.FloorY && p.Velocity.Y > 0)
                        {
                            p.Position.Y = p.FloorY;
                            p.Velocity.Y = -p.Velocity.Y * Settings.Bounciness;
                            p.Velocity.X *= 0.6f; // Ground friction

                            if (Math.Abs(p.Velocity.Y) < 15f)
                            {
                                p.Velocity.Y = 0;
                                p.Velocity.X = 0;
                                p.HasSettled = true;
                            }
                        }
                    }

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

            // Check if the emitter is finished (finite duration, timer expired, all particles dead).
            // OR if it was manually deactivated and all particles are gone.
            bool timeExpired = (Settings.Duration != float.PositiveInfinity && _durationTimer >= Settings.Duration);
            bool manuallyStopped = !IsActive;

            IsFinished = (timeExpired || manuallyStopped) && _activeParticleCount == 0;
        }

        public void EmitBurst(int count)
        {
            if (count > 0 && !string.IsNullOrEmpty(Settings.SoundCue))
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlaySfx(Settings.SoundCue, Settings.SoundPitchVariance);
            }

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
            p.HasSettled = false;

            p.Position = Position; // Start at emitter center
            p.FloorY = Position.Y + (float)(_random.NextDouble() * 2 - 1) * Settings.FloorScatterY;

            Vector2 localOffset = Vector2.Zero;
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
                        localOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                    }
                    break;
                case EmitterShape.Rectangle:
                    float halfWidth = Settings.EmitterSize.X / 2f;
                    float halfHeight = Settings.EmitterSize.Y / 2f;
                    localOffset = new Vector2(
                        (float)(_random.NextDouble() * 2 - 1) * halfWidth,
                        (float)(_random.NextDouble() * 2 - 1) * halfHeight
                    );
                    break;
            }

            if (Settings.EmitterRotation != 0f)
            {
                float cos = MathF.Cos(Settings.EmitterRotation);
                float sin = MathF.Sin(Settings.EmitterRotation);
                localOffset = new Vector2(
                    localOffset.X * cos - localOffset.Y * sin,
                    localOffset.X * sin + localOffset.Y * cos
                );
            }

            p.Position += localOffset;

            if (Settings.VelocityPattern == EmissionPattern.Radial)
            {
                float angle;
                if (Settings.Shape == EmitterShape.Circle && localOffset.LengthSquared() > 0)
                {
                    angle = MathF.Atan2(localOffset.Y, localOffset.X);
                }
                else
                {
                    angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                }
                float speed = Settings.InitialVelocityX.GetValue(_random); // Use X range as speed
                p.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            }
            else if (Settings.VelocityPattern == EmissionPattern.Cone)
            {
                float halfSpread = Settings.ConeSpread / 2f;
                float angle = Settings.ConeAngle + (float)(_random.NextDouble() * Settings.ConeSpread - halfSpread);
                float speed = Settings.InitialVelocityX.GetValue(_random); // Use X range as speed
                p.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            }
            else // Cartesian
            {
                p.Velocity = new Vector2(Settings.InitialVelocityX.GetValue(_random), Settings.InitialVelocityY.GetValue(_random));
            }

            p.Acceleration = new Vector2(Settings.InitialAccelerationX.GetValue(_random), Settings.InitialAccelerationY.GetValue(_random));
            p.StartSize = Settings.InitialSize.GetValue(_random);
            p.EndSize = Settings.EndSize.GetValue(_random);
            p.Size = p.StartSize;
            p.Rotation = Settings.InitialRotation.GetValue(_random);
            p.RotationSpeed = Settings.InitialRotationSpeed.GetValue(_random);
            p.Color = Settings.StartColor;
            p.Alpha = Settings.StartAlpha;

            // Handle spritesheet logic
            if (Settings.SpriteSheetTotalFrames > 1 && Settings.Texture != null)
            {
                int frameWidth = Settings.Texture.Width / Settings.SpriteSheetColumns;
                int frameHeight = Settings.Texture.Height / Settings.SpriteSheetRows;
                int frameIndex = _random.Next(Settings.SpriteSheetTotalFrames);
                int col = frameIndex % Settings.SpriteSheetColumns;
                int row = frameIndex / Settings.SpriteSheetColumns;
                p.SourceRectangle = new Rectangle(col * frameWidth, row * frameHeight, frameWidth, frameHeight);
            }
            else
            {
                p.SourceRectangle = Rectangle.Empty; // Signal to use the full texture
            }

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

                Rectangle? sourceRect = p.SourceRectangle.IsEmpty ? null : p.SourceRectangle;
                var origin = sourceRect.HasValue
                    ? new Vector2(sourceRect.Value.Width / 2f, sourceRect.Value.Height / 2f)
                    : new Vector2(Settings.Texture.Width / 2f, Settings.Texture.Height / 2f);

                var scale = p.Size;

                if (Settings.SnapToPixelGrid)
                {
                    // Round the origin to prevent half-pixel offsets on odd-sized textures (like 3x3)
                    Vector2 snappedOrigin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
                    spriteBatch.DrawSnapped(Settings.Texture, p.Position, sourceRect, drawColor, p.Rotation, snappedOrigin, scale, SpriteEffects.None, Settings.LayerDepth);
                }
                else
                {
                    spriteBatch.Draw(Settings.Texture, p.Position, sourceRect, drawColor, p.Rotation, origin, scale, SpriteEffects.None, Settings.LayerDepth);
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