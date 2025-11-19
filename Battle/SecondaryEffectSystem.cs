using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle
{
    public static class SecondaryEffectSystem
    {
        private static readonly Random _random = new Random();

        public static void ProcessPrimaryEffects(QueuedAction action, BattleCombatant target)
        {
            var move = action.ChosenMove;
            var attacker = action.Actor;

            if (move?.Effects != null)
            {
                foreach (var effectEntry in move.Effects)
                {
                    switch (effectEntry.Key.ToLowerInvariant())
                    {
                        case "applystatus":
                            HandleApplyStatus(attacker, target, effectEntry.Value);
                            break;
                        case "cleanse":
                            HandleCleanse(attacker, target, effectEntry.Value);
                            break;
                        case "stealbuff":
                            HandleStealBuff(attacker, target, effectEntry.Value);
                            break;
                        case "restoremana":
                            HandleRestoreMana(attacker, target, effectEntry.Value);
                            break;
                        case var s when s.StartsWith("modifystatstage"):
                            HandleModifyStatStage(attacker, target, effectEntry.Value);
                            break;
                    }
                }
            }

            if (move != null && move.MakesContact)
            {
                foreach (var relic in attacker.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("ApplyStatusOnContact", out var value))
                    {
                        HandleApplyStatus(attacker, target, value, relic);
                    }
                }
            }
        }

        public static void ProcessSecondaryEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            var attacker = action.Actor;

            ProcessActorEffects(action, finalTargets, damageResults);

            var uniqueTargets = finalTargets.Distinct().ToList();
            for (int targetIndex = 0; targetIndex < uniqueTargets.Count; targetIndex++)
            {
                var target = uniqueTargets[targetIndex];

                if (move?.Effects != null)
                {
                    foreach (var effectEntry in move.Effects)
                    {
                        switch (effectEntry.Key.ToLowerInvariant())
                        {
                            case "executeonkill":
                                if (target.IsDefeated) HandleExecuteOnKill(attacker, finalTargets, effectEntry.Value);
                                break;
                        }
                    }
                }

                if (move != null && !target.IsDefeated)
                {
                    if (move.MakesContact)
                    {
                        foreach (var relic in target.ActiveRelics)
                        {
                            if (relic.Effects.TryGetValue("ApplyStatusOnBeingHitContact", out var value))
                            {
                                HandleApplyStatus(target, attacker, value, relic);
                            }

                            if (relic.Effects.TryGetValue("IronBarbsOnContact", out var barbValue) && EffectParser.TryParseFloat(barbValue, out float percent))
                            {
                                int recoilDamage = Math.Max(1, (int)(attacker.Stats.MaxHP * (percent / 100f)));
                                attacker.ApplyDamage(recoilDamage);
                                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = attacker, RecoilDamage = recoilDamage, SourceAbility = relic });
                                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = relic });
                            }
                        }
                    }

                    bool wasCriticallyHit = damageResults.Any(r => r.WasCritical);

                    if (wasCriticallyHit)
                    {
                        foreach (var relic in target.ActiveRelics)
                        {
                            if (relic.Effects.ContainsKey("PainFuel"))
                            {
                                var (successStr, _) = target.ModifyStatStage(OffensiveStatType.Strength, 2);
                                if (successStr) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Strength, Amount = 2 });

                                var (successInt, _) = target.ModifyStatStage(OffensiveStatType.Intelligence, 2);
                                if (successInt) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Intelligence, Amount = 2 });

                                if (successStr || successInt)
                                {
                                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = relic, NarrationText = $"{target.Name}'s {relic.AbilityName} turned pain into power!" });
                                }
                            }
                        }
                    }

                    bool wasImmuneHit = damageResults.Any(r => r.Effectiveness == DamageCalculator.ElementalEffectiveness.Immune);

                    if (wasImmuneHit)
                    {
                        foreach (var relic in target.ActiveRelics)
                        {
                            if (relic.Effects.TryGetValue("ElementImmunityAndHeal", out var value))
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
                                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = relic, NarrationText = $"{target.Name}'s {relic.AbilityName} absorbed the attack!" });
                                    }
                                }
                            }
                        }
                    }
                }

                if (target.HasStatusEffect(StatusEffectType.Burn) && move != null && !target.IsDefeated)
                {
                    bool isPhysical = move.ImpactType == ImpactType.Physical;
                    bool isFire = move.OffensiveElementIDs.Contains(2);

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

            if (finalTargets.Any(t => t.IsDefeated))
            {
                foreach (var relic in attacker.ActiveRelics)
                {
                    if (relic.Effects.ContainsKey("Momentum"))
                    {
                        attacker.IsMomentumActive = true;
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = attacker, Ability = relic, NarrationText = $"{attacker.Name}'s {relic.AbilityName} is building!" });
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
                HandleLifesteal(action.Actor, totalDamage, lifestealValue, finalTargets);
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

        private static void HandleModifyStatStage(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseStatStageParams(value, out var stat, out int amount, out int chance, out string targetStr))
            {
                if (_random.Next(1, 101) <= chance)
                {
                    BattleCombatant finalTarget = targetStr.Equals("Self", StringComparison.OrdinalIgnoreCase) ? actor : target;

                    var (success, message) = finalTarget.ModifyStatStage(stat, amount);

                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = message });

                    if (success)
                    {
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = finalTarget, Stat = stat, Amount = amount });
                    }
                }
            }
        }


        private static void HandleApplyStatus(BattleCombatant actor, BattleCombatant target, string value, RelicData sourceRelic = null)
        {
            if (EffectParser.TryParseStatusEffectParams(value, out var type, out int chance, out int duration))
            {
                foreach (var relic in actor.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("StatusDurationBonus", out var bonusValue) && IsNegativeStatus(type))
                    {
                        if (EffectParser.TryParseInt(bonusValue, out int bonusDuration))
                        {
                            duration += bonusDuration;
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = relic });
                        }
                    }
                }

                if (_random.Next(1, 101) <= chance)
                {
                    bool wasNewlyApplied = target.AddStatusEffect(new StatusEffectInstance(type, duration));
                    if (sourceRelic != null)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = actor, Ability = sourceRelic, NarrationText = $"{actor.Name}'s {sourceRelic.AbilityName} afflicted {target.Name}!" });
                    }

                    if (wasNewlyApplied && IsNegativeStatus(type))
                    {
                        foreach (var relic in actor.ActiveRelics)
                        {
                            if (relic.Effects.TryGetValue("Contagion", out var contagionValue))
                            {
                                if (EffectParser.TryParseIntArray(contagionValue, out int[] contagionParams) && contagionParams.Length == 2)
                                {
                                    int contagionChance = contagionParams[0];
                                    int contagionDuration = contagionParams[1];

                                    if (_random.Next(1, 101) <= contagionChance)
                                    {
                                        var battleManager = ServiceLocator.Get<BattleManager>();
                                        var allCombatants = battleManager.AllCombatants;

                                        var potentialTargets = allCombatants.Where(c =>
                                            c.IsPlayerControlled != actor.IsPlayerControlled &&
                                            c != target &&
                                            !c.IsDefeated &&
                                            !c.HasStatusEffect(type)
                                        ).ToList();

                                        if (potentialTargets.Any())
                                        {
                                            var contagionTarget = potentialTargets[_random.Next(potentialTargets.Count)];
                                            contagionTarget.AddStatusEffect(new StatusEffectInstance(type, contagionDuration));
                                            EventBus.Publish(new GameEvents.AbilityActivated
                                            {
                                                Combatant = actor,
                                                Ability = relic,
                                                NarrationText = $"{actor.Name}'s {relic.AbilityName} spread the effect to {contagionTarget.Name}!"
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var relic in actor.ActiveRelics)
                        {
                            if (relic.Effects.ContainsKey("Sadist"))
                            {
                                var (success, message) = actor.ModifyStatStage(OffensiveStatType.Strength, 1);
                                if (success)
                                {
                                    EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = actor, Stat = OffensiveStatType.Strength, Amount = 1 });
                                    EventBus.Publish(new GameEvents.AbilityActivated
                                    {
                                        Combatant = actor,
                                        Ability = relic,
                                        NarrationText = $"{actor.Name}'s {relic.AbilityName} raised their Strength!"
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void HandleLifesteal(BattleCombatant actor, int totalDamage, string value, List<BattleCombatant> targets)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int healAmount = (int)Math.Round(totalDamage * (percentage / 100f));
                if (healAmount <= 0) return;

                bool causticBloodTriggered = false;
                RelicData sourceRelic = null;

                foreach (var target in targets.Distinct())
                {
                    var cbRelic = target.ActiveRelics.FirstOrDefault(a => a.Effects.ContainsKey("CausticBlood"));
                    if (cbRelic != null)
                    {
                        causticBloodTriggered = true;
                        sourceRelic = cbRelic;
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = cbRelic, NarrationText = $"{target.Name}'s {cbRelic.AbilityName} burns {actor.Name}!" });
                        break;
                    }
                }

                if (causticBloodTriggered)
                {
                    actor.ApplyDamage(healAmount);
                    EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = actor, RecoilDamage = healAmount, SourceAbility = sourceRelic });
                }
                else
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
                    actor.AddStatusEffect(buff);
                }
            }
        }

        private static void HandleExecuteOnKill(BattleCombatant actor, List<BattleCombatant> allTargets, string value)
        {
            var parts = value.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                string effectName = parts[0];
                string effectParams = parts[1];
                if (effectName.ToLowerInvariant().StartsWith("modifystatstage"))
                {
                    HandleModifyStatStage(actor, actor, effectParams);
                }
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

        private static void HandleRestoreMana(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int amountToRestore = (int)(target.Stats.MaxMana * (percentage / 100f));
                int manaBefore = target.Stats.CurrentMana;
                target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amountToRestore);
                int actualRestored = target.Stats.CurrentMana - manaBefore;

                if (actualRestored > 0)
                {
                    EventBus.Publish(new GameEvents.CombatantManaRestored
                    {
                        Target = target,
                        AmountRestored = actualRestored,
                        ManaBefore = manaBefore,
                        ManaAfter = target.Stats.CurrentMana
                    });
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
                    return true;
                default:
                    return false;
            }
        }
    }
}
