using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Deliveries
{
    public class DashMeleeDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Width { get; set; }
        public float Length { get; set; }
        public float Lifetime { get; set; }
        public float DashDistance { get; set; }

        private float _timer;
        private HashSet<ArenaWizard> _hitTargets = new HashSet<ArenaWizard>();
        public IEnumerable<ArenaWizard> HitTargets => _hitTargets;

        private Vector2 _startPos;
        private Vector2 _targetPos;

        public bool IsFinished => _timer >= Lifetime;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _timer = 0f;
            _hitTargets.Clear();
            _startPos = Vector2.Zero;
            _targetPos = Vector2.Zero;
        }

        public void Setup(IDelivery template)
        {
            var t = (DashMeleeDelivery)template;
            Width = t.Width;
            Length = t.Length;
            Lifetime = t.Lifetime;
            DashDistance = t.DashDistance;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pools.DashMeleeDeliveries.Get();
            inst.Setup(this);
            return inst;
        }

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _hitTargets.Clear();
            _startPos = attack.Caster.Data.Combat.Position;
            _targetPos = _startPos + attack.Direction * DashDistance;
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
        }

        public void Update(float dt, BattleContext context, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _timer += dt;

            float progress = Lifetime > 0 ? Math.Clamp(_timer / Lifetime, 0f, 1f) : 1f;
            float easedProgress = Easing.EaseOutCubic(progress);

            attack.Caster.Data.Combat.Position = Vector2.Lerp(_startPos, _targetPos, easedProgress);
            attack.Caster.Data.Combat.Position = context.Arena.ClampToArena(attack.Caster.Data.Combat.Position, 12f);

            foreach (var target in context.Arena.GetWizardsInOBB(attack.Caster.Data.Combat.Position, attack.Direction, Width, Length))
            {
                if (target == attack.Caster && !attack.Move.CanEffectSelf) continue;

                if (_hitTargets.Add(target))
                {
                    foreach (var effect in attack.Move.Effects)
                    {
                        effect.Apply(attack, target, context);
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!attack.Context.Global.ShowDebugOverlays) return;
            var pixel = attack.Context.Pixel;
            float angle = (float)Math.Atan2(attack.Direction.Y, attack.Direction.X);
            spriteBatch.Draw(pixel, attack.Caster.Data.Combat.Position, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;
            float angle = (float)Math.Atan2(direction.Y, direction.X);
            spriteBatch.Draw(context.Pixel, origin, null, Color.Blue * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }
    }
}