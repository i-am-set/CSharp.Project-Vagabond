using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            GameBg = Palette_Black;
            TerminalBg = Palette_Black;
            MapBg = Palette_Black;
            GameTextColor = Palette_Sun;
            HighlightTextColor = Palette_Fruit;
            DullTextColor = Palette_Shadow;
            ButtonHoverColor = Palette_DarkSun;
            ButtonDisableColor = Palette_DarkShadow;
            SplitMapNodeColor = Palette_Sun;
            SplitMapPathColor = Palette_DarkRust;
            HoveredCombatantOutline = Palette_DarkShadow;
            OutputTextColor = Palette_LightGray;
            InputTextColor = Palette_Gray;
            ToolTipBGColor = Palette_Black;
            ToolTipTextColor = Palette_Sun;
            ToolTipBorderColor = Palette_Sun;
            TerminalDarkGray = Palette_DarkGray;
            InputCaratColor = Palette_DarkSun;
            AlertColor = Color.Red;
            ConfirmSettingsColor = Palette_Leaf;

            StatColor_Increase = Color.Lime;
            StatColor_Decrease = Color.Red;
            StatColor_Increase_Half = Color.DarkGreen;
            StatColor_Decrease_Half = Color.Brown;

            ColorPositive = Palette_Leaf;
            ColorNegative = Palette_Rust;
            ColorCrit = Palette_DarkSun;
            ColorImmune = Palette_Sky;
            ColorConditionToMeet = Palette_DarkSun;

            ColorPercentageMin = Palette_DarkShadow;
            ColorPercentageMax = Palette_Leaf;

            ColorNarration_Default = GameTextColor;
            ColorNarration_Highlight = HighlightTextColor;
            ColorNarration_Dull = DullTextColor;
            ColorNarration_Prefix = DullTextColor;
            ColorNarration_Action = Palette_Fruit;
            ColorNarration_Spell = Palette_Sky;
            ColorNarration_Item = Palette_Leaf;
            ColorNarration_Critical = Palette_DarkSun;
            ColorNarration_Defeated = Palette_Rust;
            ColorNarration_Escaped = Palette_Sky;
            ColorNarration_Enemy = Palette_Rust;
            ColorNarration_Status = Palette_Shadow;
            ColorNarration_Health = Color.Lime;
            ColorNarration_RestModifier = Color.Magenta;

            DamageIndicatorColor = Color.Crimson;
            HealIndicatorColor = Color.Lime;
            CritcalHitIndicatorColor = Color.Yellow;
            GrazeIndicatorColor = Palette_DarkShadow;
            EffectiveIndicatorColor = Color.Cyan;
            ResistedIndicatorColor = Color.Orange;
            ImmuneIndicatorColor = Palette_Sun;
            ProtectedIndicatorColor = Color.Cyan;
            FailedIndicatorColor = Color.Red;
            TenacityBrokenIndicatorColor = Palette_Rust;
            VulnerableDamageIndicatorColor = Palette_Shadow;

            StatusEffectColors = new Dictionary<StatusEffectType, Color>
            {
                { StatusEffectType.Poison, Palette_Shadow },
                { StatusEffectType.Stun, Palette_DarkSun },
                { StatusEffectType.Regen, Palette_Leaf },
                { StatusEffectType.Dodging, Palette_Sky },
                { StatusEffectType.Burn, Palette_Rust },
                { StatusEffectType.Frostbite, Palette_Sky },
                { StatusEffectType.Silence, Palette_LightGray },
                { StatusEffectType.Protected, Palette_Sun },
                { StatusEffectType.Empowered, Palette_Fruit },
                { StatusEffectType.TargetMe, Palette_Rust },
                { StatusEffectType.Provoked, Palette_Fruit },
                { StatusEffectType.Bleeding, Palette_Rust }
            };
        }

        public static Global Instance => _instance;

        public const string GAME_VERSION = "0.1.0";

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

        public float HoverHapticStrength { get; set; } = 0.75f;
        public float ButtonHapticStrength { get; set; } = 0.5f;

        public int PoisonBaseDamage { get; set; } = 2;
        public float RegenPercent { get; set; } = 0.0625f;
        public float DodgingAccuracyMultiplier { get; set; } = 0.5f;
        public float BurnDamageMultiplier { get; set; } = 2.0f;
        public float FrostbiteAgilityMultiplier { get; set; } = 0.5f;
        public float EmpoweredDamageMultiplier { get; set; } = 1.5f;

        public float HitstopDuration_Normal { get; set; } = 0.1f;
        public float HitstopDuration_Crit { get; set; } = 0.2f;

        public Color BackgroundNoiseColor { get; set; } = new Color(32, 26, 35);
        public float BackgroundNoiseOpacity { get; set; } = 1.0f;
        public float BackgroundNoiseScale { get; set; } = 0.01f;
        public float BackgroundScrollSpeedX { get; set; } = 0.0001f;
        public float BackgroundScrollSpeedY { get; set; } = 0.0001f;
        public float BackgroundDistortionScale { get; set; } = 100.0f;
        public float BackgroundDistortionSpeed { get; set; } = 0.2f;
        public float BackgroundNoiseThreshold { get; set; } = 0.01f;

        public float CrtSaturation { get; set; } = 1.1f;
        public float CrtVibrance { get; set; } = 0.15f;

        public float ProtectAnimationSpeed { get; set; } = 1.0f;
        public int ProtectDamageFrameIndex { get; set; } = 0;

        public Color HealOverlayColor { get; set; } = Color.White;
        public float HealOverlayHangDuration { get; set; } = 0.5f;
        public float HealOverlayFadeDuration { get; set; } = 0.5f;
        public Color ManaOverlayColor { get; set; } = Color.White;

        public Color LowHealthFlashColor { get; set; } = new Color(181, 65, 49);
        public float LowHealthThreshold { get; set; } = 0.50f;
        public float LowHealthFlashSpeedMin { get; set; } = 1.5f;
        public float LowHealthFlashSpeedMax { get; set; } = 4.0f;
        public float LowHealthFlashPatternLength { get; set; } = 7.0f;

        public float SquashRecoverySpeed { get; set; } = 4f;
        public const float ItemHoverScale = 1.2f;

        public float TargetingSingleCycleSpeed { get; set; } = 1.0f;

        public bool ShowDebugOverlays { get; set; } = false;
        public bool ShowSplitMapGrid { get; set; } = false;
        public bool ShowFPS { get; set; } = false;

        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        public int previousScrollValue = Mouse.GetState().ScrollWheelValue;

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
        public Color MapBg { get; private set; }
        public Color GameTextColor { get; private set; }
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

        public Color StatColor_Increase { get; private set; }
        public Color StatColor_Decrease { get; private set; }
        public Color StatColor_Increase_Half { get; private set; }
        public Color StatColor_Decrease_Half { get; private set; }

        public Color ColorPositive { get; private set; }
        public Color ColorNegative { get; private set; }
        public Color ColorCrit { get; private set; }
        public Color ColorImmune { get; private set; }
        public Color ColorConditionToMeet { get; private set; }

        public Color ColorPercentageMin { get; private set; }
        public Color ColorPercentageMax { get; private set; }

        public Color ColorNarration_Default { get; private set; }
        public Color ColorNarration_Highlight { get; private set; }
        public Color ColorNarration_Dull { get; private set; }
        public Color ColorNarration_Prefix { get; private set; }
        public Color ColorNarration_Action { get; private set; }
        public Color ColorNarration_Spell { get; private set; }
        public Color ColorNarration_Item { get; private set; }
        public Color ColorNarration_Critical { get; private set; }
        public Color ColorNarration_Defeated { get; private set; }
        public Color ColorNarration_Escaped { get; private set; }
        public Color ColorNarration_Enemy { get; private set; }
        public Color ColorNarration_Status { get; private set; }
        public Color ColorNarration_Health { get; private set; }
        public Color ColorNarration_RestModifier { get; private set; }

        public Color DamageIndicatorColor { get; set; }
        public Color HealIndicatorColor { get; set; }
        public Color CritcalHitIndicatorColor { get; set; }
        public Color GrazeIndicatorColor { get; set; }
        public Color EffectiveIndicatorColor { get; set; }
        public Color ResistedIndicatorColor { get; set; }
        public Color ImmuneIndicatorColor { get; set; }
        public Color ProtectedIndicatorColor { get; set; }
        public Color FailedIndicatorColor { get; set; }
        public Color TenacityBrokenIndicatorColor { get; set; }
        public Color VulnerableDamageIndicatorColor { get; set; }

        public Dictionary<StatusEffectType, Color> StatusEffectColors { get; private set; }

        public Color GetNarrationColor(string tag)
        {
            string lowerTag = tag.ToLowerInvariant();

            if (lowerTag.StartsWith("c"))
            {
                if (lowerTag == "cstr") return Palette_Sun;
                if (lowerTag == "cint") return Palette_Sun;
                if (lowerTag == "cten") return Palette_Sun;
                if (lowerTag == "cagi") return Palette_Sun;

                if (lowerTag == "cpositive") return ColorPositive;
                if (lowerTag == "cnegative") return ColorNegative;
                if (lowerTag == "ccrit") return ColorCrit;
                if (lowerTag == "cimmune") return ColorImmune;
                if (lowerTag == "cctm") return ColorConditionToMeet;
                if (lowerTag == "cvulnerable") return VulnerableDamageIndicatorColor;
                if (lowerTag == "cetc") return Palette_DarkShadow;

                string effectName = lowerTag.Substring(1);
                foreach (var kvp in StatusEffectColors)
                {
                    if (kvp.Key.ToString().ToLowerInvariant() == effectName) return kvp.Value;
                }

                if (lowerTag == "cdefault") return ColorNarration_Default;
                if (lowerTag == "chighlight") return ColorNarration_Highlight;
                if (lowerTag == "cdull") return ColorNarration_Dull;
                if (lowerTag == "cprefix") return ColorNarration_Prefix;
                if (lowerTag == "caction") return ColorNarration_Action;
                if (lowerTag == "cspell") return ColorNarration_Spell;
                if (lowerTag == "citem") return ColorNarration_Item;
                if (lowerTag == "cdefeat") return ColorNarration_Defeated;
                if (lowerTag == "cescape") return ColorNarration_Escaped;
                if (lowerTag == "cenemy") return ColorNarration_Enemy;
                if (lowerTag == "cstatus") return ColorNarration_Status;
                if (lowerTag == "cslot") return ColorNarration_Default;
                if (lowerTag == "cgraze") return GrazeIndicatorColor;
                if (lowerTag == "chealth") return ColorNarration_Health;
                if (lowerTag == "cmodifier") return ColorNarration_RestModifier;

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