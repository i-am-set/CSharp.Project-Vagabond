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
                IsRare = data.IsRare,
                Knockback = data.Knockback,
                TargetSelf = data.TargetSelf,
                CanEffectSelf = data.CanEffectSelf,
                TargetClosest = data.TargetClosest,
                ProjectileTravelTime = data.DeliveryProjectileTravelTime,
                AnimationID = data.AnimationID,
                CastSoundCue = data.CastSoundCue,
                CastSoundPitchVariance = data.CastSoundPitchVariance,
                ImpactSoundCue = data.ImpactSoundCue,
                TickSoundCue = data.TickSoundCue,
                LoopSoundCue = data.LoopSoundCue,
                BounceSoundCue = data.BounceSoundCue,
                ExecuteOnChargeStart = data.ExecuteOnChargeStart,
                RequiresFocus = data.RequiresFocus,
                ShowProjectileIndicator = data.ShowProjectileIndicator,
                DeliveryImpactMidFlight = data.DeliveryImpactMidFlight
            };

            if (data.DeliveryType == "InstantAOE")
            {
                move.Delivery = new InstantAOEDelivery
                {
                    Radius = data.DeliveryRadius,
                    Lifetime = data.DeliveryLifetime,
                    TickRate = data.DeliveryTickRate,
                    PullSpeed = data.DeliveryPullSpeed,
                    CheckProjectileCollision = data.DeliveryImpactMidFlight
                };
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
                move.Delivery = new SingleTargetDelivery
                {
                    CheckProjectileCollision = data.DeliveryImpactMidFlight
                };
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
                    ProjectileTravelTime = data.DeliveryProjectileTravelTime,
                    SpreadAngle = data.DeliverySpreadAngle
                };
            }
            else if (data.DeliveryType == "Chain")
            {
                move.Delivery = new ChainDelivery
                {
                    BounceCount = data.DeliveryBounceCount,
                    BounceRadius = data.DeliveryBounceRadius,
                    BounceDelay = data.DeliveryBounceDelay > 0 ? data.DeliveryBounceDelay : 0.1f,
                    VisualDuration = data.DeliveryLifetime > 0 ? data.DeliveryLifetime : 0.3f
                };
            }
            else if (data.DeliveryType == "LingeringAOE")
            {
                move.Delivery = new LingeringAOEDelivery
                {
                    Shape = data.DeliveryRadius > 0 ? LingeringAOEDelivery.AOEShape.Circle : LingeringAOEDelivery.AOEShape.Line,
                    Radius = data.DeliveryRadius,
                    Width = data.DeliveryWidth,
                    Length = data.DeliveryLength,
                    Lifetime = data.DeliveryLifetime,
                    TickRate = data.DeliveryTickRate,
                    VisualStyle = data.DeliveryVisualStyle
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
            else if (data.EffectType == "Drain")
            {
                move.Effects.Add(new DrainEffect { DrainPercentage = data.EffectArg > 0 ? data.EffectArg : 0.5f });
            }

            return move;
        }
    }
}