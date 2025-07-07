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
                CombatLog.Log("[yellow]New round begins.");

                // CORRECTED LOGIC: Pass the time for the entire round only AFTER the last person has acted.
                Core.CurrentWorldClockManager.PassTime(seconds: GameState.COMBAT_TURN_DURATION_SECONDS);
            }

            // Update the GameState to reflect the new active entity for the next turn.
            gameState.SetCurrentTurnEntity(gameState.InitiativeOrder[_currentTurnIndex]);

            // Log whose turn it is now.
            var archetype = ArchetypeManager.Instance.GetArchetype(Core.ComponentStore.GetComponent<RenderableComponent>(gameState.CurrentTurnEntityId)?.Texture?.Name ?? "Unknown");
            var newTurnEntityName = archetype?.Name ?? $"Entity {gameState.CurrentTurnEntityId}";
            CombatLog.Log($"Turn: {newTurnEntityName}");
        }

        /// <summary>
        /// This system is not updated every frame, so this method is empty.
        /// </summary>
        public void Update(GameTime gameTime) { }
    }
}