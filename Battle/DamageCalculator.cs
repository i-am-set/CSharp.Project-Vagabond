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
        public enum ElementalEffectiveness { Neutral, Effective, Resisted, Immune }

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

            /// <summary>
            /// The elemental effectiveness of the attack.
            /// </summary>
            public ElementalEffectiveness Effectiveness;

            /// <summary>
            /// A list of abilities from the attacker that triggered during this calculation.
            /// </summary>
            public List<AbilityData> AttackerAbilitiesTriggered;

            /// <summary>
            /// A list of abilities from the defender that triggered during this calculation.
            /// </summary>
            public List<AbilityData> DefenderAbilitiesTriggered;
        }

        /// <summary>
        /// Calculates the effective power of a move after applying static bonuses from the attacker's abilities.
        /// This is used for both UI display and the final damage calculation.
        /// </summary>
        public static int GetEffectiveMovePower(BattleCombatant attacker, MoveData move)
        {
            float movePower = move.Power;

            foreach (var ability in attacker.ActiveAbilities)
            {
                if (ability.Effects.TryGetValue("DamageBonus", out var damageBonusValue))
                {
                    var parts = damageBonusValue.Split(',');
                    if (parts.Length == 2 &&
                        EffectParser.TryParseInt(parts[0].Trim(), out int elementId) &&
                        EffectParser.TryParseFloat(parts[1].Trim(), out float bonusPercent))
                    {
                        // Check if the move is a spell and has the correct element
                        if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(elementId))
                        {
                            movePower *= (1.0f + (bonusPercent / 100f));
                        }
                    }
                }
            }

            return (int)Math.Round(movePower);
        }

        /// <summary>
        /// Calculates the final damage an attacker will deal to a target with a specific move, including all modifiers.
        /// </summary>
        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f)
        {
            var attacker = action.Actor;
            var result = new DamageResult
            {
                Effectiveness = ElementalEffectiveness.Neutral,
                AttackerAbilitiesTriggered = new List<AbilityData>(),
                DefenderAbilitiesTriggered = new List<AbilityData>()
            };

            // Step 0: Handle Fixed Damage moves immediately.
            if (move.Effects.TryGetValue("FixedDamage", out var fixedDamageValue) && EffectParser.TryParseInt(fixedDamageValue, out int fixedDamage))
            {
                result.DamageAmount = fixedDamage;
                return result;
            }

            // Step 1: Handle non-damaging moves.
            if (move.Power == 0)
            {
                return result;
            }

            // Step 2: Accuracy Check & The Graze Mechanic
            if (move.Accuracy != -1) // -1 is our convention for a "True Hit" that always hits.
            {
                if (target.HasStatusEffect(StatusEffectType.Dodging))
                {
                    result.WasGraze = true;
                }
                else
                {
                    int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);
                    if (_random.Next(1, 101) > effectiveAccuracy)
                    {
                        result.WasGraze = true;
                    }
                }
            }

            // Step 3: Base Damage Calculation
            float movePower = GetEffectiveMovePower(attacker, move);

            // Add bonus from Last Stand
            if (action.IsLastActionInRound)
            {
                foreach (var ability in attacker.ActiveAbilities)
                {
                    if (ability.Effects.TryGetValue("PowerBonusLastAct", out var value) && EffectParser.TryParseFloat(value, out float bonusPercent))
                    {
                        movePower *= (1.0f + (bonusPercent / 100f));
                        result.AttackerAbilitiesTriggered.Add(ability);
                    }
                }
            }

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

            // Modifier (Ability Damage Bonus)
            foreach (var ability in attacker.ActiveAbilities)
            {
                // Adrenaline Rush
                if (ability.Effects.TryGetValue("DamageBonusLowHP", out var adrValue))
                {
                    var parts = adrValue.Split(',');
                    if (parts.Length == 2 && EffectParser.TryParseFloat(parts[0], out float hpThreshold) && EffectParser.TryParseFloat(parts[1], out float bonus))
                    {
                        if ((float)attacker.Stats.CurrentHP / attacker.Stats.MaxHP * 100f < hpThreshold)
                        {
                            finalDamage *= (1.0f + (bonus / 100f));
                            result.AttackerAbilitiesTriggered.Add(ability);
                        }
                    }
                }

                // Opportunist
                if (ability.Effects.TryGetValue("DamageBonusVsStatused", out var oppValue))
                {
                    if (target.ActiveStatusEffects.Any() && EffectParser.TryParseFloat(oppValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(ability);
                    }
                }

                // First Blood
                if (!attacker.HasUsedFirstAttack && ability.Effects.TryGetValue("FirstAttackBonus", out var fbValue))
                {
                    if (EffectParser.TryParseFloat(fbValue, out float bonus))
                    {
                        finalDamage *= (1.0f + (bonus / 100f));
                        result.AttackerAbilitiesTriggered.Add(ability);
                    }
                }

                // Bloodletter
                if (ability.Effects.TryGetValue("Bloodletter", out var bloodletterValue) && EffectParser.TryParseFloatArray(bloodletterValue, out float[] p) && p.Length == 2)
                {
                    // Void element ID is 9
                    if (move.MoveType == MoveType.Spell && move.OffensiveElementIDs.Contains(9))
                    {
                        finalDamage *= (1.0f + (p[1] / 100f));
                        result.AttackerAbilitiesTriggered.Add(ability);
                    }
                }
            }

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

            if (!result.WasGraze)
            {
                double critChance = BattleConstants.CRITICAL_HIT_CHANCE;
                // Sniper
                foreach (var ability in attacker.ActiveAbilities)
                {
                    if (ability.Effects.TryGetValue("CritChanceBonus", out var value) && EffectParser.TryParseFloat(value, out float bonus))
                    {
                        critChance += bonus / 100.0;
                    }
                }

                if (target.HasStatusEffect(StatusEffectType.Root)) critChance *= 2.0;
                if (_random.NextDouble() < critChance)
                {
                    result.WasCritical = true;
                    finalDamage *= BattleConstants.CRITICAL_HIT_MULTIPLIER;

                    // Bulwark
                    foreach (var ability in target.ActiveAbilities)
                    {
                        if (ability.Effects.TryGetValue("CritDamageReduction", out var value) && EffectParser.TryParseFloat(value, out float reductionPercent))
                        {
                            finalDamage *= (1.0f - (reductionPercent / 100f));
                            result.DefenderAbilitiesTriggered.Add(ability);
                        }
                    }
                }
            }

            if (!result.WasCritical)
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

            float elementalMultiplier = GetElementalMultiplier(move, target, result.DefenderAbilitiesTriggered);
            if (elementalMultiplier > 1.0f) result.Effectiveness = ElementalEffectiveness.Effective;
            else if (elementalMultiplier > 0f && elementalMultiplier < 1.0f) result.Effectiveness = ElementalEffectiveness.Resisted;
            else if (elementalMultiplier == 0f) result.Effectiveness = ElementalEffectiveness.Immune;

            finalDamage *= elementalMultiplier;

            if (target.HasStatusEffect(StatusEffectType.Freeze) && move.ImpactType == ImpactType.Physical)
            {
                finalDamage *= 2.0f;
            }

            // Thick Skin
            foreach (var ability in target.ActiveAbilities)
            {
                if (ability.Effects.TryGetValue("DamageReductionPhysical", out var value) && EffectParser.TryParseFloat(value, out float reduction))
                {
                    if (move.ImpactType == ImpactType.Physical)
                    {
                        finalDamage *= (1.0f - (reduction / 100f));
                    }
                }
            }

            // Vigor
            foreach (var ability in target.ActiveAbilities)
            {
                if (ability.Effects.TryGetValue("Vigor", out var vigorValue) && EffectParser.TryParseFloatArray(vigorValue, out float[] vigorParams) && vigorParams.Length == 2)
                {
                    float hpThreshold = vigorParams[0];
                    float damageReduction = vigorParams[1];

                    if ((float)target.Stats.CurrentHP / target.Stats.MaxHP * 100f > hpThreshold)
                    {
                        finalDamage *= (1.0f - (damageReduction / 100f));
                        result.DefenderAbilitiesTriggered.Add(ability);
                    }
                }
            }

            finalDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);

            if (result.WasGraze)
            {
                finalDamage *= BattleConstants.GRAZE_MULTIPLIER;
            }

            // Step 5: Final Damage & Additive Modifiers
            int finalDamageAmount = (int)Math.Floor(finalDamage);

            if (finalDamage > 0 && finalDamageAmount == 0)
            {
                finalDamageAmount = 1;
            }

            result.DamageAmount = finalDamageAmount;
            return result;
        }

        /// <summary>
        /// Calculates a "pure baseline" damage value, ignoring all conditional multipliers.
        /// This is used to determine if a hit was exceptionally strong for special visual feedback.
        /// </summary>
        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, MoveData move)
        {
            if (move.Power == 0) return 0;

            float movePower = move.Power; // Use base power, ignore RampUp

            // Use base stats, ignoring status effects
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
                    offensiveStat = attacker.Stats.Strength;
                    break;
            }

            float defensiveStat = target.Stats.Tenacity;
            if (defensiveStat <= 0) defensiveStat = 1;

            // Core damage formula
            float baseDamage = ((((2f * attacker.Stats.Level / 5f + 2f) * movePower * (offensiveStat / defensiveStat)) / 50f) + 2f);

            // Apply random variance of 1.0x as requested
            baseDamage *= BattleConstants.RANDOM_VARIANCE_MAX;

            int finalDamageAmount = (int)Math.Floor(baseDamage);
            if (baseDamage > 0 && finalDamageAmount == 0)
            {
                finalDamageAmount = 1;
            }

            return finalDamageAmount;
        }


        /// <summary>
        /// Calculates the final elemental multiplier based on the move's offensive elements and the target's defensive elements.
        /// This overload does not track which abilities triggered.
        /// </summary>
        public static float GetElementalMultiplier(MoveData move, BattleCombatant target)
        {
            return GetElementalMultiplier(move, target, null);
        }

        /// <summary>
        /// Calculates the final elemental multiplier based on the move's offensive elements and the target's defensive elements.
        /// </summary>
        public static float GetElementalMultiplier(MoveData move, BattleCombatant target, List<AbilityData> defenderAbilitiesTriggered)
        {
            // Check for elemental immunities from abilities first.
            foreach (var ability in target.ActiveAbilities)
            {
                // Handles abilities like Photosynthesis that grant immunity and heal.
                if (ability.Effects.TryGetValue("ElementImmunityAndHeal", out var healValue))
                {
                    var parts = healValue.Split(',');
                    if (parts.Length == 2 && EffectParser.TryParseInt(parts[0], out int immuneElementId))
                    {
                        if (move.OffensiveElementIDs.Contains(immuneElementId))
                        {
                            defenderAbilitiesTriggered?.Add(ability);
                            return 0f; // Grant immunity
                        }
                    }
                }

                // Handles abilities like Grounding that grant simple immunity.
                if (ability.Effects.TryGetValue("ElementImmunity", out var immunityValue))
                {
                    if (EffectParser.TryParseInt(immunityValue, out int immuneElementId))
                    {
                        if (move.OffensiveElementIDs.Contains(immuneElementId))
                        {
                            defenderAbilitiesTriggered?.Add(ability);
                            return 0f; // Grant immunity
                        }
                    }
                }
            }

            var targetDefensiveElements = target.GetEffectiveDefensiveElementIDs();
            if (move.OffensiveElementIDs == null || !move.OffensiveElementIDs.Any() ||
                targetDefensiveElements == null || !targetDefensiveElements.Any())
            {
                return 1.0f;
            }

            float finalMultiplier = 1.0f;

            foreach (int offensiveId in move.OffensiveElementIDs)
            {
                if (BattleDataCache.InteractionMatrix.TryGetValue(offensiveId, out var attackRow))
                {
                    foreach (int defensiveId in targetDefensiveElements)
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