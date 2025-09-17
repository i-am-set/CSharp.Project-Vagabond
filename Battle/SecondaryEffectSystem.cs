using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A stateless, static system for processing secondary effects of moves.
    /// It maps effect IDs to specific logic and publishes an event upon completion.
    /// </summary>
    public static class SecondaryEffectSystem
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Processes all secondary effects listed in a move after the primary action (damage/healing) has resolved.
        /// </summary>
        public static void ProcessEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            if (move?.Effects == null || !move.Effects.Any())
            {
                EventBus.Publish(new GameEvents.SecondaryEffectComplete());
                return;
            }

            // Process effects that apply once to the actor, based on aggregate results.
            ProcessActorEffects(action, finalTargets, damageResults);

            // Process effects that apply once per unique target.
            var uniqueTargets = finalTargets.Distinct().ToList();
            foreach (var target in uniqueTargets)
            {
                bool wasDefeatedByThisAction = target.IsDefeated && finalTargets.Contains(target);
                foreach (var effectEntry in move.Effects)
                {
                    ProcessTargetEffect(effectEntry.Key, effectEntry.Value, action.Actor, target, wasDefeatedByThisAction, finalTargets);
                }
            }

            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        private static void ProcessActorEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            if (move.Effects.TryGetValue("Lifesteal", out var lifestealValue))
            {
                int totalDamage = damageResults.Sum(r => r.DamageAmount);
                HandleLifesteal(action.Actor, totalDamage, lifestealValue);
            }
            if (move.Effects.TryGetValue("SelfDebuff", out var selfDebuffValue))
            {
                HandleSelfDebuff(action.Actor, selfDebuffValue);
            }
        }

        private static void ProcessTargetEffect(string key, string value, BattleCombatant actor, BattleCombatant target, bool wasDefeated, List<BattleCombatant> allTargets)
        {
            switch (key.ToLowerInvariant())
            {
                case "applystatus":
                    HandleApplyStatus(actor, target, value);
                    break;
                case "cleanse":
                    HandleCleanse(actor, target, value);
                    break;
                case "stealbuff":
                    HandleStealBuff(actor, target, value);
                    break;
                case "executeonkill":
                    if (wasDefeated) HandleExecuteOnKill(actor, allTargets, value);
                    break;
            }
        }

        private static void HandleApplyStatus(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseStatusEffectParams(value, out var type, out int chance, out int duration))
            {
                if (_random.Next(1, 101) <= chance)
                {
                    target.AddStatusEffect(new StatusEffectInstance(type, duration));
                }
            }
        }

        private static void HandleLifesteal(BattleCombatant actor, int totalDamage, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int healAmount = (int)Math.Round(totalDamage * (percentage / 100f));
                if (healAmount > 0)
                {
                    int hpBefore = (int)actor.VisualHP;
                    actor.ApplyHealing(healAmount);
                    EventBus.Publish(new GameEvents.CombatantHealed
                    {
                        Actor = actor,
                        Target = actor,
                        HealAmount = healAmount,
                        VisualHPBefore = hpBefore
                    });
                }
            }
        }

        private static void HandleCleanse(BattleCombatant actor, BattleCombatant target, string value)
        {
            // For now, we assume "Cleanse" removes all negative status effects from the target.
            // This could be expanded to parse specific effects or categories.
            target.ActiveStatusEffects.RemoveAll(e => IsNegativeStatus(e.EffectType));
        }

        private static void HandleStealBuff(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseInt(value, out int chance) && _random.Next(1, 101) <= chance)
            {
                var buff = target.ActiveStatusEffects.FirstOrDefault(e => !IsNegativeStatus(e.EffectType));
                if (buff != null)
                {
                    target.ActiveStatusEffects.Remove(buff);
                    actor.AddStatusEffect(buff); // Adds the buff with its remaining duration
                }
            }
        }

        private static void HandleExecuteOnKill(BattleCombatant actor, List<BattleCombatant> allTargets, string value)
        {
            // This is a meta-effect. It parses the effect to execute from its value.
            // Example: "ExecuteOnKill": "ApplyStatus(StrengthUp,100,2)"
            // For simplicity, we'll assume the target of the sub-effect is the actor.
            var parts = value.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                string effectName = parts[0];
                string effectParams = parts[1];
                ProcessTargetEffect(effectName, effectParams, actor, actor, false, allTargets);
            }
        }

        private static void HandleSelfDebuff(BattleCombatant actor, string value)
        {
            if (EffectParser.TryParseStatusEffectParams(value, out var type, out int chance, out int duration))
            {
                if (_random.Next(1, 101) <= chance)
                {
                    actor.AddStatusEffect(new StatusEffectInstance(type, duration));
                }
            }
        }

        private static bool IsNegativeStatus(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison:
                case StatusEffectType.Stun:
                case StatusEffectType.Burn:
                case StatusEffectType.Freeze:
                case StatusEffectType.Blind:
                case StatusEffectType.Confuse:
                case StatusEffectType.Silence:
                case StatusEffectType.Fear:
                case StatusEffectType.Root:
                case StatusEffectType.IntelligenceDown:
                case StatusEffectType.AgilityDown:
                    return true;
                default:
                    return false;
            }
        }
    }
}