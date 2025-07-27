using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Physics;
using System;
using System.Collections.Generic;
using System.Numerics;

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
        public const float FEET_PER_WORLD_TILE = 200f; // The physical distance of a single world tile
        public const float FEET_PER_LOCAL_TILE = FEET_PER_WORLD_TILE / LOCAL_GRID_SIZE;
        public const float FEET_PER_SECOND_PER_SPEED_UNIT = 4.0f; // A character with speed 1.0 moves at X ft/s.

        // Physics constants
        public const float PHYSICS_UPDATES_PER_SECOND = 60f;
        public const float FIXED_PHYSICS_TIMESTEP = 1f / PHYSICS_UPDATES_PER_SECOND;

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
        public const int COMBAT_TURN_DURATION_SECONDS = 5;
        public const float COMBAT_ACTION_DELAY_SECONDS = 0.5f;
        public const float VISUAL_SPEED_MULTIPLIER = 0.8f;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // INSTANCE VARIABLES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Settings variables
        public bool UseImperialUnits { get; set; } = false;
        public bool Use24HourClock { get; set; } = false;

        // Time scale multipliers
        public float TimeScaleMultiplier1 { get; set; } = 1.0f;
        public float TimeScaleMultiplier2 { get; set; } = 2.0f;
        public float TimeScaleMultiplier3 { get; set; } = 5.0f;

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


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // DICE SYSTEM SETTINGS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // --- Physics & Simulation ---

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
        public int DiceSolverSubsteps { get; set; } = 8;

        /// <summary>
        /// Controls the "slipperiness" of surfaces (both dice and the floor).
        /// How to use: Higher values create more friction, making dice stop rolling sooner. Lower values make them feel more like ice.
        /// Example: 0.1f is very slippery, 2.0f is very rough.
        /// </summary>
        public float DiceFrictionCoefficient { get; set; } = 1.5f;

        /// <summary>
        /// Controls the bounciness of a collision.
        /// How to use: This is the maximum speed at which a contact point can separate after a collision. A value of 0 means no bounce (inelastic). Higher values allow for more significant bounces.
        /// Example: 2.0f is a small bounce, 10.0f is a very noticeable bounce.
        /// </summary>
        public float DiceBounciness { get; set; } = 10f;

        /// <summary>
        /// Defines how "hard" or "soft" a collision is, like a spring.
        /// How to use: Higher values make the connection stiffer, like a hard rubber ball hitting concrete. Lower values make it feel softer, like a bouncy ball.
        /// Example: 5 is soft, 30 is very hard.
        /// </summary>
        public float DiceSpringStiffness { get; set; } = 20f;

        /// <summary>
        /// Controls how quickly the bounce effect from a collision dissipates.
        /// How to use: This is a ratio. A value of 1.0 is "critically damped," meaning it stops bouncing immediately. A low value (close to 0) creates a very bouncy, oscillating effect. A value greater than 1 will feel sluggish and overdamped.
        /// Example: 0.1f is very bouncy, 1.0f has no oscillation.
        /// </summary>
        public float DiceSpringDamping { get; set; } = 0.1f;

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
        /// The amount of beveling on the collider's corners, as a percentage of the die's size.
        /// How to use: This creates a more realistic "rounded cube" shape for physics, which helps the die tumble more naturally. A value of 0 would be a perfect, sharp cube.
        /// Example: 0.2 means the corners are beveled by 20% of the die's size.
        /// </summary>
        public float DiceColliderBevelRatio { get; set; } = 0.2f;

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
        public float DiceThrowForceMin { get; set; } = 10f;

        /// <summary>
        /// The maximum force applied to a die when it is thrown into the scene.
        /// </summary>
        public float DiceThrowForceMax { get; set; } = 75f;

        /// <summary>
        /// The maximum angular velocity (spin) applied to a die on any axis when thrown. The actual spin will be a random value between -Max and +Max.
        /// How to use: Higher values create a much faster, more chaotic tumble.
        /// Example: 20 is a gentle tumble, 100 is a very fast spin.
        /// </summary>
        public float DiceInitialAngularVelocityMax { get; set; } = 100f;

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
        public float DiceEnumerationStepDuration { get; set; } = 0.2f;

        /// <summary>
        /// The duration in seconds of the white flash at the start of a die's enumeration animation.
        /// </summary>
        public float DiceEnumerationFlashDuration { get; set; } = 0.08f;

        /// <summary>
        /// The maximum amount a die scales up during its enumeration "pop" animation (e.g., 1.25f is 125% of its normal size).
        /// </summary>
        public float DiceEnumerationMaxScale { get; set; } = 1.25f;

        /// <summary>
        /// The vertical distance (in screen pixels) that the result number appears above or below the die during enumeration.
        /// </summary>
        public float DiceResultTextYOffset { get; set; } = 45f;

        /// <summary>
        /// The delay in seconds after all dice have been counted before the result numbers start moving to the center.
        /// </summary>
        public float DicePostEnumerationDelay { get; set; } = 0.3f;

        /// <summary>
        /// The duration in seconds of the animation where individual result numbers fly to the center of the screen.
        /// </summary>
        public float DiceGatheringDuration { get; set; } = 0.5f;

        /// <summary>
        /// The duration in seconds for the pause after a sum animation is complete, before the next group is processed.
        /// </summary>
        public float DicePostSumDelayDuration { get; set; } = 0.75f;

        /// <summary>
        /// The duration in seconds for existing sums to slide over to make room for a new sum.
        /// </summary>
        public float DiceSumShiftDuration { get; set; } = 0.4f;

        /// <summary>
        /// The duration in seconds for a new sum to animate from the center to its final position in the list.
        /// </summary>
        public float DiceNewSumAnimationDuration { get; set; } = 0.5f;

        /// <summary>
        /// The duration in seconds for the multiplier animation phase.
        /// </summary>
        public float DiceMultiplierAnimationDuration { get; set; } = 0.75f;

        /// <summary>
        /// The duration in seconds for the modifier animation phase.
        /// </summary>
        public float DiceModifierAnimationDuration { get; set; } = 0.75f;

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
        public float DiceSettleDelay { get; set; } = 0.5f;

        /// <summary>
        /// The maximum time (in seconds) a roll can be in progress. If dice are still moving after this time, the failsafe for stuck dice is triggered.
        /// </summary>
        public float DiceRollTimeout { get; set; } = 8f;

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

        // --- Debugging ---

        /// <summary>
        /// Controls the size of the R/G/B axis lines drawn at each vertex of the physics collider in debug mode.
        /// How to use: A larger value makes the debug markers bigger and easier to see.
        /// </summary>
        public float DiceDebugAxisLineSize { get; set; } = 0.5f;

    }
}