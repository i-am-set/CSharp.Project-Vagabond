using Microsoft.Xna.Framework;
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
                            float initiationRange = (float)Math.Ceiling(combatant.AttackRange) + 1;
                            if (distanceToPlayerNextStep <= initiationRange)
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


            // The time tick is now determined solely by the player's next action.
            var playerStats = _componentStore.GetComponent<StatsComponent>(_gameState.PlayerEntityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerStats == null || playerPos == null)
            {
                _gameState.ToggleExecutingActions(false); // Failsafe
                return;
            }

            if (playerActionQueue.ActionQueue.TryDequeue(out IAction action) && action is MoveAction nextPlayerMoveAction)
            {
                Vector2 moveDir = nextPlayerMoveAction.Destination - playerPos.LocalPosition;
                float timeTick = _gameState.GetSecondsPassedDuringMovement(playerStats, nextPlayerMoveAction.Mode, default, moveDir, true);

                ActivityType activity = nextPlayerMoveAction.Mode switch
                {
                    MovementMode.Walk => ActivityType.Walking,
                    MovementMode.Jog => ActivityType.Jogging,
                    MovementMode.Run => ActivityType.Running,
                    _ => ActivityType.Walking
                };

                // Pass time, which will give AI their action budget via the OnTimePassed event.
                _worldClockManager.PassTime(timeTick, 0, activity);

                // Execute the player's move.
                ExecuteNextMove(_gameState.PlayerEntityId, nextPlayerMoveAction.Destination, nextPlayerMoveAction.Mode);

                // Announce the player's new position to all listeners (like the AI system).
                EventBus.Publish(new GameEvents.PlayerMoved { NewPosition = nextPlayerMoveAction.Destination, Map = MapView.Local });
            }

            // After the player's move has been processed, check if combat should start.
            CheckForCombatInitiation();
        }

        private void ExecuteNextMove(int entityId, Vector2 nextStep, MovementMode mode)
        {
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
            if (localPosComp != null && statsComp != null)
            {
                int energyCost = _gameState.GetMovementEnergyCost(new MoveAction(entityId, nextStep, mode), true);
                if (statsComp.CanExertEnergy(energyCost))
                {
                    statsComp.ExertEnergy(energyCost);
                }
                else
                {
                    // If the AI intended to run but can't, downgrade to jog.
                    if (mode == MovementMode.Run)
                    {
                        mode = MovementMode.Jog;
                    }
                }

                Vector2 moveDir = nextStep - localPosComp.LocalPosition;
                float timeCostOfStep = _gameState.GetSecondsPassedDuringMovement(statsComp, mode, default, moveDir, true);

                // Pass the IN-GAME time cost to the interpolation component.
                var interp = new InterpolationComponent(localPosComp.LocalPosition, nextStep, timeCostOfStep, mode);
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
                    float initiationRange = (float)Math.Ceiling(combatant.AttackRange) + 1;
                    if (distance <= initiationRange)
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