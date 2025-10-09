using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Particles
{
    public static class ParticleEffects
    {
        /// <summary>
        /// Creates the settings for a dirt spray/dust kick-up effect.
        /// </summary>
        public static ParticleEmitterSettings CreateDirtSpray()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(Global.GRID_CELL_SIZE / 2f, Global.GRID_CELL_SIZE / 2f);
            settings.EmissionRate = 0; // We will use bursts, not a continuous rate
            settings.MaxParticles = 100; // Increased for bursts
            settings.Duration = 0.5f; // Auto-destroy after particles fade

            // Initial Particle
            settings.Lifetime = new FloatRange(0.1f, 0.4f);
            // Velocity will be set dynamically based on movement direction.
            // We set a base speed range here.
            settings.InitialVelocityX = new FloatRange(0f, 2f); // This is speed, not a vector component
            settings.InitialVelocityY = new FloatRange(0f); // Unused for this effect's velocity calculation
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-2f, 2f);

            // Over Lifetime
            settings.Gravity = new Vector2(0, 10f); // A little gravity to pull the dirt down
            settings.Drag = 1.5f; // Particles will slow to a stop
            settings.StartColor = global.Palette_Red;
            settings.EndColor = new Color(100, 30, 25); // Dark, dusty red
            settings.StartAlpha = 0.9f; // This is now the peak alpha
            settings.EndAlpha = 0.0f; // This is ignored but good practice to set
            settings.AlphaFadeInAndOut = true;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.AlphaBlend;

            return settings;
        }

        /// <summary>
        /// Creates the settings for a bright, sharp spark effect.
        /// </summary>
        public static ParticleEmitterSettings CreateSparks()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0; // Burst only
            settings.BurstCount = 20;
            settings.MaxParticles = 20;
            settings.Duration = 0.3f; // Auto-destroy after particles fade

            // Initial Particle
            settings.Lifetime = new FloatRange(0.1f, 0.25f); // Slightly longer lifetime for visible trails
            // Velocity is set manually in the handler for a radial burst.
            // We set a speed range here that the handler can use.
            settings.InitialVelocityX = new FloatRange(250f, 400f); // Represents higher speed
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f); // This will now be the trail's thickness
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);

            // Over Lifetime
            settings.Gravity = new Vector2(0, 50f); // A little gravity
            settings.Drag = 2.5f; // High drag to stop sparks quickly
            settings.StartColor = Color.White; // Start as pure white
            settings.EndColor = global.Palette_LightYellow; // Fade to a pale yellow to simulate cooling
            settings.StartAlpha = 1.0f; // Fully opaque at start
            settings.EndAlpha = 0.5f;
            settings.AlphaFadeInAndOut = false;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.Additive; // Crucial for a bright, glowing effect
            settings.LayerDepth = 0.9f; // Draw on top

            return settings;
        }

        /// <summary>
        /// Creates the settings for a white, explosive impact effect.
        /// </summary>
        public static ParticleEmitterSettings CreateSumImpact()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(40f, 40f);
            settings.EmissionRate = 0; // Burst only
            settings.BurstCount = 25;
            settings.MaxParticles = 25;
            settings.Duration = 1.1f; // Auto-destroy after particles fade

            // Initial Particle
            settings.Lifetime = new FloatRange(0.5f, 1.0f);
            settings.InitialVelocityX = new FloatRange(-50f, 50f); // Symmetrical radial explosion
            settings.InitialVelocityY = new FloatRange(-50f, 50f); // Symmetrical radial explosion
            settings.InitialSize = new FloatRange(0.1f, 2f);
            settings.EndSize = new FloatRange(5f, 8f);
            settings.InterpolateSize = true;
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-MathHelper.Pi, MathHelper.Pi);

            // Over Lifetime
            settings.Gravity = Vector2.Zero; // No gravity for a top-down effect
            settings.Drag = 2f; // Particles slow down significantly
            settings.StartColor = Color.White;
            settings.EndColor = Color.White;
            settings.StartAlpha = 0.35f;
            settings.EndAlpha = 0.0f;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.AlphaBlend;
            settings.LayerDepth = 0.8f;

            return settings;
        }

        /// <summary>
        /// Creates the settings for a generic, fiery UI effect.
        /// The color and emitter size should be set on the emitter instance.
        /// </summary>
        public static ParticleEmitterSettings CreateUIFire()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Circle;
            settings.EmitFrom = EmissionSource.Volume;
            settings.EmitterSize = new Vector2(8f, 8f); // Default size, should be overridden
            settings.EmissionRate = 150f; // Continuous emission while active
            settings.MaxParticles = 100;
            settings.Duration = 0.7f; // Auto-destroy after particles fade

            // Initial Particle
            settings.Lifetime = new FloatRange(0.3f, 0.6f);
            settings.InitialVelocityX = new FloatRange(-15f, 15f); // Horizontal spread
            settings.InitialVelocityY = new FloatRange(-50f, -30f); // Upward velocity
            settings.InitialSize = new FloatRange(1f, 2.5f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);

            // Over Lifetime
            settings.Gravity = new Vector2(0, 10f); // Slight downward pull
            settings.Drag = 1.0f;
            // Colors will be set dynamically on the emitter instance.
            settings.StartColor = global.Palette_Orange;
            settings.EndColor = global.Palette_Orange; // Fade to same color (alpha handles transparency)
            settings.StartAlpha = 0.8f;
            settings.EndAlpha = 0.0f;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.Additive; // For a fiery glow
            settings.LayerDepth = 0.3f; // Draw behind the box itself

            return settings;
        }

        /// <summary>
        /// Creates the settings for a digital, square particle burst.
        /// </summary>
        public static ParticleEmitterSettings CreateDigitalBurst()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            // Emitter
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0; // Burst only
            settings.BurstCount = 50;
            settings.MaxParticles = 50;
            settings.Duration = 0.5f; // Auto-destroy after particles fade

            // Initial Particle
            settings.Lifetime = new FloatRange(0.2f, 0.4f);
            // Velocity is set manually for a radial burst. This is the speed.
            settings.InitialVelocityX = new FloatRange(150f, 300f);
            settings.InitialVelocityY = new FloatRange(0f);
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0f);
            settings.InitialRotationSpeed = new FloatRange(0f);

            // Over Lifetime
            settings.Gravity = Vector2.Zero;
            settings.Drag = 4f; // High drag for a quick stop
            settings.StartColor = global.Palette_BrightWhite;
            settings.EndColor = global.Palette_Teal;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.9f;

            return settings;
        }

        /// <summary>
        /// Creates a composite fireball effect from multiple layered particle emitters.
        /// </summary>
        /// <returns>A list of ParticleEmitterSettings objects, one for each layer of the effect.</returns>
        public static List<ParticleEmitterSettings> CreateLayeredFireball()
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var global = ServiceLocator.Get<Global>();
            var layers = new List<ParticleEmitterSettings>();

            // --- Layer 1: Red Base (Back) ---
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
            redBase.LayerDepth = 0.3f; // Furthest back
            redBase.SpriteSheetColumns = 6;
            redBase.SpriteSheetRows = 1;
            redBase.SpriteSheetTotalFrames = 6;
            redBase.StartColor = new Color(255, 50, 0); // Deep Red
            redBase.EndColor = new Color(20, 0, 0, 100); // Fades to a dark, smoky color
            layers.Add(redBase);

            // --- Layer 2: Orange Body (Middle) ---
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
            orangeBody.LayerDepth = 0.4f; // In the middle
            orangeBody.SpriteSheetColumns = 6;
            orangeBody.SpriteSheetRows = 1;
            orangeBody.SpriteSheetTotalFrames = 6;
            orangeBody.StartColor = new Color(255, 120, 0); // Bright Orange
            orangeBody.EndColor = new Color(200, 20, 0); // Fades to Red
            layers.Add(orangeBody);

            // --- Layer 3: Yellow Core (Front) ---
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
            yellowCore.LayerDepth = 0.5f; // Closest to camera
            yellowCore.SpriteSheetColumns = 6;
            yellowCore.SpriteSheetRows = 1;
            yellowCore.SpriteSheetTotalFrames = 6;
            yellowCore.StartColor = new Color(255, 255, 150); // White-Yellow
            yellowCore.EndColor = new Color(255, 150, 0); // Fades to Orange
            layers.Add(yellowCore);

            return layers;
        }

        /// <summary>
        /// A swirling magical effect.
        /// </summary>
        public static ParticleEmitterSettings CreateMagicSwirl()
        {
            var settings = ParticleEmitterSettings.CreateDefault();
            var global = ServiceLocator.Get<Global>();

            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 200;
            settings.MaxParticles = 200;
            settings.Duration = 1.5f; // Emitter runs for 1.5s then stops, auto-cleans up

            settings.Lifetime = new FloatRange(0.8f, 1.2f);
            settings.InitialVelocityX = new FloatRange(80f); // Speed
            settings.InitialVelocityY = new FloatRange(0f); // Unused
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.EndSize = new FloatRange(0f);
            settings.InterpolateSize = true;

            settings.Gravity = Vector2.Zero;
            settings.Drag = 0.5f;
            settings.StartColor = global.Palette_LightPurple;
            settings.EndColor = global.Palette_Teal;
            settings.StartAlpha = 1.0f;
            settings.EndAlpha = 0.0f;

            settings.VectorFieldInfluence = 1.0f; // Strong influence from the vector field for swirling

            settings.Texture = ServiceLocator.Get<SpriteManager>().SoftParticleSprite;
            settings.BlendMode = BlendState.Additive;
            settings.LayerDepth = 0.7f;

            return settings;
        }
    }
}