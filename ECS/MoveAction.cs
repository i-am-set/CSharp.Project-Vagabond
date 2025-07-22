using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Defines the different modes of movement available.
    /// </summary>
    public enum MovementMode
    {
        Walk,
        Jog,
        Run
    }

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
        /// The mode of movement (Walk, Jog, Run).
        /// </summary>
        public MovementMode Mode { get; }

        public MoveAction(int actorId, Vector2 destination, MovementMode mode)
        {
            ActorId = actorId;
            Destination = destination;
            Mode = mode;
            IsComplete = false;
        }
    }
}