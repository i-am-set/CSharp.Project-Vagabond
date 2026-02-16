using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        // --- BREAK ECONOMY ---
        public const float SHIELDED_DAMAGE_MULT = 0.5f;

        // --- SWITCH ANIMATION TUNING ---
        public const float SWITCH_ANIMATION_DURATION = 0.5f;
        public const float SWITCH_VERTICAL_OFFSET = 20f;

        /// <summary>
        /// A lookup table for stat multipliers based on the current stat stage (-6 to +6).
        /// Follows the standard formula from Pokémon.
        /// </summary>
        public static readonly Dictionary<int, float> StatStageMultipliers = new Dictionary<int, float>
        {
            { 2, 2.0f },    // Max Boost
            { 1, 1.5f },    // Stage 1
            { 0, 1.0f },    // Neutral
            { -1, 0.67f },  // ~1/1.5
            { -2, 0.5f }    // 1/2.0
        };
    }
}