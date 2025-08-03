using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    }
}