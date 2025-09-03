using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes the player's OUT-OF-COMBAT action queue, executing one action at a time
    /// and applying its effects to the game state. This system drives the game clock.
    /// </summary>
    public class ActionExecutionSystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private WorldClockManager _worldClockManager;
        private readonly ChunkManager _chunkManager;

        // Timer for the new tick-based movement system
        private float _moveTickTimer = 0f;
        private bool _isFirstActionInQueue = true;

        public ActionExecutionSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
        }

        public void StartExecution()
        {
            _isFirstActionInQueue = true;
        }


        public void StopExecution()
        {
            _isFirstActionInQueue = true;
        }

        /// <summary>
        /// Called when an action queue is forcefully stopped mid-execution.
        /// </summary>
        public void HandleInterruption()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            int playerEntityId = _gameState.PlayerEntityId;
            _componentStore.RemoveComponent<MoveAction>(playerEntityId);
            _componentStore.RemoveComponent<RestAction>(playerEntityId);
        }

        /// <summary>
        /// Updates the action system, processing actions for the player out of combat.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            if (_gameState.IsPaused || !_gameState.IsExecutingActions)
            {
                return;
            }

            // Calculate the duration of the current tick based on the time scale.
            float currentTickDuration = Global.ACTION_TICK_DURATION_SECONDS / _worldClockManager.TimeScale;

            // If this is the first action in the queue, execute it immediately without a timer.
            if (_isFirstActionInQueue && _gameState.PendingActions.Any())
            {
                ProcessNextActionInQueue(currentTickDuration);
                _isFirstActionInQueue = false;
                _moveTickTimer = 0f; // Reset the timer so the *next* action has the full delay.
                return;
            }

            // For all subsequent actions, wait for the tick timer.
            _moveTickTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_moveTickTimer >= currentTickDuration)
            {
                _moveTickTimer -= currentTickDuration; // Decrement, keeping any leftover time for the next frame.
                ProcessNextActionInQueue(currentTickDuration);
            }
        }

        private void ProcessNextActionInQueue(float tickDuration)
        {
            int playerEntityId = _gameState.PlayerEntityId;
            var playerActionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(playerEntityId);

            if (playerActionQueueComp != null && playerActionQueueComp.ActionQueue.Count > 0)
            {
                IAction nextAction = playerActionQueueComp.ActionQueue.Dequeue();
                float actionCostInGameSeconds = CalculateSecondsForAction(_gameState, nextAction);

                ActivityType activity = ActivityType.Waiting;
                if (nextAction is MoveAction ma)
                {
                    activity = ma.Mode switch
                    {
                        MovementMode.Walk => ActivityType.Walking,
                        MovementMode.Jog => ActivityType.Jogging,
                        MovementMode.Run => ActivityType.Running,
                        _ => ActivityType.Waiting
                    };
                    ApplyMoveActionEffects(_gameState, playerEntityId, ma);
                }
                else if (nextAction is RestAction ra)
                {
                    activity = ActivityType.Waiting;
                    ApplyRestActionEffects(_gameState, playerEntityId, ra);
                }

                // The clock's visual animation will last for the duration of this tick.
                _worldClockManager.PassTime(actionCostInGameSeconds, tickDuration, activity);
            }
            else if (_gameState.IsExecutingActions)
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Action queue completed." });
            }
        }

        private float CalculateSecondsForAction(GameState gameState, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var previousPosition = gameState.PlayerWorldPos;
                var mapData = gameState.GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                Vector2 moveDirection = moveAction.Destination - previousPosition;
                var playerStats = gameState.PlayerStats;
                if (playerStats == null) return 0;

                return gameState.GetSecondsPassedDuringMovement(playerStats, moveAction.Mode, mapData, moveDirection);
            }
            else if (action is RestAction restAction)
            {
                switch (restAction.RestType)
                {
                    case RestType.ShortRest: return gameState.PlayerStats.ShortRestDuration * 60;
                    case RestType.LongRest: return gameState.PlayerStats.LongRestDuration * 60;
                    case RestType.FullRest: return gameState.PlayerStats.FullRestDuration * 60;
                }
            }
            return 0;
        }

        private void ApplyMoveActionEffects(GameState gameState, int entityId, MoveAction action)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            int energyCost = gameState.GetMovementEnergyCost(action);
            stats?.ExertEnergy(energyCost);

            var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
            Vector2 oldWorldPos = posComp.WorldPosition;
            posComp.WorldPosition = action.Destination;

            _chunkManager.UpdateEntityChunk(entityId, oldWorldPos, action.Destination);
            EventBus.Publish(new GameEvents.PlayerMoved { NewPosition = action.Destination });

        }

        private void ApplyRestActionEffects(GameState gameState, int entityId, RestAction action)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            stats.Rest(action.RestType);
            string restType = action.RestType.ToString().Replace("Rest", "");
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[rest]Completed {restType.ToLower()} rest. Energy is now {stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}." });
        }
    }
}