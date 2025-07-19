
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
        private WorldClockManager _worldClockManager;
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

        public void Update(GameTime gameTime) { }

        public void ProcessCombatTurn(int entityId)
        {
            // Combat logic remains unchanged
            _gameState ??= ServiceLocator.Get<GameState>();

            var aiPos = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var combatant = _componentStore.GetComponent<CombatantComponent>(entityId);
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            var aiName = EntityNamer.GetName(entityId);

            if (aiPos == null || combatant == null || actionQueue == null || stats == null || playerPos == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{aiName} cannot act (missing components)." });
                actionQueue?.ActionQueue.Enqueue(new EndTurnAction(entityId));
                return;
            }

            var simulatedPosition = aiPos.LocalPosition;
            List<Vector2> pathToMove = new List<Vector2>();
            bool isRunning = true;

            var path = _gameState.GetAffordablePath(entityId, simulatedPosition, playerPos.LocalPosition, true, out _);

            if (path == null || !path.Any())
            {
                isRunning = false;
                path = _gameState.GetAffordablePath(entityId, simulatedPosition, playerPos.LocalPosition, false, out _);
            }

            if (path != null && path.Any())
            {
                if (path.Count > 1)
                {
                    pathToMove = path.Take(path.Count - 1).ToList();
                }
            }

            if (pathToMove.Any())
            {
                string moveMode = isRunning ? "runs" : "walks";
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} {moveMode} towards the player." });
                foreach (var step in pathToMove)
                {
                    actionQueue.ActionQueue.Enqueue(new MoveAction(entityId, step, isRunning));
                }
            }

            var finalPositionAfterMove = pathToMove.Any() ? pathToMove.Last() : simulatedPosition;
            if (Vector2.Distance(finalPositionAfterMove, playerPos.LocalPosition) <= combatant.AttackRange)
            {
                var attack = _componentStore.GetComponent<AvailableAttacksComponent>(entityId)?.Attacks.FirstOrDefault();
                if (attack != null)
                {
                    actionQueue.ActionQueue.Enqueue(new AttackAction(entityId, _gameState.PlayerEntityId, attack.Name));
                }
            }

            actionQueue.ActionQueue.Enqueue(new EndTurnAction(entityId));
        }

        public void ProcessEntities(float timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>(); // Lazy load
            if (_gameState.IsInCombat) return;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !_componentStore.HasComponent<NPCTagComponent>(entityId)) continue;

                aiComp.ActionTimeBudget += timeBudget;
                var pathComp = _componentStore.GetComponent<AIPathComponent>(entityId);
                var progressComp = _componentStore.GetComponent<MovementProgressComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);

                if (pathComp == null)
                {
                    pathComp = new AIPathComponent();
                    _componentStore.AddComponent(entityId, pathComp);
                }
                if (progressComp == null)
                {
                    progressComp = new MovementProgressComponent();
                    _componentStore.AddComponent(entityId, progressComp);
                }
                if (statsComp == null) continue;

                pathComp.RepathTimer += timeBudget;
                bool isIdle = !_componentStore.HasComponent<InterpolationComponent>(entityId);

                if (isIdle)
                {
                    if (!aiComp.NextStep.HasValue || !pathComp.HasPath() || pathComp.RepathTimer >= REPATH_INTERVAL)
                    {
                        DecideNextGoal(entityId, aiComp, pathComp);
                    }

                    var intentComp = _componentStore.GetComponent<AIIntentComponent>(entityId);
                    bool isRunning = (intentComp?.CurrentIntent == AIIntent.Pursuing || intentComp?.CurrentIntent == AIIntent.Fleeing) && statsComp.CanExertEnergy(1);
                    float currentSpeed = isRunning ? statsComp.RunSpeed : statsComp.WalkSpeed;

                    // Calculate how much distance (in tiles) the AI could have covered
                    float potentialDistance = timeBudget * (currentSpeed / Global.SECONDS_PER_FOOT_SCALING_FACTOR) / (Global.FEET_PER_WORLD_TILE / Global.LOCAL_GRID_SIZE);
                    progressComp.Progress += potentialDistance;

                    while (progressComp.Progress >= 1.0f)
                    {
                        if (pathComp.HasPath())
                        {
                            aiComp.NextStep = pathComp.Path[pathComp.CurrentPathIndex];
                            pathComp.CurrentPathIndex++;
                        }

                        if (aiComp.NextStep.HasValue)
                        {
                            ExecuteAIAction(entityId, new MoveAction(entityId, aiComp.NextStep.Value, isRunning));
                            progressComp.Progress -= 1.0f;
                            aiComp.NextStep = null; // Consume the step
                        }
                        else
                        {
                            // No next step, so can't move. Clear progress to avoid infinite loops.
                            progressComp.Progress = 0;
                            break;
                        }
                    }
                }
                aiComp.ActionTimeBudget = 0; // Budget is consumed by movement progress
            }
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
            bool inAttackRange = distanceToPlayer <= combatantComp.AttackRange;

            SetAIIntent(entityId, AIIntent.None);

            if (inAggroRange)
            {
                switch (personalityComp.Personality)
                {
                    case AIPersonalityType.Aggressive:
                        if (inAttackRange) InitiateCombat(entityId, aiComp);
                        else PursuePlayer(entityId, pathComp);
                        break;
                    case AIPersonalityType.Neutral:
                        if (personalityComp.IsProvoked)
                        {
                            if (inAttackRange) InitiateCombat(entityId, aiComp);
                            else PursuePlayer(entityId, pathComp);
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

        private void InitiateCombat(int aiEntityId, AIComponent aiComp)
        {
            SetAIIntent(aiEntityId, AIIntent.None);
            var pathComp = _componentStore.GetComponent<AIPathComponent>(aiEntityId);
            pathComp?.Clear();
            aiComp.NextStep = null;

            var aiName = EntityNamer.GetName(aiEntityId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[warning]{aiName} has spotted you and is moving to attack!" });
            _gameState.RequestCombatInitiation(aiEntityId);
            aiComp.ActionTimeBudget = 0;
        }

        private void PursuePlayer(int entityId, AIPathComponent pathComp)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            if (stats != null && stats.CanExertEnergy(1))
            {
                SetAIIntent(entityId, AIIntent.Pursuing);
            }

            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            var playerPos = GetPlayerRelativeLocalPosition(entityId);
            if (!playerPos.HasValue)
            {
                Wander(entityId, _componentStore.GetComponent<AIComponent>(entityId));
                return;
            }

            bool isRunning = _componentStore.GetComponent<AIIntentComponent>(entityId)?.CurrentIntent == AIIntent.Pursuing;
            var path = Pathfinder.FindPath(entityId, localPosComp.LocalPosition, playerPos.Value, _gameState, isRunning, PathfindingMode.Moves, MapView.Local);
            if (path != null && path.Any())
            {
                pathComp.Path = path;
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

            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
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

            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (_gameState.IsPositionPassable(targetPos, MapView.Local))
                {
                    aiComp.NextStep = targetPos;
                    return; // Found a valid move, exit
                }
            }
        }

        private float CalculateAIActionTimeCost(int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var stats = _componentStore.GetComponent<StatsComponent>(entityId);
                var localPos = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (stats == null || localPos == null) return 1000;

                Vector2 moveDir = moveAction.Destination - localPos.LocalPosition;
                return _gameState.GetSecondsPassedDuringMovement(stats, moveAction.IsRunning, default, moveDir, true);
            }
            return 1;
        }

        private void ExecuteAIAction(int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);

                if (localPosComp != null && statsComp != null)
                {
                    if (moveAction.IsRunning)
                    {
                        statsComp.ExertEnergy(1);
                    }

                    // Visual duration is now inversely proportional to the entity's speed.
                    float currentSpeed = moveAction.IsRunning ? statsComp.RunSpeed : statsComp.WalkSpeed;
                    float visualDuration = (BASE_AI_STEP_DURATION / currentSpeed) / _worldClockManager.TimeScale;

                    var interp = new InterpolationComponent(localPosComp.LocalPosition, moveAction.Destination, visualDuration, moveAction.IsRunning);
                    _componentStore.AddComponent(entityId, interp);
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

        public Dictionary<int, List<Vector2>> SimulateMovement(float timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            var allPreviewPaths = new Dictionary<int, List<Vector2>>();
            if (timeBudget <= 0) return allPreviewPaths;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                if (entityId == _gameState.PlayerEntityId) continue;

                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);

                if (aiComp == null || statsComp == null || localPosComp == null) continue;

                var entityPreviewPath = new List<Vector2>();
                var simulatedPosition = localPosComp.LocalPosition;
                var simulatedProgress = 0f; // A preview should always start from a clean slate.
                int simulatedEnergy = statsComp.CurrentEnergyPoints;

                var intentComp = _componentStore.GetComponent<AIIntentComponent>(entityId);
                bool isRunning = (intentComp?.CurrentIntent == AIIntent.Pursuing || intentComp?.CurrentIntent == AIIntent.Fleeing);
                float currentSpeed = isRunning && simulatedEnergy > 0 ? statsComp.RunSpeed : statsComp.WalkSpeed;

                float potentialDistance = timeBudget * (currentSpeed / Global.SECONDS_PER_FOOT_SCALING_FACTOR) / (Global.FEET_PER_WORLD_TILE / Global.LOCAL_GRID_SIZE);
                simulatedProgress += potentialDistance;

                while (simulatedProgress >= 1.0f)
                {
                    var nextStep = GetNextStepForSimulation(entityId, simulatedPosition);
                    if (nextStep.HasValue)
                    {
                        if (isRunning)
                        {
                            if (simulatedEnergy > 0)
                            {
                                simulatedEnergy--;
                            }
                            else
                            {
                                // Ran out of energy mid-simulation, stop running.
                                break;
                            }
                        }
                        entityPreviewPath.Add(nextStep.Value);
                        simulatedPosition = nextStep.Value;
                        simulatedProgress -= 1.0f;
                    }
                    else
                    {
                        break; // No valid move, stop simulating for this entity
                    }
                }

                if (entityPreviewPath.Any())
                {
                    allPreviewPaths[entityId] = entityPreviewPath;
                }
            }

            return allPreviewPaths;
        }

        private Vector2? GetNextStepForSimulation(int entityId, Vector2 currentSimulatedPosition)
        {
            // This is a simplified version of DecideNextGoal for prediction purposes.
            var personalityComp = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
            var combatantComp = _componentStore.GetComponent<CombatantComponent>(entityId);
            var playerPos = GetPlayerRelativeLocalPosition(entityId);

            if (playerPos.HasValue && personalityComp != null && combatantComp != null)
            {
                float distanceToPlayer = Vector2.Distance(currentSimulatedPosition, playerPos.Value);
                if (distanceToPlayer <= combatantComp.AggroRange)
                {
                    if (personalityComp.Personality == AIPersonalityType.Aggressive || (personalityComp.Personality == AIPersonalityType.Neutral && personalityComp.IsProvoked))
                    {
                        // If already adjacent, don't move closer.
                        if (distanceToPlayer <= 1.5f) // Using 1.5f to account for diagonals
                        {
                            return null;
                        }

                        // Pursue
                        var path = Pathfinder.FindPath(entityId, currentSimulatedPosition, playerPos.Value, _gameState, true, PathfindingMode.Moves, MapView.Local);
                        if (path != null && path.Any())
                        {
                            return path[0];
                        }
                    }
                }
            }

            // Default to wandering
            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = currentSimulatedPosition + offset;
                if (_gameState.IsPositionPassable(targetPos, MapView.Local))
                {
                    return targetPos;
                }
            }

            return null;
        }
    }
}
