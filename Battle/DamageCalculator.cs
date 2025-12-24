using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public static class DamageCalculator
    {
        private static readonly Random _random = new Random();
        private const float GLOBAL_DAMAGE_SCALAR = 0.25f;
        private const int FLAT_DAMAGE_BONUS = 2;
        public enum ElementalEffectiveness { Neutral, Effective, Resisted, Immune }

        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public bool WasProtected; // NEW: Flag for protected hits
            public ElementalEffectiveness Effectiveness;
            public List<RelicData> AttackerAbilitiesTriggered;
            public List<RelicData> DefenderAbilitiesTriggered;
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f, bool? overrideCrit = null)
        {
            var attacker = action.Actor;
            var result = new DamageResult
            {
                Effectiveness = ElementalEffectiveness.Neutral,
                AttackerAbilitiesTriggered = new List<RelicData>(),
                DefenderAbilitiesTriggered = new List<RelicData>(),
                WasProtected = false
            };

            // 1. Build Combat Context
            var ctx = new CombatContext
            {
                Actor = attacker,
                Target = target,
                Move = move,
                MultiTargetModifier = multiTargetModifier,
                IsLastAction = action.IsLastActionInRound
            };

            // 2. Accuracy Check
            if (move.Accuracy != -1)
            {
                bool ignoreEvasion = attacker.AccuracyModifiers.Any(m => m.ShouldIgnoreEvasion(ctx));

                // Check Move-specific abilities for IgnoreEvasion
                if (!ignoreEvasion)
                {
                    foreach (var ability in move.Abilities)
                    {
                        if (ability is IAccuracyModifier am && am.ShouldIgnoreEvasion(ctx))
                        {
                            ignoreEvasion = true;
                            break;
                        }
                    }
                }

                // Dodging Status Logic: Multiplies incoming accuracy
                float accuracyMultiplier = 1.0f;
                if (target.HasStatusEffect(StatusEffectType.Dodging) && !ignoreEvasion)
                {
                    accuracyMultiplier = Global.Instance.DodgingAccuracyMultiplier;
                }

                int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);

                // Apply Move-specific accuracy modifiers
                foreach (var ability in move.Abilities)
                {
                    if (ability is IAccuracyModifier am)
                    {
                        effectiveAccuracy = am.ModifyAccuracy(effectiveAccuracy, ctx);
                    }
                }

                // Apply Dodging multiplier to the hit chance
                float hitChance = effectiveAccuracy * accuracyMultiplier;

                if (_random.Next(1, 101) > hitChance) result.WasGraze = true;
            }

            // 3. Calculate Base Power (Allow Move Abilities to modify Power)
            float power = move.Power;
            foreach (var ability in move.Abilities)
            {
                if (ability is ICalculationModifier cm)
                {
                    power = cm.ModifyBasePower(power, ctx);
                }
            }

            if (power == 0) return result;

            // 4. Calculate Base Damage
            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = target.GetEffectiveTenacity();

            // Apply Armor Penetration (Attacker Abilities + Move Abilities)
            float penetration = 0f;
            foreach (var mod in attacker.DefensePenetrationModifiers)
            {
                penetration += mod.GetDefensePenetration(ctx);
            }
            foreach (var ability in move.Abilities)
            {
                if (ability is IDefensePenetrationModifier dpm)
                {
                    penetration += dpm.GetDefensePenetration(ctx);
                }
            }

            defensiveStat *= (1.0f - Math.Clamp(penetration, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (power * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            ctx.BaseDamage = baseDamage;

            // 5. Apply Outgoing Modifiers (Attacker + Move)
            float currentDamage = baseDamage;
            foreach (var modifier in attacker.OutgoingDamageModifiers)
            {
                currentDamage = modifier.ModifyOutgoingDamage(currentDamage, ctx);
            }
            foreach (var ability in move.Abilities)
            {
                if (ability is IOutgoingDamageModifier odm)
                {
                    currentDamage = odm.ModifyOutgoingDamage(currentDamage, ctx);
                }
            }

            currentDamage *= multiTargetModifier;

            // 6. Critical Hits
            if (!result.WasGraze)
            {
                bool isCrit = overrideCrit ?? CheckCritical(attacker, target, ctx, move);
                if (isCrit)
                {
                    result.WasCritical = true;
                    ctx.IsCritical = true;
                    float critMultiplier = BattleConstants.CRITICAL_HIT_MULTIPLIER;
                    foreach (var mod in target.CritModifiers) critMultiplier = mod.ModifyCritDamage(critMultiplier, ctx);
                    currentDamage *= critMultiplier;
                }
            }

            // 7. Elemental Calculation
            float elementalMultiplier = GetElementalMultiplier(move, target);
            if (elementalMultiplier > 1.0f) result.Effectiveness = ElementalEffectiveness.Effective;
            else if (elementalMultiplier > 0f && elementalMultiplier < 1.0f) result.Effectiveness = ElementalEffectiveness.Resisted;
            else if (elementalMultiplier == 0f) result.Effectiveness = ElementalEffectiveness.Immune;

            currentDamage *= elementalMultiplier;

            // 8. Apply Incoming Modifiers
            foreach (var modifier in target.IncomingDamageModifiers)
            {
                currentDamage = modifier.ModifyIncomingDamage(currentDamage, ctx);
            }

            // Burn Status Logic: Multiplies incoming damage
            if (target.HasStatusEffect(StatusEffectType.Burn))
            {
                currentDamage *= Global.Instance.BurnDamageMultiplier;
            }

            currentDamage *= (float)(_random.NextDouble() * (BattleConstants.RANDOM_VARIANCE_MAX - BattleConstants.RANDOM_VARIANCE_MIN) + BattleConstants.RANDOM_VARIANCE_MIN);
            if (result.WasGraze) currentDamage *= BattleConstants.GRAZE_MULTIPLIER;

            int finalDamageAmount = (int)Math.Floor(currentDamage);
            if (currentDamage > 0 && finalDamageAmount == 0) finalDamageAmount = 1;
            if (finalDamageAmount < 0) finalDamageAmount = 0;

            result.DamageAmount = finalDamageAmount;
            return result;
        }

        public static int CalculateBaselineDamage(BattleCombatant attacker, BattleCombatant target, MoveData move)
        {
            if (move.Power == 0) return 0;
            var ctx = new CombatContext { Actor = attacker, Target = target, Move = move };

            // Apply Calculation Modifiers to Power
            float power = move.Power;
            foreach (var ability in move.Abilities)
            {
                if (ability is ICalculationModifier cm) power = cm.ModifyBasePower(power, ctx);
            }

            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = target.GetEffectiveTenacity();

            float penetration = 0f;
            foreach (var mod in attacker.DefensePenetrationModifiers) penetration += mod.GetDefensePenetration(ctx);
            foreach (var ability in move.Abilities)
            {
                if (ability is IDefensePenetrationModifier dpm) penetration += dpm.GetDefensePenetration(ctx);
            }

            defensiveStat *= (1.0f - Math.Clamp(penetration, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (power * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;
            ctx.BaseDamage = baseDamage;
            float currentDamage = baseDamage;

            foreach (var modifier in attacker.OutgoingDamageModifiers) currentDamage = modifier.ModifyOutgoingDamage(currentDamage, ctx);
            foreach (var ability in move.Abilities)
            {
                if (ability is IOutgoingDamageModifier odm) currentDamage = odm.ModifyOutgoingDamage(currentDamage, ctx);
            }

            foreach (var modifier in target.IncomingDamageModifiers) currentDamage = modifier.ModifyIncomingDamage(currentDamage, ctx);

            if (target.HasStatusEffect(StatusEffectType.Burn))
            {
                currentDamage *= Global.Instance.BurnDamageMultiplier;
            }

            currentDamage *= BattleConstants.RANDOM_VARIANCE_MAX;
            int finalDamageAmount = (int)Math.Floor(currentDamage);
            if (currentDamage > 0 && finalDamageAmount == 0) finalDamageAmount = 1;
            return finalDamageAmount;
        }

        private static float GetOffensiveStat(BattleCombatant attacker, OffensiveStatType type)
        {
            return type switch
            {
                OffensiveStatType.Strength => attacker.GetEffectiveStrength(),
                OffensiveStatType.Intelligence => attacker.GetEffectiveIntelligence(),
                OffensiveStatType.Tenacity => attacker.GetEffectiveTenacity(),
                OffensiveStatType.Agility => attacker.GetEffectiveAgility(),
                _ => attacker.GetEffectiveStrength(),
            };
        }

        private static bool CheckCritical(BattleCombatant attacker, BattleCombatant target, CombatContext ctx, MoveData move)
        {
            float critChance = BattleConstants.CRITICAL_HIT_CHANCE;

            // Attacker Modifiers
            foreach (var mod in attacker.CritModifiers) critChance = mod.ModifyCritChance(critChance, ctx);

            // Move Modifiers
            foreach (var ability in move.Abilities)
            {
                if (ability is ICritModifier cm) critChance = cm.ModifyCritChance(critChance, ctx);
            }

            return _random.NextDouble() < critChance;
        }

        public static float GetElementalMultiplier(MoveData move, BattleCombatant target)
        {
            if (!move.OffensiveElementIDs.Any()) return 1.0f;

            var (weaknesses, resistances) = target.GetEffectiveElementalAffinities();
            float finalMultiplier = 1.0f;

            foreach (int offensiveId in move.OffensiveElementIDs)
            {
                if (weaknesses.Contains(offensiveId))
                {
                    finalMultiplier *= 2.0f;
                }

                if (resistances.Contains(offensiveId))
                {
                    finalMultiplier *= 0.5f;
                }
            }
            return finalMultiplier;
        }

        public static int GetEffectiveMovePower(BattleCombatant attacker, MoveData move)
        {
            var ctx = new CombatContext { Actor = attacker, Move = move, BaseDamage = move.Power };
            float power = move.Power;

            // Apply Calculation Modifiers
            foreach (var ability in move.Abilities)
            {
                if (ability is ICalculationModifier cm) power = cm.ModifyBasePower(power, ctx);
            }

            // Apply Outgoing Modifiers
            foreach (var mod in attacker.OutgoingDamageModifiers) power = mod.ModifyOutgoingDamage(power, ctx);
            foreach (var ability in move.Abilities)
            {
                if (ability is IOutgoingDamageModifier odm) power = odm.ModifyOutgoingDamage(power, ctx);
            }

            return (int)power;
        }
    }
}