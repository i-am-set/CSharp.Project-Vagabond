namespace ProjectVagabond
{
    /// <summary>
    /// Represents a resting action for an entity.
    /// </summary>
    public class RestAction : IAction
    {
        /// <inheritdoc />
        public int ActorId { get; }

        /// <inheritdoc />
        public bool IsComplete { get; set; }

        /// <summary>
        /// Gets the type of rest to perform (Short, Long, Full).
        /// </summary>
        public RestType RestType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RestAction"/> class.
        /// </summary>
        /// <param name="actorId">The ID of the entity that will rest.</param>
        /// <param name="restType">The type of rest to perform.</param>
        public RestAction(int actorId, RestType restType)
        {
            ActorId = actorId;
            RestType = restType;
            IsComplete = false;
        }
    }
}