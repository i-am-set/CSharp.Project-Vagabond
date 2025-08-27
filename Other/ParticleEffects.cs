using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
            settings.MaxParticles = 20;

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
            settings.MaxParticles = 25;

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
            settings.MaxParticles = 50;

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
        /// Creates a composite fireball effect from three distinct particle emitters.
        /// To use, create three emitters from the returned settings and update them from the same position.
        /// </summary>
        /// <returns>A list of three ParticleEmitterSettings objects for the fireball effect.</returns>
        public static List<ParticleEmitterSettings> CreateFireball()
        {
            var global = ServiceLocator.Get<Global>();
            var settingsList = new List<ParticleEmitterSettings>();

            // 1. Red Outer Flames (large, slow, long-lived)
            var redFlames = ParticleEmitterSettings.CreateDefault();
            redFlames.Shape = EmitterShape.Circle;
            redFlames.EmitFrom = EmissionSource.Volume;
            redFlames.EmitterSize = new Vector2(50f, 50f);
            redFlames.MaxParticles = 250;
            redFlames.EmissionRate = 200;
            redFlames.Lifetime = new FloatRange(1.2f, 1.8f);
            redFlames.InitialVelocityX = new FloatRange(-20f, 20f);
            redFlames.InitialVelocityY = new FloatRange(-40f, -20f);
            redFlames.Gravity = new Vector2(0, -50f); // Negative Y for buoyancy
            redFlames.Drag = 0.5f;
            redFlames.InitialSize = new FloatRange(12f, 16f);
            redFlames.EndSize = new FloatRange(0f);
            redFlames.InterpolateSize = true;
            redFlames.StartColor = global.Palette_Red;
            redFlames.EndColor = new Color(150, 20, 20);
            redFlames.BlendMode = BlendState.Additive;
            settingsList.Add(redFlames);

            // 2. Orange Middle Flames (medium, faster)
            var orangeFlames = ParticleEmitterSettings.CreateDefault();
            orangeFlames.Shape = EmitterShape.Circle;
            orangeFlames.EmitFrom = EmissionSource.Volume;
            orangeFlames.EmitterSize = new Vector2(40f, 40f);
            orangeFlames.MaxParticles = 200;
            orangeFlames.EmissionRate = 180;
            orangeFlames.Lifetime = new FloatRange(1.0f, 1.5f);
            orangeFlames.InitialVelocityX = new FloatRange(-15f, 15f);
            orangeFlames.InitialVelocityY = new FloatRange(-60f, -40f);
            orangeFlames.Gravity = new Vector2(0, -50f);
            orangeFlames.Drag = 0.4f;
            orangeFlames.InitialSize = new FloatRange(8f, 12f);
            orangeFlames.EndSize = new FloatRange(0f);
            orangeFlames.InterpolateSize = true;
            orangeFlames.StartColor = global.Palette_Orange;
            orangeFlames.EndColor = global.Palette_Red;
            orangeFlames.BlendMode = BlendState.Additive;
            settingsList.Add(orangeFlames);

            // 3. Yellow Core Flames (small, fastest, short-lived)
            var yellowFlames = ParticleEmitterSettings.CreateDefault();
            yellowFlames.Shape = EmitterShape.Circle;
            yellowFlames.EmitFrom = EmissionSource.Volume;
            yellowFlames.EmitterSize = new Vector2(25f, 25f);
            yellowFlames.MaxParticles = 150;
            yellowFlames.EmissionRate = 160;
            yellowFlames.Lifetime = new FloatRange(0.8f, 1.2f);
            yellowFlames.InitialVelocityX = new FloatRange(-10f, 10f);
            yellowFlames.InitialVelocityY = new FloatRange(-80f, -60f);
            yellowFlames.Gravity = new Vector2(0, -50f);
            yellowFlames.Drag = 0.3f;
            yellowFlames.InitialSize = new FloatRange(4f, 7f);
            yellowFlames.EndSize = new FloatRange(0f);
            yellowFlames.InterpolateSize = true;
            yellowFlames.StartColor = global.Palette_Yellow;
            yellowFlames.EndColor = global.Palette_Orange;
            yellowFlames.BlendMode = BlendState.Additive;
            settingsList.Add(yellowFlames);

            return settingsList;
        }
    }
}