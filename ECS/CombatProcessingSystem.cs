using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A centralized system that processes the chosen actions of the entity whose turn it is.
    /// This system unifies the action pipeline for all combatants.
    /// </summary>
    public class CombatProcessingSystem : ISystem
    {
        private GameState _gameState;
        private ComponentStore _componentStore;
        private CombatResolutionSystem _combatResolutionSystem;
        private CombatTurnSystem _combatTurnSystem;
        private WorldClockManager _worldClockManager;

        private float _actionDelayTimer = 0f;
        private const float ACTION_DELAY_SECONDS = 0.5f; // A standard delay between distinct actions.
        private const float VISUAL_SPEED_MULTIPLIER = 0.8f; // Makes animations slightly faster than the raw time cost.

        public CombatProcessingSystem() { }

        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            _combatResolutionSystem ??= ServiceLocator.Get<CombatResolutionSystem>();
            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            if (!_gameState.IsInCombat) return;

            int currentEntityId = _gameState.CurrentTurnEntityId;

            // If the entity is currently moving, wait for the animation to finish.
            // This takes priority over the action delay timer.
            if (_componentStore.HasComponent<InterpolationComponent>(currentEntityId))
            {
                return;
            }

            // If we are in a post-action delay, count it down.
            if (_actionDelayTimer > 0)
            {
                _actionDelayTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                return;
            }

            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(currentEntityId);

            if (actionQueue != null && actionQueue.ActionQueue.TryDequeue(out IAction nextAction))
            {
                bool isAI = _componentStore.HasComponent<AIComponent>(currentEntityId);

                // Process the dequeued action
                switch (nextAction)
                {
                    case MoveAction moveAction:
                        ApplyMoveActionEffects(moveAction);
                        break;

                    case AttackAction attackAction:
                        _combatResolutionSystem.ResolveAction(attackAction);
                        break;

                    case EndTurnAction:
                        _combatTurnSystem.EndCurrentTurn();
                        break;
                }

                // A delay is needed unless the action just processed was a move
                // AND the next action in the queue is also a move.
                bool isConsecutiveMove = false;
                if (nextAction is MoveAction)
                {
                    if (actionQueue.ActionQueue.TryPeek(out IAction peekedAction) && peekedAction is MoveAction)
                    {
                        isConsecutiveMove = true;
                    }
                }

                if (!isConsecutiveMove)
                {
                    _actionDelayTimer = ACTION_DELAY_SECONDS;
                }


                // After processing an action, check if the queue is now empty.
                if (!actionQueue.ActionQueue.Any())
                {
                    // If it's the player's turn and their queue is empty,
                    // it means their move/attack sequence is done. Return control to them.
                    if (!isAI)
                    {
                        _gameState.UIState = CombatUIState.Default;
                    }
                }
            }
            else if (_componentStore.HasComponent<AIComponent>(currentEntityId))
            {
                // Safety net: If an AI's queue is somehow empty at the start of its processing, end its turn.
                _combatTurnSystem.EndCurrentTurn();
            }
        }

        private void ApplyMoveActionEffects(MoveAction action)
        {
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(action.ActorId);
            var statsComp = _componentStore.GetComponent<StatsComponent>(action.ActorId);

            if (localPosComp != null && statsComp != null)
            {
                if (action.Mode != MovementMode.Walk)
                {
                    statsComp.ExertEnergy(1);
                }

                // Calculate the actual in-game time this single step will take.
                Vector2 moveDir = action.Destination - localPosComp.LocalPosition;
                float timeCostOfStep = _gameState.GetSecondsPassedDuringMovement(statsComp, action.Mode, default, moveDir, true);

                // The visual duration is proportional to the in-game time cost, scaled by the world clock.
                float visualDuration = (timeCostOfStep / _worldClockManager.TimeScale) * VISUAL_SPEED_MULTIPLIER;

                var interp = new InterpolationComponent(localPosComp.LocalPosition, action.Destination, visualDuration, action.Mode);
                _componentStore.AddComponent(action.ActorId, interp);
            }
        }
    }
}