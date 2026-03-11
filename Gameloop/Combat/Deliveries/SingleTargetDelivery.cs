using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Deliveries
{
    public class SingleTargetDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => false;
        private float _visualTimer;

        public void Reset()
        {
            _visualTimer = 0f;
            IsFinished = false;
        }

        public void Setup(IDelivery template)
        {
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pools.SingleTargetDeliveries.Get();
            inst.Setup(this);
            return inst;
        }

        public void Start(ActiveAttack attack)
        {
            IsFinished = false;
            _visualTimer = 0.25f;
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            if (attack.TargetWizard != null && attack.TargetWizard.Data.Stats.CurrentHP > 0)
            {
                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, attack.TargetWizard, context);
                }
            }
        }

        public void Update(float dt, BattleContext context, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _visualTimer -= dt;
            if (_visualTimer <= 0) IsFinished = true;
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            var global = attack.Context.Global;
            var circle = attack.Context.SpriteManager.CircleTextureSprite;
            if (circle != null)
            {
                Vector2 targetPos = attack.TargetWizard != null ? attack.TargetWizard.Data.Combat.Position : attack.TargetPosition;
                float scale = 16f / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);

                if (attack.Move.ShowProjectileIndicator && !attack.HasTriggeredImpact)
                {
                    float pulse = 0.05f + 0.00f * MathF.Sin(attack.ActiveTime * 12f);
                    spriteBatch.Draw(circle, targetPos, null, global.Palette_Rust * pulse, 0f, origin, scale, SpriteEffects.None, 0f);
                }

                if (global.ShowDebugOverlays)
                {
                    spriteBatch.Draw(circle, targetPos, null, Color.Lime * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;
            var circle = context.SpriteManager.CircleTextureSprite;
            if (circle != null)
            {
                float scale = 16f / circle.Width;
                Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, targetPos, null, Color.Green * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}