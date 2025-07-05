using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Represents a movement action for an entity to a specific destination.
    /// </summary>
    public class MoveAction : IAction
    {
        /// <inheritdoc />
        public int ActorId { get; }

        /// <inheritdoc />
        public bool IsComplete { get; set; }

        /// <summary>
        /// Gets the target destination for the movement.
        /// </summary>
        public Vector2 Destination { get; }

        /// <summary>
        /// Gets a value indicating whether the movement is a run.
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveAction"/> class.
        /// </summary>
        /// <param name="actorId">The ID of the entity that will move.</param>
        /// <param name="destination">The target world or local coordinate.</param>
        /// <param name="isRunning">True if the entity should run; otherwise, false.</param>
        public MoveAction(int actorId, Vector2 destination, bool isRunning)
        {
            ActorId = actorId;
            Destination = destination;
            IsRunning = isRunning;
            IsComplete = false;
        }
    }
}