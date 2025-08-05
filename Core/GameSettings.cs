using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond
{
    public enum WindowMode
    {
        Windowed,
        Fullscreen,
        Borderless
    }

    public class GameSettings
    {
        // Graphics Settings
        public Point Resolution { get; set; }
        public WindowMode Mode { get; set; }
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
            Mode = WindowMode.Windowed;
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

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            bool isNativeResolution = (Resolution.X == displayMode.Width && Resolution.Y == displayMode.Height);

            WindowMode effectiveMode = Mode;
            // If user selects Windowed mode but at their native resolution, upgrade it to Borderless
            // to provide the best fit experience that respects the taskbar.
            if (Mode == WindowMode.Windowed && isNativeResolution)
            {
                effectiveMode = WindowMode.Borderless;
            }

            // Handle window mode logic based on the effective mode
            if (effectiveMode == WindowMode.Fullscreen)
            {
                gdm.IsFullScreen = true;
                game.Window.IsBorderless = false;
                gdm.PreferredBackBufferWidth = Resolution.X;
                gdm.PreferredBackBufferHeight = Resolution.Y;
            }
            else if (effectiveMode == WindowMode.Borderless)
            {
                gdm.IsFullScreen = false;
                game.Window.IsBorderless = true;
                gdm.PreferredBackBufferWidth = displayMode.Width;
                gdm.PreferredBackBufferHeight = displayMode.Height;
            }
            else // WindowMode.Windowed (and not native resolution)
            {
                gdm.IsFullScreen = false;
                game.Window.IsBorderless = false;
                gdm.PreferredBackBufferWidth = Resolution.X;
                gdm.PreferredBackBufferHeight = Resolution.Y;
            }

            // Apply all pending graphics changes
            gdm.ApplyChanges();

            // Post-apply adjustments for window position
            if (effectiveMode == WindowMode.Borderless)
            {
                game.Window.Position = Point.Zero;
            }
            else if (effectiveMode == WindowMode.Windowed)
            {
                // Center the window on the screen
                int centerX = (displayMode.Width - game.Window.ClientBounds.Width) / 2;
                int centerY = (displayMode.Height - game.Window.ClientBounds.Height) / 2;
                game.Window.Position = new Point(centerX, centerY);
            }

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