using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

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
        public int DisplayIndex { get; set; }
        public float Gamma { get; set; }

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
            DisplayIndex = 0;
            Gamma = 1.5f;

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
            // Ensure HiDef profile for best performance/compatibility
            gdm.GraphicsProfile = GraphicsProfile.HiDef;

            // NOTE: IsFixedTimeStep is now ALWAYS false in Core.cs to prevent windowed mode stutter.
            // The frame limiter is handled manually in Core.Update.

            // VSync Logic:
            // In Windowed/Borderless, DWM enforces VSync. Enabling MonoGame's VSync often causes stutter due to double-waiting.
            // In Fullscreen, MonoGame's VSync works as expected.
            if (Mode == WindowMode.Fullscreen)
            {
                gdm.SynchronizeWithVerticalRetrace = IsVsync;
            }
            else
            {
                // In windowed mode, we generally want this OFF to let DWM handle composition timing.
                // However, if the user specifically requested VSync, we can try to honor it, 
                // but usually 'false' is smoother on Windows 10/11.
                // For now, we force it false in windowed mode to solve the "choppy" report.
                gdm.SynchronizeWithVerticalRetrace = false;
            }

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

            // Handle window mode logic directly based on the user's selection.
            if (Mode == WindowMode.Fullscreen)
            {
                gdm.IsFullScreen = true;
                game.Window.IsBorderless = false;
                gdm.PreferredBackBufferWidth = Resolution.X;
                gdm.PreferredBackBufferHeight = Resolution.Y;
            }
            else if (Mode == WindowMode.Borderless)
            {
                gdm.IsFullScreen = false;
                game.Window.IsBorderless = true;
                // For borderless, use the native resolution of the default monitor.
                gdm.PreferredBackBufferWidth = displayMode.Width;
                gdm.PreferredBackBufferHeight = displayMode.Height;
            }
            else // WindowMode.Windowed
            {
                gdm.IsFullScreen = false;
                game.Window.IsBorderless = false;

                gdm.PreferredBackBufferWidth = Resolution.X;
                gdm.PreferredBackBufferHeight = Resolution.Y;
            }

            gdm.ApplyChanges();

            // Post-apply adjustments for window position
            if (Mode == WindowMode.Borderless)
            {
                // For borderless, we want the window at the top-left of its monitor.
                game.Window.Position = Point.Zero;
            }
            else if (Mode == WindowMode.Windowed)
            {
                // Calculate center position relative to the primary display.
                int screenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                int screenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

                int windowWidth = game.Window.ClientBounds.Width;
                int windowHeight = game.Window.ClientBounds.Height;

                int centerX = (screenWidth - windowWidth) / 2;
                int centerY = (screenHeight - windowHeight) / 2;

                // Ensure the Y position is at least a small positive value (e.g., 20 pixels)
                // to keep the title bar on screen and visible.
                game.Window.Position = new Point(centerX, Math.Max(20, centerY));
            }

            game.OnResize(null, null);
        }

        /// <summary>
        /// Applies the general game settings to the Global configuration object.
        /// </summary>
        public void ApplyGameSettings()
        {
            var global = ServiceLocator.Get<Global>();
            global.UseImperialUnits = UseImperialUnits;
            global.Use24HourClock = Use24HourClock;
        }
    }
}