using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Deliveries
{
    public class InstantAOEDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Radius { get; set; }
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
            Radius = ((InstantAOEDelivery)template).Radius;
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
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
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