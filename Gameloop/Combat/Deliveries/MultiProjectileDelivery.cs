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
    public sealed class MultiProjectileDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public int ProjectileCount { get; set; }
        public float Duration { get; set; }
        public string ProjectileAnimationID { get; set; }
        public float ProjectileTravelTime { get; set; }
        public float SpreadAngle { get; set; }

        private float _timer;
        private int _projectilesSpawned;
        private static readonly Random _random = new Random();
        private MoveDefinition _childMove;

        public bool IsFinished => _timer >= Duration;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _timer = 0f;
            _projectilesSpawned = 0;
        }

        public void Setup(IDelivery template)
        {
            var t = (MultiProjectileDelivery)template;
            ProjectileCount = t.ProjectileCount;
            Duration = t.Duration;
            ProjectileAnimationID = t.ProjectileAnimationID;
            ProjectileTravelTime = t.ProjectileTravelTime;
            SpreadAngle = t.SpreadAngle;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<MultiProjectileDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<MultiProjectileDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _projectilesSpawned = 0;

            if (_childMove == null)
            {
                _childMove = new MoveDefinition
                {
                    Name = attack.Move.Name + " (Missile)",
                    BasePower = attack.Move.BasePower,
                    ChargeTime = ProjectileTravelTime > 0 ? ProjectileTravelTime : 0.4f,
                    Knockback = attack.Move.Knockback,
                    TargetSelf = false,
                    CanEffectSelf = attack.Move.CanEffectSelf,
                    ExecuteOnChargeStart = true,
                    RequiresFocus = false,
                    ShowProjectileIndicator = false,
                    DeliveryImpactMidFlight = attack.Move.DeliveryImpactMidFlight,
                    CastSoundCue = attack.Move.CastSoundCue,
                    CastSoundPitchVariance = attack.Move.CastSoundPitchVariance,
                    ImpactSoundCue = attack.Move.ImpactSoundCue,
                    TickSoundCue = attack.Move.TickSoundCue,
                    LoopSoundCue = attack.Move.LoopSoundCue,
                    BounceSoundCue = attack.Move.BounceSoundCue,
                    Delivery = SpreadAngle > 0 ? new InstantAOEDelivery { Radius = 12f, CheckProjectileCollision = attack.Move.DeliveryImpactMidFlight } : new SingleTargetDelivery { CheckProjectileCollision = attack.Move.DeliveryImpactMidFlight },
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

            int expected = ProjectileCount;
            if (Duration > 0)
            {
                float interval = Duration / ProjectileCount;
                expected = Math.Min(ProjectileCount, (int)(_timer / interval) + 1);
                if (_timer >= Duration) expected = ProjectileCount;
            }

            while (_projectilesSpawned < expected)
            {
                SpawnProjectile(context, attack);
                _projectilesSpawned++;
            }
        }

        private void SpawnProjectile(BattleContext context, ActiveAttack parentAttack)
        {
            ArenaWizard target = null;
            Vector2 targetPos;
            Vector2 dir;

            if (SpreadAngle > 0)
            {
                float baseAngle = MathF.Atan2(parentAttack.Direction.Y, parentAttack.Direction.X);
                float offset = (float)(_random.NextDouble() * SpreadAngle - SpreadAngle / 2f);
                float finalAngle = baseAngle + offset;
                dir = new Vector2(MathF.Cos(finalAngle), MathF.Sin(finalAngle));

                float dist = 40f + (float)_random.NextDouble() * 160f;
                targetPos = parentAttack.Caster.Data.Combat.Position + dir * dist;
                targetPos = context.Arena.ClampToArena(targetPos, 4f);

                dir = targetPos - parentAttack.Caster.Data.Combat.Position;
                if (dir.LengthSquared() > 0) dir.Normalize();
                else dir = new Vector2(1, 0);
            }
            else
            {
                var validTargets = new List<ArenaWizard>();
                foreach (var w in context.Arena.Wizards)
                {
                    if (w != parentAttack.Caster && w.Data.Stats.CurrentHP > 0 && !w.Data.Combat.IsTeleporting) validTargets.Add(w);
                }

                if (validTargets.Count > 0)
                {
                    target = validTargets[_random.Next(validTargets.Count)];
                }

                targetPos = target != null ? target.Data.Combat.Position : parentAttack.Caster.Data.Combat.Position + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
                targetPos = context.Arena.ClampToArena(targetPos, 4f);

                dir = targetPos - parentAttack.Caster.Data.Combat.Position;
                if (dir.LengthSquared() > 0) dir.Normalize();
                else dir = new Vector2(1, 0);
            }

            var childAttack = Pool<ActiveAttack>.Get();
            childAttack.Reset();
            childAttack.Context = context;
            childAttack.Caster = parentAttack.Caster;
            childAttack.TargetWizard = target;
            childAttack.Move = _childMove;
            childAttack.Origin = parentAttack.Caster.Data.Combat.Position;
            childAttack.Direction = dir;
            childAttack.TargetPosition = targetPos;
            childAttack.DeliveryInstance = _childMove.Delivery.GetInstanceFromPool();
            childAttack.Animation = AnimationFactory.CreateAnimation(ProjectileAnimationID);
            childAttack.HasTriggeredImpact = false;

            context.Arena.SpawnAttack(childAttack);
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack) { }
        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context) { }
    }
}