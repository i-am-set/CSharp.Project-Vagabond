using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A system that safely handles requests to initiate combat. It waits until the player
    /// is in an idle state before officially starting the combat sequence, preventing
    /// logical deadlocks and state conflicts.
    /// </summary>
    public class CombatInitiationSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;

        public CombatInitiationSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Update(GameTime gameTime)
        {
            if (!_gameState.IsCombatInitiationPending)
            {
                return;
            }

            // Do not start combat if the player is still executing a path or visually moving.
            if (_gameState.IsExecutingActions || _componentStore.HasComponent<InterpolationComponent>(_gameState.PlayerEntityId))
            {
                return;
            }

            // It's now safe to start combat.
            _gameState.InitiateCombat(_gameState.PendingCombatants.ToList());
            _gameState.ClearCombatInitiationRequest();
        }
    }
}