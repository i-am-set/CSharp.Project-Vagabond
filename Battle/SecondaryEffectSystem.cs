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
        public static void ProcessEffects(QueuedAction action, List<DamageCalculator.DamageResult> damageResults)
        {
            if (action.ChosenMove?.Effects == null || !action.ChosenMove.Effects.Any())
            {
                EventBus.Publish(new GameEvents.SecondaryEffectComplete());
                return;
            }

            var targets = ResolveTargets(action);
            if (!targets.Any())
            {
                // Process self-only effects even if there are no targets
                ProcessSelfOnlyEffects(action);
                EventBus.Publish(new GameEvents.SecondaryEffectComplete());
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var result = (damageResults != null && i < damageResults.Count) ? damageResults[i] : new DamageCalculator.DamageResult();

                foreach (var effectEntry in action.ChosenMove.Effects)
                {
                    ProcessSingleEffect(effectEntry.Key, effectEntry.Value, action.Actor, target, result, targets);
                }
            }

            ProcessSelfOnlyEffects(action);

            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        private static void ProcessSelfOnlyEffects(QueuedAction action)
        {
            foreach (var effectEntry in action.ChosenMove.Effects)
            {
                switch (effectEntry.Key.ToLowerInvariant())
                {
                    case "selfdebuff":
                        HandleSelfDebuff(action.Actor, effectEntry.Value);
                        break;
                }
            }
        }

        private static void ProcessSingleEffect(string key, string value, BattleCombatant actor, BattleCombatant target, DamageCalculator.DamageResult result, List<BattleCombatant> allTargets)
        {
            switch (key.ToLowerInvariant())
            {
                case "applystatus":
                    HandleApplyStatus(actor, target, value);
                    break;
                case "lifesteal":
                    HandleLifesteal(actor, result, value);
                    break;
                case "detonatestatus":
                    HandleDetonateStatus(actor, target, value);
                    break;
                case "cleanse":
                    HandleCleanse(actor, target, value);
                    break;
                case "stealbuff":
                    HandleStealBuff(actor, target, value);
                    break;
                case "executeonkill":
                    if (target.IsDefeated) HandleExecuteOnKill(actor, allTargets, value);
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

        private static void HandleLifesteal(BattleCombatant actor, DamageCalculator.DamageResult result, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int healAmount = (int)Math.Round(result.DamageAmount * (percentage / 100f));
                if (healAmount > 0)
                {
                    actor.ApplyHealing(healAmount);
                }
            }
        }

        private static void HandleDetonateStatus(BattleCombatant actor, BattleCombatant target, string value)
        {
            var parts = value.Split(',');
            if (parts.Length != 2) return;
            if (Enum.TryParse<StatusEffectType>(parts[0].Trim(), true, out var statusTypeToDetonate) &&
                EffectParser.TryParseFloat(parts[1].Trim(), out float multiplier))
            {
                var effectInstance = target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == statusTypeToDetonate);
                if (effectInstance != null)
                {
                    int damagePerTurn = 0;
                    if (statusTypeToDetonate == StatusEffectType.Poison || statusTypeToDetonate == StatusEffectType.Burn)
                    {
                        damagePerTurn = Math.Max(1, target.Stats.MaxHP / 16);
                    }
                    // Add other DoT types here if they exist

                    if (damagePerTurn > 0)
                    {
                        int remainingDamage = (int)(damagePerTurn * effectInstance.DurationInTurns * multiplier);
                        target.ApplyDamage(remainingDamage);
                        target.ActiveStatusEffects.Remove(effectInstance);
                        // TODO: Publish event for narration/damage numbers
                    }
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
                ProcessSingleEffect(effectName, effectParams, actor, actor, new DamageCalculator.DamageResult(), allTargets);
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

        private static List<BattleCombatant> ResolveTargets(QueuedAction action)
        {
            var targetType = action.ChosenMove?.Target ?? action.ChosenItem?.Target ?? TargetType.None;
            var actor = action.Actor;
            var specifiedTarget = action.Target;
            var battleManager = ServiceLocator.Get<BattleManager>();
            var activeEnemies = battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
            var activePlayers = battleManager.AllCombatants.Where(c => c.IsPlayerControlled && !c.IsDefeated).ToList();

            switch (targetType)
            {
                case TargetType.Single:
                    return specifiedTarget != null && !specifiedTarget.IsDefeated ? new List<BattleCombatant> { specifiedTarget } : new List<BattleCombatant>();
                case TargetType.Every:
                    return actor.IsPlayerControlled ? activeEnemies : activePlayers;
                case TargetType.SingleAll:
                    return specifiedTarget != null && !specifiedTarget.IsDefeated ? new List<BattleCombatant> { specifiedTarget } : new List<BattleCombatant>();
                case TargetType.EveryAll:
                    return battleManager.AllCombatants.Where(c => !c.IsDefeated).ToList();
                case TargetType.Self:
                    return new List<BattleCombatant> { actor };
                case TargetType.None:
                default:
                    return new List<BattleCombatant>();
            }
        }
    }
}