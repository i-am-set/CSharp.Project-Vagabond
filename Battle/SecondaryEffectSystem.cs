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
            var attacker = action.Actor;

            // Process effects that apply once to the actor, based on aggregate results.
            ProcessActorEffects(action, finalTargets, damageResults);

            // Process effects that apply once per unique target.
            var uniqueTargets = finalTargets.Distinct().ToList();
            foreach (var target in uniqueTargets)
            {
                if (target.IsDefeated) continue; // Don't apply effects to already defeated targets

                // --- Process Move-based Effects ---
                if (move?.Effects != null)
                {
                    foreach (var effectEntry in move.Effects)
                    {
                        ProcessTargetEffect(effectEntry.Key, effectEntry.Value, attacker, target, finalTargets);
                    }
                }

                // --- Process Ability-based Reactive Effects ---
                if (move != null)
                {
                    // Attacker abilities (e.g., Venomous)
                    if (move.MakesContact)
                    {
                        foreach (var ability in attacker.ActiveAbilities)
                        {
                            if (ability.Effects.TryGetValue("ApplyStatusOnContact", out var value))
                            {
                                HandleApplyStatus(attacker, target, value, ability);
                            }
                        }
                    }

                    // Target abilities (e.g., Static Charge, Iron Barbs)
                    if (move.MakesContact)
                    {
                        foreach (var ability in target.ActiveAbilities)
                        {
                            if (ability.Effects.TryGetValue("ApplyStatusOnBeingHitContact", out var value))
                            {
                                HandleApplyStatus(target, attacker, value, ability); // Note: target applies status to attacker
                            }

                            if (ability.Effects.TryGetValue("IronBarbsOnContact", out var barbValue) && EffectParser.TryParseFloat(barbValue, out float percent))
                            {
                                int recoilDamage = Math.Max(1, (int)(attacker.Stats.MaxHP * (percent / 100f)));
                                attacker.ApplyDamage(recoilDamage);
                                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = attacker, RecoilDamage = recoilDamage, SourceAbility = ability });
                                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = ability });
                            }
                        }
                    }

                    // Target abilities (e.g. Photosynthesis heal)
                    bool wasImmuneHit = false;
                    for (int i = 0; i < finalTargets.Count; i++)
                    {
                        if (finalTargets[i] == target && damageResults[i].Effectiveness == DamageCalculator.ElementalEffectiveness.Immune)
                        {
                            wasImmuneHit = true;
                            break;
                        }
                    }

                    if (wasImmuneHit)
                    {
                        foreach (var ability in target.ActiveAbilities)
                        {
                            if (ability.Effects.TryGetValue("ElementImmunityAndHeal", out var value))
                            {
                                var parts = value.Split(',');
                                if (parts.Length == 2 && EffectParser.TryParseInt(parts[0], out int immuneElementId) && EffectParser.TryParseFloat(parts[1], out float healPercent))
                                {
                                    if (move.OffensiveElementIDs.Contains(immuneElementId))
                                    {
                                        int hpBefore = (int)target.VisualHP;
                                        int healAmount = (int)(target.Stats.MaxHP * (healPercent / 100f));
                                        target.ApplyHealing(healAmount);
                                        EventBus.Publish(new GameEvents.CombatantHealed
                                        {
                                            Actor = target,
                                            Target = target,
                                            HealAmount = healAmount,
                                            VisualHPBefore = hpBefore
                                        });
                                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = ability, NarrationText = $"{target.Name}'s {ability.AbilityName} absorbed the attack!" });
                                    }
                                }
                            }
                        }
                    }
                }

                // --- Process Reactive Status Effects on Target ---
                if (target.HasStatusEffect(StatusEffectType.Burn) && move != null)
                {
                    bool isPhysical = move.ImpactType == ImpactType.Physical;
                    bool isFire = move.OffensiveElementIDs.Contains(2); // 2 is the ElementID for Fire

                    if (isPhysical || isFire)
                    {
                        int totalDamageToThisTarget = 0;
                        for (int i = 0; i < finalTargets.Count; i++)
                        {
                            if (finalTargets[i] == target)
                            {
                                totalDamageToThisTarget += damageResults[i].DamageAmount;
                            }
                        }

                        if (totalDamageToThisTarget > 0)
                        {
                            target.ApplyDamage(totalDamageToThisTarget);
                            EventBus.Publish(new GameEvents.StatusEffectTriggered
                            {
                                Combatant = target,
                                EffectType = StatusEffectType.Burn,
                                Damage = totalDamageToThisTarget
                            });
                        }
                    }
                }
            }

            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        private static void ProcessActorEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            if (move == null) return;

            if (move.Effects.TryGetValue("Lifesteal", out var lifestealValue))
            {
                int totalDamage = damageResults.Sum(r => r.DamageAmount);
                HandleLifesteal(action.Actor, totalDamage, lifestealValue);
            }
            if (move.Effects.TryGetValue("SelfDebuff", out var selfDebuffValue))
            {
                HandleSelfDebuff(action.Actor, selfDebuffValue);
            }
            if (move.Effects.TryGetValue("Recoil", out var recoilValue))
            {
                int totalDamage = damageResults.Sum(r => r.DamageAmount);
                HandleRecoil(action.Actor, totalDamage, recoilValue);
            }
        }

        private static void ProcessTargetEffect(string key, string value, BattleCombatant actor, BattleCombatant target, List<BattleCombatant> allTargets)
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
                    if (target.IsDefeated) HandleExecuteOnKill(actor, allTargets, value);
                    break;
            }
        }

        private static void HandleApplyStatus(BattleCombatant actor, BattleCombatant target, string value, AbilityData sourceAbility = null)
        {
            if (EffectParser.TryParseStatusEffectParams(value, out var type, out int chance, out int duration))
            {
                // Check for Lingering Curse ability on the actor
                foreach (var ability in actor.ActiveAbilities)
                {
                    if (ability.Effects.TryGetValue("StatusDurationBonus", out var bonusValue) && IsNegativeStatus(type))
                    {
                        if (EffectParser.TryParseInt(bonusValue, out int bonusDuration))
                        {
                            duration += bonusDuration;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = ability });
                        }
                    }
                }

                if (_random.Next(1, 101) <= chance)
                {
                    target.AddStatusEffect(new StatusEffectInstance(type, duration));
                    if (sourceAbility != null)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = sourceAbility, NarrationText = $"{actor.Name}'s {sourceAbility.AbilityName} afflicted {target.Name}!" });
                    }
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

        private static void HandleRecoil(BattleCombatant actor, int totalDamage, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                if (totalDamage > 0)
                {
                    int recoilDamage = Math.Max(1, (int)(totalDamage * (percentage / 100f)));
                    actor.ApplyDamage(recoilDamage);
                    EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = actor, RecoilDamage = recoilDamage });
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
                ProcessTargetEffect(effectName, effectParams, actor, actor, allTargets);
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
                case StatusEffectType.StrengthDown:
                    return true;
                default:
                    return false;
            }
        }
    }
}