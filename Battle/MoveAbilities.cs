using Microsoft.Xna.Framework;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    public class ApplyStatusAbility : IAbility
    {
        public string Name => "Apply Status";
        public string Description => "Applies a status effect to the target.";
        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private static readonly Random _random = new Random();

        public ApplyStatusAbility(StatusEffectType type, int chance, int duration) { _type = type; _chance = chance; _duration = duration; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                if (_random.Next(1, 101) <= _chance)
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied && !ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} gained [pop][cStatus]{_type}[/][/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class CounterAbility : IAbility
    {
        public string Name => "Counter";
        public string Description => "Fails if not used on first turn. Dazes target.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateOutgoingDamage)
            {
                if (ctx.Actor.HasUsedFirstAttack)
                {
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                    }
                    ctx.DamageMultiplier = 0f;
                }
            }
            else if (type == CombatEventType.OnHit)
            {
                if (ctx.FinalDamage > 0)
                {
                    ctx.Target.IsDazed = true;
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [shake][cStatus]DAZED[/][/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class RestoreManaAbility : IAbility
    {
        public string Name => "Restore Mana";
        public string Description => "Restores a percentage of Max Mana to the target.";
        private readonly float _percentage;
        public RestoreManaAbility(float percentage) { _percentage = percentage; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                var target = ctx.Target;
                int amount = (int)(target.Stats.MaxMana * (_percentage / 100f));
                float before = target.Stats.CurrentMana;
                target.Stats.CurrentMana = Math.Min(target.Stats.MaxMana, target.Stats.CurrentMana + amount);
                if (target.Stats.CurrentMana > before && !ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.CombatantManaRestored { Target = target, AmountRestored = (int)(target.Stats.CurrentMana - before), ManaBefore = before, ManaAfter = target.Stats.CurrentMana });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class RecoilAbility : IAbility
    {
        public string Name => "Recoil";
        public string Description => "User takes damage after using this move.";
        private readonly float _damagePercent;
        public RecoilAbility(float damagePercent) { _damagePercent = damagePercent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.ActionComplete)
            {
                int recoilDamage = (int)(ctx.Actor.Stats.MaxHP * (_damagePercent / 100f));
                if (recoilDamage < 1) recoilDamage = 1;
                ctx.Actor.ApplyDamage(recoilDamage);
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Actor, RecoilDamage = recoilDamage });
            }
        }
    }

    public class ArmorPierceAbility : IAbility
    {
        public string Name => "Armor Pierce";
        public string Description => "Ignores a percentage of the target's Tenacity.";
        private readonly float _percent;
        public ArmorPierceAbility(float percent) { _percent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateDefensePenetration)
            {
                ctx.StatValue += _percent / 100f;
            }
        }
    }

    public class DamageRecoilAbility : IAbility
    {
        public string Name => "Damage Recoil";
        public string Description => "User takes recoil damage based on damage dealt.";
        private readonly float _percent;
        public DamageRecoilAbility(float percent) { _percent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                int recoil = (int)(ctx.FinalDamage * (_percent / 100f));
                if (recoil > 0)
                {
                    ctx.Actor.ApplyDamage(recoil);
                    if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Actor, RecoilDamage = recoil });
                }
            }
        }
    }

    public class InflictStatusAbility : IAbility
    {
        public string Name { get; }
        public string Description { get; }
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

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                if (ctx.IsGraze) return;
                if (_random.Next(1, 101) <= _chance)
                {
                    bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                    if (applied && !ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} was [pop][cStatus]{_type}[/][/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class MultiHitAbility : IAbility
    {
        public string Name => "Multi-Hit";
        public string Description => "Hits multiple times.";
        public int MinHits { get; }
        public int MaxHits { get; }
        public MultiHitAbility(int min, int max) { MinHits = min; MaxHits = max; }
        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx) { }
    }

    public class PercentageDamageAbility : IAbility
    {
        public string Name => "Gravity Crush";
        public string Description => "Deals fixed percentage of current HP.";
        private readonly float _percent;
        public PercentageDamageAbility(float percent) { _percent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateFixedDamage)
            {
                if (ctx.Target != null)
                {
                    int damage = (int)(ctx.Target.Stats.CurrentHP * (_percent / 100f));
                    ctx.StatValue = Math.Max(1, damage);
                }
            }
        }
    }

    public class ManaDamageAbility : IAbility
    {
        public string Name => "Mana Burn";
        public string Description => "Destroys target's mana to fuel damage.";
        private readonly int _maxBurnAmount;
        public ManaDamageAbility(int amount) { _maxBurnAmount = amount; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateBasePower)
            {
                if (ctx.Target == null) { ctx.BasePower = _maxBurnAmount; return; }
                int currentMana = ctx.Target.Stats.CurrentMana;
                int burnAmount = Math.Min(currentMana, _maxBurnAmount);

                if (burnAmount <= 0)
                {
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[DriftWave]But it failed![/]" });
                        EventBus.Publish(new GameEvents.MoveFailed { Actor = ctx.Actor });
                    }
                    ctx.BasePower = 0;
                }
                else
                {
                    if (!ctx.IsSimulation)
                    {
                        float before = ctx.Target.Stats.CurrentMana;
                        ctx.Target.Stats.CurrentMana -= burnAmount;
                        EventBus.Publish(new GameEvents.CombatantManaConsumed { Actor = ctx.Target, ManaBefore = before, ManaAfter = ctx.Target.Stats.CurrentMana });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name} lost {burnAmount} Mana!" });
                    }
                    ctx.BasePower = burnAmount;
                }
            }
        }
    }

    public class ManaDumpAbility : IAbility
    {
        public string Name => "Flux Discharge";
        public string Description => "Consumes all mana to deal damage.";
        public float Multiplier { get; }
        public ManaDumpAbility(float multiplier) { Multiplier = multiplier; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.CalculateBasePower)
            {
                ctx.BasePower = ctx.Actor.Stats.CurrentMana * Multiplier;
            }
            else if (type == CombatEventType.ActionComplete)
            {
                float before = ctx.Actor.Stats.CurrentMana;
                if (before > 0)
                {
                    ctx.Actor.Stats.CurrentMana = 0;
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.CombatantManaConsumed { Actor = ctx.Actor, ManaBefore = before, ManaAfter = 0 });
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name} discharged all mana!" });
                    }
                }
            }
        }
    }

    public class LifestealAbility : IAbility
    {
        public string Name => "Lifesteal";
        public string Description => "Heals user for a percentage of damage dealt.";
        private readonly float _percent;
        public LifestealAbility(float percent) { _percent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                if (ctx.Move != null && ctx.Move.MakesContact && ctx.FinalDamage > 0)
                {
                    ctx.AccumulatedLifestealPercent += _percent;
                }
            }
        }
    }

    public class ShieldBreakerAbility : IAbility
    {
        public string Name => "Shield Breaker";
        public string Description => "Breaks through protection.";
        private readonly float _breakMult;
        private readonly bool _failsIfNoProtect;
        public ShieldBreakerAbility(float mult, bool fails) { _breakMult = mult; _failsIfNoProtect = fails; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            // Logic handled in DamageCalculator via manual check or specific event if added later.
        }
    }

    public class CleanseStatusAbility : IAbility
    {
        public string Name => "Cleanse";
        public string Description => "Removes negative status effects.";

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                var target = ctx.Target;
                var negativeTypes = new[] { StatusEffectType.Poison, StatusEffectType.Burn, StatusEffectType.Frostbite, StatusEffectType.Stun, StatusEffectType.Silence, StatusEffectType.Bleeding };
                var toRemove = target.ActiveStatusEffects.Where(e => negativeTypes.Contains(e.EffectType)).ToList();

                if (toRemove.Any())
                {
                    foreach (var effect in toRemove)
                    {
                        target.ActiveStatusEffects.Remove(effect);
                        if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = target, EffectType = effect.EffectType });
                    }
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} was [pop][cStatus]cleansed[/][/]!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                    }
                }
            }
        }
    }

    public class ManaBurnOnHitAbility : IAbility
    {
        public string Name => "Mana Sever";
        public string Description => "Destroys target's mana on hit.";
        private readonly float _percent;
        public ManaBurnOnHitAbility(float percent) { _percent = percent; }

        public void OnCombatEvent(CombatEventType type, CombatTriggerContext ctx)
        {
            if (type == CombatEventType.OnHit)
            {
                int burnAmount = (int)(ctx.Target.Stats.CurrentMana * (_percent / 100f));
                if (burnAmount > 0)
                {
                    float before = ctx.Target.Stats.CurrentMana;
                    ctx.Target.Stats.CurrentMana -= burnAmount;

                    if (!ctx.IsSimulation)
                    {
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
        }
    }
}