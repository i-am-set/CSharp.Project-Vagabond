using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the behavior of AI-controlled entities.
    /// </summary>
    public class AISystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly ChunkManager _chunkManager;
        private readonly Random _random = new();
        private const float REPATH_INTERVAL = 0.5f; // Recalculate path every half a second
        private const float BASE_AI_STEP_DURATION = 0.15f; // AI moves slightly slower visually
        private static readonly Vector2[] _neighborOffsets = new Vector2[]
        {
            new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0),
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
        };

        public AISystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
        }

        public void Update(GameTime gameTime)
        {
            // The main update loop is now handled by LocalMapTurnSystem.
            // This method is kept to satisfy the ISystem interface, but the core logic
            // is now in UpdateDecisions, which is called explicitly by LocalMapTurnSystem.
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.IsInCombat) return;
        }

        public void ProcessCombatTurn(int entityId)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            var aiPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            var aiName = EntityNamer.GetName(entityId);

            // Failsafe for missing components
            if (aiPosComp == null || actionQueue == null || stats == null || playerPosComp == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{aiName} cannot act (missing components)." });
                actionQueue?.ActionQueue.Enqueue(new EndTurnAction(entityId));
                return;
            }

            // Find a path to the player.
            var path = Pathfinder.FindPath(entityId, aiPosComp.LocalPosition, playerPosComp.LocalPosition, _gameState, MovementMode.Run, PathfindingMode.Moves, MapView.Local);

            if (path != null && path.Any())
            {
                float timeUsedThisTurn = 0f;
                int simulatedEnergy = stats.CurrentEnergyPoints;
                Vector2 lastStepPosition = aiPosComp.LocalPosition;
                int movesQueued = 0;

                // Loop through the path and queue as many moves as possible within the time budget.
                foreach (var step in path)
                {
                    // Determine movement mode based on simulated energy for this turn.
                    int runCost = _gameState.GetMovementEnergyCost(new MoveAction(entityId, step, MovementMode.Run), true);
                    var moveMode = (simulatedEnergy >= runCost) ? MovementMode.Run : MovementMode.Jog;

                    // Calculate the time cost for this specific step.
                    Vector2 moveDir = step - lastStepPosition;
                    float stepTimeCost = _gameState.GetSecondsPassedDuringMovement(stats, moveMode, default, moveDir, true);

                    // Check if this move fits within the turn's time budget.
                    if (timeUsedThisTurn + stepTimeCost > Global.COMBAT_TURN_DURATION_SECONDS)
                    {
                        break; // Can't afford this move, stop planning.
                    }

                    // If affordable, queue the action and update simulated state.
                    timeUsedThisTurn += stepTimeCost;
                    actionQueue.ActionQueue.Enqueue(new MoveAction(entityId, step, moveMode));
                    movesQueued++;

                    if (moveMode == MovementMode.Run)
                    {
                        simulatedEnergy -= runCost;
                    }
                    lastStepPosition = step;
                }

                if (movesQueued > 0)
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} moves towards the player." });
                }
                else
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} waits." });
                }
            }
            else
            {
                // If no path is possible, the AI waits.
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} waits." });
            }

            // Always end the turn after planning moves.
            actionQueue.ActionQueue.Enqueue(new EndTurnAction(entityId));
        }

        public void UpdateDecisions()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.IsInCombat) return;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !_componentStore.HasComponent<NPCTagComponent>(entityId)) continue;

                var pathComp = _componentStore.GetComponent<AIPathComponent>(entityId);
                if (pathComp == null)
                {
                    pathComp = new AIPathComponent();
                    _componentStore.AddComponent(entityId, pathComp);
                }

                pathComp.RepathTimer += 1; // Increment a simple counter
                bool isIdle = !_componentStore.HasComponent<InterpolationComponent>(entityId);

                // Decide on a new goal if idle AND (the current path is finished OR it's time to repath)
                if (isIdle && (!pathComp.HasPath() || pathComp.RepathTimer >= 5))
                {
                    DecideNextGoal(entityId, aiComp, pathComp);
                    pathComp.RepathTimer = 0;
                }
            }
        }

        public Vector2? GetNextStepForExecution(int entityId)
        {
            var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
            var pathComp = _componentStore.GetComponent<AIPathComponent>(entityId);

            if (aiComp == null || pathComp == null) return null;

            if (pathComp.HasPath())
            {
                var nextStep = pathComp.Path[pathComp.CurrentPathIndex];
                pathComp.CurrentPathIndex++;
                return nextStep;
            }

            // If there's no long-term path, use the single-step decision.
            return aiComp.NextStep;
        }

        private void DecideNextGoal(int entityId, AIComponent aiComp, AIPathComponent pathComp)
        {
            pathComp.Clear();
            aiComp.NextStep = null;

            var personalityComp = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
            var combatantComp = _componentStore.GetComponent<CombatantComponent>(entityId);
            if (personalityComp == null || combatantComp == null)
            {
                Wander(entityId, aiComp);
                return;
            }

            float distanceToPlayer = GetTrueLocalDistance(entityId, _gameState.PlayerEntityId);
            bool inAggroRange = distanceToPlayer <= combatantComp.AggroRange;
            float initiationRange = (float)Math.Ceiling(combatantComp.AttackRange) + 1;
            bool inInitiationRange = distanceToPlayer <= initiationRange;

            SetAIIntent(entityId, AIIntent.None);

            if (inAggroRange)
            {
                switch (personalityComp.Personality)
                {
                    case AIPersonalityType.Aggressive:
                        if (inInitiationRange) InitiateCombat(entityId, aiComp);
                        else PursuePlayer(entityId, aiComp, pathComp);
                        break;
                    case AIPersonalityType.Neutral:
                        if (personalityComp.IsProvoked)
                        {
                            if (inInitiationRange) InitiateCombat(entityId, aiComp);
                            else PursuePlayer(entityId, aiComp, pathComp);
                        }
                        break;
                    case AIPersonalityType.Passive:
                        if (personalityComp.IsProvoked) FleeFromPlayer(entityId, aiComp);
                        break;
                    case AIPersonalityType.Fearful:
                        FleeFromPlayer(entityId, aiComp);
                        break;
                }
            }
            else
            {
                Wander(entityId, aiComp);
            }
        }

        public void InitiateCombat(int aiEntityId, AIComponent aiComp)
        {
            SetAIIntent(aiEntityId, AIIntent.None);
            var pathComp = _componentStore.GetComponent<AIPathComponent>(aiEntityId);
            pathComp?.Clear();
            aiComp.NextStep = null;

            var aiName = EntityNamer.GetName(aiEntityId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[warning]{aiName} has spotted you and is moving to attack!" });
            _gameState.RequestCombatInitiation(aiEntityId);
        }

        private void PursuePlayer(int entityId, AIComponent aiComp, AIPathComponent pathComp)
        {
            SetAIIntent(entityId, AIIntent.Pursuing);

            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            var playerPos = GetPlayerRelativeLocalPosition(entityId);
            if (!playerPos.HasValue)
            {
                Wander(entityId, aiComp);
                return;
            }

            var path = Pathfinder.FindPath(entityId, localPosComp.LocalPosition, playerPos.Value, _gameState, MovementMode.Run, PathfindingMode.Moves, MapView.Local);

            if (path != null && path.Any())
            {
                var forbiddenTiles = _gameState.GetPlayerQueuedMovePositions();
                int forbiddenIndex = path.FindIndex(step => forbiddenTiles.Contains(step));
                if (forbiddenIndex != -1)
                {
                    path = path.Take(forbiddenIndex).ToList();
                }

                if (path.Any())
                {
                    pathComp.Path = path;
                }
            }
            else
            {
                Wander(entityId, aiComp);
            }
        }

        private void FleeFromPlayer(int entityId, AIComponent aiComp)
        {
            SetAIIntent(entityId, AIIntent.Fleeing);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            var playerPos = GetPlayerRelativeLocalPosition(entityId);
            if (!playerPos.HasValue)
            {
                Wander(entityId, aiComp);
                return;
            }

            Vector2 bestFleeDirection = Vector2.Zero;
            float maxDistance = -1;
            var forbiddenTiles = _gameState.GetPlayerQueuedMovePositions();

            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (forbiddenTiles.Contains(targetPos)) continue;

                if (_gameState.IsPositionPassable(targetPos, MapView.Local))
                {
                    float newDistance = Vector2.DistanceSquared(targetPos, playerPos.Value);
                    if (newDistance > maxDistance)
                    {
                        maxDistance = newDistance;
                        bestFleeDirection = offset;
                    }
                }
            }

            if (bestFleeDirection != Vector2.Zero)
            {
                aiComp.NextStep = localPosComp.LocalPosition + bestFleeDirection;
            }
        }

        private void Wander(int entityId, AIComponent aiComp)
        {
            SetAIIntent(entityId, AIIntent.None);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            var forbiddenTiles = _gameState.GetPlayerQueuedMovePositions();
            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (forbiddenTiles.Contains(targetPos)) continue;

                if (_gameState.IsPositionPassable(targetPos, MapView.Local))
                {
                    aiComp.NextStep = targetPos;
                    return; // Found a valid move, exit
                }
            }
        }

        public float GetTrueLocalDistance(int entityId1, int entityId2)
        {
            var pos1 = _componentStore.GetComponent<PositionComponent>(entityId1);
            var localPos1 = _componentStore.GetComponent<LocalPositionComponent>(entityId1);
            var pos2 = _componentStore.GetComponent<PositionComponent>(entityId2);
            var localPos2 = _componentStore.GetComponent<LocalPositionComponent>(entityId2);

            if (pos1 == null || localPos1 == null || pos2 == null || localPos2 == null)
            {
                return float.MaxValue;
            }

            float worldOffsetX = (pos2.WorldPosition.X - pos1.WorldPosition.X) * Global.LOCAL_GRID_SIZE;
            float worldOffsetY = (pos2.WorldPosition.Y - pos1.WorldPosition.Y) * Global.LOCAL_GRID_SIZE;

            Vector2 relativePos2 = new Vector2(
                localPos2.LocalPosition.X + worldOffsetX,
                localPos2.LocalPosition.Y + worldOffsetY
            );

            return Vector2.Distance(localPos1.LocalPosition, relativePos2);
        }

        private Vector2? GetPlayerRelativeLocalPosition(int aiEntityId)
        {
            var aiPos = _componentStore.GetComponent<PositionComponent>(aiEntityId);
            var playerPos = _componentStore.GetComponent<PositionComponent>(_gameState.PlayerEntityId);
            var playerLocalPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);

            if (aiPos == null || playerPos == null || playerLocalPos == null) return null;

            float worldOffsetX = (playerPos.WorldPosition.X - aiPos.WorldPosition.X) * Global.LOCAL_GRID_SIZE;
            float worldOffsetY = (playerPos.WorldPosition.Y - aiPos.WorldPosition.Y) * Global.LOCAL_GRID_SIZE;

            return new Vector2(
                playerLocalPos.LocalPosition.X + worldOffsetX,
                playerLocalPos.LocalPosition.Y + worldOffsetY
            );
        }

        private void SetAIIntent(int entityId, AIIntent intent)
        {
            var intentComp = _componentStore.GetComponent<AIIntentComponent>(entityId);
            if (intentComp == null)
            {
                intentComp = new AIIntentComponent();
                _componentStore.AddComponent(entityId, intentComp);
            }
            intentComp.CurrentIntent = intent;
        }

        public Dictionary<int, List<(Vector2 Position, MovementMode Mode)>> SimulateMovementForDuration(float timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            var allPreviewPaths = new Dictionary<int, List<(Vector2, MovementMode)>>();

            var playerPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerPosComp == null) return allPreviewPaths;
            Vector2 playerCurrentPos = playerPosComp.LocalPosition;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                if (entityId == _gameState.PlayerEntityId) continue;

                var personality = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
                bool shouldPursue = personality != null &&
                                    (personality.Personality == AIPersonalityType.Aggressive ||
                                     (personality.Personality == AIPersonalityType.Neutral && personality.IsProvoked));

                if (!shouldPursue) continue;

                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
                if (localPosComp == null || statsComp == null) continue;

                var fullPath = Pathfinder.FindPath(entityId, localPosComp.LocalPosition, playerCurrentPos, _gameState, MovementMode.Run, PathfindingMode.Moves, MapView.Local);

                if (fullPath != null && fullPath.Any())
                {
                    var timeLimitedPath = new List<(Vector2 Position, MovementMode Mode)>();
                    int simulatedEnergy = statsComp.CurrentEnergyPoints;
                    float timeAccumulator = 0f;
                    Vector2 lastPos = localPosComp.LocalPosition;

                    foreach (var step in fullPath)
                    {
                        var moveMode = (simulatedEnergy > 0) ? MovementMode.Run : MovementMode.Jog;
                        Vector2 moveDir = step - lastPos;
                        float stepCost = _gameState.GetSecondsPassedDuringMovement(statsComp, moveMode, default, moveDir, true);

                        if (timeAccumulator + stepCost > timeBudget)
                        {
                            break; // Stop if this step would exceed the budget
                        }

                        timeAccumulator += stepCost;
                        timeLimitedPath.Add((step, moveMode));
                        if (moveMode == MovementMode.Run)
                        {
                            simulatedEnergy--;
                        }
                        lastPos = step;
                    }

                    if (timeLimitedPath.Any())
                    {
                        allPreviewPaths[entityId] = timeLimitedPath;
                    }
                }
            }

            return allPreviewPaths;
        }
    }
}