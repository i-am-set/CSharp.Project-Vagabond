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
        private WorldClockManager _worldClockManager; // NEW

        private float _actionDelayTimer = 0f;
        private const float ACTION_DELAY_SECONDS = 0.15f;
        private const float COMBAT_STEP_DURATION = 0.15f; // The visual duration of one step at 1x speed.

        public CombatProcessingSystem() { }

        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            _combatResolutionSystem ??= ServiceLocator.Get<CombatResolutionSystem>();
            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>(); // Lazy load

            if (!_gameState.IsInCombat) return;

            int currentEntityId = _gameState.CurrentTurnEntityId;

            if (_componentStore.HasComponent<InterpolationComponent>(currentEntityId))
            {
                return;
            }

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
                // We don't set the delay for moves, as the interpolation duration serves as the delay.
                if (!(nextAction is MoveAction))
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
            if (localPosComp != null)
            {
                float visualDuration = COMBAT_STEP_DURATION / _worldClockManager.TimeScale;
                var interp = new InterpolationComponent(localPosComp.LocalPosition, action.Destination, visualDuration, action.IsRunning);
                _componentStore.AddComponent(action.ActorId, interp);
            }
        }
    }
}