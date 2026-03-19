using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Audio;
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

        // Visual Style Settings
        public bool EnableGlitchEffects { get; set; }

        // Game Settings
        public bool UseImperialUnits { get; set; }
        public bool Use24HourClock { get; set; }

        // Audio Settings
        public float MasterVolume { get; set; }
        public float MusicVolume { get; set; }
        public float SfxVolume { get; set; }
        public float AmbientVolume { get; set; }
        public float UiVolume { get; set; }

        public GameSettings()
        {
            Resolution = new Point(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            Mode = WindowMode.Windowed;
            IsVsync = true;
            IsFrameLimiterEnabled = true;
            TargetFramerate = 60;
            SmallerUi = false;
            DisplayIndex = 0;
            Gamma = 1.5f;

            EnableGlitchEffects = true;

            UseImperialUnits = false;
            Use24HourClock = false;

            MasterVolume = 1.0f;
            MusicVolume = 1.0f;
            SfxVolume = 1.0f;
            AmbientVolume = 1.0f;
            UiVolume = 1.0f;
        }

        public void ApplyGraphicsSettings(GraphicsDeviceManager gdm, Core game)
        {
            gdm.GraphicsProfile = GraphicsProfile.HiDef;

            if (Mode == WindowMode.Fullscreen)
            {
                gdm.SynchronizeWithVerticalRetrace = IsVsync;
            }
            else
            {
                gdm.SynchronizeWithVerticalRetrace = false;
            }

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

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
                gdm.PreferredBackBufferWidth = displayMode.Width;
                gdm.PreferredBackBufferHeight = displayMode.Height;
            }
            else
            {
                gdm.IsFullScreen = false;
                game.Window.IsBorderless = false;

                gdm.PreferredBackBufferWidth = Resolution.X;
                gdm.PreferredBackBufferHeight = Resolution.Y;
            }

            gdm.ApplyChanges();

            if (Mode == WindowMode.Borderless)
            {
                game.Window.Position = Point.Zero;
            }
            else if (Mode == WindowMode.Windowed)
            {
                int screenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                int screenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

                int windowWidth = game.Window.ClientBounds.Width;
                int windowHeight = game.Window.ClientBounds.Height;

                int centerX = (screenWidth - windowWidth) / 2;
                int centerY = (screenHeight - windowHeight) / 2;

                game.Window.Position = new Point(centerX, Math.Max(20, centerY));
            }

            game.OnResize(null, null);
        }

        public void ApplyGameSettings()
        {
            var global = ServiceLocator.Get<Global>();
            global.UseImperialUnits = UseImperialUnits;
            global.Use24HourClock = Use24HourClock;

            var audioManager = ServiceLocator.Get<AudioManager>();
            audioManager.SetVolumes(MasterVolume, MusicVolume, SfxVolume, AmbientVolume, UiVolume);
        }
    }
}