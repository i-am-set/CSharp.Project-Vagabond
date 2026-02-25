using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Particles;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    public abstract class BattleProjectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public BattleCombatant Target;
        public Action OnImpact;
        public bool IsActive = true;

        public abstract void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos);
        public abstract void Draw(SpriteBatch spriteBatch);
        public virtual void Destroy() { }
    }

    public class HomingProjectile : BattleProjectile
    {
        private float _timer;
        private readonly float _duration;
        private readonly Vector2 _startPosition;
        private Vector2 _controlPoint;
        private float _rotation;

        private ParticleEmitter _trailEmitter;
        private readonly ParticleSystemManager _psm;
        private readonly Random _rng = new Random();

        // Magic Missile Sparkle Palette
        private readonly Color[] _sparkleColors;

        public HomingProjectile(Vector2 startPos, BattleCombatant target, Action onImpact, Color color)
        {
            Position = startPos;
            _startPosition = startPos;
            Target = target;
            OnImpact = onImpact;
            _psm = ServiceLocator.Get<ParticleSystemManager>();
            var global = ServiceLocator.Get<Global>();

            // Cache the palette for efficiency
            _sparkleColors = new Color[] { global.Palette_Sea, global.Palette_Sky, global.Palette_Leaf };

            // Duration: 0.5s to 0.7s for a snappy, magical feel
            _duration = 0.5f + (float)_rng.NextDouble() * 0.2f;

            // Control Point: Randomly offset "up and out" to create an arc
            float cpX = _rng.Next(-80, 81);
            float cpY = _rng.Next(-120, -60);
            _controlPoint = startPos + new Vector2(cpX, cpY);

            // Initialize Emitter with the new "Volume" settings
            var settings = ParticleEffects.CreateMagicMissileBody();
            _trailEmitter = _psm.CreateEmitter(settings);
            _trailEmitter.Position = Position;
        }

        public override void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos)
        {
            if (!IsActive) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _timer += dt;

            // --- Sparkle Logic ---
            // Randomly swap the emitter's start color every frame to create a glittering effect
            if (_trailEmitter != null)
            {
                _trailEmitter.Settings.StartColor = _sparkleColors[_rng.Next(0, _sparkleColors.Length)];
            }

            float progress = _timer / _duration;

            if (progress >= 1.0f)
            {
                if (Target != null && !Target.IsDefeated)
                {
                    Position = getTargetPos(Target);
                }

                OnImpact?.Invoke();
                SpawnImpact();
                IsActive = false;
                return;
            }

            // Easing: EaseInExpo for "Start slow, accelerate exponentially"
            float t = Math.Clamp(progress, 0f, 1f);
            float easedT = (t == 0) ? 0 : MathF.Pow(2, 10 * (t - 1));

            Vector2 targetPos = (Target != null) ? getTargetPos(Target) : _startPosition;

            // Quadratic Bezier Calculation
            float u = 1 - easedT;
            float tt = easedT * easedT;
            float uu = u * u;

            Vector2 nextPos = (uu * _startPosition) + (2 * u * easedT * _controlPoint) + (tt * targetPos);

            Vector2 delta = nextPos - Position;
            if (delta.LengthSquared() > 0.001f)
            {
                _rotation = MathF.Atan2(delta.Y, delta.X);
            }

            Position = nextPos;

            if (_trailEmitter != null)
            {
                _trailEmitter.Position = Position;
            }
        }

        private void SpawnImpact()
        {
            var settings = new ParticleEmitterSettings
            {
                Texture = ServiceLocator.Get<SpriteManager>().SoftParticleSprite,
                StartColor = Color.White,
                EndColor = _sparkleColors[1], // Use Sky for impact fade
                Lifetime = new FloatRange(0.2f, 0.4f),
                InitialSize = new FloatRange(10f, 20f),
                EndSize = new FloatRange(0f),
                EmissionRate = 0f,
                BurstCount = 12,
                VelocityPattern = EmissionPattern.Radial,
                InitialVelocityX = new FloatRange(150f, 250f),
                BlendMode = BlendState.Additive
            };
            var burst = _psm.CreateEmitter(settings);
            burst.Position = Position;
            burst.Settings.Duration = 0.1f;
            burst.EmitBurst(12);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Intentionally empty. 
            // The projectile's visual presence is now entirely defined by the particle volume.
        }

        public override void Destroy()
        {
            if (_trailEmitter != null)
            {
                _trailEmitter.IsActive = false;
                _trailEmitter.Settings.Duration = 0.1f; // Let existing particles fade out naturally
                _trailEmitter = null;
            }
        }
    }
}