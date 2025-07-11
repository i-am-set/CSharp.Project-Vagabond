using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// An action representing an entity moving to a new position.
    /// </summary>
    public class MoveAction : IAction
    {
        public int ActorId { get; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// The target destination of the move.
        /// </summary>
        public Vector2 Destination { get; }

        /// <summary>
        /// A flag indicating if the movement is a run.
        /// </summary>
        public bool IsRunning { get; }

        public MoveAction(int actorId, Vector2 destination, bool isRunning)
        {
            ActorId = actorId;
            Destination = destination;
            IsRunning = isRunning;
            IsComplete = false;
        }
    }
}