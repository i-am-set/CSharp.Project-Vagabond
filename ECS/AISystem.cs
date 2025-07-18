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

        // This system is not updated by the SystemManager in real-time.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Plans and queues a full turn's worth of actions for an AI entity in combat.
        /// This function runs ONCE at the start of the AI's turn.
        /// </summary>
        /// <param name="entityId">The ID of the AI entity to process.</param>
        public void ProcessCombatTurn(int entityId)
        {
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
                actionQueue?.ActionQueue.Enqueue(new EndTurnAction(entityId)); // Ensure turn ends even on error
                return;
            }

            // --- AI TURN PLANNING ---
            var simulatedPosition = aiPos.LocalPosition;
            List<Vector2> pathToMove = new List<Vector2>();
            bool isRunning = true;

            // ** GOAL: Move into attack range, then attack. **
            // 1. Try to find an affordable running path.
            var path = _gameState.GetAffordablePath(entityId, simulatedPosition, playerPos.LocalPosition, true, out _);

            // 2. If running path fails, try a walking path.
            if (path == null || !path.Any())
            {
                isRunning = false;
                path = _gameState.GetAffordablePath(entityId, simulatedPosition, playerPos.LocalPosition, false, out _);
            }

            // 3. If a path was found, prepare the movement actions.
            if (path != null && path.Any())
            {
                // The actual path to travel is all steps except the last one (which is the target's tile).
                // If the path is only one step, it means we are already adjacent, so we don't move.
                if (path.Count > 1)
                {
                    pathToMove = path.Take(path.Count - 1).ToList();
                }
            }

            // 4. Queue the movement actions.
            if (pathToMove.Any())
            {
                string moveMode = isRunning ? "runs" : "walks";
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} {moveMode} towards the player." });
                foreach (var step in pathToMove)
                {
                    actionQueue.ActionQueue.Enqueue(new MoveAction(entityId, step, isRunning));
                }
            }

            // 5. Check for attack possibility from the final position.
            var finalPositionAfterMove = pathToMove.Any() ? pathToMove.Last() : simulatedPosition;
            if (Vector2.Distance(finalPositionAfterMove, playerPos.LocalPosition) <= combatant.AttackRange)
            {
                var attack = _componentStore.GetComponent<AvailableAttacksComponent>(entityId)?.Attacks.FirstOrDefault();
                if (attack != null)
                {
                    actionQueue.ActionQueue.Enqueue(new AttackAction(entityId, _gameState.PlayerEntityId, attack.Name));
                }
            }

            // ** Final Step: Always queue an EndTurnAction to guarantee the turn ends. **
            actionQueue.ActionQueue.Enqueue(new EndTurnAction(entityId));
        }

        // Out of Combat Logic
        public void ProcessEntities(int timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.IsInCombat) return;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !_componentStore.HasComponent<NPCTagComponent>(entityId)) continue;

                aiComp.ActionTimeBudget += timeBudget;
                while (aiComp.ActionTimeBudget > 0)
                {
                    var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                    if (actionQueueComp == null) break;
                    if (actionQueueComp.ActionQueue.Count == 0)
                    {
                        DecideNextAction(entityId, aiComp);
                    }
                    if (actionQueueComp.ActionQueue.Count == 0)
                    {
                        aiComp.ActionTimeBudget = 0;
                        break;
                    }

                    IAction nextAction = actionQueueComp.ActionQueue.Peek();
                    int actionCost = CalculateAIActionCost(nextAction);
                    if (aiComp.ActionTimeBudget >= actionCost)
                    {
                        actionQueueComp.ActionQueue.Dequeue();
                        aiComp.ActionTimeBudget -= actionCost;
                        ExecuteAIAction(entityId, nextAction);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void DecideNextAction(int entityId, AIComponent aiComp)
        {
            var personalityComp = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
            var combatantComp = _componentStore.GetComponent<CombatantComponent>(entityId);
            if (personalityComp == null || combatantComp == null)
            {
                Wander(entityId); // Default to wandering if no personality
                return;
            }

            float distanceToPlayer = GetTrueLocalDistance(entityId, _gameState.PlayerEntityId);
            bool inAggroRange = distanceToPlayer <= combatantComp.AggroRange;
            bool inAttackRange = distanceToPlayer <= combatantComp.AttackRange;

            // Default to no intent
            SetAIIntent(entityId, AIIntent.None);

            if (inAggroRange)
            {
                // Player is in range, decide action based on personality
                switch (personalityComp.Personality)
                {
                    case AIPersonalityType.Aggressive:
                        if (inAttackRange) InitiateCombat(entityId);
                        else PursuePlayer(entityId);
                        break;
                    case AIPersonalityType.Neutral:
                        if (personalityComp.IsProvoked)
                        {
                            if (inAttackRange) InitiateCombat(entityId);
                            else PursuePlayer(entityId);
                        }
                        break;
                    case AIPersonalityType.Passive:
                        if (personalityComp.IsProvoked) FleeFromPlayer(entityId);
                        break;
                    case AIPersonalityType.Fearful:
                        FleeFromPlayer(entityId);
                        break;
                }
            }
            else
            {
                // Player is not in range, just wander
                Wander(entityId);
            }
        }

        private void InitiateCombat(int aiEntityId)
        {
            var aiName = EntityNamer.GetName(aiEntityId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[warning]{aiName} has spotted you and is moving to attack!" });
            _gameState.InitiateCombat(new List<int> { aiEntityId, _gameState.PlayerEntityId });
        }

        private void PursuePlayer(int entityId)
        {
            SetAIIntent(entityId, AIIntent.Pursuing);
            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (actionQueueComp == null || localPosComp == null) return;

            var playerPos = GetPlayerRelativeLocalPosition(entityId);
            if (!playerPos.HasValue)
            {
                Wander(entityId); // Can't see player, just wander
                return;
            }

            // Find the first step on a path to the player
            var path = Pathfinder.FindPath(entityId, localPosComp.LocalPosition, playerPos.Value, _gameState, false, PathfindingMode.Moves, MapView.Local);
            if (path != null && path.Any())
            {
                actionQueueComp.ActionQueue.Enqueue(new MoveAction(entityId, path.First(), false));
            }
        }

        private void FleeFromPlayer(int entityId)
        {
            SetAIIntent(entityId, AIIntent.Fleeing);
            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (actionQueueComp == null || localPosComp == null) return;

            var playerPos = GetPlayerRelativeLocalPosition(entityId);
            if (!playerPos.HasValue)
            {
                Wander(entityId); // Can't see player, just wander
                return;
            }

            // Find the best direction to flee
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
                actionQueueComp.ActionQueue.Enqueue(new MoveAction(entityId, localPosComp.LocalPosition + bestFleeDirection, false));
            }
        }

        private void Wander(int entityId)
        {
            SetAIIntent(entityId, AIIntent.None);
            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (actionQueueComp == null || localPosComp == null) return;

            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (_gameState.IsPositionPassable(targetPos, MapView.Local))
                {
                    actionQueueComp.ActionQueue.Enqueue(new MoveAction(entityId, targetPos, false));
                    return; // Found a valid move, exit
                }
            }
        }

        private int CalculateAIActionCost(IAction action) => action is MoveAction ? 4 : 1;

        private void ExecuteAIAction(int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp != null) localPosComp.LocalPosition = moveAction.Destination;
            }
        }

        /// <summary>
        /// Calculates the true distance between two entities on the local grid,
        /// accounting for them being in different world chunks.
        /// </summary>
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

            // Calculate the difference in world coordinates
            float worldOffsetX = (pos2.WorldPosition.X - pos1.WorldPosition.X) * Global.LOCAL_GRID_SIZE;
            float worldOffsetY = (pos2.WorldPosition.Y - pos1.WorldPosition.Y) * Global.LOCAL_GRID_SIZE;

            // Calculate entity2's position as if it were on entity1's local map
            Vector2 relativePos2 = new Vector2(
                localPos2.LocalPosition.X + worldOffsetX,
                localPos2.LocalPosition.Y + worldOffsetY
            );

            return Vector2.Distance(localPos1.LocalPosition, relativePos2);
        }

        /// <summary>
        /// Gets the player's position relative to the AI's local grid.
        /// </summary>
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

        /// <summary>
        /// A helper to safely get or add an AIIntentComponent and set its state.
        /// </summary>
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
    }
}