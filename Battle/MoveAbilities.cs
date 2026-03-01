using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Linq;
using static ProjectVagabond.GameEvents;

namespace ProjectVagabond.Battle.Abilities
{
    public class ApplyStatusAbility : IAbility
    {
        public string Name => "Apply Status";
        public string Description => "Applies a status effect to the target.";
        public int Priority => 0;

        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private static readonly Random _random = new Random();

        public ApplyStatusAbility(StatusEffectType type, int chance, int duration)
        {
            _type = type;
            _chance = chance;
            _duration = duration;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.WasGraze) return;

                if (_random.Next(1, 101) <= _chance)
                {
                    bool applied = reaction.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Target.Name} gained [pop][cStatus]{_type}[/][/]!" });
                    }
                }
            }
        }
    }

    public class InflictStatusAbility : IAbility
    {
        public string Name { get; }
        public string Description { get; }
        public int Priority => 0;

        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private static readonly Random _random = new Random();

        public InflictStatusAbility(string name, StatusEffectType type, int chance, int duration = 99)
        {
            Name = name;
            Description = $"Chance to inflict {type}.";
            _type = type;
            _chance = chance;
            _duration = duration;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.WasGraze) return;

                if (_random.Next(1, 101) <= _chance)
                {
                    bool applied = reaction.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Target.Name} was [pop][cStatus]{_type}[/][/]!" });
                    }
                }
            }
        }
    }

    public class InflictStatusBurnAbility : InflictStatusAbility
    {
        public InflictStatusBurnAbility(int chance, int duration = -1)
            : base("Burn", StatusEffectType.Burn, chance, duration) { }
    }

    public class InflictStatusPoisonAbility : InflictStatusAbility
    {
        public InflictStatusPoisonAbility(int chance, int duration = -1)
            : base("Poison", StatusEffectType.Poison, chance, duration) { }
    }

    public class InflictStatusFrostbiteAbility : InflictStatusAbility
    {
        public InflictStatusFrostbiteAbility(int chance, int duration = -1)
            : base("Frostbite", StatusEffectType.Frostbite, chance, duration) { }
    }

    public class InflictStatusBleedingAbility : InflictStatusAbility
    {
        public InflictStatusBleedingAbility(int chance, int duration = -1)
            : base("Bleeding", StatusEffectType.Bleeding, chance, duration) { }
    }

    public class InflictStatusStunAbility : InflictStatusAbility
    {
        public InflictStatusStunAbility(int chance, int duration = 1)
            : base("Stun", StatusEffectType.Stun, chance, duration) { }
    }

    public class InflictStatusSilenceAbility : InflictStatusAbility
    {
        public InflictStatusSilenceAbility(int chance, int duration = 3)
            : base("Silence", StatusEffectType.Silence, chance, duration) { }
    }

    public class InflictStatusProvokedAbility : InflictStatusAbility
    {
        public InflictStatusProvokedAbility(int chance, int duration = 3)
            : base("Provoke", StatusEffectType.Provoked, chance, duration) { }
    }

    public class CounterAbility : IAbility
    {
        public string Name => "Counter";
        public string Description => "Fails if not used on first turn. Dazes target.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ActionDeclaredEvent decl && decl.Move.Abilities.Contains(this))
            {
                if (decl.Actor.HasUsedFirstAttack)
                {
                    decl.IsHandled = true;
                    EventBus.Publish(new GameEvents.ActionFailed
                    {
                        Actor = decl.Actor,
                        Reason = "failed",
                        MoveName = decl.Move.MoveName
                    });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                }
            }
            else if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.DamageAmount > 0)
                {
                    reaction.Target.Tags.Add(GameplayTags.States.Dazed);
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Target.Name} was [shake][cStatus]DAZED[/][/]!" });
                }
            }
        }
    }

    public class RecoilAbility : IAbility
    {
        public string Name => "Recoil";
        public string Description => "User takes damage after using this move.";
        public int Priority => 0;

        private readonly float _damagePercent;
        public RecoilAbility(float damagePercent) { _damagePercent = damagePercent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                int recoilDamage = (int)(reaction.Actor.Stats.MaxHP * (_damagePercent / 100f));
                if (recoilDamage < 1) recoilDamage = 1;

                reaction.Actor.ApplyDamage(recoilDamage);
                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = reaction.Actor, RecoilDamage = recoilDamage });
            }
        }
    }

    public class ArmorPierceAbility : IAbility
    {
        public string Name => "Armor Pierce";
        public string Description => "Ignores a percentage of the target's Tenacity.";
        public int Priority => 0;

        private readonly float _percent;
        public ArmorPierceAbility(float percent) { _percent = percent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Move.Abilities.Contains(this))
            {
                dmgEvent.DamageMultiplier *= (1.0f + (_percent / 100f));
            }
        }
    }

    public class DamageRecoilAbility : IAbility
    {
        public string Name => "Damage Recoil";
        public string Description => "User takes recoil damage based on damage dealt.";
        public int Priority => 0;

        private readonly float _percent;
        public DamageRecoilAbility(float percent) { _percent = percent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                int damageDealt = reaction.Result.DamageAmount;
                float targetMaxHP = Math.Max(1, reaction.Target.Stats.MaxHP);
                float healthPercentageDealt = damageDealt / targetMaxHP;
                int recoil = (int)(reaction.Actor.Stats.MaxHP * healthPercentageDealt * (_percent / 100f));

                if (damageDealt > 0 && recoil < 1) recoil = 1;

                if (recoil > 0)
                {
                    reaction.Actor.ApplyDamage(recoil);
                    EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = reaction.Actor, RecoilDamage = recoil });
                }
            }
        }
    }

    public class MultiHitAbility : IAbility
    {
        public string Name => "Multi-Hit";
        public string Description => "Hits multiple times.";
        public int Priority => 0;

        public int MinHits { get; }
        public int MaxHits { get; }
        public MultiHitAbility(int min, int max) { MinHits = min; MaxHits = max; }

        public void OnEvent(GameEvent e, BattleContext context) { }
    }

    public class PercentageDamageAbility : IAbility
    {
        public string Name => "Gravity Crush";
        public string Description => "Deals fixed percentage of current HP.";
        public int Priority => 0;

        private readonly float _percent;
        public PercentageDamageAbility(float percent) { _percent = percent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Move.Abilities.Contains(this))
            {
                int damage = (int)(dmgEvent.Target.Stats.CurrentHP * (_percent / 100f));
                dmgEvent.FinalDamage = Math.Max(1, damage);
                dmgEvent.IsHandled = true;
            }
        }
    }

    public class LifestealAbility : IAbility
    {
        public string Name => "Lifesteal";
        public string Description => "Heals user for a percentage of damage dealt.";
        public int Priority => 0;

        private readonly float _percent;
        public LifestealAbility(float percent) { _percent = percent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                int damageDealt = reaction.Result.DamageAmount;
                int healAmount = (int)(damageDealt * (_percent / 100f));
                if (healAmount > 0)
                {
                    int hpBefore = (int)reaction.Actor.VisualHP;
                    reaction.Actor.ApplyHealing(healAmount);

                    EventBus.Publish(new GameEvents.CombatantHealed
                    {
                        Actor = reaction.Actor,
                        Target = reaction.Actor,
                        HealAmount = healAmount,
                        VisualHPBefore = hpBefore
                    });
                }
            }
        }
    }

    public class ShieldBreakerAbility : IAbility
    {
        public string Name => "Shield Breaker";
        public string Description => "Breaks through protection.";
        public int Priority => 10;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Move.Abilities.Contains(this))
            {
                if (dmgEvent.Target.Tags.Has(GameplayTags.States.Protected))
                {
                    dmgEvent.Target.Tags.Remove(GameplayTags.States.Protected);
                    dmgEvent.Target.ActiveStatusEffects.RemoveAll(s => s.EffectType == StatusEffectType.Protected);

                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "GUARD BROKEN!" });
                    dmgEvent.DamageMultiplier *= 1.5f;
                }
            }
        }
    }

    public class CleanseStatusAbility : IAbility
    {
        public string Name => "Cleanse";
        public string Description => "Removes negative status effects.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                var target = reaction.Target;
                var negativeTypes = new[] { StatusEffectType.Poison, StatusEffectType.Burn, StatusEffectType.Frostbite, StatusEffectType.Stun, StatusEffectType.Silence, StatusEffectType.Bleeding };
                var toRemove = target.ActiveStatusEffects.Where(eff => negativeTypes.Contains(eff.EffectType)).ToList();

                if (toRemove.Any())
                {
                    foreach (var effect in toRemove)
                    {
                        target.ActiveStatusEffects.Remove(effect);
                        EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = effect.EffectType });
                    }
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} was [pop][cStatus]cleansed[/][/]!" });
                }
            }
        }
    }

    public class RestoreGuardAbility : IAbility
    {
        public string Name => "Restore Guard";
        public string Description => "Fully restores Guard.";
        public int Priority => 0;

        private readonly int _amount;

        public RestoreGuardAbility(int amount) { _amount = amount; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                var target = reaction.Target;
                if (target.CurrentGuard < target.MaxGuard)
                {
                    int oldVal = target.CurrentGuard;

                    target.CurrentGuard = target.MaxGuard;

                    if (target.CurrentGuard != oldVal)
                    {
                        EventBus.Publish(new GameEvents.GuardChanged { Combatant = target, NewValue = target.CurrentGuard });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} fully restored Guard!" });
                    }
                }
            }
        }
    }

    public class SelfSwitchAbility : IAbility
    {
        public string Name => "Self Switch";
        public string Description => "Switch out after attacking.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (!reaction.Result.WasGraze)
                {
                    EventBus.Publish(new GameEvents.DisengageTriggered { Actor = reaction.Actor });
                }
            }
        }
    }

    public class DamageGuardAbility : IAbility
    {
        public string Name => "Damage Guard";
        public string Description => "Reduces target's Guard directly.";
        public int Priority => 0;

        private readonly int _amount;
        public DamageGuardAbility(int amount) { _amount = amount; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                var target = reaction.Target;
                if (target.CurrentGuard > 0)
                {
                    int oldVal = target.CurrentGuard;
                    target.CurrentGuard = Math.Max(0, target.CurrentGuard - _amount);

                    if (target.CurrentGuard != oldVal)
                    {
                        EventBus.Publish(new GameEvents.GuardChanged { Combatant = target, NewValue = target.CurrentGuard });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name}'s guard was damaged!" });

                        if (target.CurrentGuard == 0)
                            EventBus.Publish(new GameEvents.GuardBroken { Combatant = target });
                    }
                }
            }
        }
    }

    public class HealAbility : IAbility
    {
        public string Name => "Heal";
        public string Description => "Restores a percentage of Max HP.";
        public int Priority => 0;

        private readonly int _percent;
        public HealAbility(int percent) { _percent = percent; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                int healAmount = (int)(reaction.Target.Stats.MaxHP * (_percent / 100f));
                if (healAmount < 1) healAmount = 1;

                int hpBefore = (int)reaction.Target.VisualHP;
                reaction.Target.ApplyHealing(healAmount);

                EventBus.Publish(new GameEvents.CombatantHealed
                {
                    Actor = reaction.Actor,
                    Target = reaction.Target,
                    HealAmount = healAmount,
                    VisualHPBefore = hpBefore
                });
            }
        }
    }

    public class ModifyStatStageAbility : IAbility
    {
        public string Name => "Stat Modifier";
        public string Description => "Modifies combat stats on hit.";
        public int Priority => 0;

        private readonly OffensiveStatType _stat;
        private readonly int _amount;
        private readonly int _chance;
        private readonly string _targetScope;

        public ModifyStatStageAbility(OffensiveStatType stat, int amount, int chance, string target = "Target")
        {
            _stat = stat;
            _amount = amount;
            _chance = chance;
            _targetScope = target;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.WasGraze) return;

                if (Random.Shared.Next(1, 101) <= _chance)
                {
                    BattleCombatant targetCombatant = (_targetScope.Equals("Self", StringComparison.OrdinalIgnoreCase))
                        ? reaction.Actor
                        : reaction.Target;

                    targetCombatant.ModifyStatStage(_stat, _amount);
                }
            }
        }
    }

    public class SacrificeGuardAbility : IAbility
    {
        public string Name => "Sacrifice Guard";
        public string Description => "Lowers user's Guard on hit.";
        public int Priority => 0;

        private readonly int _amount;
        public SacrificeGuardAbility(int amount) { _amount = amount; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                var user = reaction.Actor;
                if (user.CurrentGuard > 0)
                {
                    int oldVal = user.CurrentGuard;
                    user.CurrentGuard = Math.Max(0, user.CurrentGuard - _amount);

                    if (user.CurrentGuard != oldVal)
                    {
                        EventBus.Publish(new GameEvents.GuardChanged { Combatant = user, NewValue = user.CurrentGuard });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{user.Name} sacrificed Guard!" });

                        if (user.CurrentGuard == 0)
                            EventBus.Publish(new GameEvents.GuardBroken { Combatant = user });
                    }
                }
            }
        }
    }

    public class DepleteGuardAbility : IAbility
    {
        public string Name => "Deplete Guard";
        public string Description => "Shatters target's Guard completely.";
        public int Priority => 0;

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.WasGraze) return;

                var target = reaction.Target;
                if (target.CurrentGuard > 0)
                {
                    target.CurrentGuard = 0;

                    EventBus.Publish(new GameEvents.GuardChanged { Combatant = target, NewValue = 0 });
                    EventBus.Publish(new GameEvents.GuardBroken { Combatant = target });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name}'s guard was shattered!" });
                }
            }
        }
    }

    public class ApplySelfStatusAbility : IAbility
    {
        public string Name => "Apply Self Status";
        public string Description => "Applies a status effect to the user.";
        public int Priority => 0;

        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;

        public ApplySelfStatusAbility(StatusEffectType type, int chance, int duration)
        {
            _type = type;
            _chance = chance;
            _duration = duration;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ReactionEvent reaction && reaction.TriggeringAction.ChosenMove.Abilities.Contains(this))
            {
                if (reaction.Result.WasGraze) return;

                if (Random.Shared.Next(1, 101) <= _chance)
                {
                    bool applied = reaction.Actor.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{reaction.Actor.Name} gained [pop][cStatus]{_type}[/][/]!" });
                    }
                }
            }
        }
    }

    public class BonusDamageVsStatusAbility : IAbility
    {
        public string Name => "Bonus Damage Vs Status";
        public string Description => "Deals extra damage if the target has a specific status.";
        public int Priority => 0;

        private readonly StatusEffectType _statusType;
        private readonly float _multiplier;

        public BonusDamageVsStatusAbility(StatusEffectType statusType, float multiplier)
        {
            _statusType = statusType;
            _multiplier = multiplier;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Move.Abilities.Contains(this))
            {
                if (dmgEvent.Target.HasStatusEffect(_statusType))
                {
                    dmgEvent.DamageMultiplier *= _multiplier;
                }
            }
        }
    }
}