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

            // Emitter
            settings.Shape = EmitterShape.Point;
            settings.EmissionRate = 0; // We will use bursts, not a continuous rate
            settings.MaxParticles = 50;

            // Initial Particle
            settings.Lifetime = new FloatRange(0.2f, 0.5f);
            // Velocity will be set dynamically based on movement direction.
            // We set a base speed range here.
            settings.InitialVelocityX = new FloatRange(15f, 25f); // This is speed, not a vector component
            settings.InitialVelocityY = new FloatRange(0f); // Unused for this effect's velocity calculation
            settings.InitialSize = new FloatRange(1f, 2f);
            settings.InitialRotation = new FloatRange(0, MathHelper.TwoPi);
            settings.InitialRotationSpeed = new FloatRange(-1f, 1f);

            // Over Lifetime
            settings.Gravity = new Vector2(0, 15f); // A little gravity to pull the dirt down
            settings.StartColor = new Color(139, 119, 103); // SaddleBrown-ish
            settings.EndColor = new Color(92, 77, 66);
            settings.StartAlpha = 0.8f;
            settings.EndAlpha = 0.0f;

            // Rendering
            settings.Texture = ServiceLocator.Get<Texture2D>(); // 1x1 white pixel
            settings.BlendMode = BlendState.AlphaBlend;

            return settings;
        }
    }
}