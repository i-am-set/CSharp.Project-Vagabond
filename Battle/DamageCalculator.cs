using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A stateless, static class to perform all damage calculations as defined in the architecture document.
    /// </summary>
    public static class DamageCalculator
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Contains the results of a damage calculation.
        /// </summary>
        public struct DamageResult
        {
            /// <summary>
            /// The final, integer-based damage value to be applied.
            /// </summary>
            public int DamageAmount;

            /// <summary>
            /// True if the attack was a critical hit.
            /// </summary>
            public bool WasCritical;

            /// <summary>
            /// True if the attack was a graze (a partial hit).
            /// </summary>
            public bool WasGraze;
        }

        /// <summary>
        /// Calculates the damage an attacker will deal to a target with a specific move.
        /// </summary>
        /// <param name="attacker">The combatant performing the action.</param>
        /// <param name="target">The combatant receiving the action.</param>
        /// <param name="move">The move being used.</param>
        /// <param name="multiTargetModifier">A damage modifier for moves that hit multiple targets.</param>
        /// <returns>A DamageResult struct containing the outcome of the calculation.</returns>
        public static DamageResult CalculateDamage(BattleCombatant attacker, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f)
        {
            // Step 1: Handle non-damaging moves immediately.
            if (move.Power == 0)
            {
                return new DamageResult { DamageAmount = 0, WasCritical = false, WasGraze = false };
            }

            // Step 2: Accuracy Check & The Graze Mechanic
            bool isGrazed = false;
            if (move.Accuracy != -1) // -1 is our convention for a "True Hit" that always hits.
            {
                // The "Dodging" status effect guarantees a graze (miss) on any standard attack.
                if (target.HasStatusEffect(StatusEffectType.Dodging))
                {
                    isGrazed = true;
                }
                else
                {
                    // Standard accuracy roll if not dodging.
                    int currentAccuracy = move.Accuracy;
                    if (_random.Next(1, 101) > currentAccuracy)
                    {
                        isGrazed = true;
                    }
                }
            }

            // Step 3: Base Damage Calculation
            float offensiveStat;
            switch (move.OffensiveStat)
            {
                case OffensiveStatType.Strength:
                    offensiveStat = attacker.Stats.Strength;
                    break;
                case OffensiveStatType.Intelligence:
                    offensiveStat = attacker.Stats.Intelligence;
                    break;
                case OffensiveStatType.Tenacity:
                    offensiveStat = attacker.Stats.Tenacity;
                    break;
                case OffensiveStatType.Agility:
                    offensiveStat = attacker.Stats.Agility;
                    break;
                default:
                    Debug.WriteLine($"[DamageCalculator] [WARNING] Unhandled OffensiveStatType '{move.OffensiveStat}' for move '{move.MoveID}'. Defaulting to Strength.");
                    offensiveStat = attacker.Stats.Strength;
                    break;
            }

            float defensiveStat = target.Stats.Tenacity;
            if (defensiveStat == 0) defensiveStat = 1; // Prevent division by zero

            float baseDamage = ((((2f * attacker.Stats.Level / 5f + 2f) * move.Power * (offensiveStat / defensiveStat)) / 50f) + 2f);

            // Step 4: Multiplicative Modifier Application
            float finalDamage = baseDamage;

            // Modifier 1 (Multi-Target)
            finalDamage *= multiTargetModifier;

            // Modifier 2 (Weather): Placeholder
            // finalDamage *= GetWeatherMultiplier(move);

            // Modifier 3 (Critical Hit)
            bool isCritical = false;
            if (!isGrazed)
            {
                // Placeholder critical hit chance (e.g., 6.25%)
                if (_random.NextDouble() < 0.0625)
                {
                    isCritical = true;
                    finalDamage *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                }
            }

            // Modifier 4 (Attacker/Defender Stat Buffs)
            if (!isCritical)
            {
                if (move.ImpactType == ImpactType.Physical && attacker.HasStatusEffect(StatusEffectType.StrengthUp))
                {
                    finalDamage *= BattleConstants.STRENGTH_UP_MULTIPLIER;
                }
                // Note: Other buffs/debuffs like IntelligenceUp, StrengthDown would be applied here.

                if (target.HasStatusEffect(StatusEffectType.TenacityUp))
                {
                    finalDamage *= BattleConstants.TENACITY_UP_MULTIPLIER;
                }
            }

            // Modifier 5 (Elemental Effectiveness)
            finalDamage *= GetElementalMultiplier(move, target);

            // Modifier 6 (Random Variance)
            finalDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);

            // Modifier 7 (Graze Application)
            if (isGrazed)
            {
                finalDamage *= BattleConstants.GRAZE_MULTIPLIER;
            }

            // Step 5: Final Damage
            int finalDamageAmount = (int)Math.Floor(finalDamage);

            // Ensure that any successful hit that should do damage deals at least 1 point of damage.
            if (finalDamage > 0 && finalDamageAmount == 0)
            {
                finalDamageAmount = 1;
            }

            return new DamageResult
            {
                DamageAmount = finalDamageAmount,
                WasCritical = isCritical,
                WasGraze = isGrazed
            };
        }

        /// <summary>
        /// Calculates the final elemental multiplier based on the move's offensive elements and the target's defensive elements.
        /// </summary>
        public static float GetElementalMultiplier(MoveData move, BattleCombatant target)
        {
            if (move.OffensiveElementIDs == null || !move.OffensiveElementIDs.Any() ||
                target.DefensiveElementIDs == null || !target.DefensiveElementIDs.Any())
            {
                return 1.0f;
            }

            float finalMultiplier = 1.0f;

            foreach (int offensiveId in move.OffensiveElementIDs)
            {
                // Check if the attacking element exists in our matrix
                if (BattleDataCache.InteractionMatrix.TryGetValue(offensiveId, out var attackRow))
                {
                    foreach (int defensiveId in target.DefensiveElementIDs)
                    {
                        // Check if the defending element has a rule in this row and apply it
                        if (attackRow.TryGetValue(defensiveId, out float multiplier))
                        {
                            finalMultiplier *= multiplier;
                        }
                    }
                }
            }

            return finalMultiplier;
        }
    }
}