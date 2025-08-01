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

        public CombatProcessingSystem() { }

        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _componentStore ??= ServiceLocator.Get<ComponentStore>();
            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            // Lazy-subscribe to the event to avoid initialization order issues.
            if (_combatResolutionSystem == null)
            {
                _combatResolutionSystem = ServiceLocator.Get<CombatResolutionSystem>();
                _combatResolutionSystem.OnAttackResolved += OnAttackResolved;
            }

            if (!_gameState.IsInCombat) return;

            // The body of this method has been emptied as per the refactoring brief.
            // It no longer processes any actions.
        }

        private void OnAttackResolved()
        {
            // This callback is still required to be subscribed to, but its functionality is now empty.
        }
    }
}