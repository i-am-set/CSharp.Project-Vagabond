using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Entities
{
    public class example_Enemy : Entity
    {
        private enum BanditState { Patrolling, ChasingPlayer }
        private BanditState _currentState = BanditState.Patrolling;

        public example_Enemy(Vector2 initialPosition, List<Vector2> patrolPoints)
            : base("Bandit", EntityType.Creature, initialPosition)
        {
        }

        public override void Update(int minutesPassed, GameState gameState)
        {
            float distanceToPlayer = Vector2.Distance(WorldPosition, gameState.PlayerWorldPos);
            if (distanceToPlayer < 10)
            {
                _currentState = BanditState.ChasingPlayer;
            }
            else
            {
                _currentState = BanditState.Patrolling;
            }

            // State-execution logic
            switch (_currentState)
            {
                case BanditState.Patrolling:
                    // Run the patrol logic from the example above
                    break;
                case BanditState.ChasingPlayer:
                    // Implement logic to move towards gameState.PlayerWorldPos
                    break;
            }
        }
    }
}
