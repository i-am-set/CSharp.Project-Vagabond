using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Deliveries
{
    public sealed class MeteorStrikeDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public float Radius { get; set; }
        public int ProjectileCount { get; set; }
        public float ProjectileRadius { get; set; }
        public float Duration { get; set; }
        public float FallTime { get; set; }
        public string ProjectileAnimationID { get; set; }

        private float _timer;
        private int _projectilesSpawned;
        private Vector2 _fixedCenter;
        private static readonly Random _random = new Random();
        private MoveDefinition _childMove;

        public bool IsFinished => _timer >= Duration;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _timer = 0f;
            _projectilesSpawned = 0;
            _fixedCenter = Vector2.Zero;
        }

        public void Setup(IDelivery template)
        {
            var t = (MeteorStrikeDelivery)template;
            Radius = t.Radius;
            ProjectileCount = t.ProjectileCount;
            ProjectileRadius = t.ProjectileRadius;
            Duration = t.Duration;
            FallTime = t.FallTime;
            ProjectileAnimationID = t.ProjectileAnimationID;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<MeteorStrikeDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<MeteorStrikeDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _projectilesSpawned = 0;
            _fixedCenter = attack.TargetWizard != null ? attack.TargetWizard.Data.Combat.Position : attack.TargetPosition;

            if (_childMove == null)
            {
                _childMove = new MoveDefinition
                {
                    Name = attack.Move.Name + " (Meteor)",
                    BasePower = attack.Move.BasePower,
                    ChargeTime = FallTime,
                    Knockback = attack.Move.Knockback,
                    TargetSelf = false,
                    CanEffectSelf = attack.Move.CanEffectSelf,
                    ExecuteOnChargeStart = true,
                    RequiresFocus = false,
                    ShowProjectileIndicator = true,
                    CastSoundCue = attack.Move.CastSoundCue,
                    CastSoundPitchVariance = attack.Move.CastSoundPitchVariance,
                    ImpactSoundCue = attack.Move.ImpactSoundCue,
                    TickSoundCue = attack.Move.TickSoundCue,
                    LoopSoundCue = attack.Move.LoopSoundCue,
                    BounceSoundCue = attack.Move.BounceSoundCue,
                    Delivery = new InstantAOEDelivery { Radius = ProjectileRadius, CheckProjectileCollision = false },
                    Effects = attack.Move.Effects.ToList()
                };
            }
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
        }

        public void Update(float dt, BattleContext context, ActiveAttack attack)
        {
            _timer += dt;

            int expected = Duration > 0 ? (int)((_timer / Duration) * ProjectileCount) : ProjectileCount;
            expected = Math.Min(expected, ProjectileCount);

            while (_projectilesSpawned < expected)
            {
                SpawnMeteor(context, attack);
                _projectilesSpawned++;
            }
        }

        private void SpawnMeteor(BattleContext context, ActiveAttack parentAttack)
        {
            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float r = Radius * (float)Math.Sqrt(_random.NextDouble());
            Vector2 targetPos = _fixedCenter + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);

            targetPos = context.Arena.ClampToArena(targetPos, 4f);

            Vector2 origin = targetPos + new Vector2(60, -250);

            var childAttack = Pool<ActiveAttack>.Get();
            childAttack.Reset();
            childAttack.Context = context;
            childAttack.Caster = parentAttack.Caster;
            childAttack.TargetWizard = null;
            childAttack.Move = _childMove;
            childAttack.Origin = origin;
            childAttack.Direction = Vector2.Normalize(targetPos - origin);
            childAttack.TargetPosition = targetPos;
            childAttack.DeliveryInstance = _childMove.Delivery.GetInstanceFromPool();
            childAttack.Animation = AnimationFactory.CreateAnimation(ProjectileAnimationID);
            childAttack.HasTriggeredImpact = false;

            context.Arena.SpawnAttack(childAttack);
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!attack.Context.Global.ShowDebugOverlays) return;

            var circle = attack.Context.SpriteManager.CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, _fixedCenter, null, Color.Red * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
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
                spriteBatch.Draw(circle, targetPos, null, Color.Orange * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}