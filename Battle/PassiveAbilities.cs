using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    // --- STAT MODIFIERS ---
    public class FlatStatBonusAbility : IStatModifier
    {
        public string Name => "Stat Bonus";
        public string Description => "Increases stats.";
        private readonly Dictionary<string, int> _modifiers;
        public FlatStatBonusAbility(Dictionary<string, int> modifiers) { _modifiers = modifiers; }
        public int ModifyStat(OffensiveStatType statType, int currentValue, BattleCombatant owner) => _modifiers.TryGetValue(statType.ToString(), out int bonus) ? currentValue + bonus : currentValue;
        public int ModifyMaxStat(string statName, int currentValue) => _modifiers.TryGetValue(statName, out int bonus) ? currentValue + bonus : currentValue;
    }

    public class CorneredAnimalAbility : IStatModifier
    {
        public string Name => "Cornered Animal";
        public string Description => "Boosts stat when low HP.";
        private readonly float _hpThreshold;
        private readonly OffensiveStatType _statToRaise;
        private readonly int _amount;

        public CorneredAnimalAbility(float hpThreshold, OffensiveStatType stat, int amount)
        {
            _hpThreshold = hpThreshold;
            _statToRaise = stat;
            _amount = amount;
        }

        public int ModifyStat(OffensiveStatType statType, int currentValue, BattleCombatant owner)
        {
            if (statType != _statToRaise) return currentValue;
            float hpPercent = (float)owner.Stats.CurrentHP / owner.Stats.MaxHP * 100f;
            if (hpPercent < _hpThreshold)
            {
                float multiplier = 1.0f + (0.5f * _amount);
                return (int)(currentValue * multiplier);
            }
            return currentValue;
        }
        public int ModifyMaxStat(string statName, int currentValue) => currentValue;
    }

    // --- CRIT & ACCURACY ---
    public class FlatCritBonusAbility : ICritModifier
    {
        public string Name => "Sniper";
        public string Description => "Increases crit chance.";
        private readonly float _bonusPercent;
        public FlatCritBonusAbility(float bonusPercent) { _bonusPercent = bonusPercent; }
        public float ModifyCritChance(float currentChance, CombatContext ctx) => currentChance + (_bonusPercent / 100f);
        public float ModifyCritDamage(float currentMultiplier, CombatContext ctx) => currentMultiplier;
    }

    public class CritImmunityAbility : ICritModifier
    {
        public string Name => "Bulwark";
        public string Description => "Immune to critical hits.";
        public CritImmunityAbility() { }
        public float ModifyCritChance(float currentChance, CombatContext ctx) => 0f;
        public float ModifyCritDamage(float currentMultiplier, CombatContext ctx) => currentMultiplier;
    }

    public class IgnoreEvasionAbility : IAccuracyModifier
    {
        public string Name => "Keen Eye";
        public string Description => "Attacks ignore dodging and never miss.";

        public int ModifyAccuracy(int currentAccuracy, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.Power > 0) return 999;
            return currentAccuracy;
        }

        public bool ShouldIgnoreEvasion(CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.Power > 0)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return true;
            }
            return false;
        }
    }

    public class RecklessAbandonAbility : IOutgoingDamageModifier, IAccuracyModifier
    {
        public string Name => "Reckless Abandon";
        public string Description => "Contact moves deal more damage but less accuracy.";
        private readonly float _damageMultiplier;
        private readonly float _accuracyMultiplier;

        public RecklessAbandonAbility(float damageMult, float accuracyMult)
        {
            _damageMultiplier = damageMult;
            _accuracyMultiplier = accuracyMult;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.MakesContact)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _damageMultiplier;
            }
            return currentDamage;
        }

        public int ModifyAccuracy(int currentAccuracy, CombatContext ctx)
        {
            return (ctx.Move != null && ctx.Move.MakesContact) ? (int)(currentAccuracy * _accuracyMultiplier) : currentAccuracy;
        }

        public bool ShouldIgnoreEvasion(CombatContext ctx) => false;
    }

    // --- DAMAGE MODIFIERS ---
    public class LowHPDamageBonusAbility : IOutgoingDamageModifier
    {
        public string Name => "Adrenaline";
        public string Description => "Deal more damage when HP is low.";
        private readonly float _hpThresholdPercent;
        private readonly float _damageMultiplier;

        public LowHPDamageBonusAbility(float threshold, float multiplier)
        {
            _hpThresholdPercent = threshold;
            _damageMultiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            float hpPercent = (float)ctx.Actor.Stats.CurrentHP / ctx.Actor.Stats.MaxHP * 100f;
            if (hpPercent < _hpThresholdPercent)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _damageMultiplier;
            }
            return currentDamage;
        }
    }

    public class FullHPDamageAbility : IOutgoingDamageModifier
    {
        public string Name => "Full Power";
        public string Description => "Deal more damage at full HP.";
        private readonly float _damageMultiplier;

        public FullHPDamageAbility(float multiplier)
        {
            _damageMultiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Actor.Stats.CurrentHP >= ctx.Actor.Stats.MaxHP)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _damageMultiplier;
            }
            return currentDamage;
        }
    }

    public class ElementalDamageBonusAbility : IOutgoingDamageModifier
    {
        public string Name => "Elemental Mastery";
        public string Description => "Increases damage of specific elements.";
        private readonly int _elementId;
        private readonly float _multiplier;

        public ElementalDamageBonusAbility(int elementId, float multiplier)
        {
            _elementId = elementId;
            _multiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.MoveHasElement(_elementId))
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class FirstAttackDamageAbility : IOutgoingDamageModifier
    {
        public string Name => "First Blood";
        public string Description => "First attack deals bonus damage.";
        private readonly float _multiplier;

        public FirstAttackDamageAbility(float multiplier)
        {
            _multiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (!ctx.Actor.HasUsedFirstAttack)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class StatusedTargetDamageAbility : IOutgoingDamageModifier
    {
        public string Name => "Opportunist";
        public string Description => "Deal more damage to statused targets.";
        private readonly float _multiplier;

        public StatusedTargetDamageAbility(float multiplier)
        {
            if (multiplier > 10.0f) _multiplier = 1.0f + (multiplier / 100f);
            else _multiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Target == null) return currentDamage;
            if (ctx.Target.ActiveStatusEffects.Any())
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class LastStandAbility : IOutgoingDamageModifier
    {
        public string Name => "Last Stand";
        public string Description => "Deal more damage if acting last.";
        private readonly float _multiplier;

        public LastStandAbility(float multiplier)
        {
            if (multiplier > 10.0f) _multiplier = 1.0f + (multiplier / 100f);
            else _multiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.IsLastAction)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class GlassCannonAbility : IOutgoingDamageModifier, IIncomingDamageModifier
    {
        public string Name => "Glass Cannon";
        public string Description => "Deal more, take more.";
        private readonly float _outgoingMult;
        private readonly float _incomingMult;

        public GlassCannonAbility(float outgoing, float incoming)
        {
            _outgoingMult = outgoing > 5.0f ? 1.0f + (outgoing / 100f) : outgoing;
            _incomingMult = incoming > 5.0f ? 1.0f + (incoming / 100f) : incoming;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            return currentDamage * _outgoingMult;
        }

        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
            return currentDamage * _incomingMult;
        }
    }

    public class BloodletterAbility : IOutgoingDamageModifier, IOnActionComplete
    {
        public string Name => "Bloodletter";
        public string Description => "Spells deal more damage but cost HP.";
        private readonly float _multiplier;

        public BloodletterAbility(float multiplier)
        {
            _multiplier = multiplier;
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.MoveType == MoveType.Spell)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            if (action.ChosenMove != null && action.ChosenMove.MoveType == MoveType.Spell)
            {
                int cost = Math.Max(1, (int)(owner.Stats.MaxHP * 0.05f));
                owner.ApplyDamage(cost);
                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = owner, RecoilDamage = cost });
            }
        }
    }

    public class ChainReactionAbility : IOutgoingDamageModifier, IOnHitEffect, IActionModifier, IOnActionComplete
    {
        public string Name => "Chain Reaction";
        public string Description => "Consecutive hits boost damage.";
        private readonly float _multiplier;
        private int _stacks = 0;
        private string _lastMoveID = "";
        private bool _hitThisTurn = false;

        public ChainReactionAbility(float multiplier) { _multiplier = multiplier; }

        public void ModifyAction(QueuedAction action, BattleCombatant owner)
        {
            _hitThisTurn = false;
            if (action.ChosenMove != null && action.ChosenMove.MoveID != _lastMoveID) _stacks = 0;
            if (action.ChosenMove != null) _lastMoveID = action.ChosenMove.MoveID;
        }

        public void OnHit(CombatContext ctx, int damageDealt) { _stacks++; _hitThisTurn = true; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner) { if (action.ChosenMove != null && action.ChosenMove.Power > 0 && !_hitThisTurn) _stacks = 0; }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (_stacks > 0)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * (1.0f + (_stacks * (_multiplier - 1.0f)));
            }
            return currentDamage;
        }
    }

    public class DefensePenetrationAbility : IDefensePenetrationModifier
    {
        public string Name => "Armor Piercer";
        public string Description => "Ignores a percentage of target defense.";
        private readonly float _penetrationPercent;
        public DefensePenetrationAbility(float percent) { _penetrationPercent = percent; }
        public float GetDefensePenetration(CombatContext ctx)
        {
            if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            return _penetrationPercent / 100f;
        }
    }

    public class PhysicalDamageReductionAbility : IIncomingDamageModifier
    {
        public string Name => "Thick Skin";
        public string Description => "Reduces physical damage.";
        private readonly float _multiplier;
        public PhysicalDamageReductionAbility(float reductionPercent) { _multiplier = 1.0f - (reductionPercent / 100f); }
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.ImpactType == ImpactType.Physical)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class VigorAbility : IIncomingDamageModifier
    {
        public string Name => "Vigor";
        public string Description => "Reduces damage when at full HP.";
        public VigorAbility() { }
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Target.Stats.CurrentHP >= ctx.Target.Stats.MaxHP)
            {
                if (!ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s Vigor reduced the damage!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                }
                return currentDamage * 0.5f;
            }
            return currentDamage;
        }
    }

    public class SunBlessedLeafAbility : IIncomingDamageModifier
    {
        public string Name => "Photosynthesis";
        public string Description => "Absorbs Light damage.";
        private readonly int _elementId;
        private readonly float _healPercent;
        public SunBlessedLeafAbility(int elementId, float healPercent) { _elementId = elementId; _healPercent = healPercent; }
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.MoveHasElement(_elementId))
            {
                if (!ctx.IsSimulation)
                {
                    int healAmount = (int)(ctx.Actor.Stats.MaxHP * (_healPercent / 100f));
                    int hpBefore = (int)ctx.Actor.VisualHP;
                    ctx.Actor.ApplyHealing(healAmount);
                    EventBus.Publish(new GameEvents.CombatantHealed { Actor = ctx.Actor, Target = ctx.Actor, HealAmount = healAmount, VisualHPBefore = hpBefore });
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} absorbed the attack!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
                return 0f;
            }
            return currentDamage;
        }
    }

    public class ElementalImmunityAbility : IIncomingDamageModifier
    {
        public string Name => "Elemental Immunity";
        public string Description => "Immune to specific element.";
        private readonly int _elementId;
        public ElementalImmunityAbility(int elementId) { _elementId = elementId; }
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.MoveHasElement(_elementId))
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                return 0f;
            }
            return currentDamage;
        }
    }

    // --- ELEMENTAL MODIFIERS ---
    public class AddResistanceAbility : IElementalAffinityModifier
    {
        public string Name => "Elemental Resistance";
        public string Description => "Adds a resistance.";
        private readonly int _elementId;
        public AddResistanceAbility(int elementId) { _elementId = elementId; }
        public void ModifyElementalAffinities(List<int> weaknesses, List<int> resistances, BattleCombatant owner) { if (!resistances.Contains(_elementId)) resistances.Add(_elementId); }
    }

    // --- STATUS MODIFIERS ---
    public class StatusImmunityAbility : IIncomingStatusModifier
    {
        public string Name => "Unwavering";
        public string Description => "Immune to specific status effects.";
        private readonly HashSet<StatusEffectType> _immuneTypes;
        public StatusImmunityAbility(IEnumerable<StatusEffectType> types) { _immuneTypes = new HashSet<StatusEffectType>(types); }
        public bool ShouldBlockStatus(StatusEffectType type, BattleCombatant owner)
        {
            if (_immuneTypes.Contains(type))
            {
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                return true;
            }
            return false;
        }
    }

    // --- ACTION MODIFIERS & STATEFUL ABILITIES ---
    public class AmbushPredatorAbility : IActionModifier, IOutgoingDamageModifier
    {
        public string Name => "Ambush Predator";
        public string Description => "First attack is faster but weaker.";
        private readonly int _priorityBonus;
        private readonly float _damageMultiplier;
        public AmbushPredatorAbility(int priorityBonus, float damageMult) { _priorityBonus = priorityBonus; _damageMultiplier = damageMult; }
        public void ModifyAction(QueuedAction action, BattleCombatant owner)
        {
            if (!owner.HasUsedFirstAttack && action.ChosenMove != null)
            {
                action.Priority += _priorityBonus;
                // We don't fire event here because priority is silent until turn order resolves
            }
        }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (!ctx.Actor.HasUsedFirstAttack)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _damageMultiplier;
            }
            return currentDamage;
        }
    }

    public class SpellweaverAbility : IOnActionComplete, IOutgoingDamageModifier
    {
        public string Name => "Spellweaver";
        public string Description => "Alternating spells deals more damage.";
        private readonly float _multiplier;
        private string _lastMoveID = "";
        private bool _lastWasSpell = false;

        public SpellweaverAbility(float multiplier) { _multiplier = multiplier; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            if (action.ChosenMove != null)
            {
                _lastMoveID = action.ChosenMove.MoveID;
                _lastWasSpell = action.ChosenMove.MoveType == MoveType.Spell;
            }
        }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.MoveType == MoveType.Spell && _lastWasSpell && ctx.Move.MoveID != _lastMoveID)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class MomentumAbility : IOnActionComplete, IOnKill, IOutgoingDamageModifier
    {
        public string Name => "Momentum";
        public string Description => "Kills boost next attack.";
        private readonly float _multiplier;
        private bool _isActive = false;
        public MomentumAbility(float multiplier) { _multiplier = multiplier; }
        public void OnActionComplete(QueuedAction action, BattleCombatant owner) { if (action.ChosenMove != null && action.ChosenMove.Power > 0) _isActive = false; }
        public void OnKill(CombatContext ctx)
        {
            _isActive = true;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} is building!" });
            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
        }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (_isActive && ctx.Move != null && ctx.Move.Power > 0)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }

    public class EscalationAbility : ITurnLifecycle, IOutgoingDamageModifier
    {
        public string Name => "Escalation";
        public string Description => "Damage increases every turn.";
        private readonly float _multPerRound;
        private readonly float _maxMult;
        private float _currentMult = 1.0f;

        public EscalationAbility(float multPerRound, float maxMult) { _multPerRound = multPerRound; _maxMult = maxMult; }
        public void OnTurnStart(BattleCombatant owner) { _currentMult += (_multPerRound - 1.0f); if (_currentMult > _maxMult) _currentMult = _maxMult; }
        public void OnTurnEnd(BattleCombatant owner) { }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (_currentMult > 1.0f)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _currentMult;
            }
            return currentDamage;
        }
    }

    // --- TRIGGERS ---
    public class PainFuelAbility : IOnCritReceived
    {
        public string Name => "Pain Fuel";
        public string Description => "Taking crit raises stats.";
        public void OnCritReceived(CombatContext ctx)
        {
            var target = ctx.Target;
            var (successStr, _) = target.ModifyStatStage(OffensiveStatType.Strength, 1);
            if (successStr) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Strength, Amount = 1 });
            var (successInt, _) = target.ModifyStatStage(OffensiveStatType.Intelligence, 1);
            if (successInt) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Intelligence, Amount = 1 });
            if (successStr || successInt)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name}'s {Name} turned pain into power!" });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = this });
            }
        }
    }

    public class CausticBloodAbility : ILifestealReaction
    {
        public string Name => "Caustic Blood";
        public string Description => "Lifesteal damages attacker.";
        public bool OnLifestealReceived(BattleCombatant source, int amount, BattleCombatant owner)
        {
            source.ApplyDamage(amount);
            EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = source, RecoilDamage = amount });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} burns {source.Name}!" });
            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            return true;
        }
    }

    public class SanguineThirstAbility : IOnHitEffect
    {
        public string Name => "Sanguine Thirst";
        public string Description => "Heal on contact hit.";
        private readonly float _healPercent;
        public SanguineThirstAbility(float percent) { _healPercent = percent; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (ctx.Move != null && ctx.Move.MakesContact && damageDealt > 0)
            {
                ctx.AccumulatedLifestealPercent += _healPercent;
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
            }
        }
    }

    public class ApplyStatusOnHitAbility : IOnHitEffect
    {
        public string Name => "Venomous";
        public string Description => "Chance to apply status on hit.";
        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private readonly bool _requiresContact;
        public ApplyStatusOnHitAbility(StatusEffectType type, int chance, int duration, bool requiresContact) { _type = type; _chance = chance; _duration = duration; _requiresContact = requiresContact; }
        public void OnHit(CombatContext ctx, int damageDealt)
        {
            if (_requiresContact && ctx.Move != null && !ctx.Move.MakesContact) return;
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s ability applied {_type} to {ctx.Target.Name}!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class ReactiveStatusAbility : IOnDamagedEffect
    {
        public string Name => "Reactive Status";
        public string Description => "Apply status when hit.";
        private readonly StatusEffectType _type;
        private readonly int _chance;
        private readonly int _duration;
        private readonly bool _requiresContact;
        public ReactiveStatusAbility(StatusEffectType type, int chance, int duration, bool requiresContact) { _type = type; _chance = chance; _duration = duration; _requiresContact = requiresContact; }
        public void OnDamaged(CombatContext ctx, int damageTaken)
        {
            if (_requiresContact && ctx.Move != null && !ctx.Move.MakesContact) return;
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                bool applied = ctx.Target.AddStatusEffect(new StatusEffectInstance(_type, _duration));
                if (applied)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s ability applied {_type} to {ctx.Target.Name}!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                }
            }
        }
    }

    public class ThornsAbility : IOnDamagedEffect
    {
        public string Name => "Iron Barbs";
        public string Description => "Damage attacker on contact.";
        private readonly float _damagePercent;
        public ThornsAbility(float percent) { _damagePercent = percent; }
        public void OnDamaged(CombatContext ctx, int damageTaken)
        {
            if (ctx.Move != null && ctx.Move.MakesContact)
            {
                int recoil = Math.Max(1, (int)(ctx.Target.Stats.MaxHP * (_damagePercent / 100f)));
                ctx.Target.ApplyDamage(recoil);
                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Target, RecoilDamage = recoil });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
            }
        }
    }

    public class RegenAbility : ITurnLifecycle
    {
        public string Name => "Regeneration";
        public string Description => "Heal at end of turn.";
        private readonly float _percent;
        public RegenAbility(float percent) { _percent = percent; }
        public void OnTurnStart(BattleCombatant owner) { }
        public void OnTurnEnd(BattleCombatant owner)
        {
            int heal = (int)(owner.Stats.MaxHP * (_percent / 100f));
            if (heal > 0)
            {
                int hpBefore = (int)owner.VisualHP;
                owner.ApplyHealing(heal);
                EventBus.Publish(new GameEvents.CombatantHealed { Actor = owner, Target = owner, HealAmount = heal, VisualHPBefore = hpBefore });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
        }
    }

    public class ToxicAuraAbility : ITurnLifecycle
    {
        public string Name => "Toxic Aura";
        public string Description => "Poison random combatant at end of turn.";
        private readonly int _chance;
        private readonly int _duration;
        private readonly bool _canHitAllies;
        private static readonly Random _random = new Random();

        public ToxicAuraAbility(int chance, int duration, bool canHitAllies)
        {
            _chance = chance;
            _duration = duration;
            _canHitAllies = canHitAllies;
        }

        public void OnTurnStart(BattleCombatant owner) { }
        public void OnTurnEnd(BattleCombatant owner)
        {
            if (_random.Next(1, 101) > _chance) return;

            var battleManager = ServiceLocator.Get<BattleManager>();
            var allCombatants = battleManager.AllCombatants.Where(c => !c.IsDefeated && c.IsActiveOnField && c != owner).ToList();

            var validTargets = new List<BattleCombatant>();
            validTargets.AddRange(allCombatants.Where(c => c.IsPlayerControlled != owner.IsPlayerControlled));
            if (_canHitAllies) validTargets.AddRange(allCombatants.Where(c => c.IsPlayerControlled == owner.IsPlayerControlled));

            if (validTargets.Any())
            {
                var target = validTargets[_random.Next(validTargets.Count)];
                target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Poison, _duration));
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} poisoned {target.Name}!" });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
        }
    }

    public class IntimidateAbility : IBattleLifecycle
    {
        public string Name => "Intimidate";
        public string Description => "Lowers enemy stats on entry.";
        private readonly OffensiveStatType _stat;
        private readonly int _amount;
        public IntimidateAbility(OffensiveStatType stat, int amount) { _stat = stat; _amount = amount; }
        public void OnBattleStart(BattleCombatant owner) { }
        public void OnCombatantEnter(BattleCombatant owner)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            var enemies = battleManager.AllCombatants.Where(c => c.IsPlayerControlled != owner.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
            bool anyAffected = false;
            foreach (var enemy in enemies)
            {
                var (success, _) = enemy.ModifyStatStage(_stat, _amount);
                if (success)
                {
                    anyAffected = true;
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = enemy, Stat = _stat, Amount = _amount });
                }
            }
            if (anyAffected)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} lowered opponents' {_stat}!" });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
        }
    }

    public class InsightAbility : IAbility
    {
        public string Name => "Insight";
        public string Description => "Reveals detailed enemy stats.";
    }

    // --- CUSTOM PARTY MEMBER PASSIVES ---

    public class PMPyromancerAbility : IOutgoingDamageModifier, IElementalAffinityModifier
    {
        public string Name => "Pyromancer";
        public string Description => "Deal 1.5x Fire damage and resist Fire.";

        // Hardcoded Fire Element ID (1) based on Elements.json
        private const int FIRE_ELEMENT_ID = 1;
        private const float DAMAGE_MULTIPLIER = 1.5f;

        public PMPyromancerAbility() { }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.MoveHasElement(FIRE_ELEMENT_ID))
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * DAMAGE_MULTIPLIER;
            }
            return currentDamage;
        }

        public void ModifyElementalAffinities(List<int> weaknesses, List<int> resistances, BattleCombatant owner)
        {
            if (!resistances.Contains(FIRE_ELEMENT_ID))
            {
                resistances.Add(FIRE_ELEMENT_ID);
            }
        }
    }

    public class PMAnnoyingAbility : IActionModifier
    {
        public string Name => "Annoying";
        public string Description => "Status moves have +1 priority.";
        public void ModifyAction(QueuedAction action, BattleCombatant owner)
        {
            if (action.ChosenMove != null && action.ChosenMove.ImpactType == ImpactType.Status)
            {
                action.Priority += 1;
                // Priority is silent until turn order resolves
            }
        }
    }

    public class PMScrappyAbility : IStatChangeModifier, IIncomingStatusModifier, IDazeImmunity
    {
        public string Name => "Scrappy";
        public string Description => "Immune to Strength drops, Stun, and Daze.";

        public bool ShouldBlockStatChange(OffensiveStatType stat, int amount, BattleCombatant owner)
        {
            if (stat == OffensiveStatType.Strength && amount < 0)
            {
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                return true;
            }
            return false;
        }

        public bool ShouldBlockStatus(StatusEffectType type, BattleCombatant owner)
        {
            if (type == StatusEffectType.Stun)
            {
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                return true;
            }
            return false;
        }

        public bool ShouldBlockDaze(BattleCombatant owner)
        {
            EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            return true;
        }
    }

    public class PMShortTemperAbility : IOnCritReceived
    {
        public string Name => "Short Temper";
        public string Description => "Maxes Strength when hit by a critical hit.";

        public void OnCritReceived(CombatContext ctx)
        {
            var (success, msg) = ctx.Target.ModifyStatStage(OffensiveStatType.Strength, 12);

            if (success)
            {
                EventBus.Publish(new GameEvents.CombatantStatStageChanged
                {
                    Target = ctx.Target,
                    Stat = OffensiveStatType.Strength,
                    Amount = 12
                });

                EventBus.Publish(new GameEvents.TerminalMessagePublished
                {
                    Message = $"{ctx.Target.Name}'s {Name} maxed their [cstr]Strength[/]!"
                });

                EventBus.Publish(new GameEvents.AbilityActivated
                {
                    Combatant = ctx.Target,
                    Ability = this
                });
            }
        }
    }

    public class PMMajesticAbility : IBattleLifecycle
    {
        public string Name => "Majestic";
        public string Description => "Lowers enemy Strength on entry.";

        private const OffensiveStatType STAT_TO_LOWER = OffensiveStatType.Strength;
        private const int AMOUNT = -1;

        public void OnBattleStart(BattleCombatant owner) { }

        public void OnCombatantEnter(BattleCombatant owner)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            var enemies = battleManager.AllCombatants
                .Where(c => c.IsPlayerControlled != owner.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField)
                .ToList();

            bool anyAffected = false;
            foreach (var enemy in enemies)
            {
                var (success, _) = enemy.ModifyStatStage(STAT_TO_LOWER, AMOUNT);
                if (success)
                {
                    anyAffected = true;
                    EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = enemy, Stat = STAT_TO_LOWER, Amount = AMOUNT });
                }
            }

            if (anyAffected)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} lowered opponents' {STAT_TO_LOWER}!" });
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
            }
        }
    }

    public class PMSweetpeaAbility : IAllyDamageModifier
    {
        public string Name => "Sweetpea";
        public string Description => "Reduces damage taken by allies.";
        public float ModifyAllyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this }); // Target is the ally being protected
            return currentDamage * 0.75f;
        }
    }

    public class PMSkepticAbility : IIncomingDamageModifier
    {
        public string Name => "Skeptic";
        public string Description => "Takes half damage from Spells.";

        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.MoveType == MoveType.Spell)
            {
                if (!ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                }
                return currentDamage * 0.5f;
            }
            return currentDamage;
        }
    }

    public class PM9LivesAbility : IIncomingDamageModifier
    {
        public string Name => "9 Lives";
        public string Description => "Survive lethal damage if at full HP.";

        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            var target = ctx.Target;
            if (target.Stats.CurrentHP == target.Stats.MaxHP)
            {
                if (currentDamage >= target.Stats.CurrentHP)
                {
                    if (!ctx.IsSimulation)
                    {
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name} endured the hit!" });
                        EventBus.Publish(new GameEvents.AbilityActivated { Combatant = target, Ability = this });
                    }
                    return Math.Max(0, target.Stats.CurrentHP - 1);
                }
            }
            return currentDamage;
        }
    }

    public class PMMinutiaeAbility : IOutgoingDamageModifier
    {
        public string Name => "Minutiae";
        public string Description => "Boosts moves with 60 or less Power by 1.5x.";

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Move != null && ctx.Move.Power > 0 && ctx.Move.Power <= 60)
            {
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * 1.5f;
            }
            return currentDamage;
        }
    }

    public class PMGentleSoulAbility : IBattleLifecycle
    {
        public string Name => "Gentle Soul";
        public string Description => "Restores ally HP on switch-in.";

        public void OnBattleStart(BattleCombatant owner) { }

        public void OnCombatantEnter(BattleCombatant owner)
        {
            var bm = ServiceLocator.Get<BattleManager>();

            // Debug logging to trace execution
            Debug.WriteLine($"[PMGentleSoulAbility] OnCombatantEnter called for {owner.Name}. Phase: {bm.CurrentPhase}");

            // Ignore initial battle start
            if (bm.CurrentPhase == BattleManager.BattlePhase.BattleStartIntro) return;

            // Find ally
            var ally = bm.AllCombatants.FirstOrDefault(c =>
                c.IsPlayerControlled == owner.IsPlayerControlled &&
                c != owner &&
                c.IsActiveOnField &&
                !c.IsDefeated);

            if (ally != null)
            {
                Debug.WriteLine($"[PMGentleSoulAbility] Ally found: {ally.Name}. Healing...");
                int healAmount = (int)(ally.Stats.MaxHP * 0.25f);
                if (healAmount > 0)
                {
                    int oldHP = (int)ally.VisualHP;
                    ally.ApplyHealing(healAmount);

                    EventBus.Publish(new GameEvents.CombatantHealed
                    {
                        Actor = owner,
                        Target = ally,
                        HealAmount = healAmount,
                        VisualHPBefore = oldHP
                    });

                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{owner.Name}'s {Name} healed {ally.Name}!"
                    });

                    EventBus.Publish(new GameEvents.AbilityActivated
                    {
                        Combatant = owner,
                        Ability = this
                    });
                }
            }
            else
            {
                Debug.WriteLine($"[PMGentleSoulAbility] No valid ally found to heal.");
            }
        }
    }

    public class PMWellfedAbility : IIncomingDamageModifier
    {
        public string Name => "Well-Fed";
        public string Description => "Halves damage taken when at full HP.";

        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx)
        {
            if (ctx.Target.Stats.CurrentHP >= ctx.Target.Stats.MaxHP)
            {
                if (!ctx.IsSimulation)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Target.Name}'s {Name} reduced the damage!" });
                    EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Target, Ability = this });
                }
                return currentDamage * 0.5f;
            }
            return currentDamage;
        }
    }

    public class PMStubbornAbility : IStatModifier, IOnActionComplete, IBattleLifecycle, IMoveLockAbility
    {
        public string Name => "Stubborn";
        public string Description => "Boosts Strength by 1.5x, but locks user into one move.";
        private string _lockedMoveID = null;

        public int ModifyStat(OffensiveStatType statType, int currentValue, BattleCombatant owner)
        {
            if (statType == OffensiveStatType.Strength)
            {
                // Gorilla Tactics / Choice Band logic: 1.5x Strength
                return (int)(currentValue * 1.5f);
            }
            return currentValue;
        }

        public int ModifyMaxStat(string statName, int currentValue) => currentValue;

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            // Lock into the move if not already locked
            if (_lockedMoveID == null && action.ChosenMove != null)
            {
                _lockedMoveID = action.ChosenMove.MoveID;

                // Visual Feedback
                EventBus.Publish(new GameEvents.AbilityActivated { Combatant = owner, Ability = this });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name} is [cStatus]Stubborn[/]! Locked into {action.ChosenMove.MoveName}!" });
            }
        }

        public void OnBattleStart(BattleCombatant owner) { _lockedMoveID = null; }

        public void OnCombatantEnter(BattleCombatant owner)
        {
            // Reset lock when switching in
            _lockedMoveID = null;
        }

        public string GetLockedMoveID() => _lockedMoveID;
        public void ResetLock() => _lockedMoveID = null;
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
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
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
                if (!ctx.IsSimulation) EventBus.Publish(new GameEvents.AbilityActivated { Combatant = ctx.Actor, Ability = this });
                return currentDamage * _multiplier;
            }
            return currentDamage;
        }
    }
}