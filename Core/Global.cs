using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            GameBg = Palette_Off;
            TerminalBg = Palette_Black;
            GameTextColor = Palette_Sun;
            EmphasisTextColor = Palette_DarkSun;
            HighlightTextColor = Palette_Fruit;
            DullTextColor = Palette_Black;
            ButtonHoverColor = Palette_DarkSun;
            ButtonDisableColor = Palette_Black;
            SplitMapNodeColor = Palette_Sun;
            SplitMapPathColor = Palette_DarkRust;
            HoveredCombatantOutline = Palette_DarkShadow;
            OutputTextColor = Palette_LightPale;
            InputTextColor = Palette_Gray;
            ToolTipBGColor = Palette_Black;
            ToolTipTextColor = Palette_Sun;
            ToolTipBorderColor = Palette_Sun;
            TerminalDarkGray = Palette_DarkGray;
            InputCaratColor = Palette_DarkSun;
            AlertColor = Color.Red;
            ConfirmSettingsColor = Palette_Leaf;
        }

        public static Global Instance => _instance;

        public const string GAME_VERSION = "0.1.0";

        public float SpeedUpMultiplier { get; set; } = 3.0f;

        public float UI_ButtonHoverLift { get; set; } = -1f;
        public float UI_ButtonHoverDuration { get; set; } = 0.0f;

        public const float PHYSICS_UPDATES_PER_SECOND = 60f;
        public const float FIXED_PHYSICS_TIMESTEP = 1f / PHYSICS_UPDATES_PER_SECOND;

        public const int VIRTUAL_WIDTH = 320;
        public const int VIRTUAL_HEIGHT = 180;

        public const int TERMINAL_LINE_SPACING = 12;
        public const int SPLIT_MAP_GRID_SIZE = 16;

        public const int MAX_SINGLE_MOVE_LIMIT = 20;
        public const int MAX_HISTORY_LINES = 200;
        public const int TERMINAL_HEIGHT = 300;
        public const float MIN_BACKSPACE_DELAY = 0.02f;
        public const float BACKSPACE_ACCELERATION = 0.25f;

        public const float DEFAULT_OVERFLOW_SCROLL_SPEED = 20.0f;

        public const float VALUE_DISPLAY_WIDTH = 120f;

        public const int APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING = 5;
        public const float TOOLTIP_AVERAGE_POPUP_TIME = 0.5f;
        public const int TERMINAL_Y = 25;

        public const float UniversalSlowFadeDuration = 3.0f;

        public bool DebugGodMode { get; set; } = false;

        public float HoverHapticStrength { get; set; } = 0.25f;
        public float ButtonHapticStrength { get; set; } = 0.5f;
        public float ButtonHapticDuration { get; set; } = 0.15f;
        public float LightHapticZoomPulseStrength { get; set; } = 1.0025f;
        public float HapticZoomPulseStrength { get; set; } = 1.02f;
        public float HapticZoomPulseDuration { get; set; } = 0.05f;

        public float Saturation { get; set; } = 1.1f;
        public float Vibrance { get; set; } = 0.15f;

        public bool ShowDebugOverlays { get; set; } = false;
        public bool ShowSplitMapGrid { get; set; } = false;
        public bool ShowFPS { get; set; } = false;

        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        public int previousScrollValue = Mouse.GetState().ScrollWheelValue;

        private Vector3[] _cachedShaderPalette;

        public Vector3[] GetPaletteAsVectors()
        {
            if (_cachedShaderPalette == null)
            {
                _cachedShaderPalette = new Vector3[]
                {
                Palette_Leaf.ToVector3(),
                Palette_Sky.ToVector3(),
                Palette_Sea.ToVector3(),
                Palette_DarkestPale.ToVector3(),
                Palette_DarkPale.ToVector3(),
                Palette_Pale.ToVector3(),
                Palette_LightPale.ToVector3(),
                Palette_Sun.ToVector3(),
                Palette_DarkSun.ToVector3(),
                Palette_Fruit.ToVector3(),
                Palette_Rust.ToVector3(),
                Palette_DarkRust.ToVector3(),
                Palette_Shadow.ToVector3(),
                Palette_DarkShadow.ToVector3(),
                Palette_Black.ToVector3(),
                Palette_Off.ToVector3()
                };
            }
            return _cachedShaderPalette;
        }

        public Color Palette_Leaf { get; set; } = new Color(145, 183, 115);
        public Color Palette_Sky { get; set; } = new Color(88, 148, 138);
        public Color Palette_Sea { get; set; } = new Color(63, 86, 109);

        public Color Palette_DarkestPale { get; set; } = new Color(68, 56, 70);
        public Color Palette_DarkPale { get; set; } = new Color(102, 89, 100);
        public Color Palette_Pale { get; set; } = new Color(153, 127, 115);
        public Color Palette_LightPale { get; set; } = new Color(176, 169, 135);

        public Color Palette_Sun { get; set; } = new Color(242, 236, 139);
        public Color Palette_DarkSun { get; set; } = new Color(251, 185, 84);
        public Color Palette_Fruit { get; set; } = new Color(205, 104, 61);
        public Color Palette_Rust { get; set; } = new Color(153, 61, 65);
        public Color Palette_DarkRust { get; set; } = new Color(122, 48, 69);

        public Color Palette_Shadow { get; set; } = new Color(69, 41, 63);
        public Color Palette_DarkShadow { get; set; } = new Color(46, 34, 47);

        public Color Palette_LightGray { get; set; } = new Color(85, 96, 125);
        public Color Palette_Gray { get; set; } = new Color(62, 65, 95);
        public Color Palette_DarkGray { get; set; } = new Color(42, 40, 57);
        public Color Palette_DarkerGray { get; set; } = new Color(36, 35, 46);
        public Color Palette_DarkestGray { get; set; } = new Color(26, 25, 33);
        public Color Palette_Black { get; set; } = new Color(25, 22, 28);
        public Color Palette_Off { get; set; } = new Color(10, 9, 11);

        public Color Palette_Pink { get; set; } = Color.Pink;
        public Color Palette_Purple { get; set; } = Color.Purple;
        public Color Palette_Red { get; set; } = Color.Red;
        public Color Palette_Orange { get; set; } = Color.Orange;
        public Color Palette_Yellow { get; set; } = Color.Yellow;
        public Color Palette_Green { get; set; } = Color.Green;
        public Color Palette_Blue { get; set; } = Color.CornflowerBlue;
        public Color Palette_White { get; set; } = Color.White;

        public Color PlayerColor { get; private set; } = new Color(181, 65, 49);
        public Color GameBg { get; private set; }
        public Color TerminalBg { get; private set; }
        public Color GameTextColor { get; private set; }
        public Color EmphasisTextColor { get; private set; }
        public Color HighlightTextColor { get; private set; }
        public Color DullTextColor { get; private set; }
        public Color ButtonHoverColor { get; private set; }
        public Color ButtonDisableColor { get; private set; }
        public Color SplitMapNodeColor { get; private set; }
        public Color SplitMapPathColor { get; private set; }
        public Color HoveredCombatantOutline { get; private set; }
        public Color OutputTextColor { get; private set; }
        public Color InputTextColor { get; private set; }
        public Color ToolTipBGColor { get; private set; }
        public Color ToolTipTextColor { get; private set; }
        public Color ToolTipBorderColor { get; private set; }
        public Color TerminalDarkGray { get; set; }
        public Color InputCaratColor { get; set; }
        public Color AlertColor { get; private set; }
        public Color ConfirmSettingsColor { get; private set; }

        public Color GetNarrationColor(string tag)
        {
            string lowerTag = tag.ToLowerInvariant();

            if (lowerTag.StartsWith("c"))
            {
                if (lowerTag == "cred") return Palette_Rust;
                if (lowerTag == "cyellow") return Palette_DarkSun;
                if (lowerTag == "cwhite") return Palette_Sun;
                if (lowerTag == "cpurple") return Palette_Shadow;
                if (lowerTag == "cblue") return Palette_Sky;
                if (lowerTag == "cgreen") return Palette_Leaf;
                if (lowerTag == "cpink") return Palette_Shadow;
                if (lowerTag == "corange") return Palette_Fruit;
            }

            switch (lowerTag)
            {
                case "red": return Palette_Rust;
                case "blue": return Palette_Sky;
                case "green": return Palette_Leaf;
                case "yellow": return Palette_DarkSun;
                case "orange": return Palette_Fruit;
                case "purple": return Palette_Shadow;
                case "pink": return Palette_Shadow;
                case "gray": return Palette_Gray;
                case "white": return Palette_Sun;
                case "black": return Palette_Black;
                default: return Palette_Sun;
            }
        }
    }
}