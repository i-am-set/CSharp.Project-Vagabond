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

            bool tookDamage = target.Controller.TakeDamage(damage, isCrit);

            if (tookDamage && attack.Move.Knockback > 0)
            {
                Vector2 sourcePos = attack.Caster.Data.Combat.Position;
                if (attack.DeliveryInstance is InstantAOEDelivery) sourcePos = attack.TargetPosition;
                else if (attack.DeliveryInstance is TickingBeamDelivery) sourcePos = attack.Origin;

                target.Controller.ApplyKnockback(sourcePos, attack.Move.Knockback, context.Arena);
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
}