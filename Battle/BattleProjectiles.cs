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
        public BattleCombatant Target;
        public Action OnImpact;
        public bool IsActive = true;

        public float Timer = 0f;
        public float Duration = 1f;
        public float ImpactTime = 0.5f;
        protected bool _hasImpacted = false;
        protected Vector2 _startPosition;

        public virtual void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos)
        {
            if (!IsActive) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Timer += dt;

            // Trigger impact exactly at ImpactTime
            if (Timer >= ImpactTime && !_hasImpacted)
            {
                OnImpact?.Invoke();
                _hasImpacted = true;
                OnHitTarget(getTargetPos);
            }

            // Deactivate exactly at Duration
            if (Timer >= Duration)
            {
                IsActive = false;

                // Failsafe: If duration was shorter than impact time, force impact now
                if (!_hasImpacted)
                {
                    OnImpact?.Invoke();
                    _hasImpacted = true;
                    OnHitTarget(getTargetPos);
                }
            }
        }

        protected virtual void OnHitTarget(Func<BattleCombatant, Vector2> getTargetPos) { }

        public abstract void Draw(SpriteBatch spriteBatch);
        public virtual void Destroy() { }

        protected Vector2 GetSafeTargetPos(Func<BattleCombatant, Vector2> getTargetPos)
        {
            if (Target == null || Target.IsDefeated) return _startPosition;
            Vector2 pos = getTargetPos(Target);
            if (pos == Vector2.Zero) return _startPosition; // Failsafe
            return pos;
        }
    }

    public class HomingProjectile : BattleProjectile
    {
        private Vector2 _controlPoint;
        private ParticleEmitter _trailEmitter;
        private ParticleSystemManager _psm;
        private Color[] _colors;
        private Random _rng = new Random();

        public HomingProjectile(Vector2 startPos, BattleCombatant target, Action onImpact, Color color, AnimationDefinition animDef)
        {
            _startPosition = startPos;
            Position = startPos;
            Target = target;
            OnImpact = onImpact;

            Duration = animDef.TotalDuration > 0 ? animDef.TotalDuration : 0.6f;
            ImpactTime = animDef.ImpactTime > 0 ? Math.Min(animDef.ImpactTime, Duration) : Duration;

            _psm = ServiceLocator.Get<ParticleSystemManager>();
            var g = ServiceLocator.Get<Global>();
            _colors = new Color[] { g.Palette_Sea, g.Palette_Sky, g.Palette_Leaf };

            _controlPoint = startPos + new Vector2(_rng.Next(-80, 81), _rng.Next(-120, -60));
            _trailEmitter = _psm.CreateEmitter(ParticleEffects.CreateMagicMissileBody());
            _trailEmitter.Position = Position;
        }

        public override void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos)
        {
            base.Update(gameTime, getTargetPos);

            if (!IsActive || _hasImpacted) return;

            if (_trailEmitter != null) _trailEmitter.Settings.StartColor = _colors[_rng.Next(_colors.Length)];

            float t = Math.Clamp(Timer / ImpactTime, 0f, 1f);
            float easedT = Easing.EaseInExpo(t);

            Vector2 targetPos = GetSafeTargetPos(getTargetPos);

            float u = 1 - easedT;
            float tt = easedT * easedT;
            float uu = u * u;
            Position = (uu * _startPosition) + (2 * u * easedT * _controlPoint) + (tt * targetPos);

            if (_trailEmitter != null) _trailEmitter.Position = Position;
        }

        protected override void OnHitTarget(Func<BattleCombatant, Vector2> getTargetPos)
        {
            var burst = _psm.CreateEmitter(ParticleEffects.CreateHitSparks(1.0f));
            burst.Settings.EmissionRate = 0;
            burst.Settings.Duration = 0.1f;
            burst.Position = Position;
            burst.EmitBurst(12);

            if (_trailEmitter != null)
            {
                _trailEmitter.IsActive = false;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) { }

        public override void Destroy()
        {
            if (_trailEmitter != null)
            {
                _trailEmitter.IsActive = false;
                _trailEmitter.Settings.Duration = 0.1f;
                _trailEmitter = null;
            }
        }
    }

    public class FireballProjectile : BattleProjectile
    {
        private Vector2 _controlPoint;
        private ParticleEmitter _trailEmitter;
        private ParticleSystemManager _psm;
        private Color[] _colors;
        private Random _rng = new Random();

        public FireballProjectile(Vector2 startPos, BattleCombatant target, Action onImpact, AnimationDefinition animDef)
        {
            _startPosition = startPos;
            Position = startPos;
            Target = target;
            OnImpact = onImpact;

            Duration = animDef.TotalDuration > 0 ? animDef.TotalDuration : 0.9f;
            ImpactTime = animDef.ImpactTime > 0 ? Math.Min(animDef.ImpactTime, Duration) : Duration;

            _psm = ServiceLocator.Get<ParticleSystemManager>();
            var g = ServiceLocator.Get<Global>();
            _colors = new Color[] { g.Palette_Sun, g.Palette_DarkSun, g.Palette_Fruit, g.Palette_Rust };

            _controlPoint = startPos + new Vector2(_rng.Next(-40, 41), _rng.Next(-180, -100));
            _trailEmitter = _psm.CreateEmitter(ParticleEffects.CreateFireballBody());
            _trailEmitter.Position = Position;
        }

        public override void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos)
        {
            base.Update(gameTime, getTargetPos);

            if (!IsActive || _hasImpacted) return;

            if (_trailEmitter != null) _trailEmitter.Settings.StartColor = _colors[_rng.Next(_colors.Length)];

            float t = Math.Clamp(Timer / ImpactTime, 0f, 1f);
            float smoothT = t;

            Vector2 targetPos = GetSafeTargetPos(getTargetPos);

            float u = 1 - smoothT;
            float tt = smoothT * smoothT;
            float uu = u * u;
            Position = (uu * _startPosition) + (2 * u * smoothT * _controlPoint) + (tt * targetPos);

            if (_trailEmitter != null) _trailEmitter.Position = Position;
        }

        protected override void OnHitTarget(Func<BattleCombatant, Vector2> getTargetPos)
        {
            var burst = _psm.CreateEmitter(ParticleEffects.CreateHitSparks(2.0f));
            burst.Settings.EmissionRate = 0;
            burst.Settings.Duration = 0.1f;
            burst.Position = Position;
            burst.EmitBurst(20);

            if (_trailEmitter != null)
            {
                _trailEmitter.IsActive = false;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) { }

        public override void Destroy()
        {
            if (_trailEmitter != null)
            {
                _trailEmitter.IsActive = false;
                _trailEmitter.Settings.Duration = 0.1f;
                _trailEmitter = null;
            }
        }
    }

    public class FlamethrowerProjectile : BattleProjectile
    {
        private ParticleEmitter _beamEmitter;
        private ParticleSystemManager _psm;
        private Random _rng = new Random();
        private Color[] _colors;
        private float _spawnAccumulator = 0f;

        private float _expansionTime;
        private float _retractionTime;

        public FlamethrowerProjectile(Vector2 startPos, BattleCombatant target, Action onImpact, AnimationDefinition animDef)
        {
            _startPosition = startPos;
            Position = startPos;
            Target = target;
            OnImpact = onImpact;

            Duration = animDef.TotalDuration > 0 ? animDef.TotalDuration : 1.0f;
            ImpactTime = animDef.ImpactTime > 0 ? Math.Min(animDef.ImpactTime, Duration) : Duration * 0.5f;

            // Apply granular timings
            _expansionTime = animDef.ExpansionTime > 0 ? animDef.ExpansionTime : 0.2f;
            _retractionTime = animDef.RetractionTime > 0 ? animDef.RetractionTime : 0.2f;

            _psm = ServiceLocator.Get<ParticleSystemManager>();
            var g = ServiceLocator.Get<Global>();
            _colors = new Color[] { g.Palette_Sun, g.Palette_DarkSun, g.Palette_Fruit, g.Palette_Rust };

            _beamEmitter = _psm.CreateEmitter(ParticleEffects.CreateFlamethrowerNode());
            _beamEmitter.Settings.EmissionRate = 0;
        }

        public override void Update(GameTime gameTime, Func<BattleCombatant, Vector2> getTargetPos)
        {
            base.Update(gameTime, getTargetPos);
            if (!IsActive) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 targetPos = GetSafeTargetPos(getTargetPos);

            // Head extends quickly based on ExpansionTime
            float headProgress = Math.Clamp(Timer / _expansionTime, 0f, 1f);

            // Tail retracts at the end of the duration
            float retractionStart = Math.Max(0, Duration - _retractionTime);
            float tailProgress = 0f;
            if (_retractionTime > 0 && Timer > retractionStart)
            {
                tailProgress = Math.Clamp((Timer - retractionStart) / _retractionTime, 0f, 1f);
            }

            Vector2 headPos = Vector2.Lerp(_startPosition, targetPos, headProgress);
            Vector2 tailPos = Vector2.Lerp(_startPosition, targetPos, tailProgress);

            float segmentLength = Vector2.Distance(tailPos, headPos);
            float particlesPerPixel = 1.5f;

            _spawnAccumulator += segmentLength * particlesPerPixel * dt * 60f;
            int toSpawn = (int)_spawnAccumulator;
            _spawnAccumulator -= toSpawn;

            // Ensure we spawn at least a few particles if the beam is active
            if (segmentLength > 5f && toSpawn < 2) toSpawn = 2;

            Vector2 dir = Vector2.Normalize(headPos - tailPos);
            if (dir.LengthSquared() == 0) dir = new Vector2(1, 0);
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            for (int i = 0; i < toSpawn; i++)
            {
                float t = (float)_rng.NextDouble();
                Vector2 spawnPos = Vector2.Lerp(tailPos, headPos, t);
                float jitter = (float)(_rng.NextDouble() * 8f - 4f);

                _beamEmitter.Position = spawnPos + (perp * jitter);
                _beamEmitter.Settings.StartColor = _colors[_rng.Next(_colors.Length)];
                _beamEmitter.EmitBurst(1);
            }
        }

        protected override void OnHitTarget(Func<BattleCombatant, Vector2> getTargetPos)
        {
            var burst = _psm.CreateEmitter(ParticleEffects.CreateHitSparks(0.5f));
            burst.Settings.EmissionRate = 0;
            burst.Settings.Duration = 0.1f;
            burst.Position = GetSafeTargetPos(getTargetPos);
            burst.EmitBurst(5);
        }

        public override void Draw(SpriteBatch spriteBatch) { }

        public override void Destroy()
        {
            if (_beamEmitter != null)
            {
                _beamEmitter.IsActive = false;
                _beamEmitter.Settings.Duration = 0.1f;
                _beamEmitter = null;
            }
        }
    }
}