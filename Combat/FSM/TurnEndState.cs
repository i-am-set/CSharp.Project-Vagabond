using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles end-of-turn effects and checks for victory or defeat conditions.
    /// </summary>
    public class TurnEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var currentEntityId = combatManager.CurrentTurnEntityId;
            string entityName = (currentEntityId == gameState.PlayerEntityId) ? "Player" : currentEntityId.ToString();

            var componentStore = ServiceLocator.Get<ComponentStore>();
            var deckComp = componentStore.GetComponent<CombatDeckComponent>(currentEntityId);

            if (deckComp != null)
            {
                // --- Discard Phase ---
                // Move all cards from hand to discard pile. Temporary cards are not in the hand list.
                deckComp.DiscardPile.AddRange(deckComp.Hand);
                deckComp.Hand.Clear();
                Debug.WriteLine($"    ... Discarded {deckComp.DiscardPile.Count} cards for Entity {currentEntityId}.");
            }

            // Clear any temporary actions created for this turn.
            combatManager.ClearTemporaryActions();

            Debug.WriteLine($"  --- Turn End: Entity {entityName} ---");

            // 1. Check for win/loss conditions
            // TODO: Implement logic to check if all enemies or the player are defeated.
            bool isCombatOver = false; // Placeholder

            if (isCombatOver)
            {
                combatManager.FSM.ChangeState(new CombatEndState(), combatManager);
            }
            else
            {
                // 2. Clear the actions from the completed turn.
                combatManager.ClearActionsForTurn();

                // 3. Advance to the next combatant's turn
                combatManager.AdvanceTurn();
                var nextEntityId = combatManager.CurrentTurnEntityId;
                string nextEntityName = (nextEntityId == gameState.PlayerEntityId) ? "Player" : nextEntityId.ToString();
                Debug.WriteLine($"  ... Next up: Entity {nextEntityName}");

                // 4. Transition back to the start of the next turn.
                combatManager.FSM.ChangeState(new TurnStartState(), combatManager);
            }
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}