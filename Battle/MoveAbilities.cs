using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    // --- EXISTING ABILITIES ---

    public class RestoreManaAbility : IOnHitEffect
    {
        public string Name => "Restore Mana";
        public string Description => "Restores a percentage of Max Mana to the target.";

        private readonly float _percentage;

        public RestoreManaAbility(float percentage)
        {
            _percentage = percentage;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            var target = ctx.Target;

            int amount = (int)(target.Stats.MaxMana * (_percentage / 100f));
            float before = target.Stats.CurrentMana;

            target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amount);

            if (target.Stats.CurrentMana > before)
            {
                EventBus.Publish(new GameEvents.CombatantManaRestored
                {
                    Target = target,
                    AmountRestored = (int)(target.Stats.CurrentMana - before),
                    ManaBefore = before,
                    ManaAfter = target.Stats.CurrentMana
                });
            }
        }
    }

    public class RecoilAbility : IOnActionComplete
    {
        public string Name => "Recoil";
        public string Description => "User takes damage after using this move.";

        private readonly float _damagePercent;

        public RecoilAbility(float damagePercent)
        {
            _damagePercent = damagePercent;
        }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            int recoilDamage = (int)(owner.Stats.MaxHP * (_damagePercent / 100f));
            if (recoilDamage < 1) recoilDamage = 1;

            owner.ApplyDamage(recoilDamage);

            EventBus.Publish(new GameEvents.CombatantRecoiled
            {
                Actor = owner,
                RecoilDamage = recoilDamage,
                SourceAbility = null
            });
        }
    }

    public class ArmorPierceAbility : IDefensePenetrationModifier
    {
        public string Name => "Armor Pierce";
        public string Description => "Ignores a percentage of the target's Tenacity.";

        private readonly float _percent;

        public ArmorPierceAbility(float percent)
        {
            _percent = percent;
        }

        public float GetDefensePenetration(CombatContext ctx)
        {
            return _percent / 100f;
        }
    }

    public class SelfRegenAbility : IOnActionComplete
    {
        public string Name => "Self Regen";
        public string Description => "Applies or extends Regeneration on the user.";

        private readonly int _turns;
        private readonly int _chance;

        public SelfRegenAbility(int turns, int chance)
        {
            _turns = turns;
            _chance = chance;
        }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                var existingRegen = owner.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Regen);

                if (existingRegen != null)
                {
                    existingRegen.DurationInTurns += _turns;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{owner.Name}'s [cStatus]Regeneration[/] extended by {_turns} turns!"
                    });
                }
                else
                {
                    bool applied = owner.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Regen, _turns));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished
                        {
                            Message = $"{owner.Name} gained [cStatus]Regeneration[/]!"
                        });
                    }
                }
            }
        }
    }

    public class StatChangeAbility : IOnActionComplete
    {
        public string Name => "Stat Change";
        public string Description => "Modifies the user's stat stages.";

        private readonly OffensiveStatType _stat;
        private readonly int _amount;

        public StatChangeAbility(OffensiveStatType stat, int amount)
        {
            _stat = stat;
            _amount = amount;
        }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            int currentStage = owner.StatStages[_stat];
            int newStage = Math.Clamp(currentStage + _amount, -6, 6);
            owner.StatStages[_stat] = newStage;

            int actualChange = newStage - currentStage;

            if (actualChange != 0)
            {
                EventBus.Publish(new GameEvents.CombatantStatStageChanged
                {
                    Target = owner,
                    Stat = _stat,
                    Amount = actualChange
                });

                string verb = actualChange > 0 ? "rose" : "fell";
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name}'s {_stat} {verb}!"
                });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name}'s {_stat} cannot go any {(_amount > 0 ? "higher" : "lower")}!"
                });
            }
        }
    }

    public class DamageRecoilAbility : IOnHitEffect
    {
        public string Name => "Damage Recoil";
        public string Description => "User takes recoil damage based on damage dealt.";

        private readonly float _percent;

        public DamageRecoilAbility(float percent)
        {
            _percent = percent;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            int recoil = (int)(damageDealt * (_percent / 100f));
            if (recoil > 0)
            {
                ctx.Actor.ApplyDamage(recoil);
                EventBus.Publish(new GameEvents.CombatantRecoiled
                {
                    Actor = ctx.Actor,
                    RecoilDamage = recoil,
                    SourceAbility = null
                });
            }
        }
    }

    // --- STATUS INFLICTION ABILITIES ---

    public class InflictStatusBurnAbility : IOnHitEffect
    {
        public string Name => "Inflict Burn";
        public string Description => "Chance to inflict Burn on hit.";

        private readonly int _chance;
        private static readonly Random _random = new Random();

        public InflictStatusBurnAbility(int chance)
        {
            _chance = chance;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            if (_random.Next(1, 101) <= _chance)
            {
                // Burn is permanent, so duration is irrelevant (99)
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Burn, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name} was [cStatus]burned[/]!"
                    });
                }
            }
        }
    }

    public class InflictStatusPoisonAbility : IOnHitEffect
    {
        public string Name => "Inflict Poison";
        public string Description => "Chance to inflict Poison on hit.";

        private readonly int _chance;
        private static readonly Random _random = new Random();

        public InflictStatusPoisonAbility(int chance)
        {
            _chance = chance;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            if (_random.Next(1, 101) <= _chance)
            {
                // Poison is permanent
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Poison, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name} was [cStatus]poisoned[/]!"
                    });
                }
            }
        }
    }

    public class InflictStatusFrostbiteAbility : IOnHitEffect
    {
        public string Name => "Inflict Frostbite";
        public string Description => "Chance to inflict Frostbite on hit.";

        private readonly int _chance;
        private static readonly Random _random = new Random();

        public InflictStatusFrostbiteAbility(int chance)
        {
            _chance = chance;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            if (_random.Next(1, 101) <= _chance)
            {
                // Frostbite is permanent
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Frostbite, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name} was [cStatus]frostbitten[/]!"
                    });
                }
            }
        }
    }

    public class InflictStatusStunAbility : IOnHitEffect
    {
        public string Name => "Inflict Stun";
        public string Description => "Chance to inflict Stun on hit.";

        private readonly int _chance;
        private readonly int _duration;
        private static readonly Random _random = new Random();

        public InflictStatusStunAbility(int chance, int duration)
        {
            _chance = chance;
            _duration = duration;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            if (_random.Next(1, 101) <= _chance)
            {
                var existing = ctx.Target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Stun);
                if (existing != null)
                {
                    existing.DurationInTurns += _duration;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name}'s [cStatus]Stun[/] extended by {_duration} turns!"
                    });
                }
                else
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Stun, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished
                        {
                            Message = $"{ctx.Target.Name} was [cStatus]stunned[/]!"
                        });
                    }
                }
            }
        }
    }

    public class InflictStatusSilenceAbility : IOnHitEffect
    {
        public string Name => "Inflict Silence";
        public string Description => "Chance to inflict Silence on hit.";

        private readonly int _chance;
        private readonly int _duration;
        private static readonly Random _random = new Random();

        public InflictStatusSilenceAbility(int chance, int duration)
        {
            _chance = chance;
            _duration = duration;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            if (_random.Next(1, 101) <= _chance)
            {
                var existing = ctx.Target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Silence);
                if (existing != null)
                {
                    existing.DurationInTurns += _duration;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name}'s [cStatus]Silence[/] extended by {_duration} turns!"
                    });
                }
                else
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Silence, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished
                        {
                            Message = $"{ctx.Target.Name} was [cStatus]silenced[/]!"
                        });
                    }
                }
            }
        }
    }

    public class ProtectAbility : IOnActionComplete
    {
        public string Name => "Protect";
        public string Description => "Protects the user from all effects of moves that target it during the turn.";

        private static readonly Random _random = new Random();

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            // Mark that we attempted to use Protect this turn
            owner.UsedProtectThisTurn = true;

            // Calculate success chance: 1 / (2 ^ consecutive_uses)
            // 0 uses = 100%, 1 use = 50%, 2 uses = 25%, etc.
            double chance = 1.0 / Math.Pow(2, owner.ConsecutiveProtectUses);

            if (_random.NextDouble() < chance)
            {
                // Success
                owner.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Protected, 1));
                owner.ConsecutiveProtectUses++;
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name} [cStatus]protected[/] itself!"
                });
            }
            else
            {
                // Failure
                owner.ConsecutiveProtectUses = 0;
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name}'s protection failed!"
                });
                // Trigger visual indicator
                EventBus.Publish(new GameEvents.MoveFailed { Actor = owner });
            }
        }
    }
}