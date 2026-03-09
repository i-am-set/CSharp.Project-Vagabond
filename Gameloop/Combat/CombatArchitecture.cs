using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Animations;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public static class MoveFactory
    {
        public static MoveDefinition CreateMove(MoveData data)
        {
            var move = new MoveDefinition
            {
                Name = data.Name,
                BasePower = data.BasePower,
                ChargeTime = data.ChargeTime,
                Weight = data.Weight,
                Knockback = data.Knockback,
                TargetSelf = data.TargetSelf,
                CanEffectSelf = data.CanEffectSelf,
                TargetClosest = data.TargetClosest,
                ProjectileTravelTime = data.DeliveryProjectileTravelTime,
                AnimationID = data.AnimationID,
                ExecuteOnChargeStart = data.ExecuteOnChargeStart,
                RequiresFocus = data.RequiresFocus,
                ShowProjectileIndicator = data.ShowProjectileIndicator
            };

            if (data.DeliveryType == "InstantAOE")
            {
                move.Delivery = new InstantAOEDelivery { Radius = data.DeliveryRadius };
            }
            else if (data.DeliveryType == "TickingBeam")
            {
                move.Delivery = new TickingBeamDelivery
                {
                    Width = data.DeliveryWidth,
                    Length = data.DeliveryLength,
                    Lifetime = data.DeliveryLifetime,
                    TickRate = data.DeliveryTickRate
                };
            }
            else if (data.DeliveryType == "SingleTarget" || data.DeliveryType == "Self")
            {
                move.Delivery = new SingleTargetDelivery();
            }
            else if (data.DeliveryType == "DashMelee")
            {
                move.Delivery = new DashMeleeDelivery
                {
                    Width = data.DeliveryWidth,
                    Length = data.DeliveryLength,
                    Lifetime = data.DeliveryLifetime,
                    DashDistance = data.DeliveryDashDistance
                };
            }
            else if (data.DeliveryType == "SeekAndDash")
            {
                move.Delivery = new SeekAndDashDelivery
                {
                    SeekRadius = data.DeliverySeekRadius,
                    SeekDuration = data.DeliverySeekDuration,
                    DashDistance = data.DeliveryDashDistance,
                    DashDuration = data.DeliveryDashDuration
                };
            }
            else if (data.DeliveryType == "MeteorStrike")
            {
                move.Delivery = new MeteorStrikeDelivery
                {
                    Radius = data.DeliveryRadius,
                    ProjectileCount = data.DeliveryProjectileCount,
                    ProjectileRadius = data.DeliveryProjectileRadius,
                    Duration = data.DeliveryLifetime,
                    FallTime = data.DeliveryFallTime,
                    ProjectileAnimationID = data.DeliveryProjectileAnimation
                };
            }
            else if (data.DeliveryType == "MultiProjectile")
            {
                move.Delivery = new MultiProjectileDelivery
                {
                    ProjectileCount = data.DeliveryProjectileCount,
                    Duration = data.DeliveryLifetime,
                    ProjectileAnimationID = data.DeliveryProjectileAnimation,
                    ProjectileTravelTime = data.DeliveryProjectileTravelTime
                };
            }

            if (data.EffectType == "Damage")
            {
                move.Effects.Add(new DamageEffect());
            }
            else if (data.EffectType == "Heal")
            {
                move.Effects.Add(new HealEffect { HealPercentage = data.EffectArg });
            }

            return move;
        }
    }

    public class MultiProjectileDelivery : IDelivery
    {
        public int ProjectileCount { get; set; }
        public float Duration { get; set; }
        public string ProjectileAnimationID { get; set; }
        public float ProjectileTravelTime { get; set; }

        private float _timer;
        private int _projectilesSpawned;
        private static readonly Random _random = new Random();

        public bool IsFinished => _timer >= Duration;
        public bool IsAnimationPaused => false;

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _projectilesSpawned = 0;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            // The main delivery doesn't do damage itself, the child projectiles do.
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
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
                SpawnProjectile(arena, attack);
                _projectilesSpawned++;
            }
        }

        private void SpawnProjectile(ArenaScene arena, ActiveAttack parentAttack)
        {
            var validTargets = new List<ArenaWizard>();
            foreach (var w in arena.Wizards)
            {
                if (w != parentAttack.Caster && w.CurrentHP > 0) validTargets.Add(w);
            }

            ArenaWizard target = null;
            if (validTargets.Count > 0)
            {
                target = validTargets[_random.Next(validTargets.Count)];
            }

            Vector2 targetPos = target != null ? target.Position : parentAttack.Caster.Position + new Vector2(_random.Next(-50, 50), _random.Next(-50, 50));
            targetPos = arena.ClampToArena(targetPos, 4f);

            Vector2 dir = targetPos - parentAttack.Caster.Position;
            if (dir.LengthSquared() > 0) dir.Normalize();
            else dir = new Vector2(1, 0);

            var childMove = new MoveDefinition
            {
                Name = parentAttack.Move.Name + " (Missile)",
                BasePower = parentAttack.Move.BasePower,
                ChargeTime = ProjectileTravelTime > 0 ? ProjectileTravelTime : 0.4f, // Travel time
                Weight = 0,
                Knockback = parentAttack.Move.Knockback,
                TargetSelf = false,
                CanEffectSelf = parentAttack.Move.CanEffectSelf,
                ExecuteOnChargeStart = true, // Skip telegraphing, execute immediately
                RequiresFocus = false, // Once fired, it doesn't care if the caster is hit
                ShowProjectileIndicator = false,
                Delivery = new SingleTargetDelivery(),
                Effects = parentAttack.Move.Effects.ToList()
            };

            var childAttack = new ActiveAttack
            {
                Caster = parentAttack.Caster,
                TargetWizard = target,
                Move = childMove,
                Origin = parentAttack.Caster.Position,
                Direction = dir,
                TargetPosition = targetPos,
                DeliveryInstance = childMove.Delivery.Clone(),
                Animation = AnimationFactory.CreateAnimation(ProjectileAnimationID),
                HasTriggeredImpact = false
            };

            arena.SpawnAttack(childAttack);
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack) { }
        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos) { }

        public IDelivery Clone()
        {
            return new MultiProjectileDelivery
            {
                ProjectileCount = this.ProjectileCount,
                Duration = this.Duration,
                ProjectileAnimationID = this.ProjectileAnimationID,
                ProjectileTravelTime = this.ProjectileTravelTime
            };
        }
    }

    public class MeteorStrikeDelivery : IDelivery
    {
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

        public bool IsFinished => _timer >= Duration;
        public bool IsAnimationPaused => false;

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _projectilesSpawned = 0;
            _fixedCenter = attack.TargetWizard != null ? attack.TargetWizard.Position : attack.TargetPosition;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            // The main delivery doesn't do damage itself, the child meteors do.
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            _timer += dt;

            int expected = Duration > 0 ? (int)((_timer / Duration) * ProjectileCount) : ProjectileCount;
            expected = Math.Min(expected, ProjectileCount);

            while (_projectilesSpawned < expected)
            {
                SpawnMeteor(arena, attack);
                _projectilesSpawned++;
            }
        }

        private void SpawnMeteor(ArenaScene arena, ActiveAttack parentAttack)
        {
            // Pick a random point within the main AOE radius
            float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float r = Radius * (float)Math.Sqrt(_random.NextDouble());
            Vector2 targetPos = _fixedCenter + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);

            // Clamp to arena so meteors don't fall outside
            targetPos = arena.ClampToArena(targetPos, 4f);

            // Spawn the meteor high up and slightly to the right so it falls diagonally
            Vector2 origin = targetPos + new Vector2(60, -250);

            var childMove = new MoveDefinition
            {
                Name = parentAttack.Move.Name + " (Meteor)",
                BasePower = parentAttack.Move.BasePower,
                ChargeTime = FallTime, // ParticleAnimationInstance uses ChargeTime for travel duration
                Weight = 0,
                Knockback = parentAttack.Move.Knockback,
                TargetSelf = false,
                CanEffectSelf = parentAttack.Move.CanEffectSelf,
                ExecuteOnChargeStart = true, // Skip telegraphing, execute immediately
                RequiresFocus = false,
                ShowProjectileIndicator = true, // Show the small impact circle
                Delivery = new InstantAOEDelivery { Radius = ProjectileRadius },
                Effects = parentAttack.Move.Effects.ToList()
            };

            var childAttack = new ActiveAttack
            {
                Caster = parentAttack.Caster,
                TargetWizard = null,
                Move = childMove,
                Origin = origin,
                Direction = Vector2.Normalize(targetPos - origin),
                TargetPosition = targetPos,
                DeliveryInstance = childMove.Delivery.Clone(),
                Animation = AnimationFactory.CreateAnimation(ProjectileAnimationID),
                HasTriggeredImpact = false
            };

            arena.SpawnAttack(childAttack);
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            var global = ServiceLocator.Get<Global>();
            if (!global.ShowDebugOverlays) return;

            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, _fixedCenter, null, Color.Red * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
            var global = ServiceLocator.Get<Global>();
            if (!global.ShowDebugOverlays) return;
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, targetPos, null, Color.Orange * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }

        public IDelivery Clone()
        {
            return new MeteorStrikeDelivery
            {
                Radius = this.Radius,
                ProjectileCount = this.ProjectileCount,
                ProjectileRadius = this.ProjectileRadius,
                Duration = this.Duration,
                FallTime = this.FallTime,
                ProjectileAnimationID = this.ProjectileAnimationID
            };
        }
    }

    public class SeekAndDashDelivery : IDelivery
    {
        public float SeekRadius { get; set; }
        public float SeekDuration { get; set; }
        public float DashDistance { get; set; }
        public float DashDuration { get; set; }

        private enum State { Seeking, Dashing, Biting }
        private State _state;
        private float _timer;
        private Vector2 _dashStartPos;
        private Vector2 _dashTargetPos;
        private static readonly Random _random = new Random();

        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => _state == State.Seeking;

        public void Start(ActiveAttack attack)
        {
            _state = State.Seeking;
            _timer = 0f;
            IsFinished = false;
            attack.TargetWizard = null;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            if (attack.TargetWizard != null && attack.TargetWizard.CurrentHP > 0)
            {
                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, attack.TargetWizard, arena);
                }
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (IsFinished) return;

            if (_state == State.Seeking)
            {
                _timer += dt;
                var targets = arena.GetWizardsInCircle(attack.Caster.Position, SeekRadius);
                ArenaWizard selectedTarget = null;

                var validTargets = new List<ArenaWizard>();
                foreach (var t in targets)
                {
                    if (t != attack.Caster && t.CurrentHP > 0) validTargets.Add(t);
                }

                if (validTargets.Count > 0)
                {
                    selectedTarget = validTargets[_random.Next(validTargets.Count)];
                }

                if (selectedTarget != null)
                {
                    attack.TargetWizard = selectedTarget;
                    StartDash(attack, selectedTarget.Position);
                }
                else if (_timer >= SeekDuration)
                {
                    IsFinished = true;
                    attack.IsCanceled = true;
                }
            }
            else if (_state == State.Dashing)
            {
                _timer += dt;
                float progress = DashDuration > 0 ? Math.Clamp(_timer / DashDuration, 0f, 1f) : 1f;
                float eased = Easing.EaseOutCubic(progress);

                if (attack.TargetWizard != null)
                {
                    _dashTargetPos = attack.TargetWizard.Position;
                    attack.TargetPosition = _dashTargetPos; // Keep animation target synced if they move
                    Vector2 dir = _dashTargetPos - _dashStartPos;
                    if (dir.LengthSquared() > 0)
                    {
                        dir.Normalize();
                        attack.Direction = dir;
                    }
                }

                attack.Caster.Position = Vector2.Lerp(_dashStartPos, _dashTargetPos, eased);
                attack.Caster.Position = arena.ClampToArena(attack.Caster.Position, 12f);

                if (progress >= 1f)
                {
                    _state = State.Biting;
                    attack.Origin = attack.Caster.Position;
                }
            }
            else if (_state == State.Biting)
            {
                if (attack.HasTriggeredImpact && (attack.Animation == null || attack.Animation.IsFinished))
                {
                    IsFinished = true;
                }
            }
        }

        private void StartDash(ActiveAttack attack, Vector2 targetPos)
        {
            _state = State.Dashing;
            _timer = 0f;
            _dashStartPos = attack.Caster.Position;

            Vector2 dir = targetPos - _dashStartPos;
            if (dir.LengthSquared() > 0)
            {
                dir.Normalize();
                attack.Direction = dir;
            }
            else
            {
                attack.Direction = new Vector2(1, 0);
            }

            _dashTargetPos = attack.TargetWizard != null ? targetPos : _dashStartPos + attack.Direction * 2f;
            attack.TargetPosition = _dashTargetPos; // Set immediately so animation spawns here
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null && _state == State.Seeking)
            {
                float scale = (SeekRadius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, attack.Caster.Position, null, Color.Yellow * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
        }

        public IDelivery Clone()
        {
            return new SeekAndDashDelivery
            {
                SeekRadius = this.SeekRadius,
                SeekDuration = this.SeekDuration,
                DashDistance = this.DashDistance,
                DashDuration = this.DashDuration
            };
        }
    }

    public class DashMeleeDelivery : IDelivery
    {
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

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _hitTargets.Clear();
            _startPos = attack.Caster.Position;
            _targetPos = _startPos + attack.Direction * DashDistance;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _timer += dt;

            float progress = Lifetime > 0 ? Math.Clamp(_timer / Lifetime, 0f, 1f) : 1f;
            float easedProgress = Easing.EaseOutCubic(progress);

            attack.Caster.Position = Vector2.Lerp(_startPos, _targetPos, easedProgress);
            attack.Caster.Position = arena.ClampToArena(attack.Caster.Position, 12f);

            foreach (var target in arena.GetWizardsInOBB(attack.Caster.Position, attack.Direction, Width, Length))
            {
                if (target == attack.Caster && !attack.Move.CanEffectSelf) continue;

                if (_hitTargets.Add(target))
                {
                    foreach (var effect in attack.Move.Effects)
                    {
                        effect.Apply(attack, target, arena);
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;
            var pixel = ServiceLocator.Get<Texture2D>();
            float angle = (float)Math.Atan2(attack.Direction.Y, attack.Direction.X);
            spriteBatch.Draw(pixel, attack.Caster.Position, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;
            var pixel = ServiceLocator.Get<Texture2D>();
            float angle = (float)Math.Atan2(direction.Y, direction.X);
            spriteBatch.Draw(pixel, origin, null, Color.Blue * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public IDelivery Clone()
        {
            return new DashMeleeDelivery
            {
                Width = this.Width,
                Length = this.Length,
                Lifetime = this.Lifetime,
                DashDistance = this.DashDistance
            };
        }
    }

    public interface IEffect
    {
        void Apply(ActiveAttack attack, ArenaWizard target, ArenaScene arena);
    }

    public class DamageEffect : IEffect
    {
        public void Apply(ActiveAttack attack, ArenaWizard target, ArenaScene arena)
        {
            int damage = Math.Max(1, (int)Math.Floor(attack.Move.BasePower * (attack.Caster.Power + 10) / 200f));
            bool tookDamage = target.TakeDamage(damage);

            if (tookDamage && attack.Move.Knockback > 0)
            {
                Vector2 sourcePos = attack.Caster.Position;
                if (attack.DeliveryInstance is InstantAOEDelivery) sourcePos = attack.TargetPosition;
                else if (attack.DeliveryInstance is TickingBeamDelivery) sourcePos = attack.Origin;

                target.ApplyKnockback(sourcePos, attack.Move.Knockback, arena);
            }
        }
    }

    public class HealEffect : IEffect
    {
        public float HealPercentage { get; set; } = 0.5f;

        public void Apply(ActiveAttack attack, ArenaWizard target, ArenaScene arena)
        {
            int heal = Math.Max(1, (int)(attack.Move.BasePower * HealPercentage));
            target.Heal(heal);
        }
    }

    public interface IDelivery
    {
        bool IsFinished { get; }
        bool IsAnimationPaused { get; }
        void Start(ActiveAttack attack);
        void Update(float dt, ArenaScene arena, ActiveAttack attack);
        void Draw(SpriteBatch spriteBatch, ActiveAttack attack);
        void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos);
        void TriggerImpact(ArenaScene arena, ActiveAttack attack);
        IDelivery Clone();
    }

    public class InstantAOEDelivery : IDelivery
    {
        public float Radius { get; set; }
        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => false;

        private float _visualTimer;

        public void Start(ActiveAttack attack)
        {
            IsFinished = false;
            _visualTimer = 0.25f;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            foreach (var target in arena.GetWizardsInCircle(attack.TargetPosition, Radius))
            {
                if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;

                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, target, arena);
                }
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _visualTimer -= dt;
            if (_visualTimer <= 0) IsFinished = true;
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            var global = ServiceLocator.Get<Global>();
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
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

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;

            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, targetPos, null, Color.Blue * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }

        public IDelivery Clone()
        {
            return new InstantAOEDelivery { Radius = this.Radius };
        }
    }

    public class TickingBeamDelivery : IDelivery
    {
        public float Width { get; set; }
        public float Length { get; set; }
        public float Lifetime { get; set; }
        public float TickRate { get; set; }

        private float _lifeTimer;
        private float _tickTimer;

        public bool IsFinished => _lifeTimer >= Lifetime;
        public bool IsAnimationPaused => false;

        public void Start(ActiveAttack attack)
        {
            _lifeTimer = 0f;
            _tickTimer = TickRate;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            ApplyTick(arena, attack);
            _tickTimer = 0f;
        }

        private void ApplyTick(ArenaScene arena, ActiveAttack attack)
        {
            foreach (var target in arena.GetWizardsInOBB(attack.Origin, attack.Direction, Width, Length))
            {
                if (!attack.Move.CanEffectSelf && target == attack.Caster) continue;

                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, target, arena);
                }
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _lifeTimer += dt;
            _tickTimer += dt;

            if (_tickTimer >= TickRate)
            {
                _tickTimer -= TickRate;
                ApplyTick(arena, attack);
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            float angle = (float)Math.Atan2(attack.Direction.Y, attack.Direction.X);

            spriteBatch.Draw(pixel, attack.Origin, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            float angle = (float)Math.Atan2(direction.Y, direction.X);

            spriteBatch.Draw(pixel, origin, null, Color.Blue * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
        }

        public IDelivery Clone()
        {
            return new TickingBeamDelivery
            {
                Width = this.Width,
                Length = this.Length,
                Lifetime = this.Lifetime,
                TickRate = this.TickRate
            };
        }
    }

    public class SingleTargetDelivery : IDelivery
    {
        public bool IsFinished { get; private set; }
        public bool IsAnimationPaused => false;
        private float _visualTimer;

        public void Start(ActiveAttack attack)
        {
            IsFinished = false;
            _visualTimer = 0.25f;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            if (attack.TargetWizard != null && attack.TargetWizard.CurrentHP > 0)
            {
                foreach (var effect in attack.Move.Effects)
                {
                    effect.Apply(attack, attack.TargetWizard, arena);
                }
            }
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _visualTimer -= dt;
            if (_visualTimer <= 0) IsFinished = true;
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            var global = ServiceLocator.Get<Global>();
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                Vector2 targetPos = attack.TargetWizard != null ? attack.TargetWizard.Position : attack.TargetPosition;
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

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos)
        {
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                float scale = 16f / circle.Width;
                Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, targetPos, null, Color.Green * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
            }
        }

        public IDelivery Clone()
        {
            return new SingleTargetDelivery();
        }
    }

    public class MoveDefinition
    {
        public string Name { get; set; }
        public string AnimationID { get; set; }
        public int BasePower { get; set; }
        public float ChargeTime { get; set; }
        public int Weight { get; set; }
        public float Knockback { get; set; }
        public bool TargetSelf { get; set; }
        public bool CanEffectSelf { get; set; }
        public bool TargetClosest { get; set; }
        public float ProjectileTravelTime { get; set; }
        public bool ExecuteOnChargeStart { get; set; }
        public bool RequiresFocus { get; set; }
        public bool ShowProjectileIndicator { get; set; }
        public IDelivery Delivery { get; set; }
        public List<IEffect> Effects { get; set; } = new List<IEffect>();
    }

    public class ActiveAttack
    {
        public ArenaWizard Caster { get; set; }
        public ArenaWizard TargetWizard { get; set; }
        public MoveDefinition Move { get; set; }
        public Vector2 Origin { get; set; }
        public Vector2 Direction { get; set; }
        public Vector2 TargetPosition { get; set; }
        public IDelivery DeliveryInstance { get; set; }
        public ProjectVagabond.Animations.IAnimationInstance Animation { get; set; }
        public bool HasTriggeredImpact { get; set; }
        public bool IsCanceled { get; set; }
        public bool HasStartedAnimation { get; set; }
        public float ActiveTime { get; private set; }

        public void Update(float dt, ArenaScene arena)
        {
            ActiveTime += dt;

            if (IsCanceled)
            {
                Animation?.Cancel();
                return;
            }

            DeliveryInstance.Update(dt, arena, this);

            if (IsCanceled)
            {
                Animation?.Cancel();
                return;
            }

            if (!DeliveryInstance.IsAnimationPaused)
            {
                if (!HasStartedAnimation && Animation != null)
                {
                    Animation.Start(this, arena);
                    HasStartedAnimation = true;
                }

                if (Animation != null)
                {
                    Animation.Update(dt, arena, this);
                    if (Animation.HasTriggeredImpact && !HasTriggeredImpact)
                    {
                        HasTriggeredImpact = true;
                        DeliveryInstance.TriggerImpact(arena, this);
                    }
                }
                else if (!HasTriggeredImpact)
                {
                    HasTriggeredImpact = true;
                    DeliveryInstance.TriggerImpact(arena, this);
                }
            }
        }

        public bool IsFinished => IsCanceled || ((Animation == null || Animation.IsFinished) && DeliveryInstance.IsFinished);
    }
}