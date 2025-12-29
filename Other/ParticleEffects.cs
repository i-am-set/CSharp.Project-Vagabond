using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Particles
{
    public static class ParticleEffects
    {
        /// <summary>
        /// Creates a burst of bright sparks for combat impacts.
        /// </summary>
        /// <param name="intensity">Scalar for the size and violence of the effect (1.0 = Normal, 3.0+ = Heavy).</param>
        public static ParticleEmitterSettings CreateHitSparks(float intensity = 1.0f)
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Scale particle count and size based on intensity
            int baseCount = 12;
            int scaledCount = (int)(baseCount * intensity);

            // Emitter
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(10f * intensity, 10f * intensity);
            settings.EmissionRate = 0; // Burst only
            settings.BurstCount = scaledCount;
            settings.MaxParticles = scaledCount * 2;
            settings.Duration = 0.2f + (0.1f * intensity); // Lasts slightly longer on big hits

            // Initial Particle
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.15f, 0.3f + (0.1f * intensity));

            // Velocity scales with intensity to make debris fly further
            float speedScale = 1.0f + (intensity * 0.5f);
            settings.InitialVelocityX = new FloatRange(150f * speedScale, 300f * speedScale);
            settings.InitialVelocityY = new FloatRange(0f);

            settings.InitialSize = new FloatRange(2f * intensity, 4f * intensity);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);

            // Over Lifetime
            settings.Gravity = new Vector2(0, 200f * intensity); // Heavier sparks fall faster
            settings.Drag = 3.0f;
            settings.StartColor = global.Palette_BrightWhite;
            settings.EndColor = global.Palette_Yellow;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 pixel
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.95f; // Very top

            return settings;
        }

        /// <summary>
        /// Creates an expanding ring shockwave.
        /// </summary>
        public static ParticleEmitterSettings CreateImpactRing(float intensity = 1.0f)
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 1;
            settings.MaxParticles = 1;
            settings.Duration = 0.1f + (0.1f * intensity);

            // Initial Particle
            settings.Lifetime = new FloatRange(0.1f + (0.05f * intensity));
            settings.InitialVelocityX = new FloatRange(0f);
            settings.InitialVelocityY = new FloatRange(0f);

            // The ring texture is 64x64.
            // Scale 0.1 = 6.4px
            // Scale 1.5 = 96px
            settings.InitialSize = new FloatRange(0.1f * intensity);
            settings.EndSize = new FloatRange(1.5f * intensity);
            settings.InterpolateSize = true;

            // Over Lifetime
            settings.StartColor = global.Palette_White;
            settings.EndColor = global.Palette_White;
            settings.StartAlpha = 0.8f;
            settings.EndAlpha = 0.0f;

            // Rendering
            // Use the new hollow ring texture
            settings.Texture = ServiceLocator.Get<SpriteManager>().RingTextureSprite;
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.94f;

            return settings;
        }

        public static ParticleEmitterSettings CreateDirtSpray()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(Global.GRID_CELL_SIZE / 2f, Global.GRID_CELL_SIZE / 2f);
            settings.EmissionRate = 0;
            settings.MaxParticles = 100;
            settings.Duration = 0.5f;
            settings.Lifetime = new FloatRange(0.1f, 0.4f);
            settings.InitialVelocityX = new FloatRange(0f, 2f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-2f, 2f);
            settings.Gravity = new Vector2(0, 10f);
            settings.Drag = 1.5f;
            settings.StartColor = global.Palette_Red;
            settings.EndColor = new Color(100, 30, 25);
            settings.StartAlpha = 0.9f;
            settings.EndAlpha = 0.0f;
            settings.AlphaFadeInAndOut = true;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.AlphaBlend;
            return settings;
        }

        public static ParticleEmitterSettings CreateSparks()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 20;
            settings.MaxParticles = 20;
            settings.Duration = 0.3f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.1f, 0.25f);
            settings.InitialVelocityX = new FloatRange(250f, 400f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);
            settings.Gravity = new Vector2(0, 50f);
            settings.Drag = 2.5f;
            settings.StartColor = Color.White;
            settings.EndColor = global.Palette_LightYellow;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.5f;
            settings.AlphaFadeInAndOut = false;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.9f;
            return settings;
        }

        public static ParticleEmitterSettings CreateSumImpact()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(40f, 40f);
            settings.EmissionRate = 0;
            settings.BurstCount = 25;
            settings.MaxParticles = 25;
            settings.Duration = 1.1f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.5f, 1.0f);
            settings.InitialVelocityX = new FloatRange(0f, 50f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(0.1f, 2f);
            settings.EndSize = new FloatRange(5f, 8f);
            settings.InterpolateSize = true;
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-MathHelper.Pi, MathHelper.Pi);
            settings.Gravity = Vector2.Zero;
            settings.Drag = 2f;
            settings.StartColor = Color.White;
            settings.EndColor = Color.White;
            settings.StartAlpha = 0.35f;
            settings.EndAlpha = 0.0f;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.AlphaBlend;
            settings.LayerDepth = 0.8f;
            return settings;
        }

        public static ParticleEmitterSettings CreateUIFire()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(8f, 8f);
            settings.EmissionRate = 150f;
            settings.MaxParticles = 100;
            settings.Duration = 0.7f;
            settings.Lifetime = new FloatRange(0.3f, 0.6f);
            settings.InitialVelocityX = new FloatRange(-15f, 15f);
            settings.InitialVelocityY = new FloatRange(-50f, -30f);
            settings.InitialSize = new FloatRange(1f, 2.5f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);
            settings.Gravity = new Vector2(0, 10f);
            settings.Drag = 1.0f;
            settings.StartColor = global.Palette_Orange;
            settings.EndColor = global.Palette_Orange;
            settings.StartAlpha = 0.8f;
            settings.EndAlpha = 0.0f;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.3f;
            return settings;
        }

        public static ParticleEmitterSettings CreateDigitalBurst()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 50;
            settings.MaxParticles = 50;
            settings.Duration = 0.5f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.2f, 0.4f);
            settings.InitialVelocityX = new FloatRange(150f, 300f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);
            settings.Gravity = Vector2.Zero;
            settings.Drag = 4f;
            settings.StartColor = global.Palette_BrightWhite;
            settings.EndColor = global.Palette_Teal;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.9f;
            return settings;
        }

        public static List<ParticleEmitterSettings> CreateLayeredFireball()
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var global = ServiceLocator.Get<Global>();
            var layers = new List<ParticleEmitterSettings>();
            var redBase = ParticleEmitterSettings.CreateDefault();
            redBase.Shape = EmitterShape.Circle;
            redBase.EmitFrom = EmissionSource.Volume;
            redBase.EmitterSize = new Vector2(65f, 65f);
            redBase.MaxParticles = 4000;
            redBase.EmissionRate = 4000;
            redBase.Lifetime = new FloatRange(0.4f, 0.9f);
            redBase.InitialVelocityX = new FloatRange(-40f, 40f);
            redBase.InitialVelocityY = new FloatRange(-80f, -50f);
            redBase.InitialSize = new FloatRange(1.5f, 2.2f);
            redBase.EndSize = new FloatRange(0f);
            redBase.InterpolateSize = true;
            redBase.Gravity = new Vector2(0, -150f);
            redBase.Drag = 2.5f;
            redBase.StartAlpha = 0.7f;
            redBase.AlphaFadeInAndOut = true;
            redBase.VectorFieldInfluence = 0.8f;
            redBase.Texture = spriteManager.EmberParticleSprite;
            redBase.BlendMode = BlendState.Additive;
            redBase.LayerDepth = 0.3f;
            redBase.SpriteSheetColumns = 6;
            redBase.SpriteSheetRows = 1;
            redBase.SpriteSheetTotalFrames = 6;
            redBase.StartColor = new Color(255, 50, 0);
            redBase.EndColor = new Color(20, 0, 0, 100);
            layers.Add(redBase);
            var orangeBody = ParticleEmitterSettings.CreateDefault();
            orangeBody.Shape = EmitterShape.Circle;
            orangeBody.EmitFrom = EmissionSource.Volume;
            orangeBody.EmitterSize = new Vector2(50f, 50f);
            orangeBody.MaxParticles = 3500;
            orangeBody.EmissionRate = 3500;
            orangeBody.Lifetime = new FloatRange(0.3f, 0.5f);
            orangeBody.InitialVelocityX = new FloatRange(-35f, 35f);
            orangeBody.InitialVelocityY = new FloatRange(-75f, -45f);
            orangeBody.InitialSize = new FloatRange(1.2f, 1.8f);
            orangeBody.EndSize = new FloatRange(0f);
            orangeBody.InterpolateSize = true;
            orangeBody.Gravity = new Vector2(0, -140f);
            orangeBody.Drag = 2.5f;
            orangeBody.StartAlpha = 0.8f;
            orangeBody.AlphaFadeInAndOut = true;
            orangeBody.VectorFieldInfluence = 0.8f;
            orangeBody.Texture = spriteManager.EmberParticleSprite;
            orangeBody.BlendMode = BlendState.Additive;
            orangeBody.LayerDepth = 0.4f;
            orangeBody.SpriteSheetColumns = 6;
            orangeBody.SpriteSheetRows = 1;
            orangeBody.SpriteSheetTotalFrames = 6;
            orangeBody.StartColor = new Color(255, 120, 0);
            orangeBody.EndColor = new Color(200, 20, 0);
            layers.Add(orangeBody);
            var yellowCore = ParticleEmitterSettings.CreateDefault();
            yellowCore.Shape = EmitterShape.Circle;
            yellowCore.EmitFrom = EmissionSource.Volume;
            yellowCore.EmitterSize = new Vector2(40f, 40f);
            yellowCore.MaxParticles = 2500;
            yellowCore.EmissionRate = 2500;
            yellowCore.Lifetime = new FloatRange(0.2f, 0.4f);
            yellowCore.InitialVelocityX = new FloatRange(-30f, 30f);
            yellowCore.InitialVelocityY = new FloatRange(-70f, -40f);
            yellowCore.InitialSize = new FloatRange(0.8f, 1.3f);
            yellowCore.EndSize = new FloatRange(0f);
            yellowCore.InterpolateSize = true;
            yellowCore.Gravity = new Vector2(0, -130f);
            yellowCore.Drag = 2.5f;
            yellowCore.StartAlpha = 0.9f;
            yellowCore.AlphaFadeInAndOut = true;
            yellowCore.VectorFieldInfluence = 0.8f;
            yellowCore.Texture = spriteManager.EmberParticleSprite;
            yellowCore.BlendMode = BlendState.Additive;
            yellowCore.LayerDepth = 0.5f;
            yellowCore.SpriteSheetColumns = 6;
            yellowCore.SpriteSheetRows = 1;
            yellowCore.SpriteSheetTotalFrames = 6;
            yellowCore.StartColor = new Color(255, 255, 150);
            yellowCore.EndColor = new Color(255, 150, 0);
            layers.Add(yellowCore);
            return layers;
        }

        public static ParticleEmitterSettings CreateMagicSwirl()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 200;
            settings.MaxParticles = 200;
            settings.Duration = 1.5f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.8f, 1.2f);
            settings.InitialVelocityX = new FloatRange(80f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.Gravity = Vector2.Zero;
            settings.Drag = 0.5f;
            settings.StartColor = global.Palette_LightPurple;
            settings.EndColor = global.Palette_Teal;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;
            settings.VectorFieldInfluence = 1.0f;
            settings.Texture = ServiceLocator.Get<SpriteManager>().SoftParticleSprite;
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.7f;
            return settings;
        }

        public static ParticleEmitterSettings CreateBadRollParticles()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 100;
            settings.MaxParticles = 100;
            settings.Duration = 1.2f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.6f, 1.0f);
            settings.InitialVelocityX = new FloatRange(30f, 100f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.Gravity = new Vector2(0, 155f);
            settings.Drag = 2.5f;
            settings.StartColor = global.Palette_Red;
            settings.EndColor = global.Palette_Yellow;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.9f;
            return settings;
        }

        public static ParticleEmitterSettings CreateNeutralRollParticles()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 10;
            settings.MaxParticles = 10;
            settings.Duration = 0.6f;
            settings.VelocityPattern = EmissionPattern.Radial;
            settings.Lifetime = new FloatRange(0.3f, 0.5f);
            settings.InitialVelocityX = new FloatRange(0f, 40f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.Gravity = Vector2.Zero;
            settings.Drag = 2f;
            settings.StartColor = global.Palette_White;
            settings.EndColor = global.Palette_LightGray;
            settings.StartAlpha = 0.7f;
            settings.EndAlpha = 0.0f;
            settings.Texture = ServiceLocator.Get<SpriteManager>().SoftParticleSprite;
            settings.BlendMode = BlendState.AlphaBlend;
            settings.LayerDepth = 0.8f;
            return settings;
        }

        public static ParticleEmitterSettings CreateGoodRollParticles()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0;
            settings.BurstCount = 15;
            settings.MaxParticles = 15;
            settings.Duration = 1.0f;
            settings.Lifetime = new FloatRange(0.6f, 0.9f);
            settings.InitialVelocityX = new FloatRange(-30f, 30f);
            settings.InitialVelocityY = new FloatRange(-30f, -10f);
            settings.InitialSize = new FloatRange(2f, 4f);
            settings.EndSize = new FloatRange(0.5f);
            settings.InterpolateSize = true;
            settings.Gravity = new Vector2(0, 20f);
            settings.Drag = 1f;
            settings.StartColor = global.Palette_LightGreen;
            settings.EndColor = global.Palette_DarkGray;
            settings.StartAlpha = 0.6f;
            settings.EndAlpha = 0.0f;
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-10f, 10f);
            settings.Texture = ServiceLocator.Get<SpriteManager>().SoftParticleSprite;
            settings.BlendMode = BlendState.AlphaBlend;
            settings.LayerDepth = 0.8f;
            return settings;
        }

        /// <summary>
        /// Creates a burst of rising green sparkles for healing effects.
        /// </summary>
        public static ParticleEmitterSettings CreateHealBurst()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(30f, 30f); // Wide and flat
            settings.EmissionRate = 5;
            settings.BurstCount = 20; // Increased count
            settings.Duration = 1.0f; // Give the emitter plenty of time to exist while particles float

            // Initial Particle
            settings.Lifetime = new FloatRange(2.0f, 3.0f); // Live much longer
            settings.InitialVelocityX = new FloatRange(-5f, 5f); // Reduced spread speed
            settings.InitialVelocityY = new FloatRange(-20f, -5f); // Very slow rise
            settings.InitialSize = new FloatRange(3f, 6f); // Hard-edged squares (3-6px)
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;

            // Over Lifetime
            settings.Gravity = new Vector2(0, -5f); // Tiny upward drift
            settings.Drag = 0.3f; // Low drag to let them drift
            settings.StartColor = global.Palette_LightGreen;
            settings.EndColor = global.Palette_White;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;

            // Rendering
            // Use 1x1 pixel texture for hard edges
            settings.Texture = ServiceLocator.Get<Texture2D>();
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.9f;

            return settings;
        }
    }
}