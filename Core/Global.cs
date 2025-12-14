using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global()
        {
            WaterColor = Palette_DarkBlue;
            FlatlandColor = Palette_DarkGray;
            HillColor = Palette_DarkGray;
            MountainColor = Palette_Gray;
            PeakColor = Palette_LightGray;
            PlayerColor = Palette_Red;
            PathColor = Palette_Yellow;
            RunPathColor = Palette_Orange;
            PathEndColor = Palette_Red;
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
            AlertColor = Color.Red;
            // Initialize Stat Colors
            StatColor_Strength = Color.Crimson;
            StatColor_Intelligence = Color.Magenta;
            StatColor_Tenacity = Color.Lime;
            StatColor_Agility = Color.Yellow;

            StatColor_Increase = Color.Lime;
            StatColor_Decrease = Color.Red;

            // Initialize Generic Feedback Colors
            ColorPositive = Palette_LightGreen;
            ColorNegative = Palette_Red;
            ColorCrit = Palette_Yellow;
            ColorImmune = Palette_Teal;
            ColorConditionToMeet = Palette_LightYellow;

            // Initialize Item Outline Colors
            ItemOutlineColor_Idle = Palette_Gray;
            ItemOutlineColor_Hover = Palette_BrightWhite;
            ItemOutlineColor_Selected = Color.White;

            // Initialize Item Outline Corner Colors (Red as requested)
            ItemOutlineColor_Idle_Corner = Palette_DarkGray;
            ItemOutlineColor_Hover_Corner = Palette_White;
            ItemOutlineColor_Selected_Corner = Palette_BrightWhite;

            // Initialize Narration Colors
            ColorNarration_Action = Palette_Orange;
            ColorNarration_Spell = Palette_LightBlue;
            ColorNarration_Item = Palette_Teal;
            ColorNarration_Critical = Palette_Yellow;
            ColorNarration_Defeated = Palette_Red;
            ColorNarration_Escaped = Palette_LightBlue;
            ColorNarration_Enemy = Palette_Red;
            ColorNarration_Status = Palette_LightPurple;

            // Initialize Color Mappings
            ElementColors = new Dictionary<int, Color>
        {
            { 1, Palette_White },      // Neutral
            { 2, Palette_Red },        // Fire
            { 3, Palette_LightBlue },  // Water
            { 4, Palette_Pink },       // Arcane
            { 5, Palette_Orange },     // Earth
            { 6, Palette_Gray },       // Metal
            { 7, Palette_LightPurple },// Toxic
            { 8, Palette_Teal },       // Wind
            { 9, Palette_DarkPurple }, // Void
            { 10, Palette_LightYellow },// Light
            { 11, Palette_Yellow},     // Electric
            { 12, Palette_LightBlue }, // Ice
            { 13, Palette_LightGreen } // Nature
        };

            RarityColors = new Dictionary<int, Color>
        {
            { -1, Palette_Gray },      // Basic/Action
            { 0, Color.White },        // Common
            { 1, Color.Lime },         // Uncommon
            { 2, Color.DeepSkyBlue },  // Rare
            { 3, Color.DarkOrchid },   // Epic
            { 4, Color.Red },          // Mythic
            { 5, Color.Yellow }        // Legendary
        };

            StatusEffectColors = new Dictionary<StatusEffectType, Color>
        {
            { StatusEffectType.Poison, Palette_LightPurple },
            { StatusEffectType.Stun, Palette_Yellow },
            { StatusEffectType.Regen, Palette_LightGreen },
            { StatusEffectType.Dodging, Palette_LightBlue },
            { StatusEffectType.Burn, Palette_Red },
            { StatusEffectType.Freeze, Palette_DarkBlue },
            { StatusEffectType.Blind, Palette_LightGray },
            { StatusEffectType.Confuse, Palette_Pink },
            { StatusEffectType.Silence, Palette_LightGray },
            { StatusEffectType.Fear, Palette_DarkPurple },
            { StatusEffectType.Root, Palette_Orange }
        };
        }

        public static Global Instance => _instance;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTANTS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Game version
        public const string GAME_VERSION = "0.1.0";

        // World constants
        public const float FEET_PER_WORLD_TILE = 200f; // The physical distance of a single world tile  
        public const float FEET_PER_SECOND_PER_SPEED_UNIT = 4.0f; // A character with speed 1.0 moves at X ft/s.
        public const float ACTION_TICK_DURATION_SECONDS = 0.3f; // Real-world duration of a single move/action tick at 1x speed.

        // Physics constants
        public const float PHYSICS_UPDATES_PER_SECOND = 60f;
        public const float FIXED_PHYSICS_TIMESTEP = 1f / PHYSICS_UPDATES_PER_SECOND;

        // Virtual resolution for fixed aspect ratio rendering
        public const int VIRTUAL_WIDTH = 320;
        public const int VIRTUAL_HEIGHT = 180;

        // Map settings Global
        public const float MAP_AREA_WIDTH_PERCENT = 0.8f;
        public const int MAP_TOP_PADDING = 10;
        public const int TERMINAL_AREA_HEIGHT = 75;
        public const int GRID_CELL_SIZE = 5;
        public const int FONT_SIZE = 12;
        public const int TERMINAL_LINE_SPACING = 12;
        public const int PROMPT_LINE_SPACING = 16;
        public const float NOISE_SCALE = 0.2f;
        public const int DEFAULT_TERMINAL_WIDTH = 270;
        public const int DEFAULT_TERMINAL_HEIGHT = 169;
        public const int COMBAT_TERMINAL_BUFFER = 65;
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
        public const float VALUE_DISPLAY_WIDTH = 100f;
        public const int APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING = 5;
        public const float TOOLTIP_AVERAGE_POPUP_TIME = 0.5f;
        public const int TERMINAL_Y = 25;

        // Combat settings Global
        public const int COMBAT_TURN_DURATION_SECONDS = 5;
        public const float COMBAT_ACTION_DELAY_SECONDS = 0.5f;
        public const float VISUAL_SPEED_MULTIPLIER = 0.8f;

        // Transition Settings
        public const float UniversalSlowFadeDuration = 3.0f;

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

        // Terrain levels
        public float WaterLevel { get; set; } = 0.3f;
        public float FlatlandsLevel { get; set; } = 0.6f;
        public float HillsLevel { get; set; } = 0.7f;
        public float MountainsLevel { get; set; } = 0.8f;

        // Static Color Palette
        public Color Palette_Black { get; set; } = new Color(23, 22, 28);
        public Color Palette_DarkestGray { get; set; } = new Color(26, 25, 33);
        public Color Palette_DarkerGray { get; set; } = new Color(31, 29, 47);
        public Color Palette_DarkGray { get; set; } = new Color(42, 40, 57);
        public Color Palette_Gray { get; set; } = new Color(62, 65, 95);
        public Color Palette_LightGray { get; set; } = new Color(85, 96, 125);
        public Color Palette_White { get; set; } = new Color(116, 125, 136);
        public Color Palette_Teal { get; set; } = new Color(65, 222, 149);
        public Color Palette_LightBlue { get; set; } = new Color(42, 164, 170);
        public Color Palette_DarkBlue { get; set; } = new Color(59, 119, 166);
        public Color Palette_DarkGreen { get; set; } = new Color(36, 147, 55);
        public Color Palette_LightGreen { get; set; } = new Color(86, 190, 68);
        public Color Palette_LightYellow { get; set; } = new Color(198, 222, 120);
        public Color Palette_Yellow { get; set; } = new Color(243, 194, 32);
        public Color Palette_Orange { get; set; } = new Color(196, 101, 28);
        public Color Palette_Red { get; set; } = new Color(181, 65, 49);
        public Color Palette_DarkPurple { get; set; } = new Color(97, 64, 122);
        public Color Palette_LightPurple { get; set; } = new Color(143, 61, 167);
        public Color Palette_Pink { get; set; } = new Color(234, 97, 157);
        public Color Palette_BrightWhite { get; set; } = new Color(193, 229, 234);

        // Colors
        public Color WaterColor { get; private set; }
        public Color FlatlandColor { get; private set; }
        public Color HillColor { get; private set; }
        public Color MountainColor { get; private set; }
        public Color PeakColor { get; private set; }
        public Color PlayerColor { get; private set; }
        public Color PathColor { get; private set; }
        public Color RunPathColor { get; private set; }
        public Color PathEndColor { get; private set; }
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
        public Color AlertColor { get; private set; }

        // Stat-specific Colors
        public Color StatColor_Strength { get; private set; }
        public Color StatColor_Intelligence { get; private set; }
        public Color StatColor_Tenacity { get; private set; }
        public Color StatColor_Agility { get; private set; }

        public Color StatColor_Increase { get; private set; }
        public Color StatColor_Decrease { get; private set; }

        // Generic Feedback Colors
        public Color ColorPositive { get; private set; }
        public Color ColorNegative { get; private set; }
        public Color ColorCrit { get; private set; }
        public Color ColorImmune { get; private set; }
        public Color ColorConditionToMeet { get; private set; }

        // Item Outline Colors
        public Color ItemOutlineColor_Idle { get; private set; }
        public Color ItemOutlineColor_Hover { get; private set; }
        public Color ItemOutlineColor_Selected { get; private set; }

        // Item Outline Corner Colors
        public Color ItemOutlineColor_Idle_Corner { get; private set; }
        public Color ItemOutlineColor_Hover_Corner { get; private set; }
        public Color ItemOutlineColor_Selected_Corner { get; private set; }

        // Narration Colors
        public Color ColorNarration_Action { get; private set; }
        public Color ColorNarration_Spell { get; private set; }
        public Color ColorNarration_Item { get; private set; }
        public Color ColorNarration_Critical { get; private set; }
        public Color ColorNarration_Defeated { get; private set; }
        public Color ColorNarration_Escaped { get; private set; }
        public Color ColorNarration_Enemy { get; private set; }
        public Color ColorNarration_Status { get; private set; }

        // Data-driven Colors
        public Dictionary<int, Color> ElementColors { get; private set; }
        public Dictionary<int, Color> RarityColors { get; private set; }
        public Dictionary<StatusEffectType, Color> StatusEffectColors { get; private set; }


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // DICE SYSTEM SETTINGS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // --- Physics & Simulation ---

        /// <summary>
        /// A multiplier for the physics simulation time.
        /// How to use: A value of 1.0 is normal speed. A value of 2.0 will make the dice settle twice as fast.
        /// This does not affect the quality of the simulation (the fixed timestep), only how many steps are run per frame.
        /// </summary>
        public float DiceSimulationSpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// The gravity vector for the physics simulation. Determines the "down" direction and its strength.
        /// How to use: A larger negative Y value makes dice feel heavier and fall faster. A smaller negative Y value makes them feel lighter and more "floaty".
        /// Example: A Y of -20 is light gravity, a Y of -200 is heavy gravity.
        /// </summary>
        public System.Numerics.Vector3 DiceGravity { get; set; } = new System.Numerics.Vector3(0, -100, 0);

        /// <summary>
        /// The number of velocity iterations the physics solver will perform per step.
        /// How to use: More iterations lead to more stable and accurate collision responses, especially with many stacked objects, but at a higher performance cost.
        /// Limitations: Values are typically powers of 2. 8 is usually fine, 32 is very stable.
        /// </summary>
        public int DiceSolverIterations { get; set; } = 32;

        /// <summary>
        /// The number of substeps the solver takes.
        /// How to use: More substeps improve stability for very fast-moving objects, preventing them from "tunneling" (passing through) other objects like walls.
        /// Example: 4 is standard, 8 is very robust for fast objects.
        /// </summary>
        public int DiceSolverSubsteps { get; set; } = 12;

        /// <summary>
        /// Controls the "slipperiness" of surfaces (both dice and the floor).
        /// How to use: Higher values create more friction, making dice stop rolling sooner. Lower values make them feel more like ice.
        /// Example: 0.1f is very slippery, 2.0f is very rough.
        /// </summary>
        public float DiceFrictionCoefficient { get; set; } = 1.25f;

        /// <summary>
        /// Controls the bounciness of a collision.
        /// How to use: This is the maximum speed at which a contact point can separate after a collision. A value of 0 means no bounce (inelastic). Higher values allow for more significant bounces.
        /// Example: 2.0f is a small bounce, 10.0f is a very noticeable bounce.
        /// </summary>
        public float DiceBounciness { get; set; } = 2f;

        /// <summary>
        /// Defines how "hard" or "soft" a collision is, like a spring.
        /// How to use: Higher values make the connection stiffer, like a hard rubber ball hitting concrete. Lower values make it feel softer, like a bouncy ball.
        /// Example: 5 is soft, 30 is very hard.
        /// </summary>
        public float DiceSpringStiffness { get; set; } = 30f;

        /// <summary>
        /// Controls how quickly the bounce effect from a collision dissipates.
        /// How to use: This is a ratio. A value of 1.0 is "critically damped," meaning it stops bouncing immediately without oscillation. A low value (close to 0) creates a very bouncy, oscillating effect that can lead to instability. A value greater than 1 will feel sluggish and overdamped.
        /// </summary>
        public float DiceSpringDamping { get; set; } = 1f;

        /// <summary>
        /// Controls how high the invisible containing walls are.
        /// How to use: This should be high enough that dice cannot bounce out of the play area. It is measured in the same units as the camera height and spawn height.
        /// </summary>
        public float DiceContainerWallHeight { get; set; } = 200f;

        /// <summary>
        /// Controls how thick the invisible containing walls are.
        /// How to use: This is mostly for physics stability and does not affect visuals. A thicker wall is harder for objects to tunnel through.
        /// </summary>
        public float DiceContainerWallThickness { get; set; } = 500f;

        // --- Spawning & Initial State ---

        /// <summary>
        /// The physical mass of a single die.
        /// How to use: Affects how the die reacts to forces and gravity. A higher mass will make it feel heavier and harder to push.
        /// </summary>
        public float DiceMass { get; set; } = 1f;

        /// <summary>
        /// The base size of the die's physics collider before beveling.
        /// How to use: This should generally match the visual size of your die model.
        /// </summary>
        public float DiceColliderSize { get; set; } = 1f;

        /// <summary>
        /// The amount of beveling on the D6 collider's corners, as a percentage of the die's size.
        /// How to use: This creates a more realistic "rounded cube" shape for physics, which helps the die tumble more naturally. A value of 0 would be a perfect, sharp cube.
        /// Example: 0.2 means the corners are beveled by 20% of the die's size.
        /// </summary>
        public float DiceColliderBevelRatio { get; set; } = 0.2f;

        /// <summary>
        /// The amount of beveling on the D4 collider's vertices, as a percentage of the edge length.
        /// How to use: This "shaves off" the sharp points of the tetrahedron to create a more stable physics shape. A value of 0 would be a perfect, sharp tetrahedron.
        /// Example: 0.15 means the new beveled faces start 15% of the way down each edge from the original vertex.
        /// </summary>
        public float DiceD4ColliderBevelRatio { get; set; } = 0.15f;

        /// <summary>
        /// The flatness tolerance for a D4. If the vertical distance between the 3 lowest vertices is less than this, the die is considered flat.
        /// How to use: A smaller value is stricter. This value should be small but greater than zero to account for floating-point inaccuracies.
        /// </summary>
        public float DiceD4FlatnessThreshold { get; set; } = 0.05f;

        /// <summary>
        /// The minimum height from which dice are dropped into the scene.
        /// </summary>
        public float DiceSpawnHeightMin { get; set; } = 15f;

        /// <summary>
        /// The maximum height from which dice are dropped into the scene.
        /// </summary>
        public float DiceSpawnHeightMax { get; set; } = 25f;

        /// <summary>
        /// How far off-screen (laterally) the dice will spawn before being thrown into view.
        /// </summary>
        public float DiceSpawnOffscreenMargin { get; set; } = 5f;

        /// <summary>
        /// A "no-spawn zone" at the ends of each edge to prevent dice from spawning too close to a corner and getting stuck.
        /// </summary>
        public float DiceSpawnEdgePadding { get; set; } = 5f;

        /// <summary>
        /// The minimum force applied to a die when it is thrown into the scene.
        /// </summary>
        public float DiceThrowForceMin { get; set; } = 20f;

        /// <summary>
        /// The maximum force applied to a die when it is thrown into the scene.
        /// </summary>
        public float DiceThrowForceMax { get; set; } = 50f;

        /// <summary>
        /// The maximum angular velocity (spin) applied to a die on any axis when thrown. The actual spin will be a random value between -Max and +Max.
        /// How to use: Higher values create a much faster, more chaotic tumble.
        /// Example: 20 is a gentle tumble, 100 is a very fast spin.
        /// </summary>
        public float DiceInitialAngularVelocityMax { get; set; } = 75f;

        // --- Visuals & Animation ---

        /// <summary>
        /// Controls the "zoom" level of the camera. This is the vertical size of the visible play area.
        /// How to use: A smaller value makes the dice and the rolling area appear larger on screen. A larger value makes them appear smaller.
        /// Example: 15 is very zoomed in, 40 is zoomed out.
        /// </summary>
        public float DiceCameraZoom { get; set; } = 20f;

        /// <summary>
        /// Determines the height of the camera over the play area.
        /// How to use: A higher value gives a more top-down "orthographic" view. A lower value provides a more angled "perspective" view.
        /// Example: 80 is very top-down, 30 is more angled.
        /// </summary>
        public float DiceCameraHeight { get; set; } = 60f;

        /// <summary>
        /// The minimum closing velocity between two colliding dice required to generate a spark particle effect.
        /// How to use: Higher values mean only very fast, hard impacts will create sparks. Lower values will make sparks more frequent.
        /// </summary>
        public float DiceSparkVelocityThreshold { get; set; } = 25f;

        /// <summary>
        /// The duration in seconds for each die's "pop" animation during the counting sequence.
        /// </summary>
        public float DiceEnumerationStepDuration { get; set; } = 0.3f;

        /// <summary>
        /// The duration in seconds of the white flash at the start of a die's enumeration animation.
        /// </summary>
        public float DiceEnumerationFlashDuration { get; set; } = 0.1f;

        /// <summary>
        /// The maximum amount a die scales up during its enumeration "pop" animation (e.g., 1.25f is 125% of its normal size).
        /// </summary>
        public float DiceEnumerationMaxScale { get; set; } = 1.25f;

        /// <summary>
        /// The vertical distance (in screen pixels) that the result number appears above or below the die during enumeration.
        /// </summary>
        public float DiceResultTextYOffset { get; set; } = 0f;

        /// <summary>
        /// The delay in seconds after all dice have been counted before the result numbers start moving to the center.
        /// </summary>
        public float DicePostEnumerationDelay { get; set; } = 0.5f;

        /// <summary>
        /// The duration in seconds of the animation where individual result numbers fly to the center of the screen.
        /// </summary>
        public float DiceGatheringDuration { get; set; } = 0.75f;

        /// <summary>
        /// The duration in seconds for the pause after a sum animation is complete, before the next group is processed.
        /// </summary>
        public float DicePostSumDelayDuration { get; set; } = 0.25f;

        /// <summary>
        /// The duration in seconds for existing sums to slide over to make room for a new sum.
        /// </summary>
        public float DiceSumShiftDuration { get; set; } = 0.4f;

        /// <summary>
        /// The duration in seconds for a new sum to animate from the center to its final position in the list.
        /// </summary>
        public float DiceNewSumAnimationDuration { get; set; } = 0.5f;

        /// <summary>
        /// The duration in seconds for the "inflate" part of the new sum's pop animation.
        /// </summary>
        public float DiceNewSumInflateDuration { get; set; } = 0.05f;

        /// <summary>
        /// The duration in seconds for the "hold" part of the new sum's pop animation, where it shakes.
        /// </summary>
        public float DiceNewSumHoldDuration { get; set; } = 0f;

        /// <summary>
        /// The duration in seconds for the "deflate" part of the new sum's pop animation.
        /// </summary>
        public float DiceNewSumDeflateDuration { get; set; } = 0f;

        /// <summary>
        /// The duration in seconds for the multiplier animation phase.
        /// </summary>
        public float DiceMultiplierAnimationDuration { get; set; } = 1.5f;

        /// <summary>
        /// The duration in seconds for the modifier animation phase.
        /// </summary>
        public float DiceModifierAnimationDuration { get; set; } = 1.5f;

        /// <summary>
        /// The time in seconds that final sum results will remain on screen before starting to fade out.
        /// </summary>
        public float DiceFinalSumLifetime { get; set; } = 1.0f;

        /// <summary>
        /// The duration in seconds of the shrinking animation when a final sum disappears.
        /// </summary>
        public float DiceFinalSumFadeOutDuration { get; set; } = 0.5f;

        /// <summary>
        /// The delay in seconds between each sum fading out when multiple sums are on screen.
        /// </summary>
        public float DiceFinalSumSequentialFadeDelay { get; set; } = 0.25f;

        // --- Roll Resolution & Failsafes ---

        /// <summary>
        /// How long (in seconds) the system waits after all dice have stopped moving before checking their final state. This helps prevent misreads from tiny jitters.
        /// </summary>
        public float DiceSettleDelay { get; set; } = 0.45f;

        /// <summary>
        /// The maximum time (in seconds) a roll can be in progress. If dice are still moving after this time, the failsafe for stuck dice is triggered.
        /// </summary>
        public float DiceRollTimeout { get; set; } = 6f;

        /// <summary>
        /// The absolute maximum time (in seconds) a roll can be in progress before all dice are re-rolled.
        /// This is a failsafe for a completely hung simulation where no result is being determined.
        /// </summary>
        public float DiceCompleteRollTimeout { get; set; } = 10f;

        /// <summary>
        /// The maximum number of times the system will try to re-roll a single stuck or canted die before giving up and forcing a result.
        /// This is also used as the limit for complete, simulation-wide re-rolls.
        /// </summary>
        public int DiceMaxRerollAttempts { get; set; } = 5;

        /// <summary>
        /// If a die fails all re-roll attempts, this is the face value it will be assigned.
        /// </summary>
        public int DiceForcedResultValue { get; set; } = 3;

        /// <summary>
        /// The motion threshold for determining if a die is "stopped" or "asleep".
        /// How to use: This is the squared length of the velocity vector. A higher value means the dice will be considered "stopped" even with tiny jitters. A lower value is more sensitive and requires dice to be almost perfectly still.
        /// </summary>
        public float DiceSleepThreshold { get; set; } = 0.2f;

        /// <summary>
        /// The alignment threshold for determining if a die is "canted" (resting on an edge or corner).
        /// How to use: This is the dot product of the die's up-facing vector and the world's up vector. A value of 1.0 means it's perfectly flat. Values below this threshold will trigger a re-roll nudge.
        /// Example: 0.99f is very strict, 0.95f is more lenient.
        /// </summary>
        public float DiceCantingRerollThreshold { get; set; } = 0.99f;

        /// <summary>
        /// The minimum sideways force applied to a canted die to nudge it.
        /// </summary>
        public float DiceNudgeForceMin { get; set; } = -10f;

        /// <summary>
        /// The maximum sideways force applied to a canted die to nudge it.
        /// </summary>
        public float DiceNudgeForceMax { get; set; } = 20f;

        /// <summary>
        /// The minimum upward force applied to a canted die to nudge it.
        /// </summary>
        public float DiceNudgeUpwardForceMin { get; set; } = 20f;

        /// <summary>
        /// The maximum upward force applied to a canted die to nudge it.
        /// </summary>
        public float DiceNudgeUpwardForceMax { get; set; } = 30f;

        /// <summary>
        /// The maximum torque (spin) applied to a canted die to nudge it. The actual torque will be a random value between -Max and +Max.
        /// </summary>
        public float DiceNudgeTorqueMax { get; set; } = 25f;

        /// <summary>
        /// The number of initial collisions a D4 can have that will trigger a "tumble" impulse.
        /// </summary>
        public int DiceD4MaxTumbleCollisions { get; set; } = 3;

        /// <summary>
        /// The maximum torque (spin) applied to a D4 to make it tumble on an initial collision.
        /// </summary>
        public float DiceD4TumbleTorqueMax { get; set; } = 15f;

        /// <summary>
        /// The minimum upward force applied to a D4 to make it "pop" on an initial collision.
        /// </summary>
        public float DiceD4TumbleUpwardForceMin { get; set; } = 5f;

        /// <summary>
        /// The maximum upward force applied to a D4 to make it "pop" on an initial collision.
        /// </summary>
        public float DiceD4TumbleUpwardForceMax { get; set; } = 10f;


        // --- Debugging ---

        /// <summary>
        /// Controls the size of the R/G/B axis lines drawn at each vertex of the physics collider in debug mode.
        /// How to use: A larger value makes the debug markers bigger and easier to see.
        /// </summary>
        public float DiceDebugAxisLineSize { get; set; } = 0.5f;

        // --- Target Indicator Settings ---
        public float TargetIndicatorNoiseSpeed { get; set; } = 0.5f;
        public float TargetIndicatorOffsetX { get; set; } = 6.0f;
        public float TargetIndicatorOffsetY { get; set; } = 6.0f;
        public float TargetIndicatorRotationRange { get; set; } = 0f; // Radians
        public float TargetIndicatorScaleMin { get; set; } = 1.0f;
        public float TargetIndicatorScaleMax { get; set; } = 1.0f;

        // --- Targeting Animation Settings ---
        /// <summary>
        /// The time in seconds for the single-target selection cycle to move to the next target.
        /// </summary>
        public float TargetingSingleCycleSpeed { get; set; } = 1.0f;

        /// <summary>
        /// The duration in seconds of one full blink cycle (Red -> Yellow) for multi-target selection.
        /// </summary>
        public float TargetingMultiBlinkSpeed { get; set; } = 1.5f;

        public Color GetNarrationColor(string tag)
        {
            switch (tag.ToLowerInvariant())
            {
                case "caction": return ColorNarration_Action;
                case "cspell": return ColorNarration_Spell;
                case "citem": return ColorNarration_Item;
                case "ccrit": return ColorNarration_Critical;
                case "cdefeat": return ColorNarration_Defeated;
                case "cescape": return ColorNarration_Escaped;
                case "cenemy": return ColorNarration_Enemy;
                case "cstatus": return ColorNarration_Status;
                default: return Palette_BrightWhite;
            }
        }
    }
}
