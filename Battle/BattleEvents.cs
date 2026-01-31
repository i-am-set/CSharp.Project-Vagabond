using System.Collections.Generic;
using ProjectVagabond.Battle.Abilities;

namespace ProjectVagabond.Battle
{
    public abstract class GameEvent
    {
        public bool IsHandled { get; set; } = false;
    }

    public class BattleStartedEvent : GameEvent
    {
        public List<BattleCombatant> Combatants { get; }
        public BattleStartedEvent(List<BattleCombatant> combatants) => Combatants = combatants;
    }

    public class TurnStartEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public TurnStartEvent(BattleCombatant actor) => Actor = actor;
    }

    public class TurnEndEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public TurnEndEvent(BattleCombatant actor) => Actor = actor;
    }

    public class ActionDeclaredEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public MoveData Move { get; }
        public BattleCombatant Target { get; }

        public ActionDeclaredEvent(BattleCombatant actor, MoveData move, BattleCombatant target)
        {
            Actor = actor;
            Move = move;
            Target = target;
        }
    }

    public class CalculateStatEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public OffensiveStatType StatType { get; }
        public float BaseValue { get; }
        public float FinalValue { get; set; }

        public CalculateStatEvent(BattleCombatant actor, OffensiveStatType statType, float baseValue)
        {
            Actor = actor;
            StatType = statType;
            BaseValue = baseValue;
            FinalValue = baseValue;
        }
    }

    public class CalculateDamageEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public BattleCombatant Target { get; }
        public MoveData Move { get; }
        public float BaseDamage { get; }
        public bool IsCritical { get; }
        public bool IsGraze { get; }

        // Mutable properties
        public float DamageMultiplier { get; set; } = 1.0f;
        public float FlatBonus { get; set; } = 0f;
        public int FinalDamage { get; set; }

        // Flags set by abilities
        public bool WasProtected { get; set; }
        public bool WasVulnerable { get; set; }

        public CalculateDamageEvent(BattleCombatant actor, BattleCombatant target, MoveData move, float baseDamage, bool isCritical, bool isGraze)
        {
            Actor = actor;
            Target = target;
            Move = move;
            BaseDamage = baseDamage;
            IsCritical = isCritical;
            IsGraze = isGraze;
            FinalDamage = (int)baseDamage;
        }
    }

    public class CheckHitChanceEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public BattleCombatant Target { get; }
        public MoveData Move { get; }
        public int BaseAccuracy { get; }
        public int FinalAccuracy { get; set; }

        public CheckHitChanceEvent(BattleCombatant actor, BattleCombatant target, MoveData move, int baseAccuracy)
        {
            Actor = actor;
            Target = target;
            Move = move;
            BaseAccuracy = baseAccuracy;
            FinalAccuracy = baseAccuracy;
        }
    }

    public class ReactionEvent : GameEvent
    {
        public BattleCombatant Actor { get; }
        public BattleCombatant Target { get; }
        public QueuedAction TriggeringAction { get; }
        public DamageCalculator.DamageResult Result { get; }

        public ReactionEvent(BattleCombatant actor, BattleCombatant target, QueuedAction triggeringAction, DamageCalculator.DamageResult result)
        {
            Actor = actor;
            Target = target;
            TriggeringAction = triggeringAction;
            Result = result;
        }
    }

    public class StatusAppliedEvent : GameEvent
    {
        public BattleCombatant Target { get; }
        public StatusEffectInstance StatusEffect { get; }

        public StatusAppliedEvent(BattleCombatant target, StatusEffectInstance statusEffect)
        {
            Target = target;
            StatusEffect = statusEffect;
        }
    }

    public class StatusRemovedEvent : GameEvent
    {
        public BattleCombatant Target { get; }
        public StatusEffectInstance StatusEffect { get; }

        public StatusRemovedEvent(BattleCombatant target, StatusEffectInstance statusEffect)
        {
            Target = target;
            StatusEffect = statusEffect;
        }
    }
}