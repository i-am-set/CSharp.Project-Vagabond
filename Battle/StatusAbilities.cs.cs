using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.Abilities
{
    public class PoisonLogicAbility : IAbility
    {
        public string Name => "Poison Logic";
        public string Description => "Deals damage at end of turn.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;

        public PoisonLogicAbility(StatusEffectInstance status)
        {
            _status = status;
        }

        public void OnEvent(GameEvent e)
        {
            if (e is TurnEndEvent turnEnd && turnEnd.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                int safeTurnCount = Math.Min(_status.PoisonTurnCount, 30);
                long rawDamage = (long)global.PoisonBaseDamage * (long)Math.Pow(2, safeTurnCount);
                int damage = (int)Math.Min(rawDamage, int.MaxValue);

                turnEnd.Actor.ApplyDamage(damage);
                _status.PoisonTurnCount++;

                EventBus.Publish(new GameEvents.StatusEffectTriggered
                {
                    Combatant = turnEnd.Actor,
                    EffectType = StatusEffectType.Poison,
                    Damage = damage
                });
            }
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

        public void OnEvent(GameEvent e)
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

        public void OnEvent(GameEvent e)
        {
            if (e is TurnStartEvent turnStart && turnStart.Actor.ActiveStatusEffects.Contains(_status))
            {
                turnStart.Actor.Tags.Add("State.Stunned");
                e.IsHandled = true;
                EventBus.Publish(new GameEvents.ActionFailed { Actor = turnStart.Actor, Reason = "stunned" });
            }
        }
    }

    public class BurnLogicAbility : IAbility
    {
        public string Name => "Burn Logic";
        public string Description => "Increases damage taken.";
        public int Priority => 0;

        private readonly StatusEffectInstance _status;
        public BurnLogicAbility(StatusEffectInstance status) { _status = status; }

        public void OnEvent(GameEvent e)
        {
            // Only apply if the TARGET has this status
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                dmgEvent.DamageMultiplier *= global.BurnDamageMultiplier;
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

        public void OnEvent(GameEvent e)
        {
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionEvent.Move.ImpactType == ImpactType.Status)
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

        public void OnEvent(GameEvent e)
        {
            if (e is ActionDeclaredEvent actionEvent && actionEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                if (actionEvent.Move.MoveType == MoveType.Spell)
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

        public void OnEvent(GameEvent e)
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

        public void OnEvent(GameEvent e)
        {
            // Apply if TARGET has status
            if (e is CheckHitChanceEvent hitEvent && hitEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                hitEvent.FinalAccuracy = (int)(hitEvent.FinalAccuracy * global.DodgingAccuracyMultiplier);
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

        public void OnEvent(GameEvent e)
        {
            // Apply if ACTOR has status
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Actor.ActiveStatusEffects.Contains(_status))
            {
                var global = ServiceLocator.Get<Global>();
                dmgEvent.DamageMultiplier *= global.EmpoweredDamageMultiplier;
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

        public void OnEvent(GameEvent e)
        {
            // Apply if TARGET has status
            if (e is CalculateDamageEvent dmgEvent && dmgEvent.Target.ActiveStatusEffects.Contains(_status))
            {
                // Add tag for UI/Logic
                dmgEvent.Target.Tags.Add("State.Protected");
                dmgEvent.DamageMultiplier = 0f;
            }
        }
    }
}