using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond
{
    public class GameSettings
    {
        // Graphics Settings
        public Point Resolution { get; set; }
        public bool IsFullscreen { get; set; }
        public bool IsVsync { get; set; }
        public bool IsFrameLimiterEnabled { get; set; }
        public int TargetFramerate { get; set; }
        public bool SmallerUi { get; set; }

        // Game Settings
        public bool UseImperialUnits { get; set; }
        public bool Use24HourClock { get; set; }

        // Controls
        // ...

        public GameSettings()
        {
            // Default graphics settings
            Resolution = new Point(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            IsFullscreen = false;
            IsVsync = true;
            IsFrameLimiterEnabled = true;
            TargetFramerate = 60;
            SmallerUi = false;

            // Default game settings. This class is the source of truth for defaults.
            UseImperialUnits = false;
            Use24HourClock = false;
        }

        /// <summary>
        /// Applies the current graphics settings to the game instance.
        /// This is a more robust method that sets all properties before applying.
        /// </summary>
        public void ApplyGraphicsSettings(GraphicsDeviceManager gdm, Core game)
        {
            game.IsFixedTimeStep = IsFrameLimiterEnabled;
            if (IsFrameLimiterEnabled)
            {
                game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFramerate);
            }

            gdm.SynchronizeWithVerticalRetrace = IsVsync;
            gdm.IsFullScreen = IsFullscreen;

            // Set the new resolution directly instead of using a static helper
            gdm.PreferredBackBufferWidth = Resolution.X;
            gdm.PreferredBackBufferHeight = Resolution.Y;

            // Apply all pending graphics changes
            gdm.ApplyChanges();

            // Notify the game of the resize to recalculate the render area
            game.OnResize(null, null);
        }

        /// <summary>
        /// Applies the general game settings to the Global configuration object.
        /// </summary>
        public void ApplyGameSettings()
        {
            // Get the Global instance from the ServiceLocator to apply settings
            var global = ServiceLocator.Get<Global>();
            global.UseImperialUnits = UseImperialUnits;
            global.Use24HourClock = Use24HourClock;
        }
    }
}