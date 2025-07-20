﻿﻿﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            WaterColor = Palette_DarkBlue;
            FlatlandColor = Palette_Gray;
            HillColor = Palette_Gray;
            MountainColor = Palette_Gray;
            PlayerColor = Palette_Red;
            PathColor = Palette_Yellow;
            RunPathColor = Palette_Orange;
            PathEndColor = Palette_Red;
            ShortRestColor = Palette_LightPurple;
            LongRestColor = Palette_LightPurple;
            GameBg = Palette_Black;
            TerminalBg = Palette_Black;
            MapBg = Palette_Black;
            GameTextColor = Palette_LightGray;
            ButtonHoverColor = Palette_Red;
            ButtonDisableColor = Palette_DarkGray;
            OutputTextColor = Palette_LightGray;
            InputTextColor = Palette_Gray;
            ToolTipBGColor = Palette_Black;
            ToolTipTextColor = Palette_BrightWhite;
            ToolTipBorderColor = Palette_BrightWhite;
            TerminalDarkGray = Palette_DarkGray;
            InputCaratColor = Color.Khaki;
            CombatSelectorColor = Palette_Yellow;
            CombatSelectableColor = Palette_Red;
            CombatInstructionColor = Palette_Yellow;
        }

        public static Global Instance => _instance;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTANTS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Game version
        public const string GAME_VERSION = "0.1.0";

        // World constants
        public const float GAME_SECONDS_PER_REAL_SECOND = 8f;
        public const float FEET_PER_WORLD_TILE = 377f; // The physical distance of a single world tile
        public const float FEET_PER_LOCAL_TILE = FEET_PER_WORLD_TILE / LOCAL_GRID_SIZE; // Approx 5.89 feet
        public const float FEET_PER_SECOND_PER_SPEED_UNIT = 4.0f; // A character with speed 1.0 moves at 3 ft/s.

        // Virtual resolution for fixed aspect ratio rendering
        public const int VIRTUAL_WIDTH = 960;
        public const int VIRTUAL_HEIGHT = 540;

        // Map settings Global
        public const int LOCAL_GRID_SIZE = 64;
        public const int LOCAL_GRID_CELL_SIZE = 5;
        public const int GRID_SIZE = 32;
        public const int GRID_CELL_SIZE = 10;
        public const int MAP_WIDTH = GRID_SIZE * GRID_CELL_SIZE + 10;
        public const int FONT_SIZE = 12;
        public const int TERMINAL_LINE_SPACING = 12;
        public const int PROMPT_LINE_SPACING = 16;
        public const float NOISE_SCALE = 0.2f;
        public const int DEFAULT_TERMINAL_WIDTH = 540;
        public const int DEFAULT_TERMINAL_HEIGHT = 338;
        public const int COMBAT_TERMINAL_BUFFER = 130;

        // Player stats Global
        public const int MAX_MAX_HEALTH_ENERGY = 48;
        public const int MIN_MAX_HEALTH_ENERGY = 1;

        // Input system Global
        public const int MAX_SINGLE_MOVE_LIMIT = 20;
        public const int MAX_HISTORY_LINES = 200;
        public const int TERMINAL_HEIGHT = 600;
        public const float MIN_BACKSPACE_DELAY = 0.02f;
        public const float BACKSPACE_ACCELERATION = 0.25f;

        // UI settings Global
        public const float DEFAULT_OVERFLOW_SCROLL_SPEED = 20.0f;
        public const float VALUE_DISPLAY_WIDTH = 110f;
        public const int APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING = 5;
        public const float TOOLTIP_AVERAGE_POPUP_TIME = 0.5f;
        public const int TERMINAL_Y = 50;

        // Combat settings Global
        public const int COMBAT_TURN_DURATION_SECONDS = 15;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // INSTANCE VARIABLES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Settings variables
        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        // Time scale multipliers
        public float TimeScaleMultiplier1 { get; set; } = 1.0f;
        public float TimeScaleMultiplier2 { get; set; } = 5.0f;
        public float TimeScaleMultiplier3 { get; set; } = 10.0f;

        // Input variables
        public int previousScrollValue = Mouse.GetState().ScrollWheelValue;

        // Terrain levels
        public float WaterLevel { get; set; } = 0.3f;
        public float FlatlandsLevel { get; set; } = 0.6f;
        public float HillsLevel { get; set; } = 0.7f;
        public float MountainsLevel { get; set; } = 0.8f;

        // Static Color Palette
        public Color Palette_Black { get; set; } = new Color(23, 22, 28);
        public Color Palette_DarkGray { get; set; } = new Color(46, 44, 59); // #2E2C3B
        public Color Palette_Gray { get; set; } = new Color(62, 65, 95); // #3E415F
        public Color Palette_LightGray { get; set; } = new Color(85, 96, 125); // #55607D
        public Color Palette_White { get; set; } = new Color(116, 125, 136); // #747D88
        public Color Palette_Teal { get; set; } = new Color(65, 222, 149); // #41DE95
        public Color Palette_LightBlue { get; set; } = new Color(42, 164, 170); // #2AA4AA
        public Color Palette_DarkBlue { get; set; } = new Color(59, 119, 166); // #3B77A6
        public Color Palette_DarkGreen { get; set; } = new Color(36, 147, 55); // #249337
        public Color Palette_LightGreen { get; set; } = new Color(86, 190, 68); // #56BE44
        public Color Palette_LightYellow { get; set; } = new Color(198, 222, 120); // #C6DE78
        public Color Palette_Yellow { get; set; } = new Color(243, 194, 32); // #F3C220
        public Color Palette_Orange { get; set; } = new Color(196, 101, 28); // #C4651C
        public Color Palette_Red { get; set; } = new Color(181, 65, 49); // #B54131
        public Color Palette_DarkPurple { get; set; } = new Color(97, 64, 122); // #61407A
        public Color Palette_LightPurple { get; set; } = new Color(143, 61, 167); // #8F3DA7
        public Color Palette_Pink { get; set; } = new Color(234, 97, 157); // #EA619D
        public Color Palette_BrightWhite { get; set; } = new Color(193, 229, 234); // #C1E5EA

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
        public Color GameTextColor { get; private set; }
        public Color ButtonHoverColor { get; private set; }
        public Color ButtonDisableColor { get; private set; }
        public Color OutputTextColor { get; private set; }
        public Color InputTextColor { get; private set; }
        public Color ToolTipBGColor { get; private set; }
        public Color ToolTipTextColor { get; private set; }
        public Color ToolTipBorderColor { get; private set; }
        public Color TerminalDarkGray { get; set; }
        public Color InputCaratColor { get; set; }
        public Color CombatSelectorColor { get; set; }
        public Color CombatSelectableColor { get; set; }
        public Color CombatInstructionColor { get; set; }
    }
}
