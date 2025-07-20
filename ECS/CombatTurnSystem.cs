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
        private StatusEffectSystem _statusEffectSystem;

        private int _currentTurnIndex = 0;

        public CombatTurnSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        /// <summary>
        /// Resets the turn manager and starts the first turn of combat.
        /// </summary>
        public void StartCombat()
        {
            _currentTurnIndex = 0;
            StartNewTurn();
        }

        /// <summary>
        /// Advances the combat to the next entity's turn.
        /// </summary>
        public void EndCurrentTurn()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            if (!_gameState.IsInCombat)
            {
                return;
            }

            _currentTurnIndex++;

            // Check if we've completed a full round.
            if (_currentTurnIndex >= _gameState.InitiativeOrder.Count)
            {
                _currentTurnIndex = 0; // Reset for the new round.
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = "[palette_yellow]New round begins." });
                // A combat round has a fixed, short real-world duration for its time-pass effect.
                _worldClockManager.PassTime(Global.COMBAT_TURN_DURATION_SECONDS, 0.5f);
            }

            StartNewTurn();
        }

        /// <summary>
        /// Centralized logic to begin a turn for the current entity in the initiative order.
        /// </summary>
        private void StartNewTurn()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _aiSystem ??= ServiceLocator.Get<AISystem>();
            _statusEffectSystem ??= ServiceLocator.Get<StatusEffectSystem>();

            // Update the GameState to reflect the new active entity for the turn.
            var newTurnEntityId = _gameState.InitiativeOrder[_currentTurnIndex];
            _gameState.SetCurrentTurnEntity(newTurnEntityId);

            // Process status effects at the start of the turn (e.g., poison damage).
            _statusEffectSystem.ProcessCombatTurnStart(newTurnEntityId);

            // Reset the turn-specific stats for the new entity.
            var turnStats = _componentStore.GetComponent<TurnStatsComponent>(newTurnEntityId);
            if (turnStats != null)
            {
                turnStats.HasPrimaryAction = true;
                turnStats.HasSecondaryAction = true;
                turnStats.MovementTimeUsedThisTurn = 0f;
            }
            else
            {
                // Safety: if an entity enters combat without this component, add it.
                var newTurnStats = new TurnStatsComponent();
                _componentStore.AddComponent(newTurnEntityId, newTurnStats);
            }

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
