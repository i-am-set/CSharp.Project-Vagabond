using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// A system dedicated to processing the player's OUT-OF-COMBAT action queue
    /// when the player is on the LOCAL map. It handles one visual move at a time,
    /// driven by the InterpolationSystem, and passes small increments of world time.
    /// </summary>
    public class PlayerLocalMovementSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly WorldClockManager _worldClockManager;

        private const float BASE_STEP_DURATION = 0.15f;

        public PlayerLocalMovementSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
        }

        public void Update(GameTime gameTime)
        {
            // This system is now obsolete and has been replaced by LocalMapTurnSystem.
            // The logic is kept here for reference but the system is no longer registered in Core.cs.
            return;
        }
    }
}
