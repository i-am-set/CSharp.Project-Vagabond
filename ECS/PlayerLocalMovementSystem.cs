
using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// A system dedicated to processing the player's OUT-OF-COMBAT action queue
    /// when the player is on the LOCAL map. It handles one visual move at a time,
    /// driven by the InterpolationSystem, and passes small increments of world time.
    /// </summary>
    public class PlayerLocalMovementSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly WorldClockManager _worldClockManager;

        private const float BASE_STEP_DURATION = 0.1f;

        public PlayerLocalMovementSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
        }

        public void Update(GameTime gameTime)
        {
            // This system only runs when executing a local path out of combat.
            if (!_gameState.IsExecutingActions || _gameState.PathExecutionMapView != MapView.Local || _gameState.IsInCombat)
            {
                return;
            }

            int playerId = _gameState.PlayerEntityId;

            // If the player is currently moving visually, wait for it to finish.
            if (_componentStore.HasComponent<InterpolationComponent>(playerId))
            {
                return;
            }

            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(playerId);
            if (actionQueueComp == null)
            {
                _gameState.ToggleExecutingActions(false);
                return;
            }

            // If there are actions left, process the next one.
            if (actionQueueComp.ActionQueue.TryDequeue(out IAction nextAction))
            {
                if (nextAction is MoveAction moveAction)
                {
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(playerId);
                    if (localPosComp != null)
                    {
                        // Pass time for this single step.
                        Vector2 moveDir = moveAction.Destination - localPosComp.LocalPosition;
                        float timeCost = _gameState.GetSecondsPassedDuringMovement(_gameState.PlayerStats, moveAction.IsRunning, default, moveDir, true);
                        _worldClockManager.PassTime(timeCost);

                        float visualDuration = BASE_STEP_DURATION / _worldClockManager.TimeScale;
                        var interp = new InterpolationComponent(localPosComp.LocalPosition, moveAction.Destination, visualDuration, moveAction.IsRunning);
                        _componentStore.AddComponent(playerId, interp);
                    }
                }
            }
            else
            {
                // The queue is empty, so we are done.
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Local movement complete." });
            }
        }
    }
}