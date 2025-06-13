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

        // Game Settings
        public bool UseImperialUnits { get; set; }
        public bool Use24HourClock { get; set; }

        // Controls (Placeholder for future implementation)
        // ...

        public GameSettings()
        {
            // Set default values
            Resolution = new Point(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            IsFullscreen = false;
            IsVsync = true;
            IsFrameLimiterEnabled = true;
            TargetFramerate = 60;
            UseImperialUnits = Global.Instance.UseImperialUnits;
            Use24HourClock = Global.Instance.Use24HourClock;
        }

        /// <summary>
        /// Applies the current graphics settings to the game instance.
        /// This is a more robust method that sets all properties before applying.
        /// </summary>
        public void ApplyGraphicsSettings(GraphicsDeviceManager gdm, Core game)
        {
            // 1. Apply settings that affect the Game object directly (the update loop)
            game.IsFixedTimeStep = IsFrameLimiterEnabled;
            if (IsFrameLimiterEnabled)
            {
                game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFramerate);
            }

            // 2. Set all desired properties on the GraphicsDeviceManager
            gdm.SynchronizeWithVerticalRetrace = IsVsync;
            gdm.IsFullScreen = IsFullscreen;
            Core.ResizeWindow(Resolution.X, Resolution.Y);

            // 3. Apply all changes at once.
            gdm.ApplyChanges();
        }

        /// <summary>
        /// Applies the general game settings.
        /// </summary>
        public void ApplyGameSettings()
        {
            Global.Instance.UseImperialUnits = UseImperialUnits;
            Global.Instance.Use24HourClock = Use24HourClock;
        }
    }
}