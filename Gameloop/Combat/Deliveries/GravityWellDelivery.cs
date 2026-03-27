using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Deliveries
{
    public sealed class GravityWellDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Radius { get; set; }
        public float Lifetime { get; set; }
        public float PullSpeed { get; set; }
        public float SlingshotDistance { get; set; }

        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => false;

        private float _lifeTimer;
        private Guid _loopSoundHandle = Guid.Empty;
        private bool _detonated;

        public void Reset()
        {
            _lifeTimer = 0f;
            IsFinished = false;
            _detonated = false;
            if (_loopSoundHandle != Guid.Empty)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopLoopingSfx(_loopSoundHandle);
                _loopSoundHandle = Guid.Empty;
            }
        }

        public void Setup(IDelivery template)
        {
            var t = (GravityWellDelivery)template;
            Radius = t.Radius;
            Lifetime = t.Lifetime;
            PullSpeed = t.PullSpeed;
            SlingshotDistance = t.SlingshotDistance;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<GravityWellDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<GravityWellDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            IsFinished = false;
            _detonated = false;
            _lifeTimer = 0f;
            if (!string.IsNullOrEmpty(attack.Move.LoopSoundCue))
            {
                _loopSoundHandle = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayLoopingSfx(attack.Move.LoopSoundCue);
            }
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            // Gravity well doesn't do impact damage, it detonates at the end.
        }

        public void Update(float dt, BattleContext context, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _lifeTimer += dt;

            if (!_detonated)
            {
                // Pull and trap
                foreach (var target in context.Arena.GetWizardsInCircle(attack.TargetPosition, Radius))
                {
                    if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;
                    if (target.Data.Combat.State == WizardState.Dead) continue;

                    // Prevent moving away or acting
                    target.Data.Combat.State = WizardState.Recovering;
                    target.Data.Combat.StateTimer = 0.1f;

                    Vector2 toCenter = attack.TargetPosition - target.Data.Combat.Position;
                    float distCenter = toCenter.Length();

                    Vector2 dirCenter;
                    if (distCenter > 0.1f)
                    {
                        dirCenter = toCenter / distCenter;
                    }
                    else
                    {
                        // If exactly at center, pick a stable pseudo-random direction to push them out into orbit
                        float hashAngle = (target.GetHashCode() % 1000) / 1000f * MathHelper.TwoPi;
                        dirCenter = new Vector2(MathF.Cos(hashAngle), MathF.Sin(hashAngle));
                        distCenter = 0.1f;
                    }

                    // Tangential vector (perpendicular to center)
                    Vector2 tangent = new Vector2(-dirCenter.Y, dirCenter.X);

                    // Radial speed: Pulls in at PullSpeed, but pushes out if closer than orbit radius
                    float orbitRadius = 12f;
                    float radialSpeed = (distCenter - orbitRadius) * 2f; // Spring-like radial force
                    radialSpeed = Math.Clamp(radialSpeed, -PullSpeed, PullSpeed);

                    // Tangential speed: Increases as they get closer to the center
                    float orbitFactor = Math.Clamp(1f - (distCenter / Radius), 0f, 1f);
                    float tangentialSpeed = orbitFactor * 45f; // Smooth orbiting speed

                    target.Data.Combat.Position += (dirCenter * radialSpeed + tangent * tangentialSpeed) * dt;
                    target.Data.Combat.Position = context.Arena.ClampToArena(target.Data.Combat.Position, 12f);
                }

                if (_lifeTimer >= Lifetime)
                {
                    Detonate(context, attack);
                }
            }
            else
            {
                IsFinished = true;
            }

            if (IsFinished && _loopSoundHandle != Guid.Empty)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopLoopingSfx(_loopSoundHandle);
                _loopSoundHandle = Guid.Empty;
            }
        }

        private void Detonate(BattleContext context, ActiveAttack attack)
        {
            _detonated = true;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=5;atk=0.01;sus=0.1;dec=0.5;freq=100;slide=-50;detune=0.03;delay=0.1;delfb=0.25;vol=0.3");
            ServiceLocator.Get<HapticsManager>().TriggerZoomPulse(1.05f, 0.15f);
            ServiceLocator.Get<HapticsManager>().TriggerShake(10f, 0.3f);

            foreach (var target in context.Arena.GetWizardsInCircle(attack.TargetPosition, Radius))
            {
                if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;
                if (target.Data.Combat.State == WizardState.Dead) continue;

                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, target, context);
                }

                target.Controller.ApplyKnockback(attack.TargetPosition, SlingshotDistance, context.Arena);
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
                    float pulse = 0.075f;
                    spriteBatch.Draw(circle, attack.TargetPosition, null, global.Palette_Shadow * pulse, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                if (global.ShowDebugOverlays)
                {
                    spriteBatch.Draw(circle, attack.TargetPosition, null, Color.Purple * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
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
                spriteBatch.Draw(circle, targetPos, null, Color.Purple * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
