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

            int playerEntityId = _gameState.PlayerEntityId;
            _componentStore.RemoveComponent<MoveAction>(playerEntityId);
        }

        /// <summary>
        /// Updates the action system, processing one action per frame from the player's queue.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            if (_gameState.IsPaused || !_gameState.IsExecutingActions)
            {
                return;
            }

            // If this is the first action in the queue, execute it immediately without a timer.
            if (_isFirstActionInQueue && _gameState.PendingActions.Any())
            {
                ProcessNextActionInQueue();
                _isFirstActionInQueue = false;
                _moveTickTimer = 0f; // Reset the timer so the *next* action has the full delay.
                return;
            }

            // For all subsequent actions, wait for the tick timer.
            _moveTickTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_moveTickTimer >= Global.ACTION_TICK_DURATION_SECONDS)
            {
                _moveTickTimer -= Global.ACTION_TICK_DURATION_SECONDS; // Decrement, keeping any leftover time for the next frame.
                ProcessNextActionInQueue();
            }
        }

        private void ProcessNextActionInQueue()
        {
            int playerEntityId = _gameState.PlayerEntityId;
            var playerActionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(playerEntityId);

            if (playerActionQueueComp != null && playerActionQueueComp.ActionQueue.Count > 0)
            {
                IAction nextAction = playerActionQueueComp.ActionQueue.Dequeue();

                if (nextAction is MoveAction ma)
                {
                    ApplyMoveActionEffects(_gameState, playerEntityId, ma);
                }

                // Signal that the player has completed an action, allowing other systems (like AI) to take a turn.
                EventBus.Publish(new GameEvents.PlayerActionExecuted { Action = nextAction });
            }
            else if (_gameState.IsExecutingActions) // Queue is empty, stop executing
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Action queue completed." });
            }
        }

        private void ApplyMoveActionEffects(GameState gameState, int entityId, MoveAction action)
        {
            var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
            Vector2 oldWorldPos = posComp.WorldPosition;
            posComp.WorldPosition = action.Destination;

            _chunkManager.UpdateEntityChunk(entityId, oldWorldPos, action.Destination);
            EventBus.Publish(new GameEvents.PlayerMoved { NewPosition = action.Destination });

        }
    }
}