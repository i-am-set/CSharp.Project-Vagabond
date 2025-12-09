using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
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
        public string Description => "Boosts Agility when low HP or outnumbered.";
        private readonly float _hpThreshold;
        private readonly int _enemyCountThreshold;
        private readonly float _bonusPercent;

        public CorneredAnimalAbility(float hpThreshold, int enemyCount, float bonusPercent)
        {
            _hpThreshold = hpThreshold;
            _enemyCountThreshold = enemyCount;
            _bonusPercent = bonusPercent;
        }

        public int ModifyStat(OffensiveStatType statType, int currentValue, BattleCombatant owner)
        {
            if (statType != OffensiveStatType.Agility) return currentValue;
            bool hpCondition = (float)owner.Stats.CurrentHP / owner.Stats.MaxHP * 100f < _hpThreshold;
            var battleManager = ServiceLocator.Get<BattleManager>();
            int enemyCount = battleManager.AllCombatants.Count(c => c.IsPlayerControlled != owner.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField);

            if (hpCondition || enemyCount >= _enemyCountThreshold)
            {
                // Optional: Trigger visual feedback via EventBus if needed, though stat mods usually silent
                return (int)(currentValue * (1.0f + (_bonusPercent / 100f)));
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

    public class CritDamageReductionAbility : ICritModifier
    {
        public string Name => "Bulwark";
        public string Description => "Reduces incoming crit damage.";
        private readonly float _reductionPercent;
        public CritDamageReductionAbility(float reductionPercent) { _reductionPercent = reductionPercent; }
        public float ModifyCritChance(float currentChance, CombatContext ctx) => currentChance;
        public float ModifyCritDamage(float currentMultiplier, CombatContext ctx) => currentMultiplier * (1.0f - (_reductionPercent / 100f));
    }

    public class IgnoreEvasionAbility : IAccuracyModifier
    {
        public string Name => "Keen Eye";
        public string Description => "Ignores dodging.";
        public int ModifyAccuracy(int currentAccuracy, CombatContext ctx) => currentAccuracy;
        public bool ShouldIgnoreEvasion(CombatContext ctx) => true;
    }

    // --- DAMAGE MODIFIERS ---
    public class LowHPDamageBonusAbility : IOutgoingDamageModifier
    {
        public string Name => "Adrenaline";
        public string Description => "Deal more damage when HP is low.";
        private readonly float _hpThresholdPercent;
        private readonly float _damageMultiplier;
        public LowHPDamageBonusAbility(float threshold, float bonusPercent) { _hpThresholdPercent = threshold; _damageMultiplier = 1.0f + (bonusPercent / 100f); }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx) => ((float)ctx.Actor.Stats.CurrentHP / ctx.Actor.Stats.MaxHP * 100f < _hpThresholdPercent) ? currentDamage * _damageMultiplier : currentDamage;
    }

    public class ElementalDamageBonusAbility : IOutgoingDamageModifier
    {
        public string Name => "Elemental Mastery";
        public string Description => "Increases damage of specific elements.";
        private readonly int _elementId;
        private readonly float _multiplier;
        public ElementalDamageBonusAbility(int elementId, float bonusPercent) { _elementId = elementId; _multiplier = 1.0f + (bonusPercent / 100f); }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx) => ctx.MoveHasElement(_elementId) ? currentDamage * _multiplier : currentDamage;
    }

    public class GlassCannonAbility : IOutgoingDamageModifier, IIncomingDamageModifier
    {
        public string Name => "Glass Cannon";
        public string Description => "Deal more, take more.";
        private readonly float _outgoingMult;
        private readonly float _incomingMult;
        public GlassCannonAbility(float outgoingBonus, float incomingMalus) { _outgoingMult = 1.0f + (outgoingBonus / 100f); _incomingMult = 1.0f + (incomingMalus / 100f); }
        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx) => currentDamage * _outgoingMult;
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx) => currentDamage * _incomingMult;
    }

    public class PhysicalDamageReductionAbility : IIncomingDamageModifier
    {
        public string Name => "Thick Skin";
        public string Description => "Reduces physical damage.";
        private readonly float _multiplier;
        public PhysicalDamageReductionAbility(float reductionPercent) { _multiplier = 1.0f - (reductionPercent / 100f); }
        public float ModifyIncomingDamage(float currentDamage, CombatContext ctx) => (ctx.Move != null && ctx.Move.ImpactType == ImpactType.Physical) ? currentDamage * _multiplier : currentDamage;
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
                int healAmount = (int)(ctx.Actor.Stats.MaxHP * (_healPercent / 100f));
                int hpBefore = (int)ctx.Actor.VisualHP;
                ctx.Actor.ApplyHealing(healAmount);
                EventBus.Publish(new GameEvents.CombatantHealed { Actor = ctx.Actor, Target = ctx.Actor, HealAmount = healAmount, VisualHPBefore = hpBefore });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} absorbed the attack!" });
                return 0f;
            }
            return currentDamage;
        }
    }

    // --- ACTION MODIFIERS & STATEFUL ABILITIES ---

    public class AmbushPredatorAbility : IActionModifier
    {
        public string Name => "Ambush Predator";
        public string Description => "First attack is faster but weaker.";
        private readonly int _priorityBonus;
        private readonly float _powerMultiplier;

        public AmbushPredatorAbility(int priorityBonus, float powerPenaltyPercent)
        {
            _priorityBonus = priorityBonus;
            _powerMultiplier = 1.0f + (powerPenaltyPercent / 100f); // Penalty is usually negative
        }

        public void ModifyAction(QueuedAction action, BattleCombatant owner)
        {
            if (!owner.HasUsedFirstAttack && action.ChosenMove != null)
            {
                action.Priority += _priorityBonus;
                action.ChosenMove.Power = (int)(action.ChosenMove.Power * _powerMultiplier);
                // We don't trigger EventBus here to avoid spamming log before action executes
            }
        }
    }

    public class SpellweaverAbility : IOnActionComplete, IOutgoingDamageModifier
    {
        public string Name => "Spellweaver";
        public string Description => "Actions boost next Spell.";
        private readonly float _bonusMultiplier;
        private bool _isActive = false;

        public SpellweaverAbility(float bonusPercent) { _bonusMultiplier = 1.0f + (bonusPercent / 100f); }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            if (action.ChosenMove != null)
            {
                if (action.ChosenMove.MoveType == MoveType.Action)
                {
                    _isActive = true;
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} is ready!" });
                }
                else if (action.ChosenMove.MoveType == MoveType.Spell)
                {
                    _isActive = false;
                }
            }
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (_isActive && ctx.Move != null && ctx.Move.MoveType == MoveType.Spell)
            {
                return currentDamage * _bonusMultiplier;
            }
            return currentDamage;
        }
    }

    public class MomentumAbility : IOnActionComplete, IOnKill, IOutgoingDamageModifier
    {
        public string Name => "Momentum";
        public string Description => "Kills boost next attack.";
        private readonly float _bonusMultiplier;
        private bool _isActive = false;

        public MomentumAbility(float bonusPercent) { _bonusMultiplier = 1.0f + (bonusPercent / 100f); }

        public void OnActionComplete(QueuedAction action, BattleCombatant owner)
        {
            // Reset if an attack was used (consumed the buff)
            if (action.ChosenMove != null && action.ChosenMove.Power > 0)
            {
                _isActive = false;
            }
        }

        public void OnKill(CombatContext ctx)
        {
            _isActive = true;
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} is building!" });
        }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            if (_isActive && ctx.Move != null && ctx.Move.Power > 0)
            {
                return currentDamage * _bonusMultiplier;
            }
            return currentDamage;
        }
    }

    public class EscalationAbility : ITurnLifecycle, IOutgoingDamageModifier
    {
        public string Name => "Escalation";
        public string Description => "Damage increases every turn.";
        private readonly float _bonusPerStack;
        private readonly float _maxBonus;
        private int _stacks = 0;

        public EscalationAbility(float bonusPerTurn, float maxBonus)
        {
            _bonusPerStack = bonusPerTurn;
            _maxBonus = maxBonus;
        }

        public void OnTurnStart(BattleCombatant owner) { }
        public void OnTurnEnd(BattleCombatant owner) { _stacks++; }

        public float ModifyOutgoingDamage(float currentDamage, CombatContext ctx)
        {
            float totalBonus = Math.Min(_stacks * _bonusPerStack, _maxBonus);
            return currentDamage * (1.0f + (totalBonus / 100f));
        }
    }

    // --- TRIGGERS ---

    public class PainFuelAbility : IOnCritReceived
    {
        public string Name => "Pain Fuel";
        public string Description => "Taking crit raises stats.";

        public void OnCritReceived(CombatContext ctx)
        {
            var target = ctx.Target; // The one holding this ability
            var (successStr, _) = target.ModifyStatStage(OffensiveStatType.Strength, 2);
            if (successStr) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Strength, Amount = 2 });

            var (successInt, _) = target.ModifyStatStage(OffensiveStatType.Intelligence, 2);
            if (successInt) EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = target, Stat = OffensiveStatType.Intelligence, Amount = 2 });

            if (successStr || successInt)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{target.Name}'s {Name} turned pain into power!" });
            }
        }
    }

    public class ContagionAbility : IOnStatusApplied
    {
        public string Name => "Contagion";
        public string Description => "Spreads negative status.";
        private readonly int _chance;
        private readonly int _durationBonus;

        public ContagionAbility(int chance, int durationBonus)
        {
            _chance = chance;
            _durationBonus = durationBonus;
        }

        public void OnStatusApplied(CombatContext ctx, StatusEffectInstance status)
        {
            // Only spread negative effects
            if (status.EffectType == StatusEffectType.Regen || status.EffectType == StatusEffectType.Dodging) return;

            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                var potentialTargets = battleManager.AllCombatants.Where(c =>
                    c.IsPlayerControlled != ctx.Actor.IsPlayerControlled &&
                    c != ctx.Target &&
                    !c.IsDefeated &&
                    !c.HasStatusEffect(status.EffectType)
                ).ToList();

                if (potentialTargets.Any())
                {
                    var contagionTarget = potentialTargets[random.Next(potentialTargets.Count)];
                    contagionTarget.AddStatusEffect(new StatusEffectInstance(status.EffectType, status.DurationInTurns + _durationBonus));
                    EventBus.Publish(new GameEvents.TerminalMessagePublished
                    {
                        Message = $"{ctx.Actor.Name}'s {Name} spread {status.GetDisplayName()} to {contagionTarget.Name}!"
                    });
                }
            }
        }
    }

    public class SadistAbility : IOnStatusApplied
    {
        public string Name => "Sadist";
        public string Description => "Applying status raises Strength.";

        public void OnStatusApplied(CombatContext ctx, StatusEffectInstance status)
        {
            if (status.EffectType == StatusEffectType.Regen || status.EffectType == StatusEffectType.Dodging) return;

            var (success, _) = ctx.Actor.ModifyStatStage(OffensiveStatType.Strength, 1);
            if (success)
            {
                EventBus.Publish(new GameEvents.CombatantStatStageChanged { Target = ctx.Actor, Stat = OffensiveStatType.Strength, Amount = 1 });
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s {Name} raised their Strength!" });
            }
        }
    }

    public class CausticBloodAbility : ILifestealReaction
    {
        public string Name => "Caustic Blood";
        public string Description => "Lifesteal damages attacker.";

        public bool OnLifestealReceived(BattleCombatant source, int amount, BattleCombatant owner)
        {
            // Source is the attacker trying to heal. Owner is the one with Caustic Blood.
            source.ApplyDamage(amount);
            EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = source, RecoilDamage = amount });
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} burns {source.Name}!" });
            return true; // Prevent normal healing
        }
    }

    // --- EXISTING ABILITIES (Reprinted for completeness) ---
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
                if (applied) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{ctx.Actor.Name}'s ability applied {_type} to {ctx.Target.Name}!" });
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
                ctx.Target.ApplyDamage(recoil); // Target here is the Attacker
                EventBus.Publish(new GameEvents.CombatantRecoiled { Actor = ctx.Target, RecoilDamage = recoil });
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
            }
        }
    }

    public class ToxicAuraAbility : ITurnLifecycle
    {
        public string Name => "Toxic Aura";
        public string Description => "Poison random enemy at end of turn.";
        private readonly int _chance;
        private readonly int _duration;
        public ToxicAuraAbility(int chance, int duration) { _chance = chance; _duration = duration; }
        public void OnTurnStart(BattleCombatant owner) { }
        public void OnTurnEnd(BattleCombatant owner)
        {
            var random = new Random();
            if (random.Next(1, 101) <= _chance)
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                var enemies = battleManager.AllCombatants.Where(c => c.IsPlayerControlled != owner.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                if (enemies.Any())
                {
                    var target = enemies[random.Next(enemies.Count)];
                    target.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Poison, _duration));
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} poisoned {target.Name}!" });
                }
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
            if (anyAffected) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"{owner.Name}'s {Name} lowered opponents' {_stat}!" });
        }
    }
}