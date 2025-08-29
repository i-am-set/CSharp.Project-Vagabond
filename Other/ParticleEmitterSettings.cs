using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond.Particles
{
    public enum EmitterShape { Point, Circle, Rectangle }
    public enum EmissionSource { Center, Edge, Volume }

    public class ParticleEmitterSettings
    {
        // Emitter Properties
        public EmitterShape Shape { get; set; }
        public EmissionSource EmitFrom { get; set; }
        public Vector2 EmitterSize { get; set; } // Diameter for Circle, Width/Height for Rectangle
        public float EmissionRate { get; set; }
        public int BurstCount { get; set; }
        public float Duration { get; set; } // Use float.PositiveInfinity for continuous
        public int MaxParticles { get; set; }

        // Initial Particle Properties
        public FloatRange Lifetime { get; set; }
        public FloatRange InitialVelocityX { get; set; }
        public FloatRange InitialVelocityY { get; set; }
        public FloatRange InitialAccelerationX { get; set; }
        public FloatRange InitialAccelerationY { get; set; }
        public FloatRange InitialSize { get; set; }
        public FloatRange EndSize { get; set; }
        public bool InterpolateSize { get; set; }
        public FloatRange InitialRotation { get; set; } // In radians
        public FloatRange InitialRotationSpeed { get; set; } // In radians/sec

        // Over Lifetime Properties
        public Vector2 Gravity { get; set; }
        public float Drag { get; set; } = 0f;
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public float StartAlpha { get; set; }
        public float EndAlpha { get; set; }
        public bool AlphaFadeInAndOut { get; set; } = false;

        // Physics Properties
        public float VectorFieldInfluence { get; set; }
        public float? AttractorXPosition { get; set; } = null;
        public float AttractorStrength { get; set; } = 0f;

        // Rendering Properties
        public Texture2D Texture { get; set; }
        public BlendState BlendMode { get; set; }
        public float LayerDepth { get; set; }
        public bool SnapToPixelGrid { get; set; }
        public Effect ShaderEffect { get; set; }
        public bool UsesCustomShaderData { get; set; } = false;
        public int SpriteSheetColumns { get; set; } = 1;
        public int SpriteSheetRows { get; set; } = 1;
        public int SpriteSheetTotalFrames { get; set; } = 1;


        // Global Properties
        public float TimeScale { get; set; }
        public Rectangle? CullingBounds { get; set; }
        public bool DebugDraw { get; set; }

        /// <summary>
        /// Creates a default set of particle emitter settings.
        /// </summary>
        public static ParticleEmitterSettings CreateDefault()
        {
            return new ParticleEmitterSettings
            {
                // Emitter
                Shape = EmitterShape.Point,
                EmitFrom = EmissionSource.Center,
                EmitterSize = Vector2.Zero,
                EmissionRate = 10f,
                BurstCount = 0,
                Duration = float.PositiveInfinity,
                MaxParticles = 100,

                // Initial Particle
                Lifetime = new FloatRange(1.0f, 2.0f),
                InitialVelocityX = new FloatRange(-10f, 10f),
                InitialVelocityY = new FloatRange(-10f, 10f),
                InitialAccelerationX = new FloatRange(0f),
                InitialAccelerationY = new FloatRange(0f),
                InitialSize = new FloatRange(1f, 3f),
                EndSize = new FloatRange(1f, 3f),
                InterpolateSize = false,
                InitialRotation = new FloatRange(0f),
                InitialRotationSpeed = new FloatRange(0f),

                // Over Lifetime
                Gravity = Vector2.Zero,
                Drag = 0f,
                StartColor = Color.White,
                EndColor = Color.White,
                StartAlpha = 1.0f,
                EndAlpha = 0.0f,
                AlphaFadeInAndOut = false,

                // Physics
                VectorFieldInfluence = 0f,
                AttractorXPosition = null,
                AttractorStrength = 0f,

                // Rendering
                Texture = ServiceLocator.Get<Texture2D>(), // Default 1x1 white pixel
                BlendMode = BlendState.AlphaBlend,
                LayerDepth = 0.5f,
                SnapToPixelGrid = true,
                ShaderEffect = null,
                UsesCustomShaderData = false,
                SpriteSheetColumns = 1,
                SpriteSheetRows = 1,
                SpriteSheetTotalFrames = 1,

                // Global
                TimeScale = 1.0f,
                CullingBounds = null,
                DebugDraw = false
            };
        }
    }
}