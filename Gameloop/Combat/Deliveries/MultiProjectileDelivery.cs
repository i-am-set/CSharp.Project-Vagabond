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
    public class MultiProjectileDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public int ProjectileCount { get; set; }
        public float Duration { get; set; }
        public string ProjectileAnimationID { get; set; }
        public float ProjectileTravelTime { get; set; }

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
                    Weight = 0,
                    Knockback = attack.Move.Knockback,
                    TargetSelf = false,
                    CanEffectSelf = attack.Move.CanEffectSelf,
                    ExecuteOnChargeStart = true,
                    RequiresFocus = false,
                    ShowProjectileIndicator = false,
                    Delivery = new SingleTargetDelivery(),
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
            var validTargets = new List<ArenaWizard>();
            foreach (var w in context.Arena.Wizards)
            {
                if (w != parentAttack.Caster && w.Data.Stats.CurrentHP > 0 && !w.Data.Combat.IsTeleporting) validTargets.Add(w);
            }

            ArenaWizard target = null;
            if (validTargets.Count > 0)
            {
                target = validTargets[_random.Next(validTargets.Count)];
            }

            Vector2 targetPos = target != null ? target.Data.Combat.Position : parentAttack.Caster.Data.Combat.Position + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
            targetPos = context.Arena.ClampToArena(targetPos, 4f);

            Vector2 dir = targetPos - parentAttack.Caster.Data.Combat.Position;
            if (dir.LengthSquared() > 0) dir.Normalize();
            else dir = new Vector2(1, 0);

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