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
            settings.EmitterSize = new Vector2(Global.LOCAL_GRID_CELL_SIZE / 2f, Global.LOCAL_GRID_CELL_SIZE / 2f);
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
    }
}