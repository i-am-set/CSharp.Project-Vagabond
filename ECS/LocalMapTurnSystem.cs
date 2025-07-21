﻿﻿using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the fine-grained, tick-based passage of time and action execution
    /// on the local map when the player's action queue is being processed.
    /// This ensures all entities act concurrently based on their relative speeds.
    /// </summary>
    public class LocalMapTurnSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly WorldClockManager _worldClockManager;
        private AISystem _aiSystem; // Lazy loaded

        public LocalMapTurnSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
        }

        public void Update(GameTime gameTime)
        {
            if (!_gameState.IsExecutingActions || _gameState.PathExecutionMapView != MapView.Local || _gameState.IsInCombat || _worldClockManager.IsInterpolatingTime)
            {
                return;
            }

            // If any entity is currently animating its movement, wait for it to finish.
            if (_gameState.ActiveEntities.Any(id => _componentStore.HasComponent<InterpolationComponent>(id)))
            {
                return;
            }

            var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
            if (playerActionQueue == null || !playerActionQueue.ActionQueue.Any())
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Local movement complete." });
                return;
            }

            _aiSystem ??= ServiceLocator.Get<AISystem>();

            // --- PRE-EMPTIVE COMBAT CHECK ---
            // Before any moves are made, check if the player's NEXT step will trigger combat.
            if (playerActionQueue.ActionQueue.Peek() is MoveAction playerNextMove)
            {
                Vector2 playerNextStep = playerNextMove.Destination;
                foreach (var entityId in _gameState.ActiveEntities)
                {
                    if (entityId == _gameState.PlayerEntityId) continue;

                    var personality = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
                    var combatant = _componentStore.GetComponent<CombatantComponent>(entityId);
                    bool isHostile = personality != null && combatant != null &&
                                     (personality.Personality == AIPersonalityType.Aggressive ||
                                      (personality.Personality == AIPersonalityType.Neutral && personality.IsProvoked));

                    if (isHostile)
                    {
                        var aiPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                        if (aiPosComp != null)
                        {
                            float distanceToPlayerNextStep = Vector2.Distance(aiPosComp.LocalPosition, playerNextStep);
                            if (distanceToPlayerNextStep <= Math.Ceiling(combatant.AttackRange))
                            {
                                // The player is about to step into attack range. Ambush them!
                                _aiSystem.InitiateCombat(entityId, _componentStore.GetComponent<AIComponent>(entityId));
                                return; // Stop this tick immediately. CombatInitiationSystem will handle the interruption.
                            }
                        }
                    }
                }
            }
            // --- END OF PRE-EMPTIVE CHECK ---


            // CRITICAL: Tell all AIs to decide what they want to do BEFORE this tick happens.
            _aiSystem.UpdateDecisions();

            // The time tick is now determined solely by the player's next action.
            var playerStats = _componentStore.GetComponent<StatsComponent>(_gameState.PlayerEntityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerStats == null || playerPos == null)
            {
                _gameState.ToggleExecutingActions(false); // Failsafe
                return;
            }

            if (playerActionQueue.ActionQueue.Peek() is MoveAction nextPlayerMoveAction)
            {
                Vector2 moveDir = nextPlayerMoveAction.Destination - playerPos.LocalPosition;
                float timeTick = _gameState.GetSecondsPassedDuringMovement(playerStats, nextPlayerMoveAction.IsRunning, default, moveDir, true);

                // PassTime now calculates the real-world duration internally.
                _worldClockManager.PassTime(timeTick, 0);

                // Now, update progress and execute moves for ALL entities based on that fixed time tick.
                foreach (var entityId in _gameState.ActiveEntities)
                {
                    var progressComp = _componentStore.GetComponent<MovementProgressComponent>(entityId);
                    var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);

                    if (progressComp == null)
                    {
                        progressComp = new MovementProgressComponent();
                        _componentStore.AddComponent(entityId, progressComp);
                    }
                    if (statsComp == null) continue;

                    if (entityId == _gameState.PlayerEntityId)
                    {
                        // The player always moves exactly one step per tick they initiate.
                        progressComp.Progress = 1.0f;
                    }
                    else // It's an AI
                    {
                        var intent = _componentStore.GetComponent<AIIntentComponent>(entityId);
                        bool isRunning = (intent?.CurrentIntent == AIIntent.Pursuing || intent?.CurrentIntent == AIIntent.Fleeing);
                        if (isRunning && !statsComp.CanExertEnergy(1))
                        {
                            isRunning = false;
                        }

                        _aiSystem ??= ServiceLocator.Get<AISystem>();
                        var aiPos = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                        var aiPathComp = _componentStore.GetComponent<AIPathComponent>(entityId);
                        Vector2? aiNextStepPeek = aiPathComp.HasPath() ? aiPathComp.Path[aiPathComp.CurrentPathIndex] : _componentStore.GetComponent<AIComponent>(entityId)?.NextStep;


                        if (aiPos != null && aiNextStepPeek.HasValue)
                        {
                            Vector2 aiMoveDir = aiNextStepPeek.Value - aiPos.LocalPosition;
                            float aiStepTime = _gameState.GetSecondsPassedDuringMovement(statsComp, isRunning, default, aiMoveDir, true);

                            if (aiStepTime > 0)
                            {
                                progressComp.Progress += timeTick / aiStepTime;
                            }
                        }
                    }

                    // Execute all full moves the entity has accumulated.
                    while (progressComp.Progress >= 1.0f)
                    {
                        progressComp.Progress -= 1.0f;
                        Vector2? nextStep = null;
                        bool isRunning = false;

                        if (entityId == _gameState.PlayerEntityId)
                        {
                            if (playerActionQueue.ActionQueue.TryDequeue(out IAction action) && action is MoveAction move)
                            {
                                nextStep = move.Destination;
                                isRunning = move.IsRunning;
                            }
                        }
                        else
                        {
                            nextStep = _aiSystem.GetNextStepForExecution(entityId);
                            var intent = _componentStore.GetComponent<AIIntentComponent>(entityId);
                            isRunning = (intent?.CurrentIntent == AIIntent.Pursuing || intent?.CurrentIntent == AIIntent.Fleeing);
                        }

                        if (nextStep.HasValue)
                        {
                            ExecuteNextMove(entityId, nextStep.Value, isRunning);
                        }
                    }
                }
            }

            // After all moves for this tick have been processed, check if combat should start.
            CheckForCombatInitiation();
        }

        private void ExecuteNextMove(int entityId, Vector2 nextStep, bool isRunning)
        {
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
            if (localPosComp != null && statsComp != null)
            {
                if (isRunning && statsComp.CanExertEnergy(1))
                {
                    statsComp.ExertEnergy(1);
                }
                else
                {
                    isRunning = false; // Ensure we don't run without energy
                }

                Vector2 moveDir = nextStep - localPosComp.LocalPosition;
                float timeCostOfStep = _gameState.GetSecondsPassedDuringMovement(statsComp, isRunning, default, moveDir, true);

                // Pass the IN-GAME time cost to the interpolation component.
                var interp = new InterpolationComponent(localPosComp.LocalPosition, nextStep, timeCostOfStep, isRunning);
                _componentStore.AddComponent(entityId, interp);
            }
        }

        /// <summary>
        /// Checks if the player has moved into the attack range of any hostile AI.
        /// If so, it requests that combat be initiated.
        /// </summary>
        private void CheckForCombatInitiation()
        {
            _aiSystem ??= ServiceLocator.Get<AISystem>();

            foreach (var entityId in _gameState.ActiveEntities)
            {
                if (entityId == _gameState.PlayerEntityId) continue;

                var personality = _componentStore.GetComponent<AIPersonalityComponent>(entityId);
                var combatant = _componentStore.GetComponent<CombatantComponent>(entityId);

                bool isHostile = personality != null && combatant != null &&
                                 (personality.Personality == AIPersonalityType.Aggressive ||
                                  (personality.Personality == AIPersonalityType.Neutral && personality.IsProvoked));

                if (isHostile)
                {
                    float distance = _aiSystem.GetTrueLocalDistance(entityId, _gameState.PlayerEntityId);
                    // Use the ceiling of the attack range for the check.
                    if (distance <= Math.Ceiling(combatant.AttackRange))
                    {
                        _gameState.RequestCombatInitiation(entityId);
                        // We can break here because once one AI initiates combat, the process will handle everything else.
                        return;
                    }
                }
            }
        }
    }
}
