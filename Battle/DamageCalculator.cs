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
            // Step 0: Handle Fixed Damage moves immediately.
            if (move.Effects.TryGetValue("FixedDamage", out var fixedDamageValue) && EffectParser.TryParseInt(fixedDamageValue, out int fixedDamage))
            {
                return new DamageResult { DamageAmount = fixedDamage, WasCritical = false, WasGraze = false };
            }

            // Step 1: Handle non-damaging moves.
            if (move.Power == 0)
            {
                return new DamageResult { DamageAmount = 0, WasCritical = false, WasGraze = false };
            }

            // Step 2: Accuracy Check & The Graze Mechanic
            bool isGrazed = false;
            if (move.Accuracy != -1) // -1 is our convention for a "True Hit" that always hits.
            {
                if (target.HasStatusEffect(StatusEffectType.Dodging))
                {
                    isGrazed = true;
                }
                else
                {
                    int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);
                    if (_random.Next(1, 101) > effectiveAccuracy)
                    {
                        isGrazed = true;
                    }
                }
            }

            // Step 3: Base Damage Calculation
            float movePower = move.Power;

            // Add bonus from Ramping Damage
            if (move.Effects.ContainsKey("RampUp") && attacker.RampingMoveCounters.TryGetValue(move.MoveID, out int useCount))
            {
                if (EffectParser.TryParseIntArray(move.Effects["RampUp"], out int[] rampParams) && rampParams.Length == 2)
                {
                    int bonusPerUse = rampParams[0];
                    int maxBonus = rampParams[1];
                    movePower += Math.Min(useCount * bonusPerUse, maxBonus);
                }
            }

            float offensiveStat;
            switch (move.OffensiveStat)
            {
                case OffensiveStatType.Strength:
                    offensiveStat = attacker.GetEffectiveStrength();
                    break;
                case OffensiveStatType.Intelligence:
                    offensiveStat = attacker.GetEffectiveIntelligence();
                    break;
                case OffensiveStatType.Tenacity:
                    offensiveStat = attacker.GetEffectiveTenacity();
                    break;
                case OffensiveStatType.Agility:
                    offensiveStat = attacker.GetEffectiveAgility();
                    break;
                default:
                    offensiveStat = attacker.GetEffectiveStrength();
                    break;
            }

            float defensiveStat = target.GetEffectiveTenacity();
            // Apply Armor Pierce
            if (move.Effects.TryGetValue("ArmorPierce", out var armorPierceValue) && EffectParser.TryParseFloat(armorPierceValue, out float piercePercent))
            {
                defensiveStat *= (1.0f - (piercePercent / 100f));
            }
            if (defensiveStat <= 0) defensiveStat = 1; // Prevent division by zero or negative defense

            float baseDamage = ((((2f * attacker.Stats.Level / 5f + 2f) * movePower * (offensiveStat / defensiveStat)) / 50f) + 2f);

            // Step 4: Multiplicative Modifier Application
            float finalDamage = baseDamage;

            // Modifier (Execute)
            if (move.Effects.TryGetValue("Execute", out var executeValue) && EffectParser.TryParseFloatArray(executeValue, out float[] execParams) && execParams.Length == 2)
            {
                float hpThreshold = execParams[0];
                float damageMultiplier = execParams[1];
                if ((float)target.Stats.CurrentHP / target.Stats.MaxHP <= (hpThreshold / 100f))
                {
                    finalDamage *= damageMultiplier;
                }
            }

            finalDamage *= multiTargetModifier;

            bool isCritical = false;
            if (!isGrazed)
            {
                double critChance = BattleConstants.CRITICAL_HIT_CHANCE;
                if (target.HasStatusEffect(StatusEffectType.Root)) critChance *= 2.0;
                if (_random.NextDouble() < critChance)
                {
                    isCritical = true;
                    finalDamage *= BattleConstants.CRITICAL_HIT_MULTIPLIER;
                }
            }

            if (!isCritical)
            {
                if (move.ImpactType == ImpactType.Physical && attacker.HasStatusEffect(StatusEffectType.StrengthUp))
                {
                    finalDamage *= BattleConstants.STRENGTH_UP_MULTIPLIER;
                }
                if (target.HasStatusEffect(StatusEffectType.TenacityUp))
                {
                    finalDamage *= BattleConstants.TENACITY_UP_MULTIPLIER;
                }
            }

            finalDamage *= GetElementalMultiplier(move, target);

            if (target.HasStatusEffect(StatusEffectType.Freeze) && move.ImpactType == ImpactType.Physical)
            {
                finalDamage *= 2.0f;
            }

            finalDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);

            if (isGrazed)
            {
                finalDamage *= BattleConstants.GRAZE_MULTIPLIER;
            }

            // Step 5: Final Damage & Additive Modifiers
            int finalDamageAmount = (int)Math.Floor(finalDamage);

            if (target.HasStatusEffect(StatusEffectType.Burn) && move.ImpactType == ImpactType.Physical)
            {
                finalDamageAmount += Math.Max(1, (int)(target.Stats.MaxHP / 16f));
            }

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
                if (BattleDataCache.InteractionMatrix.TryGetValue(offensiveId, out var attackRow))
                {
                    foreach (int defensiveId in target.DefensiveElementIDs)
                    {
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