using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;

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
                AnimationID = data.AnimationID,
                ExecuteOnChargeStart = data.ExecuteOnChargeStart,
                RequiresFocus = data.RequiresFocus
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

        public void Start(ActiveAttack attack)
        {
            _timer = 0f;
            _hitTargets.Clear();
            _startPos = attack.Caster.Position;
            _targetPos = _startPos + attack.Direction * DashDistance;
        }

        public void TriggerImpact(ArenaScene arena, ActiveAttack attack)
        {
            // Handled continuously in Update
        }

        public void Update(float dt, ArenaScene arena, ActiveAttack attack)
        {
            if (!attack.HasTriggeredImpact) return;

            _timer += dt;

            float progress = Lifetime > 0 ? Math.Clamp(_timer / Lifetime, 0f, 1f) : 1f;
            float easedProgress = Easing.EaseOutCubic(progress);

            // Move the caster along the eased curve
            attack.Caster.Position = Vector2.Lerp(_startPos, _targetPos, easedProgress);

            // Check hitbox (OBB in front of caster)
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
            int damage = Math.Max(1, (int)Math.Floor(attack.Move.BasePower * (attack.Caster.Strength + 10) / 200f));
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
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;

            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null)
            {
                float scale = (Radius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, attack.TargetPosition, null, Color.Red * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
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
            if (!ServiceLocator.Get<Global>().ShowDebugOverlays) return;
            var circle = ServiceLocator.Get<SpriteManager>().CircleTextureSprite;
            if (circle != null && attack.TargetWizard != null)
            {
                float scale = 16f / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, attack.TargetWizard.Position, null, Color.Lime * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
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
        public bool ExecuteOnChargeStart { get; set; }
        public bool RequiresFocus { get; set; }
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
    }
}