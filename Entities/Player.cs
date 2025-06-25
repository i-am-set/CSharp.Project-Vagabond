using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    public class Player : Entity
    {
        public PlayerStats Stats { get; }

        public Player(Vector2 initialPosition)
            : base("Player", EntityType.Creature, initialPosition)
        {
            Stats = new PlayerStats(5, 5, 5, 5, 5);
        }

        /// <summary>
        /// The player's actions are executed directly by the GameState's UpdateMovement method,
        /// which processes one action at a time from the queue. This method is a placeholder
        /// for the player entity but the core logic remains in GameState for now to handle
        /// the step-by-step execution tied to user input and screen updates.
        /// </summary>
        public override void Update(int secondsPassed, GameState gameState)
        {
            // Player's passive time-based updates could go here in the future (e.g., hunger, passive healing).
            // For now, active actions are handled by GameState.SimulateWorldTick.
        }
    }
}