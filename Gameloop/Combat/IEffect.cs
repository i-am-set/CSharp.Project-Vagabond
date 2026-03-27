using Microsoft.Xna.Framework;
using ProjectVagabond.Deliveries;
using System;

namespace ProjectVagabond.Battle
{
    public interface IEffect
    {
        void Apply(ActiveAttack attack, ArenaWizard target, BattleContext context);
    }

    public class DamageEffect : IEffect
    {
        private static readonly Random _random = new Random();

        public void Apply(ActiveAttack attack, ArenaWizard target, BattleContext context)
        {
            bool isCrit = _random.Next(24) == 0;

            int targetTenacity = target.Data.Stats.Tenacity;
            if (isCrit)
            {
                targetTenacity = Math.Max(1, targetTenacity / 2);
            }

            int damage = Math.Max(1, (int)Math.Floor(attack.Move.BasePower * (attack.Caster.Data.Stats.Power + 10) / ((targetTenacity + 10) * 13.33f)));

            int oldHP = target.Data.Stats.CurrentHP;
            bool tookDamage = target.Controller.TakeDamage(damage, isCrit, attack.Caster);
            int actualDamage = oldHP - target.Data.Stats.CurrentHP;

            if (tookDamage)
            {
                float damagePercent = (float)actualDamage / target.Data.Stats.MaxHP;
                var haptics = ServiceLocator.Get<HapticsManager>();

                if (isCrit || damagePercent > 0.15f)
                {
                    haptics.TriggerImpactTwist(damagePercent * 2f, 0.2f);
                    haptics.TriggerShake(damagePercent * 5f, 0.2f);
                    context.Arena.TriggerHitstop(context.Global.HitstopDuration_Crit);
                }
                else
                {
                    haptics.TriggerShake(damagePercent * 2f, 0.1f);
                    context.Arena.TriggerHitstop(context.Global.HitstopDuration_Normal);
                }

                if (attack.Move.Knockback != 0)
                {
                    Vector2 sourcePos = attack.Caster.Data.Combat.Position;
                    if (attack.DeliveryInstance is InstantAOEDelivery) sourcePos = attack.TargetPosition;
                    else if (attack.DeliveryInstance is TickingBeamDelivery) sourcePos = attack.Origin;

                    target.Controller.ApplyKnockback(sourcePos, attack.Move.Knockback, context.Arena);
                }
            }
        }
    }

    public class HealEffect : IEffect
    {
        public float HealPercentage { get; set; } = 0.5f;

        public void Apply(ActiveAttack attack, ArenaWizard target, BattleContext context)
        {
            int heal = Math.Max(1, (int)(attack.Move.BasePower * HealPercentage));
            target.Controller.Heal(heal);
        }
    }

    public class DrainEffect : IEffect
    {
        public float DrainPercentage { get; set; } = 0.5f;
        private static readonly Random _random = new Random();

        public void Apply(ActiveAttack attack, ArenaWizard target, BattleContext context)
        {
            bool isCrit = _random.Next(24) == 0;

            int targetTenacity = target.Data.Stats.Tenacity;
            if (isCrit)
            {
                targetTenacity = Math.Max(1, targetTenacity / 2);
            }

            int damage = Math.Max(1, (int)Math.Floor(attack.Move.BasePower * (attack.Caster.Data.Stats.Power + 10) / ((targetTenacity + 10) * 13.33f)));

            int oldHP = target.Data.Stats.CurrentHP;
            bool tookDamage = target.Controller.TakeDamage(damage, isCrit, attack.Caster);
            int actualDamage = oldHP - target.Data.Stats.CurrentHP;

            if (tookDamage)
            {
                float damagePercent = (float)actualDamage / target.Data.Stats.MaxHP;
                var haptics = ServiceLocator.Get<HapticsManager>();

                if (isCrit || damagePercent > 0.15f)
                {
                    haptics.TriggerImpactTwist(damagePercent * 5f, 0.2f);
                    haptics.TriggerShake(damagePercent * 10f, 0.2f);
                    context.Arena.TriggerHitstop(context.Global.HitstopDuration_Crit);
                }
                else
                {
                    haptics.TriggerShake(damagePercent * 5f, 0.1f);
                    context.Arena.TriggerHitstop(context.Global.HitstopDuration_Normal);
                }

                if (actualDamage > 0)
                {
                    int healAmount = Math.Max(1, (int)(actualDamage * DrainPercentage));
                    attack.Caster.Controller.Heal(healAmount);
                }

                if (attack.Move.Knockback != 0)
                {
                    Vector2 sourcePos = attack.Caster.Data.Combat.Position;
                    if (attack.DeliveryInstance is InstantAOEDelivery) sourcePos = attack.TargetPosition;
                    else if (attack.DeliveryInstance is TickingBeamDelivery) sourcePos = attack.Origin;

                    target.Controller.ApplyKnockback(sourcePos, attack.Move.Knockback, context.Arena);
                }
            }
        }
    }
}