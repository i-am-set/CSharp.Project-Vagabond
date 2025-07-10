using Microsoft.Xna.Framework;

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

        public CombatProcessingSystem() { }

        /// <summary>
        /// Updates the system, processing the action of the current turn's entity.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void Update(GameTime gameTime)
        {
            // Lazy-load all dependencies on first use
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            _combatResolutionSystem ??= ServiceLocator.Get<CombatResolutionSystem>();
            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();

            if (!_gameState.IsInCombat) return;

            int entityId = _gameState.CurrentTurnEntityId;
            bool actionWasProcessed = false;

            // Check for and process a chosen attack
            var chosenAttack = _componentStore.GetComponent<ChosenAttackComponent>(entityId);
            if (chosenAttack != null)
            {
                _combatResolutionSystem.ResolveAction(entityId, chosenAttack);
                _componentStore.RemoveComponent<ChosenAttackComponent>(entityId);
                actionWasProcessed = true;
            }
            else // An entity can't move and attack in the same turn
            {
                // Check for and process a move action
                var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                if (actionQueue != null && actionQueue.ActionQueue.Count > 0)
                {
                    if (actionQueue.ActionQueue.Peek() is MoveAction moveAction)
                    {
                        actionQueue.ActionQueue.Dequeue();
                        ApplyMoveActionEffects(entityId, moveAction);
                        actionWasProcessed = true;
                    }
                }
            }

            // If the current entity took an action, their turn is over.
            if (actionWasProcessed)
            {
                _combatTurnSystem.EndCurrentTurn();
            }
        }

        /// <summary>
        /// Applies the effects of a move action for a given entity.
        /// Simplified for local, in-combat movement.
        /// </summary>
        /// <param name="entityId">The ID of the entity to move.</param>
        /// <param name="action">The move action to apply.</param>
        private void ApplyMoveActionEffects(int entityId, MoveAction action)
        {
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            Vector2 nextPosition = action.Destination;

            localPosComp.LocalPosition = new Vector2(
                MathHelper.Clamp(nextPosition.X, 0, Global.LOCAL_GRID_SIZE - 1),
                MathHelper.Clamp(nextPosition.Y, 0, Global.LOCAL_GRID_SIZE - 1)
            );
        }
    }
}