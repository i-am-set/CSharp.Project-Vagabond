using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Deliveries
{
    public sealed class TickingBeamDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Width { get; set; }
        public float Length { get; set; }
        public float Lifetime { get; set; }
        public float TickRate { get; set; }

        private float _lifeTimer;
        private float _tickTimer;

        public bool IsFinished => _lifeTimer >= Lifetime;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _lifeTimer = 0f;
            _tickTimer = 0f;
        }

        public void Setup(IDelivery template)
        {
            var t = (TickingBeamDelivery)template;
            Width = t.Width;
            Length = t.Length;
            Lifetime = t.Lifetime;
            TickRate = t.TickRate;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<TickingBeamDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<TickingBeamDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            _lifeTimer = 0f;
            _tickTimer = TickRate;
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            ApplyTick(context, attack);
            _tickTimer = 0f;
        }

        private void ApplyTick(BattleContext context, ActiveAttack attack)
        {
            foreach (var target in context.Arena.GetWizardsInOBB(attack.Origin, attack.Direction, Width, Length))
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

            _lifeTimer += dt;
            _tickTimer += dt;

            if (_tickTimer >= TickRate)
            {
                _tickTimer -= TickRate;
                ApplyTick(context, attack);
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!attack.Context.Global.ShowDebugOverlays) return;

            var pixel = attack.Context.Pixel;
            float angle = (float)Math.Atan2(attack.Direction.Y, attack.Direction.X);

            spriteBatch.Draw(pixel, attack.Origin, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;

            float angle = (float)Math.Atan2(direction.Y, direction.X);
            spriteBatch.Draw(context.Pixel, origin, null, Color.Blue * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }
    }
}