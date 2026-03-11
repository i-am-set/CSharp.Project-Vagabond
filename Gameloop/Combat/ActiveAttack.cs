using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Animations;
using ProjectVagabond.Deliveries;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle
{
    public struct BattleContext
    {
        public ArenaScene Arena;
        public Global Global;
        public SpriteManager SpriteManager;
        public Core Core;
        public ParticleSystemManager ParticleSystemManager;
        public TextureFactory TextureFactory;
        public Texture2D Pixel;
    }

    public class ActiveAttack : IPoolable
    {
        public bool IsPooled { get; set; }
        public ArenaWizard Caster { get; set; }
        public ArenaWizard TargetWizard { get; set; }
        public MoveDefinition Move { get; set; }
        public Vector2 Origin { get; set; }
        public Vector2 Direction { get; set; }
        public Vector2 TargetPosition { get; set; }
        public IDelivery DeliveryInstance { get; set; }
        public IAnimationInstance Animation { get; set; }
        public bool HasTriggeredImpact { get; set; }
        public bool IsCanceled { get; set; }
        public bool HasStartedAnimation { get; set; }
        public float ActiveTime { get; private set; }

        public BattleContext Context { get; set; }

        private bool _hasNotifiedCaster;

        public void Reset()
        {
            Caster = null;
            TargetWizard = null;
            Move = null;
            Origin = Vector2.Zero;
            Direction = Vector2.Zero;
            TargetPosition = Vector2.Zero;

            if (DeliveryInstance != null) DeliveryInstance.ReturnToPool();
            DeliveryInstance = null;

            if (Animation != null) Animation.ReturnToPool();
            Animation = null;

            HasTriggeredImpact = false;
            IsCanceled = false;
            HasStartedAnimation = false;
            ActiveTime = 0f;
            Context = default;

            _hasNotifiedCaster = false;
        }

        public void ReturnToPool()
        {
            Pool<ActiveAttack>.Return(this);
        }

        public void Update(float dt, BattleContext context)
        {
            ActiveTime += dt;

            if (IsCanceled)
            {
                Animation?.Cancel();
            }
            else
            {
                DeliveryInstance?.Update(dt, context, this);

                if (IsCanceled)
                {
                    Animation?.Cancel();
                }
                else if (DeliveryInstance != null && !DeliveryInstance.IsAnimationPaused)
                {
                    if (!HasStartedAnimation && Animation != null)
                    {
                        Animation.Start(this, context);
                        HasStartedAnimation = true;
                    }

                    if (Animation != null)
                    {
                        Animation.Update(dt, context, this);
                        if (Animation.HasTriggeredImpact && !HasTriggeredImpact)
                        {
                            HasTriggeredImpact = true;
                            DeliveryInstance.TriggerImpact(context, this);
                        }
                    }
                    else if (!HasTriggeredImpact)
                    {
                        HasTriggeredImpact = true;
                        DeliveryInstance.TriggerImpact(context, this);
                    }
                }
            }

            if (IsFinished && !_hasNotifiedCaster)
            {
                _hasNotifiedCaster = true;
                Caster?.Controller?.NotifyAttackFinished(this);
            }
        }

        public bool IsFinished => IsCanceled || ((Animation == null || Animation.IsFinished) && (DeliveryInstance == null || DeliveryInstance.IsFinished));
    }
}