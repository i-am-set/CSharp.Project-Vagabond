using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Deliveries
{
    public class SeekAndDashDelivery : IDelivery
    {
        public bool IsPooled { get; set; }
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

        public void Reset()
        {
            _state = State.Seeking;
            _timer = 0f;
            _dashStartPos = Vector2.Zero;
            _dashTargetPos = Vector2.Zero;
            IsFinished = false;
        }

        public void Setup(IDelivery template)
        {
            var t = (SeekAndDashDelivery)template;
            SeekRadius = t.SeekRadius;
            SeekDuration = t.SeekDuration;
            DashDistance = t.DashDistance;
            DashDuration = t.DashDuration;
        }

        public IDelivery GetInstanceFromPool()
        {
            var inst = Pools.SeekAndDashDeliveries.Get();
            inst.Setup(this);
            return inst;
        }

        public void Start(ActiveAttack attack)
        {
            _state = State.Seeking;
            _timer = 0f;
            IsFinished = false;
            attack.TargetWizard = null;
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
            if (IsFinished) return;

            if (_state == State.Seeking)
            {
                _timer += dt;
                var targets = context.Arena.GetWizardsInCircle(attack.Caster.Data.Combat.Position, SeekRadius);
                ArenaWizard selectedTarget = null;

                var validTargets = new List<ArenaWizard>();
                foreach (var t in targets)
                {
                    if (t != attack.Caster && t.Data.Stats.CurrentHP > 0 && !t.Data.Combat.IsTeleporting) validTargets.Add(t);
                }

                if (validTargets.Count > 0)
                {
                    selectedTarget = validTargets[_random.Next(validTargets.Count)];
                }

                if (selectedTarget != null)
                {
                    attack.TargetWizard = selectedTarget;
                    StartDash(attack, selectedTarget.Data.Combat.Position);
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
                    _dashTargetPos = attack.TargetWizard.Data.Combat.Position;
                    attack.TargetPosition = _dashTargetPos;
                    Vector2 dir = _dashTargetPos - _dashStartPos;
                    if (dir.LengthSquared() > 0)
                    {
                        dir.Normalize();
                        attack.Direction = dir;
                    }
                }

                attack.Caster.Data.Combat.Position = Vector2.Lerp(_dashStartPos, _dashTargetPos, eased);
                attack.Caster.Data.Combat.Position = context.Arena.ClampToArena(attack.Caster.Data.Combat.Position, 12f);

                if (progress >= 1f)
                {
                    _state = State.Biting;
                    attack.Origin = attack.Caster.Data.Combat.Position;
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
            _dashStartPos = attack.Caster.Data.Combat.Position;

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
            attack.TargetPosition = _dashTargetPos;
        }

        public void Draw(SpriteBatch spriteBatch, ActiveAttack attack)
        {
            if (!attack.Context.Global.ShowDebugOverlays) return;
            var circle = attack.Context.SpriteManager.CircleTextureSprite;
            if (circle != null && _state == State.Seeking)
            {
                float scale = (SeekRadius * 2f) / circle.Width;
                Vector2 origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
                spriteBatch.Draw(circle, attack.Caster.Data.Combat.Position, null, Color.Yellow * 0.3f, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 origin, Vector2 direction, Vector2 targetPos, BattleContext context)
        {
        }
    }
}