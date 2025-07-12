﻿using Microsoft.Xna.Framework;

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

            int currentEntityId = _gameState.CurrentTurnEntityId;

            // --- Player Turn Processing ---
            // If it's the player's turn, we only process an action if one has been chosen.
            // We do not end the turn automatically.
            if (currentEntityId == _gameState.PlayerEntityId)
            {
                var chosenAttack = _componentStore.GetComponent<ChosenAttackComponent>(currentEntityId);
                if (chosenAttack != null)
                {
                    // An action was selected via the UI. Process it.
                    _combatResolutionSystem.ResolveAction(currentEntityId, chosenAttack);
                    _componentStore.RemoveComponent<ChosenAttackComponent>(currentEntityId);

                    // After the action is resolved, return the UI to the default state
                    // so the player can choose another action or end their turn.
                    _gameState.UIState = CombatUIState.Default;
                }
            }
            // --- AI Turn Processing ---
            // If it's an AI's turn, we process its action and then immediately end its turn.
            else
            {
                bool actionWasProcessed = false;

                // Check for and process a chosen attack
                var chosenAttack = _componentStore.GetComponent<ChosenAttackComponent>(currentEntityId);
                if (chosenAttack != null)
                {
                    _combatResolutionSystem.ResolveAction(currentEntityId, chosenAttack);
                    _componentStore.RemoveComponent<ChosenAttackComponent>(currentEntityId);
                    actionWasProcessed = true;
                }
                else
                {
                    // Check for and process a move action
                    var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(currentEntityId);
                    if (actionQueue != null && actionQueue.ActionQueue.Count > 0)
                    {
                        if (actionQueue.ActionQueue.Peek() is MoveAction moveAction)
                        {
                            actionQueue.ActionQueue.Dequeue();
                            ApplyMoveActionEffects(currentEntityId, moveAction);
                            actionWasProcessed = true;
                        }
                    }
                }

                // An AI's turn always ends after its action (or lack thereof) is processed.
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
