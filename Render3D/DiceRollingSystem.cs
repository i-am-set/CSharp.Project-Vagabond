using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using XnaQuaternion = Microsoft.Xna.Framework.Quaternion;
using BepuVector3 = System.Numerics.Vector3;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// Coordinates the physics, rendering, and animation controllers to manage the
    /// entire 3D dice rolling simulation.
    /// </summary>
    public class DiceRollingSystem
    {
        private enum RollState
        {
            Idle,
            Rolling,
            Settling,
            Animating,
            Complete
        }

        // Core Components
        private readonly Global _global;
        private readonly Random _random = new Random();
        private readonly DicePhysicsController _physicsController;
        private readonly DiceSceneRenderer _renderer;
        private readonly DiceAnimationController _animationController;

        // Object Pooling & State
        private const int InitialPoolSize = 10;
        private readonly List<RenderableDie> _activeDice = new List<RenderableDie>();
        private readonly List<RenderableDie> _diePool = new List<RenderableDie>();
        private readonly Dictionary<BodyHandle, RenderableDie> _bodyToDieMap = new Dictionary<BodyHandle, RenderableDie>();
        private List<DiceGroup> _currentRollGroups;

        // State Tracking
        private RollState _currentState = RollState.Idle;
        private float _settleTimer = 0f;

        // Failsafe State
        private float _rollInProgressTimer;
        private readonly Dictionary<RenderableDie, int> _rerollAttempts = new Dictionary<RenderableDie, int>();
        private readonly Dictionary<RenderableDie, int> _forcedResults = new Dictionary<RenderableDie, int>();
        private int _completeRerollAttempts;

        public bool DebugShowColliders { get; set; } = false;
        public event Action<DiceRollResult> OnRollCompleted;

        public DiceRollingSystem()
        {
            _global = ServiceLocator.Get<Global>();
            // Fetch the globally registered controllers instead of creating them.
            _physicsController = ServiceLocator.Get<DicePhysicsController>();
            _renderer = ServiceLocator.Get<DiceSceneRenderer>();
            _animationController = ServiceLocator.Get<DiceAnimationController>();
        }

        public void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
        {
            // The controllers are already instantiated, now we initialize them.
            _physicsController.Initialize();
            _renderer.Initialize(content);
            _animationController.Initialize();

            // Pre-populate the object pool
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _diePool.Add(new RenderableDie(graphicsDevice, Color.White, ""));
            }

            // Subscribe to events
            EventBus.Subscribe<GameEvents.DiceCollisionOccurred>(HandleDiceCollision);
            _animationController.OnAnimationComplete += FinalizeAndReportResults;
        }

        public void Roll(List<DiceGroup> rollGroups)
        {
            // 1. Reset state from any previous roll
            ClearRollState();
            _currentRollGroups = rollGroups;

            // 2. Configure the scene for the new roll
            int totalDice = rollGroups.Sum(g => g.NumberOfDice);
            var (viewWidth, viewHeight) = _renderer.UpdateCamera(totalDice);
            _physicsController.UpdateBoundaryPositions(viewWidth, viewHeight);

            // 3. Ensure physics shapes are cached and spawn dice
            foreach (var group in rollGroups)
            {
                var modelVertices = _renderer.GetVerticesForModel(group.DieType);
                _physicsController.CacheShapeForScale(group.DieType, group.Scale, modelVertices);

                int spawnSideForGroup = _random.Next(4);
                for (int i = 0; i < group.NumberOfDice; i++)
                {
                    SpawnAndThrowDie(group, spawnSideForGroup, viewWidth, viewHeight);
                }
            }

            _currentState = RollState.Rolling;
        }

        private void ClearRollState()
        {
            foreach (var pair in _bodyToDieMap)
            {
                _physicsController.RemoveBody(pair.Key);
                pair.Value.Reset();
                _diePool.Add(pair.Value);
            }
            _activeDice.Clear();
            _bodyToDieMap.Clear();

            _rollInProgressTimer = 0f;
            _rerollAttempts.Clear();
            _forcedResults.Clear();
            _completeRerollAttempts = 0;
            _currentRollGroups = null;
        }

        private void SpawnAndThrowDie(DiceGroup group, int spawnSide, float viewWidth, float viewHeight)
        {
            RenderableDie die = GetDieFromPool();
            die.GroupId = group.GroupId;
            die.Tint = group.Tint;
            die.BaseScale = group.Scale;
            die.DieType = group.DieType;
            die.CurrentModel = _renderer.GetModelForDieType(group.DieType);
            _activeDice.Add(die);

            var (spawnPos, linearVel, angularVel) = CalculateThrowVectors(spawnSide, viewWidth, viewHeight);
            var handle = _physicsController.AddDieBody(group, spawnPos, linearVel, angularVel);
            _bodyToDieMap.Add(handle, die);
        }

        private RenderableDie GetDieFromPool()
        {
            if (_diePool.Count > 0)
            {
                var die = _diePool.Last();
                _diePool.RemoveAt(_diePool.Count - 1);
                return die;
            }
            return new RenderableDie(ServiceLocator.Get<GraphicsDevice>(), Color.White, "");
        }

        private (BepuVector3 pos, BepuVector3 linVel, BepuVector3 angVel) CalculateThrowVectors(int spawnSide, float viewWidth, float viewHeight)
        {
            float offscreenMargin = _global.DiceSpawnOffscreenMargin;
            float spawnHeightMin = _global.DiceSpawnHeightMin;
            float spawnHeightMax = _global.DiceSpawnHeightMax;
            float spawnEdgePadding = _global.DiceSpawnEdgePadding;

            float physicsWorldWidth = 40f * ((float)Global.VIRTUAL_WIDTH / Global.VIRTUAL_HEIGHT);
            float physicsWorldHeight = 40f;

            float centerX = physicsWorldWidth / 2f;
            float centerZ = physicsWorldHeight / 2f;
            float visibleMinX = centerX - viewWidth / 2f;
            float visibleMaxX = centerX + viewWidth / 2f;
            float visibleMinZ = centerZ - viewHeight / 2f;
            float visibleMaxZ = centerZ + viewHeight / 2f;

            BepuVector3 spawnPos;
            switch (spawnSide)
            {
                case 0: // Left
                    spawnPos = new BepuVector3(visibleMinX - offscreenMargin, (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin), (float)(_random.NextDouble() * (viewHeight - spawnEdgePadding * 2) + visibleMinZ + spawnEdgePadding));
                    break;
                case 1: // Right
                    spawnPos = new BepuVector3(visibleMaxX + offscreenMargin, (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin), (float)(_random.NextDouble() * (viewHeight - spawnEdgePadding * 2) + visibleMinZ + spawnEdgePadding));
                    break;
                case 2: // Top
                    spawnPos = new BepuVector3((float)(_random.NextDouble() * (viewWidth - spawnEdgePadding * 2) + visibleMinX + spawnEdgePadding), (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin), visibleMinZ - offscreenMargin);
                    break;
                default: // Bottom
                    spawnPos = new BepuVector3((float)(_random.NextDouble() * (viewWidth - spawnEdgePadding * 2) + visibleMinX + spawnEdgePadding), (float)(_random.NextDouble() * (spawnHeightMax - spawnHeightMin) + spawnHeightMin), visibleMaxZ + offscreenMargin);
                    break;
            }

            var targetPos = new BepuVector3(centerX, 0, centerZ);
            var direction = BepuVector3.Normalize(targetPos - spawnPos);
            float throwForce = (float)(_random.NextDouble() * (_global.DiceThrowForceMax - _global.DiceThrowForceMin) + _global.DiceThrowForceMin);
            var linearVel = direction * throwForce;

            float maxAngVel = _global.DiceInitialAngularVelocityMax;
            var angularVel = new BepuVector3((float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel), (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel), (float)(_random.NextDouble() * maxAngVel * 2 - maxAngVel));

            return (spawnPos, linearVel, angularVel);
        }

        public void PhysicsStep(float deltaTime)
        {
            if (_currentState == RollState.Idle || _currentState == RollState.Complete) return;
            _physicsController.Update(deltaTime);
        }

        public void Update(GameTime gameTime)
        {
            if (_currentState == RollState.Idle || _currentState == RollState.Complete) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            SynchronizeVisuals();

            switch (_currentState)
            {
                case RollState.Rolling:
                    UpdateRollingState(deltaTime);
                    break;
                case RollState.Settling:
                    UpdateSettlingState(deltaTime);
                    break;
                case RollState.Animating:
                    _animationController.Update(gameTime);
                    break;
            }
        }

        private void SynchronizeVisuals()
        {
            foreach (var pair in _bodyToDieMap)
            {
                var body = _physicsController.GetBodyReference(pair.Key);
                var pose = body.Pose;
                var position = new XnaVector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
                var orientation = new XnaQuaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);
                pair.Value.World = Matrix.CreateFromQuaternion(orientation) * Matrix.CreateTranslation(position);
            }
        }

        private void UpdateRollingState(float deltaTime)
        {
            bool isCurrentlyRolling = !_physicsController.AreAllDiceSleeping(_bodyToDieMap.Keys);

            if (isCurrentlyRolling)
            {
                _rollInProgressTimer += deltaTime;
            }
            else
            {
                _currentState = RollState.Settling;
                _settleTimer = 0f;
                return;
            }

            // Failsafes for stuck rolls
            if (_rollInProgressTimer > _global.DiceCompleteRollTimeout)
            {
                HandleCompleteReroll();
            }
            else if (_rollInProgressTimer > _global.DiceRollTimeout)
            {
                HandleStuckDice();
            }
        }

        private void UpdateSettlingState(float deltaTime)
        {
            if (!_physicsController.AreAllDiceSleeping(_bodyToDieMap.Keys))
            {
                _currentState = RollState.Rolling;
                _rollInProgressTimer = 0f;
                return;
            }

            _settleTimer += deltaTime;
            if (_settleTimer < _global.DiceSettleDelay) return;

            // Validate the final positions of the dice
            var cantedDiceHandles = new List<BodyHandle>();
            foreach (var pair in _bodyToDieMap)
            {
                var die = pair.Value;
                var vertices = _physicsController.GetColliderVertices(die.DieType, die.BaseScale);
                var (_, alignment) = DiceResultHelper.GetFaceValueAndAlignment(die.DieType, die.World, vertices);
                if (alignment < _global.DiceCantingRerollThreshold)
                {
                    cantedDiceHandles.Add(pair.Key);
                }
            }

            if (cantedDiceHandles.Any())
            {
                foreach (var handle in cantedDiceHandles)
                {
                    _physicsController.NudgeDie(handle);
                }
                _currentState = RollState.Rolling;
                _rollInProgressTimer = 0f;
            }
            else
            {
                // Roll is valid, begin the animation sequence
                var settledDiceWithValues = new List<(RenderableDie die, int value)>();
                foreach (var die in _activeDice)
                {
                    int value = _forcedResults.TryGetValue(die, out int forcedValue)
                        ? forcedValue
                        : DiceResultHelper.GetFaceValue(die.DieType, die.World, _physicsController.GetColliderVertices(die.DieType, die.BaseScale));
                    settledDiceWithValues.Add((die, value));
                }

                _animationController.Start(settledDiceWithValues, _currentRollGroups, _renderer.RenderTarget);
                _currentState = RollState.Animating;
            }
        }

        private void FinalizeAndReportResults()
        {
            var result = new DiceRollResult();
            var rawResults = new Dictionary<string, List<int>>();

            foreach (var die in _activeDice)
            {
                if (!rawResults.ContainsKey(die.GroupId))
                {
                    rawResults[die.GroupId] = new List<int>();
                }
                int value = _forcedResults.TryGetValue(die, out int forcedValue)
                    ? forcedValue
                    : DiceResultHelper.GetFaceValue(die.DieType, die.World, _physicsController.GetColliderVertices(die.DieType, die.BaseScale));
                rawResults[die.GroupId].Add(value);
            }

            foreach (var group in _currentRollGroups)
            {
                if (rawResults.TryGetValue(group.GroupId, out var values))
                {
                    if (group.ResultProcessing == DiceResultProcessing.Sum)
                    {
                        int sum = values.Sum();
                        float multipliedValue = sum * group.Multiplier;
                        sum = (int)(group.Multiplier < 1.0f ? Math.Floor(multipliedValue) : Math.Ceiling(multipliedValue));
                        sum += group.Modifier;
                        result.ResultsByGroup[group.GroupId] = new List<int> { sum };
                    }
                    else
                    {
                        result.ResultsByGroup[group.GroupId] = values;
                    }
                }
            }

            OnRollCompleted?.Invoke(result);
            _currentState = RollState.Complete;
        }

        private void HandleDiceCollision(GameEvents.DiceCollisionOccurred e)
        {
            if (_renderer.RenderTarget == null) return;
            _animationController.HandleDiceCollision(e, _renderer.View, _renderer.Projection, _renderer.RenderTarget);
        }

        private void HandleStuckDice()
        {
            var stuckDiceHandles = _bodyToDieMap.Keys.Where(h => _physicsController.GetBodyReference(h).Awake).ToList();
            if (!stuckDiceHandles.Any()) return;

            foreach (var handle in stuckDiceHandles)
            {
                HandleReroll(_bodyToDieMap[handle], handle);
            }
            _rollInProgressTimer = 0f;
        }

        private void HandleReroll(RenderableDie die, BodyHandle? handleToRemove = null)
        {
            _rerollAttempts[die] = _rerollAttempts.GetValueOrDefault(die) + 1;

            if (handleToRemove.HasValue)
            {
                _physicsController.RemoveBody(handleToRemove.Value);
                _bodyToDieMap.Remove(handleToRemove.Value);
            }

            if (_rerollAttempts[die] >= _global.DiceMaxRerollAttempts)
            {
                _forcedResults[die] = _global.DiceForcedResultValue;
            }
            else
            {
                var group = _currentRollGroups.First(g => g.GroupId == die.GroupId);
                var (viewWidth, viewHeight) = _renderer.UpdateCamera(_activeDice.Count);
                // Remove the die from the active list before re-spawning to avoid duplicates
                _activeDice.Remove(die);
                SpawnAndThrowDie(group, _random.Next(4), viewWidth, viewHeight);
            }
        }

        private void HandleCompleteReroll()
        {
            _completeRerollAttempts++;
            if (_completeRerollAttempts >= _global.DiceMaxRerollAttempts)
            {
                HandleForcedCompletion();
                return;
            }
            Roll(new List<DiceGroup>(_currentRollGroups)); // Re-roll with the same request
        }

        private void HandleForcedCompletion()
        {
            foreach (var die in _activeDice)
            {
                _forcedResults[die] = _global.DiceForcedResultValue;
            }
            FinalizeAndReportResults();
            _currentState = RollState.Idle;
        }

        public RenderTarget2D Draw(BitmapFont font)
        {
            if (_currentState == RollState.Idle || _currentState == RollState.Complete) return null;

            _renderer.BeginDraw();

            var diceToDraw = _activeDice.Where(d => !_forcedResults.ContainsKey(d) && !d.IsDespawned);
            var colliderVertices = new Dictionary<RenderableDie, List<BepuVector3>>();
            if (DebugShowColliders)
            {
                foreach (var die in diceToDraw)
                {
                    colliderVertices[die] = _physicsController.GetColliderVertices(die.DieType, die.BaseScale);
                }
            }
            _renderer.DrawDiceScene(diceToDraw, colliderVertices, DebugShowColliders);

            if (_currentState == RollState.Animating)
            {
                var spriteBatch = ServiceLocator.Get<SpriteBatch>();
                _animationController.DrawOverlays(spriteBatch, font);
            }

            _renderer.EndDraw();
            return _renderer.RenderTarget;
        }
    }
}