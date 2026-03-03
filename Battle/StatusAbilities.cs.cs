using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.Abilities
{
    public class PoisonLogicAbility : IAbility
    {
        public string Name => "Poison Logic";
        public string Description => "Deals flat damage at end of turn based on turns active.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public PoisonLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is TurnEndEvent turnEnd && turnEnd.Actor.ActiveStatusEffects.Contains(_status))
            {
                _status.PoisonTurnCount++;
                int damage = _status.PoisonTurnCount; // Scales +1 flat damage per turn

                turnEnd.Actor.ApplyDamage(damage);
                EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = turnEnd.Actor, EffectType = StatusEffectType.Poison, Damage = damage });
            }
        }
    }

    public class BleedingLogicAbility : IAbility
    {
        public string Name => "Bleeding Logic";
        public string Description => "Deals 1 damage at end of turn and when attacking.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public BleedingLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is TurnEndEvent turnEnd && turnEnd.Actor.ActiveStatusEffects.Contains(_status))
            {
                ApplyBleedDamage(turnEnd.Actor);
            }
            else if (e is ActionDeclaredEvent actionDecl && actionDecl.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionDecl.Move != null && actionDecl.Move.Power > 0)
                {
                    ApplyBleedDamage(actionDecl.Actor);
                    if (actionDecl.Actor.Stats.CurrentHP <= 0)
                    {
                        actionDecl.IsHandled = true;
                        EventBus.Publish(new GameEvents.ActionFailed { Actor = actionDecl.Actor, Reason = "bleeding", MoveName = actionDecl.Move.MoveName });
                        if (!actionDecl.Actor.IsDying) EventBus.Publish(new GameEvents.CombatantVisualDeath { Victim = actionDecl.Actor });
                    }
                }
            }
        }

        private void ApplyBleedDamage(BattleCombatant combatant)
        {
            int damage = 1; // Flat 1 damage
            combatant.ApplyDamage(damage);
            EventBus.Publish(new GameEvents.StatusEffectTriggered { Combatant = combatant, EffectType = StatusEffectType.Bleeding, Damage = damage });
        }
    }

    public class RegenLogicAbility : IAbility
    {
        public string Name => "Regen Logic";
        public string Description => "Heals at end of turn.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;

        public RegenLogicAbility(StatusEffectInstance status)
        {
            _status = status;
        }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is TurnEndEvent turnEnd && turnEnd.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                int healAmount = (int)(turnEnd.Actor.Stats.MaxHP * global.RegenPercent);

                if (healAmount > 0)
                {
                    int hpBefore = (int)turnEnd.Actor.VisualHP;
                    turnEnd.Actor.ApplyHealing(healAmount);

                    EventBus.Publish(new GameEvents.CombatantHealed
                    {
                        Actor = turnEnd.Actor,
                        Target = turnEnd.Actor,
                        HealAmount = healAmount,
                        VisualHPBefore = hpBefore
                    });
                }
            }
        }
    }

    public class StunLogicAbility : IAbility
    {
        public string Name => "Stun Logic";
        public string Description => "Prevents action.";
        public int Priority => 100;

        private readonly StatusEffectInstance _status;
        public StunLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is TurnStartEvent turnStart && turnStart.Actor.ActiveStatusEffects.Contains(_status))
            {
                turnStart.Actor.Tags.Add(GameplayTags.States.Stunned);
                e.IsHandled = true;

                if (!_status.IsPermanent)
                {
                    _status.DurationInTurns--;
                }
            }
        }
    }

    public class BurnLogicAbility : IAbility
    {
        public string Name => "Burn Logic";
        public string Description => "Increases damage taken by 1.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public BurnLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                dmgEvent.FlatBonus += 1f; // Flat +1 damage taken
            }
        }
    }

    public class ProvokeLogicAbility : IAbility
    {
        public string Name => "Provoke Logic";
        public string Description => "Prevents non-attack moves.";
        public int Priority => 10;

        private readonly StatusEffectInstance _status;
        public ProvokeLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionEvent.Move != null && actionEvent.Move.ImpactType == ImpactType.Status)
                {
                    e.IsHandled = true;
                    EventBus.Publish(new GameEvents.ActionFailed
                    {
                        Actor = actionEvent.Actor,
                        Reason = "provoked",
                        MoveName = actionEvent.Move.MoveName
                    });
                }
            }
        }
    }

    public class SilenceLogicAbility : IAbility
    {
        public string Name => "Silence Logic";
        public string Description => "Prevents spells.";
        public int Priority => 10;

        private readonly StatusEffectInstance _status;
        public SilenceLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionEvent.Move != null && actionEvent.Move.MoveType == MoveType.Spell)
                {
                    e.IsHandled = true;
                    EventBus.Publish(new GameEvents.ActionFailed
                    {
                        Actor = actionEvent.Actor,
                        Reason = "silenced",
                        MoveName = actionEvent.Move.MoveName
                    });
                }
            }
        }
    }

    public class FrostbiteLogicAbility : IAbility
    {
        public string Name => "Frostbite Logic";
        public string Description => "Reduces Agility.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public FrostbiteLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateStatEvent statEvent && statEvent.StatType == OffensiveStatType.Agility && statEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                statEvent.FinalValue *= global.FrostbiteAgilityMultiplier;
            }
        }
    }

    public class DodgingLogicAbility : IAbility
    {
        public string Name => "Dodging Logic";
        public string Description => "Increases evasion.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public DodgingLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CheckHitChanceEvent hitEvent && hitEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                hitEvent.FinalAccuracy = (int)(hitEvent.FinalAccuracy * global.DodgingAccuracyMultiplier);
            }
        }
    }

    public class BlindLogicAbility : IAbility
    {
        public string Name => "Blind Logic";
        public string Description => "Decreases accuracy.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public BlindLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CheckHitChanceEvent hitEvent && hitEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                hitEvent.FinalAccuracy = (int)(hitEvent.FinalAccuracy * global.BlindAccuracyMultiplier);
            }
        }
    }

    public class VulnerableLogicAbility : IAbility
    {
        public string Name => "Vulnerable Logic";
        public string Description => "Increases damage taken for one hit, then is consumed.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public VulnerableLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            // Only proc on actual hits, not simulations
            if (!context.IsSimulation && e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                if (dmgEvent.Move.Power > 0) // Ensure it's a damaging move
                {
                    var global = ServiceLocator.Get<Global>();
                    dmgEvent.DamageMultiplier *= global.VulnerableDamageMultiplier;
                    dmgEvent.FinalDamage = (int)(dmgEvent.FinalDamage * global.VulnerableDamageMultiplier);
                    dmgEvent.WasVulnerable = true;

                    // Consume the status effect immediately
                    dmgEvent.Target.ActiveStatusEffects.Remove(_status);
                    EventBus.Publish(new GameEvents.StatusEffectRemoved { Combatant = dmgEvent.Target, EffectType = StatusEffectType.Vulnerable });
                }
            }
        }
    }

    public class TrappedLogicAbility : IAbility
    {
        public string Name => "Trapped Logic";
        public string Description => "Prevents switching out.";
        public int Priority => 10;

        private readonly StatusEffectInstance _status;
        public TrappedLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionEvent.ActionType == QueuedActionType.Switch)
                {
                    actionEvent.IsHandled = true;
                    EventBus.Publish(new GameEvents.ActionFailed
                    {
                        Actor = actionEvent.Actor,
                        Reason = "trapped",
                        MoveName = "SWITCH"
                    });
                }
            }
        }
    }

    public class EmpoweredLogicAbility : IAbility
    {
        public string Name => "Empowered Logic";
        public string Description => "Increases outgoing damage.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public EmpoweredLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                dmgEvent.DamageMultiplier *= global.EmpoweredDamageMultiplier;
                dmgEvent.FinalDamage = (int)(dmgEvent.FinalDamage * global.EmpoweredDamageMultiplier);
            }
        }
    }

    public class ProtectedLogicAbility : IAbility
    {
        public string Name => "Protected Logic";
        public string Description => "Blocks damage.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public ProtectedLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                dmgEvent.Target.Tags.Add(GameplayTags.States.Protected);
                dmgEvent.DamageMultiplier = 0f;
                dmgEvent.FinalDamage = 0;
                dmgEvent.WasProtected = true;
            }
        }
    }

    public class WideProtectedLogicAbility : IAbility
    {
        public string Name => "Wide Protected Logic";
        public string Description => "Blocks AoE damage.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public WideProtectedLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e, BattleContext context)
        {
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                var targetType = dmgEvent.Move.Target;
                bool isAoE = targetType == TargetType.Both || targetType == TargetType.Every || targetType == TargetType.All || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery || targetType == TargetType.RandomAll;
                bool isEnemyAttack = dmgEvent.Actor.IsPlayerControlled != dmgEvent.Target.IsPlayerControlled;

                if (isAoE && isEnemyAttack)
                {
                    dmgEvent.Target.Tags.Add(GameplayTags.States.Protected);
                    dmgEvent.DamageMultiplier = 0f;
                    dmgEvent.FinalDamage = 0;
                    dmgEvent.WasProtected = true;
                }
            }
        }
    }
}
