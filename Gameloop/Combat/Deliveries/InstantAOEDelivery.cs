using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Deliveries
{
    public sealed class InstantAOEDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Radius { get; set; }
        public float Lifetime { get; set; }
        public float TickRate { get; set; }
        public float PullSpeed { get; set; }
        public bool CheckProjectileCollision { get; set; } = true;
        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => false;

        private float _visualTimer;
        private float _lifeTimer;
        private float _tickTimer;

        public void Reset()
        {
            _visualTimer = 0f;
            _lifeTimer = 0f;
            _tickTimer = 0f;
            IsFinished = false;
            CheckProjectileCollision = true;
        }

        public void Setup(IDelivery template)
        {
            var t = (InstantAOEDelivery)template;
            Radius = t.Radius;
            Lifetime = t.Lifetime;
            TickRate = t.TickRate;
            PullSpeed = t.PullSpeed;
            CheckProjectileCollision = t.CheckProjectileCollision;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<InstantAOEDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<InstantAOEDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            IsFinished = false;
            _visualTimer = 0.25f;
            _lifeTimer = 0f;
            _tickTimer = 0f;
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            ApplyAOE(context, attack);
        }

        private void ApplyAOE(BattleContext context, ActiveAttack attack)
        {
            foreach (var target in context.Arena.GetWizardsInCircle(attack.TargetPosition, Radius))
            {
                if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;

                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, target, context);
                }
            }
        }

        public void Update(float dt, BattleContext context, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact)
            {
                if (CheckProjectileCollision && attack.Animation != null && attack.Animation.CurrentProjectilePosition.HasValue)
                {
                    Vector2 projPos = attack.Animation.CurrentProjectilePosition.Value;
                    float hitRadius = 4f;

                    foreach (var target in context.Arena.Wizards)
                    {
                        if (target == attack.Caster) continue;
                        if (target.Data.Combat.State == WizardState.Dead) continue;

                        if (CollisionMath.RectangleIntersectsCircle(target.Controller.GetHitbox(context.SpriteManager), projPos, hitRadius))
                        {
                            attack.TargetPosition = projPos;
                            attack.Animation.ForceImpact(projPos);
                            break;
                        }
                    }
                }
                return;
            }

            if (Lifetime > 0)
            {
                _lifeTimer += dt;

                if (PullSpeed > 0)
                {
                    foreach (var target in context.Arena.GetWizardsInCircle(attack.TargetPosition, Radius))
                    {
                        if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;
                        if (target.Data.Combat.State == WizardState.Dead) continue;

                        Vector2 dir = attack.TargetPosition - target.Data.Combat.Position;
                        float dist = dir.Length();
                        if (dist > 0)
                        {
                            dir.Normalize();
                            float moveDist = Math.Min(PullSpeed * dt, dist);
                            target.Data.Combat.Position += dir * moveDist;
                            target.Data.Combat.Position = context.Arena.ClampToArena(target.Data.Combat.Position, 12f);
                        }
                    }
                }

                if (TickRate > 0)
                {
                    _tickTimer += dt;
                    if (_tickTimer >= TickRate)
                    {
                        _tickTimer -= TickRate;
                        ApplyAOE(context, attack);
                    }
                }
                if (_lifeTimer >= Lifetime) IsFinished = true;
            }
            else
            {
                _visualTimer -= dt;
                if (_visualTimer <= 0) IsFinished = true;
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            var global = attack.Context.Global;
            var circle = attack.Context.SpriteManager.CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);

                if (attack.Move.ShowProjectileIndicator && !attack.HasTriggeredImpact)
                {
                    float pulse = 0.075f + 0.0f * MathF.Sin(attack.ActiveTime * 12f);
                    spriteBatch.Draw(circle, attack.TargetPosition, null, global.Palette_Rust * pulse, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                if (global.ShowDebugOverlays)
                {
                    spriteBatch.Draw(circle, attack.TargetPosition, null, Color.Red * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;

            var circle = context.SpriteManager.CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, targetPos, null, Color.Blue * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}