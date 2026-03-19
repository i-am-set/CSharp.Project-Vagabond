using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Deliveries
{
    public sealed class LingeringAOEDelivery : IDelivery
    {
        public enum AOEShape { Circle, Line }
        public bool IsPooled { get; set; }
        public AOEShape Shape { get; set; }
        public float Radius { get; set; }
        public float Width { get; set; }
        public float Length { get; set; }
        public float Lifetime { get; set; }
        public float TickRate { get; set; }
        public string VisualStyle { get; set; }

        private float _lifeTimer;
        private float _tickTimer;
        private Vector2 _origin;
        private Vector2 _direction;

        private struct BrambleLine { public Vector2 Start; public Vector2 End; public float GrowDelay; }
        private List<BrambleLine> _brambles = new List<BrambleLine>();

        private ParticleEmitter _emitter;

        private static readonly Random _random = new Random();

        public bool IsFinished => _lifeTimer >= Lifetime;
        public bool IsAnimationPaused => false;

        public void Reset()
        {
            _lifeTimer = 0f;
            _tickTimer = 0f;
            _brambles.Clear();
            if (_emitter != null)
            {
                _emitter.IsActive = false;
                _emitter = null;
            }
        }

        public void Setup(IDelivery template)
        {
            var t = (LingeringAOEDelivery)template;
            Shape = t.Shape;
            Radius = t.Radius;
            Width = t.Width;
            Length = t.Length;
            Lifetime = t.Lifetime;
            TickRate = t.TickRate;
            VisualStyle = t.VisualStyle;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pool<LingeringAOEDelivery>.Get();
            inst.Setup(this);
            return inst;
        }

        public void ReturnToPool()
        {
            Pool<LingeringAOEDelivery>.Return(this);
        }

        public void Start(ActiveAttack attack)
        {
            _lifeTimer = 0f;
            _tickTimer = TickRate;
            _origin = attack.TargetPosition;

            if (Shape == AOEShape.Line)
            {
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                _direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
            else
            {
                _direction = Vector2.UnitX;
            }

            GenerateVisuals(attack);
        }

        private void GenerateVisuals(ActiveAttack attack)
        {
            _brambles.Clear();

            if (VisualStyle == "Brambles")
            {
                int count = (int)(Radius * Radius * 0.15f);
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    float r = (float)Math.Sqrt(_random.NextDouble()) * Radius;
                    Vector2 start = _origin + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                    float lineAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    float length = 4f + (float)_random.NextDouble() * 10f;
                    Vector2 end = start + new Vector2(MathF.Cos(lineAngle) * length, MathF.Sin(lineAngle) * length);
                    _brambles.Add(new BrambleLine { Start = start, End = end, GrowDelay = (float)_random.NextDouble() * 0.5f });
                }
            }
            else if (VisualStyle == "FireWall")
            {
                var settings = ParticleEmitterSettings.CreateDefault();
                settings.Shape = EmitterShape.Rectangle;
                settings.EmitFrom = EmissionSource.Volume;
                settings.EmitterSize = new Vector2(Length, Width);
                settings.EmitterRotation = MathF.Atan2(_direction.Y, _direction.X);
                settings.EmissionRate = 1000f;
                settings.MaxParticles = 1000;
                settings.Duration = Lifetime;
                settings.VelocityPattern = EmissionPattern.Cartesian;
                settings.InitialVelocityX = new FloatRange(-5f, 5f);
                settings.InitialVelocityY = new FloatRange(-25f, -10f);
                settings.Lifetime = new FloatRange(0.3f, 0.6f);
                settings.InitialSize = new FloatRange(2f, 4f);
                settings.EndSize = new FloatRange(0f, 0f);
                settings.InterpolateSize = true;
                settings.StartColor = attack.Context.Global.Palette_Fruit;
                settings.EndColor = attack.Context.Global.Palette_Rust;
                settings.StartAlpha = 1.0f;
                settings.EndAlpha = 0.0f;
                settings.BlendMode = BlendState.Additive;
                settings.Texture = attack.Context.SpriteManager.SoftParticleSprite;

                _emitter = attack.Context.ParticleSystemManager.CreateEmitter(settings);
                _emitter.Position = _origin + _direction * (Length / 2f);
            }
        }

        public void TriggerImpact(BattleContext context, ActiveAttack attack)
        {
            ApplyTick(context, attack);
            _tickTimer = 0f;
        }

        private void ApplyTick(BattleContext context, ActiveAttack attack)
        {
            IEnumerable<ArenaWizard> targets;
            if (Shape == AOEShape.Circle)
            {
                targets = context.Arena.GetWizardsInCircle(_origin, Radius);
            }
            else
            {
                targets = context.Arena.GetWizardsInOBB(_origin, _direction, Width, Length);
            }

            foreach (var target in targets)
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

            if (IsFinished && _emitter != null)
            {
                _emitter.IsActive = false;
                _emitter = null;
            }
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            float alpha = 1f;
            if (Lifetime - _lifeTimer < 0.5f) alpha = (Lifetime - _lifeTimer) / 0.5f;

            if (VisualStyle == "Brambles")
            {
                foreach (var b in _brambles)
                {
                    float growProgress = Math.Clamp((_lifeTimer - b.GrowDelay) / 0.3f, 0f, 1f);
                    if (growProgress > 0)
                    {
                        Vector2 currentEnd = Vector2.Lerp(b.Start, b.End, Easing.EaseOutCubic(growProgress));

                        Vector2 p1 = Vector2.Lerp(b.Start, currentEnd, 0.33f);
                        Vector2 p2 = Vector2.Lerp(b.Start, currentEnd, 0.66f);

                        spriteBatch.DrawLineSnapped(b.Start, p1, attack.Context.Global.Palette_Black * alpha, 3f);
                        spriteBatch.DrawLineSnapped(p1, p2, attack.Context.Global.Palette_Black * alpha, 2f);
                        spriteBatch.DrawLineSnapped(p2, currentEnd, attack.Context.Global.Palette_Black * alpha, 1f);

                        spriteBatch.DrawLineSnapped(b.Start, p1, attack.Context.Global.Palette_DarkestPale * alpha, 1f);
                    }
                }
            }

            if (attack.Context.Global.ShowDebugOverlays)
            {
                var pixel = attack.Context.Pixel;
                if (Shape == AOEShape.Circle)
                {
                    var circle = attack.Context.SpriteManager.CircleTextureSprite;
                    if (circle != null)
                    {
                        float scale = (Radius * 2f) / circle.Width;
                        Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                        spriteBatch.Draw(circle, _origin, null, Color.Red * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    float angle = (float)Math.Atan2(_direction.Y, _direction.X);
                    spriteBatch.Draw(pixel, _origin, null, Color.Red * 0.3f, angle, new Vector2(0, 0.5f), new Vector2(Length, Width), SpriteEffects.None, 0f);
                }
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
            if (!context.Global.ShowDebugOverlays) return;

            if (Shape == AOEShape.Circle)
            {
                var circle = context.SpriteManager.CircleTextureSprite;
                if (circle != null)
                {
                    float scale = (Radius * 2f) / circle.Width;
                    Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                    spriteBatch.Draw(circle, targetPos, null, Color.Blue * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
                }
            }
            else
            {
                var circle = context.SpriteManager.CircleTextureSprite;
                if (circle != null)
                {
                    float scale = (Length * 2f) / circle.Width;
                    Vector2 texOrigin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                    spriteBatch.Draw(circle, targetPos, null, Color.Blue * 0.3f, 0f, texOrigin, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}