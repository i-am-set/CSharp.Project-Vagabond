using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities; // New Namespace
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
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
                            target.ActiveStatusEffects.RemoveAll(e => IsNegativeStatus(e.EffectType));
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

            // OnHit effects are now handled in ProcessSecondaryEffects to ensure they happen after damage calculation
            // or concurrently with it, but logically separated from move-specific effects.
        }

        public static void ProcessSecondaryEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            var attacker = action.Actor;

            var ctx = new CombatContext
            {
                Actor = attacker,
                Move = move,
                Item = action.ChosenItem
            };

            ProcessMoveSelfEffects(action, finalTargets, damageResults);

            var uniqueTargets = finalTargets.Distinct().ToList();
            for (int i = 0; i < uniqueTargets.Count; i++)
            {
                var target = uniqueTargets[i];
                ctx.Target = target;

                int damageToTarget = 0;
                bool wasCrit = false;

                for (int k = 0; k < finalTargets.Count; k++)
                {
                    if (finalTargets[k] == target)
                    {
                        damageToTarget += damageResults[k].DamageAmount;
                        if (damageResults[k].WasCritical) wasCrit = true;
                    }
                }

                ctx.IsCritical = wasCrit;

                // --- NEW ABILITY SYSTEM HOOKS ---

                // A. Attacker OnHit
                foreach (var ability in attacker.OnHitEffects)
                {
                    ability.OnHit(ctx, damageToTarget);
                }

                // B. Defender OnDamaged
                var defenderCtx = new CombatContext
                {
                    Actor = target,   // The one owning the ability
                    Target = attacker,// The one who attacked
                    Move = move,
                    BaseDamage = damageToTarget
                };

                foreach (var ability in target.OnDamagedEffects)
                {
                    ability.OnDamaged(defenderCtx, damageToTarget);
                }

                // C. Defender OnCritReceived
                if (wasCrit)
                {
                    foreach (var ability in target.OnCritReceivedEffects)
                    {
                        ability.OnCritReceived(defenderCtx);
                    }
                }

                // --- END NEW SYSTEM ---

                // Legacy Move Effects (ExecuteOnKill)
                if (move?.Effects != null)
                {
                    if (move.Effects.TryGetValue("ExecuteOnKill", out var val) && target.IsDefeated)
                    {
                        HandleExecuteOnKill(attacker, val);
                    }
                }

                // Status Effect Triggers (Burn)
                if (target.HasStatusEffect(StatusEffectType.Burn) && move != null && !target.IsDefeated)
                {
                    bool isPhysical = move.ImpactType == ImpactType.Physical;
                    bool isFire = move.OffensiveElementIDs.Contains(2);

                    if (isPhysical || isFire)
                    {
                        if (damageToTarget > 0)
                        {
                            target.ApplyDamage(damageToTarget);
                            EventBus.Publish(new GameEvents.StatusEffectTriggered
                            {
                                Combatant = target,
                                EffectType = StatusEffectType.Burn,
                                Damage = damageToTarget
                            });
                        }
                    }
                }
            }

            EventBus.Publish(new GameEvents.SecondaryEffectComplete());
        }

        private static void ProcessMoveSelfEffects(QueuedAction action, List<BattleCombatant> finalTargets, List<DamageCalculator.DamageResult> damageResults)
        {
            var move = action.ChosenMove;
            if (move == null) return;

            int totalDamage = damageResults.Sum(r => r.DamageAmount);

            if (move.Effects.TryGetValue("Lifesteal", out var lifestealValue) && EffectParser.TryParseFloat(lifestealValue, out float percentage))
            {
                int healAmount = (int)Math.Round(totalDamage * (percentage / 100f));
                if (healAmount > 0)
                {
                    HandleLifesteal(action.Actor, healAmount, finalTargets);
                }
            }

            if (move.Effects.TryGetValue("Recoil", out var recoilValue) && EffectParser.TryParseFloat(recoilValue, out float recoilPercent))
            {
                if (totalDamage > 0)
                {
                    int recoilDamage = Math.Max(1, (int)(totalDamage * (recoilPercent / 100f)));
                    action.Actor.ApplyDamage(recoilDamage);
                    EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = action.Actor, RecoilDamage = recoilDamage });
                }
            }

            if (move.Effects.TryGetValue("SelfDebuff", out var selfDebuffValue))
            {
                if (EffectParser.TryParseStatusEffectParams(selfDebuffValue, out var type, out int chance, out int duration))
                {
                    if (_random.Next(1, 101) <= chance)
                        action.Actor.AddStatusEffect(new StatusEffectInstance(type, duration));
                }
            }
        }

        private static void HandleLifesteal(BattleCombatant actor, int healAmount, List<BattleCombatant> targets)
        {
            bool handled = false;

            // Check if any target has a reaction to lifesteal (e.g. Caustic Blood)
            foreach (var target in targets.Distinct())
            {
                foreach (var reaction in target.LifestealReactions)
                {
                    if (reaction.OnLifestealReceived(actor, healAmount, target))
                    {
                        handled = true;
                    }
                }
            }

            if (!handled)
            {
                int hpBefore = (int)actor.VisualHP;
                actor.ApplyHealing(healAmount);
                EventBus.Publish(new GameEvents.CombatantHealed { Actor = actor, Target = actor, HealAmount = healAmount, VisualHPBefore = hpBefore });
            }
        }

        private static void HandleApplyStatus(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseStatusEffectParams(value, out var type, out int chance, out int duration))
            {
                // Check for duration bonuses (Legacy check, should be IAbility later)
                foreach (var relic in actor.ActiveRelics)
                {
                    if (relic.Effects.TryGetValue("StatusDurationBonus", out var bonusValue) && IsNegativeStatus(type))
                    {
                        if (EffectParser.TryParseInt(bonusValue, out int bonusDuration)) duration += bonusDuration;
                    }
                }

                if (_random.Next(1, 101) <= chance)
                {
                    var newStatus = new StatusEffectInstance(type, duration);
                    bool wasNewlyApplied = target.AddStatusEffect(newStatus);

                    if (wasNewlyApplied && IsNegativeStatus(type))
                    {
                        // --- NEW: Trigger OnStatusApplied Effects ---
                        var ctx = new CombatContext { Actor = actor, Target = target };
                        foreach (var effect in actor.OnStatusAppliedEffects)
                        {
                            effect.OnStatusApplied(ctx, newStatus);
                        }
                    }
                }
            }
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
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{actor.Name} stole {buff.GetDisplayName()} from {target.Name}!" });
                }
            }
        }

        private static void HandleRestoreMana(BattleCombatant actor, BattleCombatant target, string value)
        {
            if (EffectParser.TryParseFloat(value, out float percentage))
            {
                int amount = (int)(target.Stats.MaxMana * (percentage / 100f));
                float before = target.Stats.CurrentMana;
                target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amount);
                if (target.Stats.CurrentMana > before)
                {
                    EventBus.Publish(new GameEvents.CombatantManaRestored { Target = target, AmountRestored = (int)(target.Stats.CurrentMana - before), ManaBefore = before, ManaAfter = target.Stats.CurrentMana });
                }
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
                    if (success)
                    {
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = finalTarget, Stat = stat, Amount = amount });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = message });
                    }
                }
            }
        }

        private static void HandleExecuteOnKill(BattleCombatant actor, string value)
        {
            var parts = value.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string effectName = parts[0];
                string effectParams = parts[1];
                if (effectName.StartsWith("ModifyStatStage"))
                {
                    HandleModifyStatStage(actor, actor, effectParams);
                }
            }
        }

        private static bool IsNegativeStatus(StatusEffectType type)
        {
            return type != StatusEffectType.Regen && type != StatusEffectType.Dodging;
        }
    }
}