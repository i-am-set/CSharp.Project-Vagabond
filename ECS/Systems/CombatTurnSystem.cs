
using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the sequence of turns during combat.
    /// This system is not driven by the main game loop timer, but by explicit calls to EndCurrentTurn().
    /// </summary>
    public class CombatTurnSystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private WorldClockManager _worldClockManager;
        private AISystem _aiSystem;

        private int _currentTurnIndex = 0;

        public CombatTurnSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

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
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();
            _aiSystem ??= ServiceLocator.Get<AISystem>();

            if (!_gameState.IsInCombat)
            {
                return;
            }

            _currentTurnIndex++;

            // Check if we've completed a full round.
            if (_currentTurnIndex >= _gameState.InitiativeOrder.Count)
            {
                _currentTurnIndex = 0; // Reset for the new round.
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[yellow]New round begins." });
                _worldClockManager.PassTime(seconds: GameState.COMBAT_TURN_DURATION_SECONDS);
            }

            // Update the GameState to reflect the new active entity for the next turn.
            var newTurnEntityId = _gameState.InitiativeOrder[_currentTurnIndex];
            _gameState.SetCurrentTurnEntity(newTurnEntityId);

            // Log whose turn it is now.
            var newTurnEntityName = EntityNamer.GetName(_gameState.CurrentTurnEntityId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"Turn: {newTurnEntityName}" });

            // If the new entity is an AI, tell it to take its turn.
            if (_componentStore.HasComponent<AIComponent>(newTurnEntityId))
            {
                _aiSystem.ProcessCombatTurn(newTurnEntityId);
            }
            else if (newTurnEntityId == _gameState.PlayerEntityId)
            {
                // It's the player's turn, reset their UI state to default
                _gameState.UIState = CombatUIState.Default;
            }
        }

        /// <summary>
        /// This system is not updated every frame, so this method is empty.
        /// </summary>
        public void Update(GameTime gameTime) { }
    }
}