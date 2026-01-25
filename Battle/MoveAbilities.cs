using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

namespace ProjectVagabond.Battle.Abilities
{
    public class ApplyStatusAbility : IOnHitEffect
    {
        public string Name => "Apply Status";
        public string Description => "Applies a status effect to the target.";
        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;

        public ApplyStatusAbility(StatusEffectType type, int chance, int duration)
        {
            _type = type;
            _chance = chance;
            _duration = duration;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                if (applied)
                {
                    string msg = _type == StatusEffectType.Empowered
                        ? $"{ctx.Target.Name} is [pop][cStatus]Empowered[/][/]!"
                        : $"{ctx.Target.Name} gained [pop][cStatus]{_type}[/][/]!";

                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
                    // Fire event on success
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class CounterAbility : IOutgoingDamageModifier, IOnHitEffect
    {
        public string Name => "Counter";
        public string Description => "Fails if not used on first turn. Dazes target.";

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Actor.HasUsedFirstAttack)
            {
                if (!ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                    EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                }
                return 0f;
            }
            return currentDamage;
        }

        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (damageDealt > 0)
            {
                ctx.Target.IsDazed = true;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [shake][cStatus]DAZED[/][/]!" });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            }
        }
    }

    public class RestoreManaAbility : IOnHitEffect
    {
        public string Name => "Restore Mana";
        public string Description => "Restores a percentage of Max Mana to the target.";
        private readonly float _percentage;
        public RestoreManaAbility(float percentage) { _percentage = percentage; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            var target = ctx.Target;
            int amount = (int)(target.Stats.MaxMana * (_percentage / 100f));
            float before = target.Stats.CurrentMana;
            target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amount);
            if (target.Stats.CurrentMana > before)
            {
                EventBus.Publish(new GameEvents.CombatantManaRestored { Target = target, AmountRestored = (int)(target.Stats.CurrentMana - before), ManaBefore = before, ManaAfter = target.Stats.CurrentMana });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            }
        }
    }

    public class RecoilAbility : IOnActionComplete
    {
        public string Name => "Recoil";
        public string Description => "User takes damage after using this move.";
        private readonly float _damagePercent;
        public RecoilAbility(float damagePercent) { _damagePercent = damagePercent; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            int recoilDamage = (int)(owner.Stats.MaxHP * (_damagePercent / 100f));
            if (recoilDamage < 1) recoilDamage = 1;
            owner.ApplyDamage(recoilDamage);
            EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = owner, RecoilDamage = recoilDamage, SourceAbility = null });
            // Recoil is usually self-evident, but we can fire an event if desired.
            // EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
        }
    }

    public class ArmorPierceAbility : IDefensePenetrationModifier
    {
        public string Name => "Armor Pierce";
        public string Description => "Ignores a percentage of the target's Tenacity.";
        private readonly float _percent;
        public ArmorPierceAbility(float percent) { _percent = percent; }
        public float GetDefensePenetration(CombatContext ctx) => _percent / 100f;
    }

    public class SelfRegenAbility : IOnActionComplete
    {
        public string Name => "Self Regen";
        public string Description => "Applies or extends Regeneration on the user.";
        private readonly int _turns;
        private readonly int _chance;
        public SelfRegenAbility(int turns, int chance) { _turns = turns; _chance = chance; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                var existingRegen = owner.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Regen);
                if (existingRegen != null)
                {
                    existingRegen.DurationInTurns += _turns;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s [cStatus]Regeneration[/] extended by {_turns} turns!" });
                }
                else
                {
                    bool applied = owner.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Regen, _turns));
                    if (applied) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name} gained [pop][cStatus]Regeneration[/][/]!" });
                }
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
        }
    }

    public class StatChangeAbility : IOnActionComplete
    {
        public string Name => "Stat Change";
        public string Description => "Modifies the user's stat stages.";
        private readonly OffensiveStatType _stat;
        private readonly int _amount;
        public StatChangeAbility(OffensiveStatType stat, int amount) { _stat = stat; _amount = amount; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            var (success, msg) = owner.ModifyStatStage(_stat, _amount);
            if (success)
            {
                EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = owner, Stat = _stat, Amount = _amount });
                string animatedMsg = msg.Replace("rose", "[wave]rose[/]").Replace("fell", "[shake]fell[/]");
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = animatedMsg });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
        }
    }

    public class DamageRecoilAbility : IOnHitEffect
    {
        public string Name => "Damage Recoil";
        public string Description => "User takes recoil damage based on damage dealt.";
        private readonly float _percent;
        public DamageRecoilAbility(float percent) { _percent = percent; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            int recoil = (int)(damageDealt * (_percent / 100f));
            if (recoil > 0)
            {
                ctx.Actor.ApplyDamage(recoil);
                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Actor, RecoilDamage = recoil, SourceAbility = null });
                // EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            }
        }
    }

    public class InflictStatusBurnAbility : IOnHitEffect
    {
        public string Name => "Inflict Burn";
        public string Description => "Chance to inflict Burn on hit.";
        private readonly int _chance;
        private static readonly Random _random = new Random();
        public InflictStatusBurnAbility(int chance) { _chance = chance; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;
            if (_random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Burn, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [pop][cStatus]burned[/][/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
        public InflictStatusPoisonAbility(int chance) { _chance = chance; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;
            if (_random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Poison, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [pop][cStatus]poisoned[/][/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class InflictStatusBleedAbility : IOnHitEffect
    {
        public string Name => "Inflict Bleed";
        public string Description => "Chance to inflict Bleeding on hit.";
        private readonly int _chance;
        private static readonly Random _random = new Random();
        public InflictStatusBleedAbility(int chance) { _chance = chance; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;
            if (_random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Bleeding, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} is [pop][cStatus]bleeding[/][/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
        public InflictStatusFrostbiteAbility(int chance) { _chance = chance; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;
            if (_random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Frostbite, 99));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [pop][cStatus]frostbitten[/][/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
        public InflictStatusStunAbility(int chance, int duration) { _chance = chance; _duration = duration; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.IsGraze) return;
            if (_random.Next(1, 101) <= _chance)
            {
                var existing = ctx.Target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Stun);
                if (existing != null)
                {
                    existing.DurationInTurns += _duration;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s [cStatus]Stun[/] extended by {_duration} turns!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
                else
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Stun, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [shake][cStatus]stunned[/][/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
            public InflictStatusSilenceAbility(int chance, int duration) { _chance = chance; _duration = duration; }
            public void OnHit(CombatContext ctx, int damageDealt)
            {
                if (ctx.IsGraze) return;
                if (_random.Next(1, 101) <= _chance)
                {
                    var existing = ctx.Target.ActiveStatusEffects.FirstOrDefault(e => e.EffectType == StatusEffectType.Silence);
                    if (existing != null)
                    {
                        existing.DurationInTurns += _duration;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s [cStatus]Silence[/] extended by {_duration} turns!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                    else
                    {
                        bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Silence, _duration));
                        if (applied)
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [DriftWave][cStatus]silenced[/][/]!" });
                            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                        }
                    }
                }
            }
        }

        public class DazzlingAbility : IOnHitEffect
        {
            public string Name => "Dazzling";
            public string Description => "Chance to daze the target.";
            private readonly int _chance;
            private static readonly Random _random = new Random();

            public DazzlingAbility(int chance)
            {
                _chance = chance;
            }

            public void OnHit(CombatContext ctx, int damageDealt)
            {
                if (ctx.IsGraze) return;
                if (_random.Next(1, 101) <= _chance)
                {
                    ctx.Target.IsDazed = true;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [shake]DAZED[/] by the blow!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name} [popwave][cStatus]protected[/][/] itself!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                }
                else
                {
                    owner.ConsecutiveProtectUses = 0;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s protection [shake]failed[/]!" });
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
                if (Enum.TryParse<OffensiveStatType>(stat1, true, out var s1)) _changes.Add((s1, amt1));
                if (!string.IsNullOrEmpty(stat2) && Enum.TryParse<OffensiveStatType>(stat2, true, out var s2)) _changes.Add((s2, amt2));
            }
            public void OnHit(CombatContext ctx, int damageDealt)
            {
                if (ctx.IsGraze) return;
                bool anySuccess = false;
                foreach (var change in _changes)
                {
                    var (success, msg) = ctx.Target.ModifyStatStage(change.Stat, change.Amount);
                    if (success)
                    {
                        anySuccess = true;
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = ctx.Target, Stat = change.Stat, Amount = change.Amount });
                    }
                    string animatedMsg = msg.Replace("rose", "[wave]rose[/]").Replace("fell", "[shake]fell[/]");
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = animatedMsg });
                }
                if (anySuccess)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }

        public class DisengageAbility : IOnActionComplete
        {
            public string Name => "Disengage";
            public string Description => "Switches the user out after attacking.";
            public void OnActionComplete(QueuedAction action, BattleCombatant owner)
            {
                EventBus.Publish(new GameEvents.DisengageTriggered { Actor = owner });
                // No ability activation event needed, the switch is the feedback
            }
        }

        public class RandomStatChangeAbility : IOnActionComplete
        {
            public string Name => "Random Stat Change";
            public string Description => "Modifies random stats of the user.";
            private readonly int[] _amounts;
            private static readonly Random _random = new Random();
            public RandomStatChangeAbility(int[] amounts) { _amounts = amounts; }
            public void OnActionComplete(QueuedAction action, BattleCombatant owner)
            {
                var stats = new List<OffensiveStatType> { OffensiveStatType.Strength, OffensiveStatType.Intelligence, OffensiveStatType.Tenacity, OffensiveStatType.Agility };
                int n = stats.Count;
                while (n > 1) { n--; int k = _random.Next(n + 1); (stats[k], stats[n]) = (stats[n], stats[k]); }
                int count = Math.Min(_amounts.Length, stats.Count);
                bool anySuccess = false;
                for (int i = 0; i < count; i++)
                {
                    var (success, msg) = owner.ModifyStatStage(stats[i], _amounts[i]);
                    if (success)
                    {
                        anySuccess = true;
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = owner, Stat = stats[i], Amount = _amounts[i] });
                    }
                    string animatedMsg = msg.Replace("rose", "[wave]rose[/]").Replace("fell", "[shake]fell[/]");
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = animatedMsg });
                }
                if (anySuccess)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                }
            }
        }

        public class InflictRandomStatChangeAbility : IOnHitEffect
        {
            public string Name => "Inflict Random Stat Change";
            public string Description => "Modifies random stats of the target.";
            private readonly int[] _amounts;
            private static readonly Random _random = new Random();
            public InflictRandomStatChangeAbility(int[] amounts) { _amounts = amounts; }
            public void OnHit(CombatContext ctx, int damageDealt)
            {
                if (ctx.IsGraze) return;
                var stats = new List<OffensiveStatType> { OffensiveStatType.Strength, OffensiveStatType.Intelligence, OffensiveStatType.Tenacity, OffensiveStatType.Agility };
                int n = stats.Count;
                while (n > 1) { n--; int k = _random.Next(n + 1); (stats[k], stats[n]) = (stats[n], stats[k]); }
                int count = Math.Min(_amounts.Length, stats.Count);
                bool anySuccess = false;
                for (int i = 0; i < count; i++)
                {
                    var (success, msg) = ctx.Target.ModifyStatStage(stats[i], _amounts[i]);
                    if (success)
                    {
                        anySuccess = true;
                        EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = ctx.Target, Stat = stats[i], Amount = _amounts[i] });
                    }
                    string animatedMsg = msg.Replace("rose", "[wave]rose[/]").Replace("fell", "[shake]fell[/]");
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = animatedMsg });
                }
                if (anySuccess)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }

        public class ShieldBreakerAbility : IShieldBreaker
        {
            public string Name => "Shield Breaker";
            public string Description => "Breaks through protection.";
            public float BreakDamageMultiplier { get; }
            public bool FailsIfNoProtect { get; }
            public ShieldBreakerAbility(float damageMultiplier, bool failsIfNoProtect) { BreakDamageMultiplier = damageMultiplier; FailsIfNoProtect = failsIfNoProtect; }
        }

        public class CleanseStatusAbility : IOnHitEffect
        {
            public string Name => "Cleanse";
            public string Description => "Removes negative status effects.";

            public void OnHit(CombatContext ctx, int damageDealt)
            {
                var target = ctx.Target;
                var negativeTypes = new[]
                {
                StatusEffectType.Poison,
                StatusEffectType.Burn,
                StatusEffectType.Frostbite,
                StatusEffectType.Stun,
                StatusEffectType.Silence,
                StatusEffectType.Bleeding
            };

                var toRemove = target.ActiveStatusEffects
                    .Where(e => negativeTypes.Contains(e.EffectType))
                    .ToList();

                if (toRemove.Any())
                {
                    foreach (var effect in toRemove)
                    {
                        target.ActiveStatusEffects.Remove(effect);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = effect.EffectType });
                    }
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} was [pop][cStatus]cleansed[/][/]!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Nothing to cleanse." });
                }
            }
        }

        public class ManaDamageAbility : ICalculationModifier
        {
            public string Name => "Mana Burn";
            public string Description => "Destroys target's mana to fuel damage.";
            private readonly int _maxBurnAmount;

            public ManaDamageAbility(int amount)
            {
                _maxBurnAmount = amount;
            }

            public float ModifyBasePower(float basePower, CombatContext ctx)
            {
                if (ctx.Target == null)
                {
                    return _maxBurnAmount;
                }

                int currentMana = ctx.Target.Stats.CurrentMana;
                int burnAmount = Math.Min(currentMana, _maxBurnAmount);

                if (burnAmount <= 0)
                {
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                    }
                    return 0;
                }

                if (ctx.IsSimulation)
                {
                    return burnAmount;
                }

                float before = ctx.Target.Stats.CurrentMana;
                ctx.Target.Stats.CurrentMana -= burnAmount;

                EventBus.Publish(new GameEvents.CombatantManaConsumed
                {
                    Actor = ctx.Target,
                    ManaBefore = before,
                    ManaAfter = ctx.Target.Stats.CurrentMana
                });

                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{ctx.Target.Name} lost {burnAmount} Mana!"
                });

                return burnAmount;
            }
        }

        public class ManaDumpAbility : ICalculationModifier, IOnActionComplete
        {
            public string Name => "Flux Discharge";
            public string Description => "Consumes all mana to deal damage.";
            public float Multiplier { get; }

            public ManaDumpAbility(float multiplier)
            {
                Multiplier = multiplier;
            }

            public float ModifyBasePower(float basePower, CombatContext ctx)
            {
                // Calculate power based on current mana
                return ctx.Actor.Stats.CurrentMana * Multiplier;
            }

            public void OnActionComplete(QueuedAction action, BattleCombatant owner)
            {
                // Drain mana after the move is executed
                float before = owner.Stats.CurrentMana;
                if (before > 0)
                {
                    owner.Stats.CurrentMana = 0;
                    EventBus.Publish(new GameEvents.CombatantManaConsumed
                    {
                        Actor = owner,
                        ManaBefore = before,
                        ManaAfter = 0
                    });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{owner.Name} discharged all mana!"
                    });
                }
            }
        }

        public class LifestealAbility : IOnHitEffect
        {
            public string Name => "Lifesteal";
            public string Description => "Heals user for a percentage of damage dealt.";
            private readonly float _percent;

            public LifestealAbility(float percent)
            {
                _percent = percent;
            }

            public void OnHit(CombatContext ctx, int damageDealt)
            {
                if (damageDealt > 0)
                {
                    // Accumulate lifesteal percentage instead of healing immediately
                    ctx.AccumulatedLifestealPercent += _percent;
                    // Do NOT fire AbilityActivated here, as requested.
                }
            }
        }

        public class ConditionalCounterAbility : IOutgoingDamageModifier
        {
            public string Name => "Predator's Instinct";
            public string Description => "Fails if target is not attacking.";

            public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
            {
                // In simulation (UI/AI), assume it works to show potential damage
                if (ctx.IsSimulation) return currentDamage;

                var bm = ServiceLocator.Get<BattleManager>();

                // Check the action queue for the target's action
                var targetAction = bm.ActionQueue.FirstOrDefault(a => a.Actor == ctx.Target);

                bool isAttacking = false;

                if (targetAction != null)
                {
                    if (targetAction.Type == QueuedActionType.Move && targetAction.ChosenMove != null && targetAction.ChosenMove.Power > 0)
                    {
                        isAttacking = true;
                    }
                }

                if (!isAttacking)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                    EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                    return 0f;
                }

                return currentDamage;
            }
        }

        public class PercentageDamageAbility : IFixedDamageModifier
        {
            public string Name => "Gravity Crush";
            public string Description => "Deals fixed percentage of current HP.";
            private readonly float _percent;

            public PercentageDamageAbility(float percent)
            {
                _percent = percent;
            }

            public int GetFixedDamage(CombatContext ctx)
            {
                if (ctx.Target == null) return 0;
                int damage = (int)(ctx.Target.Stats.CurrentHP * (_percent / 100f));
                return Math.Max(1, damage); // Always deal at least 1 damage
            }
        }

        public class MultiHitAbility : IAbility
        {
            public string Name => "Multi-Hit";
            public string Description => "Hits multiple times.";
            public int MinHits { get; }
            public int MaxHits { get; }

            public MultiHitAbility(int min, int max)
            {
                MinHits = min;
                MaxHits = max;
            }
        }

        public class AlwaysCritAbility : ICritModifier
        {
            public string Name => "Precision";
            public string Description => "Always lands a critical hit.";

            public float ModifyCritChance(float currentChance, CombatContext ctx)
            {
                return 1.0f; // 100% chance
            }

            public float ModifyCritDamage(float currentMultiplier, CombatContext ctx) => currentMultiplier;
        }

        public class RestoreManaOnKillAbility : IOnKill
        {
            public string Name => "Soul Siphon";
            public string Description => "Restores Mana on kill.";
            private readonly float _percent;

            public RestoreManaOnKillAbility(float percent)
            {
                _percent = percent;
            }

            public void OnKill(CombatContext ctx)
            {
                int amount = (int)(ctx.Actor.Stats.MaxMana * (_percent / 100f));
                float before = ctx.Actor.Stats.CurrentMana;
                ctx.Actor.Stats.CurrentMana = Math.Min(ctx.Actor.Stats.MaxMana, ctx.Actor.Stats.CurrentMana + amount);

                if (ctx.Actor.Stats.CurrentMana > before)
                {
                    EventBus.Publish(new GameEvents.CombatantManaRestored
                    {
                        Target = ctx.Actor,
                        AmountRestored = (int)(ctx.Actor.Stats.CurrentMana - before),
                        ManaBefore = before,
                        ManaAfter = ctx.Actor.Stats.CurrentMana
                    });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name} absorbed the soul!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }

        public class InspireOnHitAbility : IOnHitEffect
        {
            public string Name => "Inspire";
            public string Description => "Buffs a random ally on hit.";
            private readonly List<OffensiveStatType> _stats;
            private readonly int _amount;
            private static readonly Random _random = new Random();

            public InspireOnHitAbility(string stat1, string stat2, int amount)
            {
                _stats = new List<OffensiveStatType>();
                if (Enum.TryParse<OffensiveStatType>(stat1, true, out var s1)) _stats.Add(s1);
                if (Enum.TryParse<OffensiveStatType>(stat2, true, out var s2)) _stats.Add(s2);
                _amount = amount;
            }

            public void OnHit(CombatContext ctx, int damageDealt)
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                var allies = battleManager.AllCombatants
                    .Where(c => c.IsPlayerControlled == ctx.Actor.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField)
                    .ToList();

                if (allies.Any())
                {
                    var target = allies[_random.Next(allies.Count)];
                    bool anySuccess = false;
                    foreach (var stat in _stats)
                    {
                        var (success, msg) = target.ModifyStatStage(stat, _amount);
                        if (success)
                        {
                            anySuccess = true;
                            EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = stat, Amount = _amount });
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s song inspired {target.Name}!" });
                        }
                    }
                    if (anySuccess)
                    {
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }

        public class ManaBurnOnHitAbility : IOnHitEffect
        {
            public string Name => "Mana Sever";
            public string Description => "Destroys target's mana on hit.";
            private readonly float _percent;

            public ManaBurnOnHitAbility(float percent)
            {
                _percent = percent;
            }

            public void OnHit(CombatContext ctx, int damageDealt)
            {
                int burnAmount = (int)(ctx.Target.Stats.CurrentMana * (_percent / 100f));
                if (burnAmount > 0)
                {
                    float before = ctx.Target.Stats.CurrentMana;
                    ctx.Target.Stats.CurrentMana -= burnAmount;

                    EventBus.Publish(new GameEvents.CombatantManaConsumed
                    {
                        Actor = ctx.Target,
                        ManaBefore = before,
                        ManaAfter = ctx.Target.Stats.CurrentMana
                    });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} lost {burnAmount} Mana!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }

        public class HydroScalingAbility : IOutgoingDamageModifier
        {
            public string Name => "Hydro Scaling";
            public string Description => "Damage scales with Water spells.";
            private readonly float _multiplierPerSpell;

            public HydroScalingAbility(float multiplier)
            {
                _multiplierPerSpell = multiplier;
            }

            public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
            {
                int waterSpellCount = 0;
                foreach (var entry in ctx.Actor.Spells)
                {
                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var move))
                    {
                        if (move.OffensiveElementIDs.Contains(2)) // 2 is Water
                        {
                            waterSpellCount++;
                        }
                    }
                }

                if (waterSpellCount > 0)
                {
                    float bonus = (_multiplierPerSpell - 1.0f) * waterSpellCount;
                    return currentDamage * (1.0f + bonus);
                }
                return currentDamage;
            }
        }

        public class BlightArcaneMasteryAbility : IOutgoingDamageModifier
        {
            public string Name => "Cultist Mastery";
            public string Description => "Boosts Blight and Arcane damage.";
            private readonly float _multiplier;

            public BlightArcaneMasteryAbility(float multiplier)
            {
                _multiplier = multiplier;
            }

            public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
            {
                if (ctx.MoveHasElement(6) || ctx.MoveHasElement(4)) // 6=Blight, 4=Arcane
                {
                    return currentDamage * _multiplier;
                }
                return currentDamage;
            }
        }
    }
}
