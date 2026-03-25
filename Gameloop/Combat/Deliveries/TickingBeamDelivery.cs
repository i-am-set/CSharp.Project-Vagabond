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

        public float CurrentLength { get; private set; }

        private float _lifeTimer;
        private float _tickTimer;
        private Guid _loopSoundHandle = Guid.Empty;

        public bool IsFinished => _lifeTimer >= Lifetime;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _lifeTimer = 0f;
            _tickTimer = 0f;
            CurrentLength = 0f;
            if (_loopSoundHandle != Guid.Empty)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopLoopingSfx(_loopSoundHandle);
                _loopSoundHandle = Guid.Empty;
            }
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

            if (Length <= 0f)
            {
                float distToEdge = CollisionMath.RaycastAABB(attack.Origin, attack.Direction, attack.Context.Arena.ArenaBounds);
                CurrentLength = distToEdge > 0 ? distToEdge : 1000f;
            }
            else
            {
                CurrentLength = Length;
            }

            if (!string.IsNullOrEmpty(attack.Move.LoopSoundCue))
            {
                _loopSoundHandle = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayLoopingSfx(attack.Move.LoopSoundCue);
            }
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            if (!string.IsNullOrEmpty(attack.Move.ImpactSoundCue))
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(attack.Move.ImpactSoundCue);
            }
            ApplyTick(context, attack);
            _tickTimer = 0f;
        }

        private void ApplyTick(BattleContext context, ActiveAttack attack)
        {
            foreach (var target in context.Arena.GetWizardsInOBB(attack.Origin, attack.Direction, Width, CurrentLength))
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
                if (!string.IsNullOrEmpty(attack.Move.TickSoundCue))
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(attack.Move.TickSoundCue);
                }
                ApplyTick(context, attack);
            }

            if (IsFinished && _loopSoundHandle != Guid.Empty)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopLoopingSfx(_loopSoundHandle);
                _loopSoundHandle = Guid.Empty;
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!attack.Context.Global.ShowDebugOverlays) return;

            var pixel = attack.Context.Pixel;
            float angle = (float)Math.Atan2(attack.Direction.Y, attack.Direction.X);

            spriteBatch.Draw(pixel, attack.Origin, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(CurrentLength, Width), SpriteEffects.None, 0f);
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;

            float angle = (float)Math.Atan2(direction.Y, direction.X);

            float drawLength = Length;
            if (drawLength <= 0f)
            {
                float distToEdge = CollisionMath.RaycastAABB(origin, direction, context.Arena.ArenaBounds);
                drawLength = distToEdge > 0 ? distToEdge : 1000f;
            }

            spriteBatch.Draw(context.Pixel, origin, null, Color.Blue * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(drawLength, Width), SpriteEffects.None, 0f);
        }
    }
}