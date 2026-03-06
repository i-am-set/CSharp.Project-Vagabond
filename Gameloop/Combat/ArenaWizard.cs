using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
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
        Dead
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
        public int CurrentHP;
        public int Strength;
        public int Intelligence;
        public int Tenacity;
        public int Agility;

        public WizardState State = WizardState.Moving;
        public List<MoveDefinition> Moves = new List<MoveDefinition>();

        private float _actionTimer;
        private float _stateTimer;
        private MoveDefinition _queuedMove;
        private Vector2 _queuedTargetPos;
        private Vector2 _queuedDirection;
        private ActiveAttack _currentActiveAttack;

        private readonly Random _random = new Random();

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
            CurrentHP = MaxHP;
            Speed = Agility * 5f + 10f;

            _actionTimer = GetRandomActionTime();

            LoadMoves(data);
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

        public void Update(float dt, ArenaScene arena)
        {
            if (CurrentHP <= 0)
            {
                State = WizardState.Dead;
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
                HopTimer += dt * Speed * 0.25f;
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

            var potentialTargets = arena.GetAllWizards().Where(w => w != this && w.CurrentHP > 0).ToList();
            if (potentialTargets.Count == 0)
            {
                _actionTimer = GetRandomActionTime();
                return;
            }

            var target = potentialTargets[_random.Next(potentialTargets.Count)];
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

        public void DrawDebug(SpriteBatch spriteBatch)
        {
            if (State == WizardState.Telegraphing && _queuedMove != null)
            {
                _queuedMove.Delivery.DrawTelegraph(spriteBatch, Position, _queuedDirection, _queuedTargetPos);
            }
        }

        private float GetRandomActionTime()
        {
            return 3.0f + (float)_random.NextDouble() * 4.0f;
        }
    }
}