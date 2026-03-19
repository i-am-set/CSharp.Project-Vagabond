using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Deliveries
{
    public sealed class ChainDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
        public int BounceCount { get; set; }
        public float BounceRadius { get; set; }
        public float BounceDelay { get; set; }
        public float VisualDuration { get; set; }
        public HashSet<ArenaWizard> HitTargets { get; set; }

        private float _timer;
        private bool _impactTriggered;
        private bool _nextTargetSpawned;
        private List<Vector2> _lightningPoints = new List<Vector2>();
        private static readonly Random _random = new Random();

        public bool IsFinished => _timer >= VisualDuration;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _timer = 0f;
            _impactTriggered = false;
            _nextTargetSpawned = false;
            _lightningPoints.Clear();
            HitTargets = null;
        }

        public void Setup(IDelivery template)
        {
            var t = (ChainDelivery)template;
            BounceCount = t.BounceCount;
            BounceRadius = t.BounceRadius;
            BounceDelay = t.BounceDelay;
            VisualDuration = t.VisualDuration;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<ChainDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<ChainDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _impactTriggered = false;
            _nextTargetSpawned = false;
            _lightningPoints.Clear();
            if (HitTargets == null) HitTargets = new HashSet<ArenaWizard>();
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
            if (!attack.HasTriggeredImpact)
            {
                if (attack.Animation != null && attack.Animation.CurrentProjectilePosition.HasValue)
                {
                    Vector2 projPos = attack.Animation.CurrentProjectilePosition.Value;
                    float hitRadius = 8f;

                    foreach (var target in context.Arena.Wizards)
                    {
                        if (target == attack.Caster) continue;
                        if (target.Data.Combat.State == WizardState.Dead) continue;
                        if (HitTargets != null && HitTargets.Contains(target)) continue;

                        if (CollisionMath.RectangleIntersectsCircle(target.Controller.GetHitbox(context.SpriteManager), projPos, hitRadius))
                        {
                            attack.TargetWizard = target;
                            attack.TargetPosition = projPos;
                            attack.Animation.ForceImpact(projPos);
                            break;
                        }
                    }
                }
                return;
            }

            _timer += dt;

            if (!_impactTriggered)
            {
                _impactTriggered = true;
                Vector2 start = attack.Origin;
                Vector2 end = attack.TargetWizard != null ? attack.TargetWizard.Data.Combat.Position : attack.TargetPosition;
                GenerateLightning(start, end);
            }

            if (!_nextTargetSpawned && _timer >= BounceDelay)
            {
                _nextTargetSpawned = true;
                if (BounceCount > 0 && attack.TargetWizard != null)
                {
                    if (HitTargets == null) HitTargets = new HashSet<ArenaWizard>();
                    HitTargets.Add(attack.TargetWizard);

                    ArenaWizard nextTarget = null;
                    float closestDist = BounceRadius * BounceRadius;
                    foreach (var w in context.Arena.Wizards)
                    {
                        if (w == attack.TargetWizard || w == attack.Caster || w.Data.Combat.State == WizardState.Dead) continue;
                        if (HitTargets.Contains(w)) continue;

                        float dist = Vector2.DistanceSquared(attack.TargetWizard.Data.Combat.Position, w.Data.Combat.Position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            nextTarget = w;
                        }
                    }

                    if (nextTarget != null)
                    {
                        var nextDelivery = (ChainDelivery)this.GetInstanceFromPool();
                        nextDelivery.BounceCount = this.BounceCount - 1;
                        nextDelivery.BounceRadius = this.BounceRadius;
                        nextDelivery.BounceDelay = this.BounceDelay;
                        nextDelivery.VisualDuration = this.VisualDuration;
                        nextDelivery.HitTargets = new HashSet<ArenaWizard>(this.HitTargets);

                        var nextAttack = Pool<ActiveAttack>.Get();
                        nextAttack.Reset();
                        nextAttack.Context = context;
                        nextAttack.Caster = attack.Caster;
                        nextAttack.TargetWizard = nextTarget;
                        nextAttack.Move = attack.Move;
                        nextAttack.Origin = attack.TargetWizard.Data.Combat.Position;
                        nextAttack.TargetPosition = nextTarget.Data.Combat.Position;
                        nextAttack.Direction = Vector2.Normalize(nextAttack.TargetPosition - nextAttack.Origin);
                        nextAttack.DeliveryInstance = nextDelivery;
                        nextAttack.Animation = null;
                        nextAttack.HasTriggeredImpact = false;

                        context.Arena.SpawnAttack(nextAttack);
                    }
                }
            }
        }

        private void GenerateLightning(Vector2 start, Vector2 end)
        {
            _lightningPoints.Clear();
            _lightningPoints.Add(start);

            float dist = Vector2.Distance(start, end);
            int segments = Math.Max(1, (int)(dist / 12f));
            Vector2 dir = end - start;
            if (dir.LengthSquared() > 0) dir.Normalize();
            else dir = new Vector2(1, 0);

            Vector2 normal = new Vector2(-dir.Y, dir.X);

            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector2 basePos = Vector2.Lerp(start, end, t);
                float offset = (float)(_random.NextDouble() * 16f - 8f);
                _lightningPoints.Add(basePos + normal * offset);
            }
            _lightningPoints.Add(end);
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (_impactTriggered && _lightningPoints.Count >= 2)
            {
                float alpha = 1f - Math.Clamp(_timer / VisualDuration, 0f, 1f);
                if (alpha <= 0) return;

                Color outerColor = attack.Context.Global.Palette_Sky * alpha;
                Color innerColor = Color.White * alpha;

                for (int i = 0; i < _lightningPoints.Count - 1; i++)
                {
                    spriteBatch.DrawLineSnapped(_lightningPoints[i], _lightningPoints[i + 1], outerColor, 3f);
                    spriteBatch.DrawLineSnapped(_lightningPoints[i], _lightningPoints[i + 1], innerColor, 1f);
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
                spriteBatch.Draw(circle, targetPos, null, Color.Cyan * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}