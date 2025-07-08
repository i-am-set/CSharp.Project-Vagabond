using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the sequence of turns during combat.
    /// This system is not driven by the main game loop timer, but by explicit calls to EndCurrentTurn().
    /// </summary>
    public class CombatTurnSystem : ISystem
    {
        private int _currentTurnIndex = 0;

        /// <summary>
        /// Resets the turn manager to the beginning of the initiative order.
        /// Called when combat starts.
        /// </summary>
        public void StartCombat()
        {
            _currentTurnIndex = 0;
        }

        /// <summary>
        /// Advances the combat to the next entity's turn.
        /// </summary>
        public void EndCurrentTurn()
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat)
            {
                return;
            }

            _currentTurnIndex++;

            // Check if we've completed a full round.
            if (_currentTurnIndex >= gameState.InitiativeOrder.Count)
            {
                _currentTurnIndex = 0; // Reset for the new round.
                Core.CurrentTerminalRenderer.AddCombatLog("[yellow]New round begins.");
                Core.CurrentWorldClockManager.PassTime(seconds: GameState.COMBAT_TURN_DURATION_SECONDS);
            }

            // Update the GameState to reflect the new active entity for the next turn.
            var newTurnEntityId = gameState.InitiativeOrder[_currentTurnIndex];
            gameState.SetCurrentTurnEntity(newTurnEntityId);

            // Log whose turn it is now.
            var newTurnEntityName = EntityNamer.GetName(gameState.CurrentTurnEntityId);
            Core.CurrentTerminalRenderer.AddCombatLog($"Turn: {newTurnEntityName}");

            // If the new entity is an AI, tell it to take its turn.
            if (Core.ComponentStore.HasComponent<AIComponent>(newTurnEntityId))
            {
                Core.AISystem.ProcessCombatTurn(newTurnEntityId);
            }
            else if (newTurnEntityId == gameState.PlayerEntityId)
            {
                // It's the player's turn, reset their UI state to default
                gameState.UIState = CombatUIState.Default;
            }
        }

        /// <summary>
        /// This system is not updated every frame, so this method is empty.
        /// </summary>
        public void Update(GameTime gameTime) { }
    }
}