using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    // ... (Previous abilities unchanged) ...

    public class CounterAbility : IOutgoingDamageModifier, IOnHitEffect
    {
        public string Name => "Counter";
        public string Description => "Fails if not used on first turn. Dazes target.";

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            // If user has already acted in this battle, the move fails (0 damage)
            if (ctx.Actor.HasUsedFirstAttack)
            {
                // Only trigger events if this is a real execution, not a UI/AI simulation
                if (!ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "But it failed!" });
                    EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                }
                return 0f;
            }
            return currentDamage;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            // Only apply Daze if damage was actually dealt (move didn't fail)
            if (damageDealt > 0)
            {
                ctx.Target.IsDazed = true;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [cStatus]DAZED[/]!" });
            }
        }
    }

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
            owner.UsedProtectThisTurn = true;
            double chance = 1.0 / Math.Pow(2, owner.ConsecutiveProtectUses);

            if (_random.NextDouble() < chance)
            {
                owner.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Protected, 1));
                owner.ConsecutiveProtectUses++;
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name} [cStatus]protected[/] itself!"
                });
            }
            else
            {
                owner.ConsecutiveProtectUses = 0;
                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{owner.Name}'s protection failed!"
                });
                EventBus.Publish(new GameEvents.MoveFailed { Actor = owner });
            }
        }
    }

    public class InflictStatChangeAbility : IOnHitEffect
    {
        public string Name => "Inflict Stat Change";
        public string Description => "Lowers the target's stats.";

        private readonly List<(OffensiveStatType Stat, int Amount)> _changes = new List<(OffensiveStatType, int)>();

        public InflictStatChangeAbility(string stat1, int amt1, string stat2 = null, int amt2 = 0)
        {
            if (Enum.TryParse<OffensiveStatType>(stat1, true, out var s1))
            {
                _changes.Add((s1, amt1));
            }

            if (!string.IsNullOrEmpty(stat2) && Enum.TryParse<OffensiveStatType>(stat2, true, out var s2))
            {
                _changes.Add((s2, amt2));
            }
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            foreach (var change in _changes)
            {
                var (success, _) = ctx.Target.ModifyStatStage(change.Stat, change.Amount);
                if (success)
                {
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged
                    {
                        Target = ctx.Target,
                        Stat = change.Stat,
                        Amount = change.Amount
                    });

                    string verb = change.Amount > 0 ? "rose" : "fell";
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name}'s {change.Stat} {verb}!"
                    });
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name}'s {change.Stat} cannot go any {(change.Amount > 0 ? "higher" : "lower")}!"
                    });
                }
            }
        }
    }

    public class DisengageAbility : IOnActionComplete
    {
        public string Name => "Disengage";
        public string Description => "Switches the user out after attacking.";

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            if (owner.IsPlayerControlled)
            {
                EventBus.Publish(new GameEvents.DisengageTriggered { Actor = owner });
            }
        }
    }

    public class RandomStatChangeAbility : IOnActionComplete
    {
        public string Name => "Random Stat Change";
        public string Description => "Modifies random stats of the user.";

        private readonly int[] _amounts;
        private static readonly Random _random = new Random();

        public RandomStatChangeAbility(int[] amounts)
        {
            _amounts = amounts;
        }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            var stats = new List<OffensiveStatType>
            {
                OffensiveStatType.Strength,
                OffensiveStatType.Intelligence,
                OffensiveStatType.Tenacity,
                OffensiveStatType.Agility
            };

            int n = stats.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (stats[k], stats[n]) = (stats[n], stats[k]);
            }

            int count = Math.Min(_amounts.Length, stats.Count);

            for (int i = 0; i < count; i++)
            {
                var stat = stats[i];
                var amount = _amounts[i];

                var (success, _) = owner.ModifyStatStage(stat, amount);
                if (success)
                {
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged
                    {
                        Target = owner,
                        Stat = stat,
                        Amount = amount
                    });

                    string verb = amount > 0 ? "rose" : "fell";
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{owner.Name}'s {stat} {verb}!"
                    });
                }
            }
        }
    }

    public class InflictRandomStatChangeAbility : IOnHitEffect
    {
        public string Name => "Inflict Random Stat Change";
        public string Description => "Modifies random stats of the target.";

        private readonly int[] _amounts;
        private static readonly Random _random = new Random();

        public InflictRandomStatChangeAbility(int[] amounts)
        {
            _amounts = amounts;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;

            var stats = new List<OffensiveStatType>
            {
                OffensiveStatType.Strength,
                OffensiveStatType.Intelligence,
                OffensiveStatType.Tenacity,
                OffensiveStatType.Agility
            };

            int n = stats.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (stats[k], stats[n]) = (stats[n], stats[k]);
            }

            int count = Math.Min(_amounts.Length, stats.Count);

            for (int i = 0; i < count; i++)
            {
                var stat = stats[i];
                var amount = _amounts[i];

                var (success, _) = ctx.Target.ModifyStatStage(stat, amount);
                if (success)
                {
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged
                    {
                        Target = ctx.Target,
                        Stat = stat,
                        Amount = amount
                    });

                    string verb = amount > 0 ? "rose" : "fell";
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Target.Name}'s {stat} {verb}!"
                    });
                }
            }
        }
    }

    public class ShieldBreakerAbility : IShieldBreaker
    {
        public string Name => "Shield Breaker";
        public string Description => "Breaks through protection.";

        public float BreakDamageMultiplier { get; }
        public bool FailsIfNoProtect { get; }

        public ShieldBreakerAbility(float damageMultiplier, bool failsIfNoProtect)
        {
            BreakDamageMultiplier = damageMultiplier;
            FailsIfNoProtect = failsIfNoProtect;
        }
    }

    public class ExecuteAbility : ICalculationModifier, ICritModifier
    {
        public string Name => "Execute";
        public string Description => "Deals bonus damage or crits low HP targets.";

        private readonly float _threshold;
        private readonly float _multiplier;

        public ExecuteAbility(float threshold, float multiplier)
        {
            _threshold = threshold;
            _multiplier = multiplier;
        }

        public float ModifyBasePower(float basePower, CombatContext ctx)
        {
            if (_multiplier == -99) return basePower;

            // FIX: Check for null target (e.g. UI display)
            if (ctx.Target == null) return basePower;

            float hpPercent = (float)ctx.Target.Stats.CurrentHP / ctx.Target.Stats.MaxHP * 100f;
            if (hpPercent <= _threshold)
            {
                return basePower * _multiplier;
            }
            return basePower;
        }

        public float ModifyCritChance(float currentChance, CombatContext ctx)
        {
            if (_multiplier != -99) return currentChance;

            // FIX: Check for null target (e.g. UI display)
            if (ctx.Target == null) return currentChance;

            float hpPercent = (float)ctx.Target.Stats.CurrentHP / ctx.Target.Stats.MaxHP * 100f;
            if (hpPercent <= _threshold)
            {
                return 1.0f; // Guaranteed crit
            }
            return currentChance;
        }

        public float ModifyCritDamage(float currentMultiplier, CombatContext ctx)
        {
            return currentMultiplier;
        }
    }
}