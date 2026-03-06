using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    public enum WizardState
    {
        Moving,
        Telegraphing,
        Casting,
        Recovering,
        Dead,
        Sparring
    }

    public class ArenaWizard
    {
        public Vector2 Position;
        public Vector2 TargetPosition;
        public float Speed;
        public int PortraitIndex;
        public bool IsPlayer;
        public float HopTimer;

        public int MaxHP;
        public int CurrentHP { get; private set; }

        public float InvincibilityDuration = 0.67f;
        public float InvincibilityTimer { get; private set; }

        public int Strength;
        public int Intelligence;
        public int Tenacity;
        public int Agility;

        public WizardState State = WizardState.Moving;
        public List<MoveDefinition> Moves = new List<MoveDefinition>();

        public bool IsSparPassive;
        public float SparCooldownTimer;
        public MoveDefinition SparMove;

        public bool IsHovered;

        // --- Tunables ---
        public float HealthBarLingerDuration = 2.5f;
        public float HealthBarMinAlpha = 0.75f;
        public float DeadBodyFadeDuration = 16.0f;
        public float DeadBodyMinAlpha = 0.1f;

        public float TimeSinceDeath { get; private set; } = 0f;

        private float _actionTimer;
        private float _stateTimer;
        private MoveDefinition _queuedMove;
        private Vector2 _queuedTargetPos;
        private Vector2 _queuedDirection;
        private ActiveAttack _currentActiveAttack;

        private ArenaWizard _sparOpponent;
        private bool _isSparWinner;
        private int _sparPhase;
        private float _sparTimer;

        private float[] _heartFlashTimers;
        private int[] _heartFlashFrame;

        private float _healthBarVisibilityTimer = 0f;
        private float _healthBarAlpha;

        private static readonly Random _random = new Random();

        public void Initialize(WizardCatData data, Vector2 startPos, bool isPlayer)
        {
            Position = startPos;
            TargetPosition = startPos;
            IsPlayer = isPlayer;
            PortraitIndex = int.TryParse(data.MemberID, out int pid) ? pid : 0;
            HopTimer = (float)(_random.NextDouble() * MathHelper.TwoPi);

            Strength = data.Strength;
            Intelligence = data.Intelligence;
            Tenacity = data.Tenacity;
            Agility = data.Agility;

            MaxHP = Tenacity * 2;

            int maxHearts = (MaxHP + 1) / 2;
            _heartFlashTimers = new float[maxHearts];
            _heartFlashFrame = new int[maxHearts];

            CurrentHP = MaxHP;
            Speed = Agility * 2.5f + 5f;

            _healthBarAlpha = HealthBarMinAlpha;
            _actionTimer = GetRandomActionTime();

            LoadMoves(data);

            SparMove = new MoveDefinition
            {
                Name = data.SparName ?? "Scratch",
                BasePower = data.SparBasePower > 0 ? data.SparBasePower : 10,
                ChargeTime = 0,
                Weight = 0,
                CanTargetSelf = false,
                Delivery = new SelfDelivery()
            };

            if ((data.SparEffectType ?? "Damage") == "Damage")
            {
                SparMove.Effects.Add(new DamageEffect());
            }
            else if (data.SparEffectType == "Heal")
            {
                SparMove.Effects.Add(new HealEffect { HealPercentage = 1.0f });
            }
        }

        private void LoadMoves(WizardCatData data)
        {
            Moves.Clear();
            string[] slots = { data.Move1, data.Move2, data.Move3, data.Move4 };

            foreach (var slot in slots)
            {
                if (!string.IsNullOrWhiteSpace(slot) && GameDataCache.Moves.TryGetValue(slot, out var moveData))
                {
                    Moves.Add(MoveFactory.CreateMove(moveData));
                }
            }
        }

        public Rectangle GetHitbox(SpriteManager spriteManager)
        {
            var bounds = spriteManager.GetPlayerSpriteBounds(PortraitIndex);
            float hopOffset = State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(HopTimer)) * 4f;
            return new Rectangle(
                (int)MathF.Round(Position.X) + bounds.X,
                (int)MathF.Round(Position.Y + hopOffset) + bounds.Y,
                bounds.Width,
                bounds.Height
            );
        }

        public void TakeDamage(int amount)
        {
            if (InvincibilityTimer > 0 || State == WizardState.Dead || CurrentHP <= 0) return;

            int oldHP = CurrentHP;
            CurrentHP = Math.Clamp(CurrentHP - amount, 0, MaxHP);

            if (CurrentHP < oldHP)
            {
                TriggerHeartFlash(oldHP, CurrentHP);
                InvincibilityTimer = InvincibilityDuration;
            }
        }

        public void Heal(int amount)
        {
            if (State == WizardState.Dead) return;
            CurrentHP = Math.Clamp(CurrentHP + amount, 0, MaxHP);
        }

        private void TriggerHeartFlash(int oldHP, int newHP)
        {
            if (_heartFlashTimers == null) return;
            int maxHearts = _heartFlashTimers.Length;
            for (int i = 0; i < maxHearts; i++)
            {
                int oldHeartVal = Math.Clamp(oldHP - i * 2, 0, 2);
                int newHeartVal = Math.Clamp(newHP - i * 2, 0, 2);
                if (oldHeartVal > newHeartVal)
                {
                    _heartFlashTimers[i] = 0.75f;
                    if (oldHeartVal == 2 && newHeartVal == 0) _heartFlashFrame[i] = 4;
                    else if (oldHeartVal == 2 && newHeartVal == 1) _heartFlashFrame[i] = 3;
                    else if (oldHeartVal == 1 && newHeartVal == 0) _heartFlashFrame[i] = 5;
                }
            }
        }

        public void InitiateSpar(ArenaWizard opponent, bool isWinner, ArenaScene arena)
        {
            State = WizardState.Sparring;
            _sparOpponent = opponent;
            _isSparWinner = isWinner;
            _sparPhase = 0;
            _sparTimer = 0.25f;

            TargetPosition = arena.GetRandomArenaPoint();
            _actionTimer = GetRandomActionTime();

            _queuedMove = null;
            _currentActiveAttack = null;
        }

        public float GetDeathAlpha()
        {
            if (State != WizardState.Dead) return 1.0f;
            float progress = Math.Clamp(TimeSinceDeath / DeadBodyFadeDuration, 0f, 1f);
            return MathHelper.Lerp(1.0f, DeadBodyMinAlpha, progress);
        }

        public void Update(float dt, ArenaScene arena)
        {
            if (InvincibilityTimer > 0)
            {
                InvincibilityTimer -= dt;
            }

            if (SparCooldownTimer > 0)
            {
                SparCooldownTimer -= dt;
            }

            bool isFlashing = false;
            if (_heartFlashTimers != null)
            {
                for (int i = 0; i < _heartFlashTimers.Length; i++)
                {
                    if (_heartFlashTimers[i] > 0)
                    {
                        _heartFlashTimers[i] -= dt;
                        isFlashing = true;
                    }
                }
            }

            if (IsHovered || isFlashing)
            {
                _healthBarVisibilityTimer = HealthBarLingerDuration;
                _healthBarAlpha = 1.0f;
            }
            else if (_healthBarVisibilityTimer > 0)
            {
                _healthBarVisibilityTimer -= dt;
                _healthBarAlpha = 1.0f;
            }
            else if (_healthBarAlpha > HealthBarMinAlpha)
            {
                _healthBarAlpha = Math.Max(HealthBarMinAlpha, _healthBarAlpha - dt * 4f);
            }

            if (State == WizardState.Dead)
            {
                TimeSinceDeath += dt;
                return;
            }

            if (CurrentHP <= 0)
            {
                if (InvincibilityTimer <= 0)
                {
                    State = WizardState.Dead;
                    TimeSinceDeath = 0f;
                }
                return;
            }

            switch (State)
            {
                case WizardState.Moving:
                    UpdateMovement(dt, arena);
                    _actionTimer -= dt;
                    if (_actionTimer <= 0)
                    {
                        PrepareAttack(arena);
                    }
                    break;

                case WizardState.Telegraphing:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0)
                    {
                        ExecuteAttack(arena);
                    }
                    break;

                case WizardState.Casting:
                    if (_currentActiveAttack == null || _currentActiveAttack.DeliveryInstance.IsFinished)
                    {
                        State = WizardState.Recovering;
                        _stateTimer = 0.25f;
                    }
                    break;

                case WizardState.Recovering:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0)
                    {
                        State = WizardState.Moving;
                        _actionTimer = GetRandomActionTime();
                        IsSparPassive = true;
                        SparCooldownTimer = 1.0f;
                    }
                    break;

                case WizardState.Sparring:
                    _sparTimer -= dt;
                    if (_sparTimer <= 0)
                    {
                        if (_sparPhase == 0)
                        {
                            if (_isSparWinner && _sparOpponent != null && _sparOpponent.State != WizardState.Dead)
                            {
                                foreach (var effect in SparMove.Effects)
                                {
                                    effect.Apply(this, _sparOpponent, SparMove);
                                }

                                Vector2 midpoint = (Position + _sparOpponent.Position) / 2f;
                                var psm = ServiceLocator.Get<ParticleSystemManager>();
                                var emitter = psm.CreateEmitter(ParticleEffects.CreateHitSparks(0.5f));
                                emitter.Position = midpoint;
                                emitter.EmitBurst(5);
                            }
                            _sparPhase = 1;
                            _sparTimer = 0.5f;
                        }
                        else if (_sparPhase == 1)
                        {
                            State = WizardState.Moving;
                            IsSparPassive = true;
                            SparCooldownTimer = 1.0f;
                            _sparOpponent = null;
                        }
                    }
                    break;
            }
        }

        private void UpdateMovement(float dt, ArenaScene arena)
        {
            float dist = Vector2.Distance(Position, TargetPosition);
            if (dist < 1f)
            {
                TargetPosition = arena.GetRandomArenaPoint();
            }

            Vector2 dir = TargetPosition - Position;
            if (dir.LengthSquared() > 0)
            {
                dir.Normalize();
                Position += dir * Speed * dt;
                HopTimer += dt * Speed * 0.5f;
            }
        }

        private void PrepareAttack(ArenaScene arena)
        {
            if (Moves.Count == 0)
            {
                _actionTimer = GetRandomActionTime();
                return;
            }

            int totalWeight = Moves.Sum(m => m.Weight);
            int roll = _random.Next(totalWeight);
            int currentWeight = 0;

            foreach (var move in Moves)
            {
                currentWeight += move.Weight;
                if (roll < currentWeight)
                {
                    _queuedMove = move;
                    break;
                }
            }

            ArenaWizard target = null;

            if (_queuedMove.Delivery is SelfDelivery)
            {
                target = this;
            }
            else
            {
                var potentialTargets = arena.GetAllWizards().Where(w => w != this && w.CurrentHP > 0).ToList();
                if (potentialTargets.Count == 0)
                {
                    _actionTimer = GetRandomActionTime();
                    return;
                }
                target = potentialTargets[_random.Next(potentialTargets.Count)];
            }

            _queuedTargetPos = target.Position;

            _queuedDirection = _queuedTargetPos - Position;
            if (_queuedDirection.LengthSquared() > 0)
            {
                _queuedDirection.Normalize();
            }
            else
            {
                _queuedDirection = new Vector2(1, 0);
            }

            State = WizardState.Telegraphing;
            _stateTimer = _queuedMove.ChargeTime;
        }

        private void ExecuteAttack(ArenaScene arena)
        {
            var attack = new ActiveAttack
            {
                Caster = this,
                Move = _queuedMove,
                Origin = Position,
                Direction = _queuedDirection,
                TargetPosition = _queuedTargetPos,
                DeliveryInstance = _queuedMove.Delivery.Clone()
            };

            _currentActiveAttack = attack;
            arena.SpawnAttack(attack);

            State = WizardState.Casting;
        }

        public void DrawUI(SpriteBatch spriteBatch, SpriteManager spriteManager)
        {
            if (State == WizardState.Dead || _healthBarAlpha <= 0f) return;

            var sheet = spriteManager.HealthHearts3x3SpriteSheet;
            if (sheet == null) return;

            int maxHearts = (MaxHP + 1) / 2;
            int heartWidth = 3;
            int spacing = 1;
            int totalWidth = maxHearts * heartWidth + (maxHearts - 1) * spacing;

            float hopOffset = State == WizardState.Dead ? 0f : -MathF.Abs(MathF.Sin(HopTimer)) * 4f;

            int wizX = (int)MathF.Round(Position.X);
            int wizY = (int)MathF.Round(Position.Y + hopOffset);

            int startX = wizX - (totalWidth / 2) - 1;
            int startY = wizY - 10;

            Color drawColor = Color.White * _healthBarAlpha;

            for (int i = 0; i < maxHearts; i++)
            {
                int heartVal = Math.Clamp(CurrentHP - i * 2, 0, 2);
                int frameIndex = 2;

                if (heartVal == 2) frameIndex = 0;
                else if (heartVal == 1) frameIndex = 1;

                if (_heartFlashTimers != null && i < _heartFlashTimers.Length && _heartFlashTimers[i] > 0)
                {
                    bool isFlashFrame = (_heartFlashTimers[i] % 0.15f) > 0.075f;
                    if (isFlashFrame)
                    {
                        frameIndex = _heartFlashFrame[i];
                    }
                }

                var sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, 3);
                Vector2 pos = new Vector2(startX + i * (heartWidth + spacing), startY);

                spriteBatch.DrawSnapped(sheet, pos, sourceRect, drawColor);
            }
        }

        public void DrawDebug(SpriteBatch spriteBatch, SpriteManager spriteManager)
        {
            if (ServiceLocator.Get<Global>().ShowDebugOverlays)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                var hitbox = GetHitbox(spriteManager);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Y, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Bottom - 1, hitbox.Width, 1), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.X, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(hitbox.Right - 1, hitbox.Y, 1, hitbox.Height), Color.Lime * 0.5f);
            }

            if (State == WizardState.Telegraphing && _queuedMove != null)
            {
                _queuedMove.Delivery.DrawTelegraph(spriteBatch, Position, _queuedDirection, _queuedTargetPos);
            }
        }

        private float GetRandomActionTime()
        {
            return 2.0f + (float)_random.NextDouble() * 6.0f;
        }
    }
}