using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            WaterColor = palette_DarkBlue;
            FlatlandColor = palette_Gray;
            HillColor = palette_Gray;
            MountainColor = palette_Gray;
            PlayerColor = palette_Red;
            PathColor = palette_Yellow;
            RunPathColor = palette_Orange;
            PathEndColor = palette_Red;
            ShortRestColor = palette_LightPurple;
            LongRestColor = palette_LightPurple;
            GameBg = palette_Black;
            TerminalBg = palette_Black;
            MapBg = palette_Black;
            TextColor = Color.White;
            OutputTextColor = Color.Gray;
        }

        public static Global Instance => _instance;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTANTS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Map settings Global
        public const int GRID_SIZE = 32;
        public const int GRID_CELL_SIZE = 10;
        public const int MAP_WIDTH = GRID_SIZE * GRID_CELL_SIZE + 10;
        public const int FONT_SIZE = 12;
        public const int TERMINAL_LINE_SPACING = 12;
        public const int PROMPT_LINE_SPACING = 16;
        public const float NOISE_SCALE = 0.2f;
        public const int DEFAULT_TERMINAL_WIDTH = 700;

        // Player stats Global
        public const int MAX_MAX_HEALTH_ENERGY = 48;
        public const int MIN_MAX_HEALTH_ENERGY = 1;

        // Input system Global
        public const float MOVE_DELAY_SECONDS = 0.5f;
        public const int MAX_SINGLE_MOVE_LIMIT = 20;
        public const int MAX_HISTORY_LINES = 200;
        public const int TERMINAL_HEIGHT = 600;
        public const float MIN_BACKSPACE_DELAY = 0.02f;
        public const float BACKSPACE_ACCELERATION = 0.25f;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // INSTANCE VARIABLES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        
        // Core variables
        public SpriteFont DefaultFont { get; set; }
        public GraphicsDeviceManager CurrentGraphics;
        public SpriteBatch CurrentSpriteBatch;

        // Settings variables
        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        // Input variables
        public int previousScrollValue = Mouse.GetState().ScrollWheelValue;

        // Terrain levels
        public float WaterLevel { get; set; } = 0.3f;
        public float FlatlandsLevel { get; set; } = 0.6f;
        public float HillsLevel { get; set; } = 0.7f;
        public float MountainsLevel { get; set; } = 0.8f;

        // Static Color Palette
        public Color palette_Black { get; set; } = new Color(23, 22, 28);
        public Color palette_DarkGray { get; set; } = new Color(46, 44, 59); // #2E2C3B
        public Color palette_Gray { get; set; } = new Color(62, 65, 95); // #3E415F
        public Color palette_LightGray { get; set; } = new Color(85, 96, 125); // #55607D
        public Color palette_White { get; set; } = new Color(116, 125, 136); // #747D88
        public Color palette_Teal { get; set; } = new Color(65, 222, 149); // #41DE95
        public Color palette_LightBlue { get; set; } = new Color(42, 164, 170); // #2AA4AA
        public Color palette_DarkBlue { get; set; } = new Color(59, 119, 166); // #3B77A6
        public Color palette_DarkGreen { get; set; } = new Color(36, 147, 55); // #249337
        public Color palette_LightGreen { get; set; } = new Color(86, 190, 68); // #56BE44
        public Color palette_LightYellow { get; set; } = new Color(198, 222, 120); // #C6DE78
        public Color palette_Yellow { get; set; } = new Color(243, 194, 32); // #F3C220
        public Color palette_Orange { get; set; } = new Color(196, 101, 28); // #C4651C
        public Color palette_Red { get; set; } = new Color(181, 65, 49); // #B54131
        public Color palette_DarkPurple { get; set; } = new Color(97, 64, 122); // #61407A
        public Color palette_LightPurple { get; set; } = new Color(143, 61, 167); // #8F3DA7
        public Color palette_Pink { get; set; } = new Color(234, 97, 157); // #EA619D
        public Color palette_BrightWhite { get; set; } = new Color(193, 229, 234); // #C1E5EA

        // Colors
        public Color WaterColor { get; private set; }
        public Color FlatlandColor { get; private set; }
        public Color HillColor { get; private set; }
        public Color MountainColor { get; private set; }
        public Color PlayerColor { get; private set; }
        public Color PathColor { get; private set; }
        public Color RunPathColor { get; private set; }
        public Color PathEndColor { get; private set; }
        public Color ShortRestColor { get; private set; }
        public Color LongRestColor { get; private set; }
        public Color GameBg { get; private set; }
        public Color TerminalBg { get; private set; }
        public Color MapBg { get; private set; }
        public Color TextColor { get; private set; }
        public Color OutputTextColor { get; private set; }
    }
}