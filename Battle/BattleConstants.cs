using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A static class to hold all "magic numbers" and constants for the battle system,
    /// making them easy to find and tune.
    /// </summary>
    public static class BattleConstants
    {
        public const float CRITICAL_HIT_MULTIPLIER = 1.5f;
        public const float CRITICAL_HIT_CHANCE = 0.0625f;
        public const float RANDOM_VARIANCE_MIN = 0.85f;
        public const float RANDOM_VARIANCE_MAX = 1.00f;
        public const float GRAZE_MULTIPLIER = 0.25f;
        public const float MULTI_TARGET_MODIFIER = 0.75f;

        /// <summary>
        /// A lookup table for stat multipliers based on the current stat stage (-6 to +6).
        /// Follows the standard formula from Pokémon.
        /// </summary>
        public static readonly Dictionary<int, float> StatStageMultipliers = new Dictionary<int, float>
        {
            { 6, 4.0f },    // 8/2
            { 5, 3.5f },    // 7/2
            { 4, 3.0f },    // 6/2
            { 3, 2.5f },    // 5/2
            { 2, 2.0f },    // 4/2
            { 1, 1.5f },    // 3/2
            { 0, 1.0f },    // 2/2
            { -1, 2f/3f },  // approx 0.66f
            { -2, 2f/4f },  // 0.5f
            { -3, 2f/5f },  // 0.4f
            { -4, 2f/6f },  // approx 0.33f
            { -5, 2f/7f },  // approx 0.28f
            { -6, 2f/8f }   // 0.25f
        };
    }
}