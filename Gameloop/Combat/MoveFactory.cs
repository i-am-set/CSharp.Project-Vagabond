using ProjectVagabond.Deliveries;

namespace ProjectVagabond.Battle
{
    public static class MoveFactory
    {
        public static MoveDefinition CreateMove(MoveData data)
        {
            var move = new MoveDefinition
            {
                Name = data.Name,
                BasePower = data.BasePower,
                ChargeTime = data.ChargeTime,
                Weight = data.Weight,
                Knockback = data.Knockback,
                TargetSelf = data.TargetSelf,
                CanEffectSelf = data.CanEffectSelf,
                TargetClosest = data.TargetClosest,
                ProjectileTravelTime = data.DeliveryProjectileTravelTime,
                AnimationID = data.AnimationID,
                ExecuteOnChargeStart = data.ExecuteOnChargeStart,
                RequiresFocus = data.RequiresFocus,
                ShowProjectileIndicator = data.ShowProjectileIndicator
            };

            if (data.DeliveryType == "InstantAOE")
            {
                move.Delivery = new InstantAOEDelivery { Radius = data.DeliveryRadius };
            }
            else if (data.DeliveryType == "TickingBeam")
            {
                move.Delivery = new TickingBeamDelivery
                {
                    Width = data.DeliveryWidth,
                    Length = data.DeliveryLength,
                    Lifetime = data.DeliveryLifetime,
                    TickRate = data.DeliveryTickRate
                };
            }
            else if (data.DeliveryType == "SingleTarget" || data.DeliveryType == "Self")
            {
                move.Delivery = new SingleTargetDelivery();
            }
            else if (data.DeliveryType == "DashMelee")
            {
                move.Delivery = new DashMeleeDelivery
                {
                    Width = data.DeliveryWidth,
                    Length = data.DeliveryLength,
                    Lifetime = data.DeliveryLifetime,
                    DashDistance = data.DeliveryDashDistance
                };
            }
            else if (data.DeliveryType == "SeekAndDash")
            {
                move.Delivery = new SeekAndDashDelivery
                {
                    SeekRadius = data.DeliverySeekRadius,
                    SeekDuration = data.DeliverySeekDuration,
                    DashDistance = data.DeliveryDashDistance,
                    DashDuration = data.DeliveryDashDuration
                };
            }
            else if (data.DeliveryType == "MeteorStrike")
            {
                move.Delivery = new MeteorStrikeDelivery
                {
                    Radius = data.DeliveryRadius,
                    ProjectileCount = data.DeliveryProjectileCount,
                    ProjectileRadius = data.DeliveryProjectileRadius,
                    Duration = data.DeliveryLifetime,
                    FallTime = data.DeliveryFallTime,
                    ProjectileAnimationID = data.DeliveryProjectileAnimation
                };
            }
            else if (data.DeliveryType == "MultiProjectile")
            {
                move.Delivery = new MultiProjectileDelivery
                {
                    ProjectileCount = data.DeliveryProjectileCount,
                    Duration = data.DeliveryLifetime,
                    ProjectileAnimationID = data.DeliveryProjectileAnimation,
                    ProjectileTravelTime = data.DeliveryProjectileTravelTime
                };
            }

            if (data.EffectType == "Damage")
            {
                move.Effects.Add(new DamageEffect());
            }
            else if (data.EffectType == "Heal")
            {
                move.Effects.Add(new HealEffect { HealPercentage = data.EffectArg });
            }

            return move;
        }
    }
}