using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectVagabond.Battle
{
    public static class DamageCalculator
    {
        private static readonly Random _random = new Random();
        private const float GLOBAL_DAMAGE_SCALAR = 0.125f;
        private const int FLAT_DAMAGE_BONUS = 1;

        private const float BASELINE_DEFENSE_DIVISOR = 5.0f;

        public struct DamageResult
        {
            public int DamageAmount;
            public bool WasCritical;
            public bool WasGraze;
            public bool WasProtected;
            public bool WasVulnerable;
        }

        public static DamageResult CalculateDamage(QueuedAction action, BattleCombatant target, MoveData move, float multiTargetModifier = 1.0f, bool? overrideCrit = null, bool isSimulation = false)
        {
            var attacker = action.Actor;
            var result = new DamageResult
            {
                WasProtected = false,
                WasVulnerable = false
            };

            var ctx = new CombatTriggerContext
            {
                Actor = attacker,
                Target = target,
                Move = move,
                Action = action,
                IsSimulation = isSimulation
            };

            // 1. Accuracy Check
            if (move.Accuracy != -1)
            {
                ctx.IsCancelled = false;
                attacker.NotifyAbilities(CombatEventType.CheckEvasion, ctx);
                foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CheckEvasion, ctx);
                bool ignoreEvasion = ctx.IsCancelled;

                float accuracyMultiplier = 1.0f;
                if (target.HasStatusEffect(StatusEffectType.Dodging) && !ignoreEvasion)
                {
                    accuracyMultiplier = Global.Instance.DodgingAccuracyMultiplier;
                }

                int effectiveAccuracy = attacker.GetEffectiveAccuracy(move.Accuracy);
                float hitChance = effectiveAccuracy * accuracyMultiplier;

                if (_random.Next(1, 101) > hitChance) result.WasGraze = true;
            }

            // --- Guard Interaction Logic ---
            ctx.BreakGuard = false;
            attacker.NotifyAbilities(CombatEventType.CheckGuardInteraction, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CheckGuardInteraction, ctx);

            if (target.HasStatusEffect(StatusEffectType.Protected))
            {
                if (!ctx.BreakGuard)
                {
                    result.WasProtected = true;
                    result.DamageAmount = 0;
                    return result;
                }
                else
                {
                    if (!isSimulation)
                    {
                        target.ActiveStatusEffects.RemoveAll(e => e.EffectType == StatusEffectType.Protected);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = StatusEffectType.Protected });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "GUARD BROKEN!" });
                    }
                }
            }

            // 2. Fixed Damage Check
            ctx.StatValue = 0;
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateFixedDamage, ctx);
            if (ctx.StatValue > 0)
            {
                result.DamageAmount = (int)ctx.StatValue;
                return result;
            }

            // 3. Base Power
            ctx.BasePower = move.Power;
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateBasePower, ctx);
            if (ctx.BasePower == 0) return result;

            // 4. Base Damage
            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = BASELINE_DEFENSE_DIVISOR;

            ctx.StatValue = 0f;
            attacker.NotifyAbilities(CombatEventType.CalculateDefensePenetration, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateDefensePenetration, ctx);
            float penetration = ctx.StatValue;

            defensiveStat *= (1.0f - Math.Clamp(penetration, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float statRatio = offensiveStat / defensiveStat;
            float baseDamage = (ctx.BasePower * statRatio * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            // 5. Outgoing Modifiers
            ctx.StatValue = baseDamage;
            ctx.ResetMultipliers();

            attacker.NotifyAbilities(CombatEventType.CalculateOutgoingDamage, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateOutgoingDamage, ctx);

            if (attacker.HasStatusEffect(StatusEffectType.Empowered)) ctx.DamageMultiplier *= Global.Instance.EmpoweredDamageMultiplier;

            float currentDamage = ctx.StatValue * ctx.DamageMultiplier * multiTargetModifier;

            // 6. Critical Hits
            if (!result.WasGraze)
            {
                bool isCrit = overrideCrit ?? CheckCritical(attacker, target, ctx);
                if (isCrit)
                {
                    result.WasCritical = true;
                    ctx.IsCritical = true;

                    ctx.StatValue = BattleConstants.CRITICAL_HIT_MULTIPLIER;
                    target.NotifyAbilities(CombatEventType.CheckCritDamage, ctx);
                    currentDamage *= ctx.StatValue;
                }
            }

            // 7. Incoming Modifiers
            ctx.StatValue = currentDamage;
            ctx.ResetMultipliers();
            target.NotifyAbilities(CombatEventType.CalculateIncomingDamage, ctx);

            // Ally Modifiers
            var battleManager = ServiceLocator.Get<BattleManager>();
            var allies = battleManager.AllCombatants.Where(c => c != target && c.IsPlayerControlled == target.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField);
            foreach (var ally in allies) ally.NotifyAbilities(CombatEventType.CalculateAllyDamage, ctx);

            currentDamage = ctx.StatValue * ctx.DamageMultiplier;

            // --- Tenacity Shield Break Logic ---
            if (target.CurrentTenacity <= 0)
            {
                currentDamage *= 2.0f;
                result.WasVulnerable = true; // Flag for UI
            }

            if (target.HasStatusEffect(StatusEffectType.Burn)) currentDamage *= Global.Instance.BurnDamageMultiplier;

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
            var ctx = new CombatTriggerContext { Actor = attacker, Target = target, Move = move, IsSimulation = true };

            ctx.BasePower = move.Power;
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateBasePower, ctx);

            float offensiveStat = GetOffensiveStat(attacker, move.OffensiveStat);
            float defensiveStat = BASELINE_DEFENSE_DIVISOR;

            ctx.StatValue = 0f;
            attacker.NotifyAbilities(CombatEventType.CalculateDefensePenetration, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateDefensePenetration, ctx);

            defensiveStat *= (1.0f - Math.Clamp(ctx.StatValue, 0f, 1f));
            if (defensiveStat < 1) defensiveStat = 1;

            float baseDamage = (ctx.BasePower * (offensiveStat / defensiveStat) * GLOBAL_DAMAGE_SCALAR) + FLAT_DAMAGE_BONUS;

            ctx.StatValue = baseDamage;
            ctx.ResetMultipliers();
            attacker.NotifyAbilities(CombatEventType.CalculateOutgoingDamage, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateOutgoingDamage, ctx);
            if (attacker.HasStatusEffect(StatusEffectType.Empowered)) ctx.DamageMultiplier *= Global.Instance.EmpoweredDamageMultiplier;

            float currentDamage = ctx.StatValue * ctx.DamageMultiplier;

            ctx.StatValue = currentDamage;
            ctx.ResetMultipliers();
            target.NotifyAbilities(CombatEventType.CalculateIncomingDamage, ctx);
            currentDamage = ctx.StatValue * ctx.DamageMultiplier;

            if (target.CurrentTenacity <= 0)
            {
                currentDamage *= 2.0f;
            }

            if (target.HasStatusEffect(StatusEffectType.Burn)) currentDamage *= Global.Instance.BurnDamageMultiplier;

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

        private static bool CheckCritical(BattleCombatant attacker, BattleCombatant target, CombatTriggerContext ctx)
        {
            ctx.StatValue = BattleConstants.CRITICAL_HIT_CHANCE;
            attacker.NotifyAbilities(CombatEventType.CheckCritChance, ctx);
            foreach (var ab in ctx.Move.Abilities) ab.OnCombatEvent(CombatEventType.CheckCritChance, ctx);
            return _random.NextDouble() < ctx.StatValue;
        }

        public static int GetEffectiveMovePower(BattleCombatant attacker, MoveData move)
        {
            var ctx = new CombatTriggerContext { Actor = attacker, Move = move, BasePower = move.Power, IsSimulation = true };
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateBasePower, ctx);

            ctx.StatValue = ctx.BasePower;
            ctx.ResetMultipliers();
            attacker.NotifyAbilities(CombatEventType.CalculateOutgoingDamage, ctx);
            foreach (var ab in move.Abilities) ab.OnCombatEvent(CombatEventType.CalculateOutgoingDamage, ctx);

            if (attacker.HasStatusEffect(StatusEffectType.Empowered)) ctx.DamageMultiplier *= Global.Instance.EmpoweredDamageMultiplier;

            return (int)(ctx.StatValue * ctx.DamageMultiplier);
        }
    }
}