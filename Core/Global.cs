using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Items;
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
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            // Initialize UI Colors
            GameBg = Palette_Black;
            TerminalBg = Palette_Black;
            MapBg = Palette_Black;
            GameTextColor = Palette_DarkSun;
            HighlightTextColor = Palette_Fruit;
            DullTextColor = Palette_Shadow;
            ButtonHoverColor = Palette_Sun;
            ButtonDisableColor = Palette_DarkShadow;
            SplitMapNodeColor = Palette_DarkSun;
            SplitMapPathColor = Palette_DarkRust;
            OutputTextColor = Palette_LightGray;
            InputTextColor = Palette_Gray;
            ToolTipBGColor = Palette_Black;
            ToolTipTextColor = Palette_Sun;
            ToolTipBorderColor = Palette_Sun;
            TerminalDarkGray = Palette_DarkGray;
            InputCaratColor = Palette_DarkSun;
            AlertColor = Color.Red;
            ConfirmSettingsColor = Palette_Leaf;

            // Initialize Stat Colors
            StatColor_Increase = Color.Lime;
            StatColor_Decrease = Color.Red;
            StatColor_Increase_Half = Color.DarkGreen;
            StatColor_Decrease_Half = Color.Brown;

            // Initialize Generic Feedback Colors
            ColorPositive = Palette_Leaf;
            ColorNegative = Palette_Rust;
            ColorCrit = Palette_DarkSun;
            ColorImmune = Palette_Sky;
            ColorConditionToMeet = Palette_DarkSun;

            // Initialize Percentage Gradient Colors
            ColorPercentageMin = Palette_DarkShadow;
            ColorPercentageMax = Palette_Leaf;

            // Initialize Narration Colors
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

            // Initialize Combat Indicator Colors
            DamageIndicatorColor = Color.Crimson;
            HealIndicatorColor = Color.Lime;
            CritcalHitIndicatorColor = Color.Yellow;
            GrazeIndicatorColor = Palette_DarkShadow;
            EffectiveIndicatorColor = Color.Cyan;
            ResistedIndicatorColor = Color.Orange;
            ImmuneIndicatorColor = Palette_Sun;
            ProtectedIndicatorColor = Color.Cyan;
            FailedIndicatorColor = Color.Red;

            // Initialize Color Mappings
            ElementColors = new Dictionary<int, Color>
            {
                { 0, Palette_Sun },       // Neutral
                { 1, Palette_Rust },         // Fire
                { 2, Palette_Sky },        // Water
                { 3, Palette_Leaf },       // Nature
                { 4, Palette_Shadow },        // Arcane
                { 5, Palette_DarkSun }, // Divine
                { 6, Palette_Shadow }       // Blight
            };

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

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTANTS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Game version
        public const string GAME_VERSION = "0.1.0";

        // Physics constants
        public const float PHYSICS_UPDATES_PER_SECOND = 60f;
        public const float FIXED_PHYSICS_TIMESTEP = 1f / PHYSICS_UPDATES_PER_SECOND;

        // Virtual resolution for fixed aspect ratio rendering
        public const int VIRTUAL_WIDTH = 320;
        public const int VIRTUAL_HEIGHT = 180;

        // Map settings Global
        public const int TERMINAL_LINE_SPACING = 12;
        public const int SPLIT_MAP_GRID_SIZE = 16;
        // Player stats Global
        public const int MAX_MAX_HEALTH_ENERGY = 48;
        public const int MIN_MAX_HEALTH_ENERGY = 1;

        // Input system Global
        public const int MAX_SINGLE_MOVE_LIMIT = 20;
        public const int MAX_HISTORY_LINES = 200;
        public const int TERMINAL_HEIGHT = 300;
        public const float MIN_BACKSPACE_DELAY = 0.02f;
        public const float BACKSPACE_ACCELERATION = 0.25f;

        // UI settings Global
        public const float DEFAULT_OVERFLOW_SCROLL_SPEED = 20.0f;

        // Increased from 100f to 120f to widen the value area
        public const float VALUE_DISPLAY_WIDTH = 120f;

        public const int APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING = 5;
        public const float TOOLTIP_AVERAGE_POPUP_TIME = 0.5f;
        public const int TERMINAL_Y = 25;

        // Transition Settings
        public const float UniversalSlowFadeDuration = 3.0f;

        // --- HAPTICS TUNING ---
        public float HoverHapticStrength { get; set; } = 0.75f;
        public float ButtonHapticStrength { get; set; } = 1.0f;

        // --- ECONOMY SETTINGS ---
        public float PriceMultiplier_Consumable { get; set; } = 0.4f; // Consumables are 40% of gear price

        // --- DYNAMIC DROP TUNING ---
        public float Economy_GlobalScalar { get; set; } = 1.0f;
        public int Economy_BaseDrop { get; set; } = 10;
        public float Economy_Variance { get; set; } = 0.4f;

        // --- STATUS EFFECT TUNING ---
        public int PoisonBaseDamage { get; set; } = 2;
        public float RegenPercent { get; set; } = 0.0625f; // 1/16th
        public float DodgingAccuracyMultiplier { get; set; } = 0.5f;
        public float BurnDamageMultiplier { get; set; } = 2.0f;
        public float FrostbiteAgilityMultiplier { get; set; } = 0.5f;
        public float EmpoweredDamageMultiplier { get; set; } = 1.5f;

        // --- HITSTOP (FRAME FREEZE) TUNING ---
        public float HitstopDuration_Normal { get; set; } = 0.1f;
        public float HitstopDuration_Crit { get; set; } = 0.2f;

        // --- BACKGROUND NOISE TUNING ---
        public Color BackgroundNoiseColor { get; set; } = new Color(32, 26, 35);
        public float BackgroundNoiseOpacity { get; set; } = 1.0f;
        public float BackgroundNoiseScale { get; set; } = 0.05f;
        public float BackgroundScrollSpeedX { get; set; } = 0.01f;
        public float BackgroundScrollSpeedY { get; set; } = 0.005f;
        public float BackgroundDistortionScale { get; set; } = 100.0f;
        public float BackgroundDistortionSpeed { get; set; } = 0.5f;
        public float BackgroundNoiseThreshold { get; set; } = 0.01f; // Tolerance for "Black" detection

        // --- CRT & COLOR TUNING ---
        public float CrtSaturation { get; set; } = 1.1f; // 1.0 is default, >1.0 pops colors
        public float CrtVibrance { get; set; } = 0.15f;  // Boosts muted colors

        // --- PROTECT ANIMATION TUNING ---
        public float ProtectAnimationSpeed { get; set; } = 1.0f;
        public int ProtectDamageFrameIndex { get; set; } = 0;

        // --- HEAL OVERLAY TUNING ---
        public Color HealOverlayColor { get; set; } = Color.White;
        public float HealOverlayHangDuration { get; set; } = 0.5f;
        public float HealOverlayFadeDuration { get; set; } = 0.5f;
        public Color ManaOverlayColor { get; set; } = Color.White;

        // --- LOW HEALTH FLASH TUNING ---
        public Color LowHealthFlashColor { get; set; } = new Color(181, 65, 49); // Palette_Rust
        public float LowHealthThreshold { get; set; } = 0.25f; // 50%
        public float LowHealthFlashSpeedMin { get; set; } = 1.5f; // Reduced from 2.0
        public float LowHealthFlashSpeedMax { get; set; } = 4.0f; // Reduced from 10.0
        public float LowHealthFlashPatternLength { get; set; } = 7.0f; // Controls the cycle length (Flash-Flash-Pause)

        // --- ANIMATION TUNING ---
        public float SquashRecoverySpeed { get; set; } = 4f;
        public const float ItemHoverScale = 1.2f; // Global scale for hovered items in UI

        // --- TARGETING ANIMATION SETTINGS ---
        public float TargetingSingleCycleSpeed { get; set; } = 1.0f; // Increased from 1.0f for slower cycling


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // INSTANCE VARIABLES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Debugging variables
        public bool ShowDebugOverlays { get; set; } = false;
        public bool ShowSplitMapGrid { get; set; } = false;

        // Settings variables
        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        // Input variables
        public int previousScrollValue = Mouse.GetState().ScrollWheelValue;

        // Static Color Palette
        public Color Palette_ElementFire { get; set; } = new Color(255, 85, 0);
        public Color Palette_ElementWater { get; set; } = new Color(0, 170, 255);
        public Color Palette_ElementNature { get; set; } = new Color(0, 255, 85);
        public Color Palette_ElementArcane { get; set; } = new Color(170, 0, 255);
        public Color Palette_ElementDivine { get; set; } = new Color(255, 255, 85);
        public Color Palette_ElementBlight { get; set; } = new Color(170, 85, 255);

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


        // Colors
        public Color PlayerColor { get; private set; } = new Color(181, 65, 49); // Palette_Rust
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
        public Color OutputTextColor { get; private set; }
        public Color InputTextColor { get; private set; }
        public Color ToolTipBGColor { get; private set; }
        public Color ToolTipTextColor { get; private set; }
        public Color ToolTipBorderColor { get; private set; }
        public Color TerminalDarkGray { get; set; }
        public Color InputCaratColor { get; set; }
        public Color AlertColor { get; private set; }
        public Color ConfirmSettingsColor { get; private set; }

        // Stat-specific Colors
        public Color StatColor_Increase { get; private set; }
        public Color StatColor_Decrease { get; private set; }
        public Color StatColor_Increase_Half { get; private set; }
        public Color StatColor_Decrease_Half { get; private set; }

        // Generic Feedback Colors
        public Color ColorPositive { get; private set; }
        public Color ColorNegative { get; private set; }
        public Color ColorCrit { get; private set; }
        public Color ColorImmune { get; private set; }
        public Color ColorConditionToMeet { get; private set; }

        // Percentage Gradient Colors
        public Color ColorPercentageMin { get; private set; }
        public Color ColorPercentageMax { get; private set; }

        // Narration Colors
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

        // Combat Indicator Colors
        public Color DamageIndicatorColor { get; set; }
        public Color HealIndicatorColor { get; set; }
        public Color CritcalHitIndicatorColor { get; set; }
        public Color GrazeIndicatorColor { get; set; }
        public Color EffectiveIndicatorColor { get; set; }
        public Color ResistedIndicatorColor { get; set; }
        public Color ImmuneIndicatorColor { get; set; }
        public Color ProtectedIndicatorColor { get; set; }
        public Color FailedIndicatorColor { get; set; }

        // Data-driven Colors
        public Dictionary<int, Color> ElementColors { get; private set; }
        public Dictionary<StatusEffectType, Color> StatusEffectColors { get; private set; }


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // DICE SYSTEM SETTINGS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // --- Physics & Simulation ---
        public float DiceSimulationSpeedMultiplier { get; set; } = 1f;
        public System.Numerics.Vector3 DiceGravity { get; set; } = new System.Numerics.Vector3(0, -100, 0);
        public int DiceSolverIterations { get; set; } = 32;
        public int DiceSolverSubsteps { get; set; } = 12;
        public float DiceFrictionCoefficient { get; set; } = 1.25f;
        public float DiceBounciness { get; set; } = 2f;
        public float DiceSpringStiffness { get; set; } = 30f;
        public float DiceSpringDamping { get; set; } = 1f;
        public float DiceContainerWallHeight { get; set; } = 200f;
        public float DiceContainerWallThickness { get; set; } = 500f;

        // --- Spawning & Initial State ---
        public float DiceMass { get; set; } = 1f;
        public float DiceColliderSize { get; set; } = 1f;
        public float DiceColliderBevelRatio { get; set; } = 0.2f;
        public float DiceD4ColliderBevelRatio { get; set; } = 0.15f;
        public float DiceD4FlatnessThreshold { get; set; } = 0.05f;
        public float DiceSpawnHeightMin { get; set; } = 15f;
        public float DiceSpawnHeightMax { get; set; } = 25f;
        public float DiceSpawnOffscreenMargin { get; set; } = 5f;
        public float DiceSpawnEdgePadding { get; set; } = 5f;
        public float DiceThrowForceMin { get; set; } = 20f;
        public float DiceThrowForceMax { get; set; } = 50f;
        public float DiceInitialAngularVelocityMax { get; set; } = 75f;

        // --- Visuals & Animation ---
        public float DiceCameraZoom { get; set; } = 20f;
        public float DiceCameraHeight { get; set; } = 60f;
        public float DiceSparkVelocityThreshold { get; set; } = 25f;
        public float DiceEnumerationStepDuration { get; set; } = 0.3f;
        public float DiceEnumerationFlashDuration { get; set; } = 0.1f;
        public float DiceEnumerationMaxScale { get; set; } = 1.25f;
        public float DiceResultTextYOffset { get; set; } = 0f;
        public float DicePostEnumerationDelay { get; set; } = 0.5f;
        public float DiceGatheringDuration { get; set; } = 0.75f;
        public float DicePostSumDelayDuration { get; set; } = 0.25f;
        public float DiceSumShiftDuration { get; set; } = 0.4f;
        public float DiceNewSumAnimationDuration { get; set; } = 0.5f;
        public float DiceNewSumInflateDuration { get; set; } = 0.05f;
        public float DiceNewSumHoldDuration { get; set; } = 0f;
        public float DiceNewSumDeflateDuration { get; set; } = 0f;
        public float DiceMultiplierAnimationDuration { get; set; } = 1.5f;
        public float DiceModifierAnimationDuration { get; set; } = 1.5f;
        public float DiceFinalSumLifetime { get; set; } = 1.0f;
        public float DiceFinalSumFadeOutDuration { get; set; } = 0.5f;
        public float DiceFinalSumSequentialFadeDelay { get; set; } = 0.25f;

        // --- Roll Resolution & Failsafes ---
        public float DiceSettleDelay { get; set; } = 0.45f;
        public float DiceRollTimeout { get; set; } = 6f;
        public float DiceCompleteRollTimeout { get; set; } = 10f;
        public int DiceMaxRerollAttempts { get; set; } = 5;
        public int DiceForcedResultValue { get; set; } = 3;
        public float DiceSleepThreshold { get; set; } = 0.2f;
        public float DiceCantingRerollThreshold { get; set; } = 0.99f;
        public float DiceNudgeForceMin { get; set; } = -10f;
        public float DiceNudgeForceMax { get; set; } = 20f;
        public float DiceNudgeUpwardForceMin { get; set; } = 20f;
        public float DiceNudgeUpwardForceMax { get; set; } = 30f;
        public float DiceNudgeTorqueMax { get; set; } = 25f;
        public int DiceD4MaxTumbleCollisions { get; set; } = 3;
        public float DiceD4TumbleTorqueMax { get; set; } = 15f;
        public float DiceD4TumbleUpwardForceMin { get; set; } = 5f;
        public float DiceD4TumbleUpwardForceMax { get; set; } = 10f;

        // --- Debugging ---
        public float DiceDebugAxisLineSize { get; set; } = 0.5f;

        // --- Target Indicator Settings ---
        public float TargetIndicatorNoiseSpeed { get; set; } = 0.5f;
        public float TargetIndicatorOffsetX { get; set; } = 6.0f;
        public float TargetIndicatorOffsetY { get; set; } = 6.0f;
        public float TargetIndicatorRotationRange { get; set; } = 0f;
        public float TargetIndicatorScaleMin { get; set; } = 1.0f;
        public float TargetIndicatorScaleMax { get; set; } = 1.0f;

        public Color GetNarrationColor(string tag)
        {
            string lowerTag = tag.ToLowerInvariant();

            // 1. Handle "c" prefixed tags (Semantic)
            if (lowerTag.StartsWith("c"))
            {
                // Stats - Now mapped to Palette_Sun
                if (lowerTag == "cstr") return Palette_Sun;
                if (lowerTag == "cint") return Palette_Sun;
                if (lowerTag == "cten") return Palette_Sun;
                if (lowerTag == "cagi") return Palette_Sun;

                // Feedback
                if (lowerTag == "cpositive") return ColorPositive;
                if (lowerTag == "cnegative") return ColorNegative;
                if (lowerTag == "ccrit") return ColorCrit;
                if (lowerTag == "cimmune") return ColorImmune;
                if (lowerTag == "cctm") return ColorConditionToMeet;
                if (lowerTag == "cetc") return Palette_DarkGray;

                // Elements
                if (lowerTag == "cneutral") return ElementColors.GetValueOrDefault(0, Color.White);
                if (lowerTag == "cfire") return ElementColors.GetValueOrDefault(1, Color.White);
                if (lowerTag == "cwater") return ElementColors.GetValueOrDefault(2, Color.White);
                if (lowerTag == "cnature") return ElementColors.GetValueOrDefault(3, Color.White);
                if (lowerTag == "carcane") return ElementColors.GetValueOrDefault(4, Color.White);
                if (lowerTag == "cdivine") return ElementColors.GetValueOrDefault(5, Color.White);
                if (lowerTag == "cblight") return ElementColors.GetValueOrDefault(6, Color.White);

                // Status Effects
                string effectName = lowerTag.Substring(1);
                foreach (var kvp in StatusEffectColors)
                {
                    if (kvp.Key.ToString().ToLowerInvariant() == effectName) return kvp.Value;
                }

                // Narration Specific
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

            // 2. Handle Standard Color Names (Fallback)
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
