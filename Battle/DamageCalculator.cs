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
                DefenderAbilitiesTriggered = new List<RelicData>()
            };

            if (move.Effects.TryGetValue("FixedDamage", out var fixedDamageValue) && EffectParser.TryParseInt(fixedDamageValue, out int fixedDamage))
            {
                result.DamageAmount = fixedDamage;
                return result;
            }

            if (move.Power == 0) return result;

            // 2. Build Combat Context
            var ctx = new CombatContext
            {
                Actor = attacker,
                Target = target,
                Move = move,
                MultiTargetModifier = multiTargetModifier,
                IsLastAction = action.IsLastActionInRound
            };

            // 3. Accuracy Check
            if (move.Accuracy != -1)
            {
                bool ignoreEvasion = attacker.AccuracyModifiers.Any(m => m.ShouldIgnoreEvasion(ctx));
                if (target.HasStatusEffect(StatusEffectType.Dodging) && !ignoreEvasion) result.WasGraze = true;
                else
                {
                    int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);
                    if (_random.Next(1, 101) > effectiveAccuracy) result.WasGraze = true;
                }
            }

            // 4. Calculate Base Damage
            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = target.GetEffectiveTenacity();

            // Apply Armor Penetration (Move + Abilities)
            float penetration = 0f;
            if (move.Effects.TryGetValue("ArmorPierce", out var armorPierceValue) && EffectParser.TryParseFloat(armorPierceValue, out float piercePercent))
            {
                penetration += (piercePercent / 100f);
            }
            foreach (var mod in attacker.DefensePenetrationModifiers)
            {
                penetration += mod.GetDefensePenetration(ctx);
            }

            defensiveStat *= (1.0f - Math.Clamp(penetration, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (move.Power * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            ctx.BaseDamage = baseDamage;

            // 5. Apply Outgoing Modifiers
            float currentDamage = baseDamage;
            foreach (var modifier in attacker.OutgoingDamageModifiers)
            {
                currentDamage = modifier.ModifyOutgoingDamage(currentDamage, ctx);
            }

            if (move.Effects.TryGetValue("Execute", out var executeValue) && EffectParser.TryParseFloatArray(executeValue, out float[] execParams) && execParams.Length == 2)
            {
                if ((float)target.Stats.CurrentHP / target.Stats.MaxHP <= (execParams[0] / 100f)) currentDamage *= execParams[1];
            }

            currentDamage *= multiTargetModifier;

            // 7. Critical Hits
            if (!result.WasGraze)
            {
                bool isCrit = overrideCrit ?? CheckCritical(attacker, target, ctx);
                if (isCrit)
                {
                    result.WasCritical = true;
                    ctx.IsCritical = true;
                    float critMultiplier = BattleConstants.CRITICAL_HIT_MULTIPLIER;
                    foreach (var mod in target.CritModifiers) critMultiplier = mod.ModifyCritDamage(critMultiplier, ctx);
                    currentDamage *= critMultiplier;
                }
            }

            // 8. Elemental Calculation
            float elementalMultiplier = GetElementalMultiplier(move, target);
            if (elementalMultiplier > 1.0f) result.Effectiveness = ElementalEffectiveness.Effective;
            else if (elementalMultiplier > 0f && elementalMultiplier < 1.0f) result.Effectiveness = ElementalEffectiveness.Resisted;
            else if (elementalMultiplier == 0f) result.Effectiveness = ElementalEffectiveness.Immune;

            currentDamage *= elementalMultiplier;

            // 9. Apply Incoming Modifiers
            foreach (var modifier in target.IncomingDamageModifiers)
            {
                currentDamage = modifier.ModifyIncomingDamage(currentDamage, ctx);
            }

            if (target.HasStatusEffect(StatusEffectType.Freeze) && move.ImpactType == ImpactType.Physical) currentDamage *= 2.0f;

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
            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = target.GetEffectiveTenacity();

            float penetration = 0f;
            if (move.Effects.TryGetValue("ArmorPierce", out var armorPierceValue) && EffectParser.TryParseFloat(armorPierceValue, out float piercePercent)) penetration += (piercePercent / 100f);
            foreach (var mod in attacker.DefensePenetrationModifiers) penetration += mod.GetDefensePenetration(ctx);
            defensiveStat *= (1.0f - Math.Clamp(penetration, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (move.Power * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;
            ctx.BaseDamage = baseDamage;
            float currentDamage = baseDamage;
            foreach (var modifier in attacker.OutgoingDamageModifiers) currentDamage = modifier.ModifyOutgoingDamage(currentDamage, ctx);
            foreach (var modifier in target.IncomingDamageModifiers) currentDamage = modifier.ModifyIncomingDamage(currentDamage, ctx);
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

        private static bool CheckCritical(BattleCombatant attacker, BattleCombatant target, CombatContext ctx)
        {
            float critChance = BattleConstants.CRITICAL_HIT_CHANCE;
            foreach (var mod in attacker.CritModifiers) critChance = mod.ModifyCritChance(critChance, ctx);
            if (target.HasStatusEffect(StatusEffectType.Root)) critChance *= 2.0f;
            return _random.NextDouble() < critChance;
        }

        public static float GetElementalMultiplier(MoveData move, BattleCombatant target)
        {
            var targetDefensiveElements = target.GetEffectiveDefensiveElementIDs();
            if (!move.OffensiveElementIDs.Any() || !targetDefensiveElements.Any()) return 1.0f;
            float finalMultiplier = 1.0f;
            foreach (int offensiveId in move.OffensiveElementIDs)
            {
                if (BattleDataCache.InteractionMatrix.TryGetValue(offensiveId, out var attackRow))
                {
                    foreach (int defensiveId in targetDefensiveElements)
                    {
                        if (attackRow.TryGetValue(defensiveId, out float multiplier)) finalMultiplier *= multiplier;
                    }
                }
            }
            return finalMultiplier;
        }

        public static int GetEffectiveMovePower(BattleCombatant attacker, MoveData move)
        {
            var ctx = new CombatContext { Actor = attacker, Move = move, BaseDamage = move.Power };
            float power = move.Power;
            foreach (var mod in attacker.OutgoingDamageModifiers) power = mod.ModifyOutgoingDamage(power, ctx);
            return (int)power;
        }
    }
}