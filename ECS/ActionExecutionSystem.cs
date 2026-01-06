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

        // Timer for the gameplay tick-based action processing
        private float _moveTickTimer = 0f;
        private bool _isFirstActionInQueue = true;

        // State for visual movement animation
        private bool _isAnimatingMove = false;
        private float _animationTimer = 0f;
        private const float ANIMATION_DURATION = 0.025f;
        private Vector2 _animationStartPosition;
        private Vector2 _animationEndPosition;

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
            _isAnimatingMove = false; // Stop any ongoing animation
            var renderPosComp = _componentStore.GetComponent<RenderPositionComponent>(playerEntityId);
            var posComp = _componentStore.GetComponent<PositionComponent>(playerEntityId);
            if (renderPosComp != null && posComp != null)
            {
                // Snap visual position to logical position on interruption
                renderPosComp.WorldPosition = posComp.WorldPosition;
            }
        }

        /// <summary>
        /// Updates the action system, processing one action per frame from the player's queue.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            // Handle visual animation interpolation
            if (_isAnimatingMove)
            {
                _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_animationTimer / ANIMATION_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);

                var renderPosComp = _componentStore.GetComponent<RenderPositionComponent>(_gameState.PlayerEntityId);
                if (renderPosComp != null)
                {
                    renderPosComp.WorldPosition = Vector2.Lerp(_animationStartPosition, _animationEndPosition, easedProgress);
                }

                if (progress >= 1.0f)
                {
                    _isAnimatingMove = false;
                    if (renderPosComp != null)
                    {
                        renderPosComp.WorldPosition = _animationEndPosition; // Snap to final position
                    }
                }
            }

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
                    var renderPosComp = _componentStore.GetComponent<RenderPositionComponent>(playerEntityId);
                    if (renderPosComp != null)
                    {
                        // Set up animation state
                        _animationStartPosition = renderPosComp.WorldPosition;
                        _animationEndPosition = ma.Destination;
                        _animationTimer = 0f;
                        _isAnimatingMove = true;
                    }

                    // The logical move still happens instantly
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