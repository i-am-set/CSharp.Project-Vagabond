namespace ProjectVagabond
{
    /// <summary>
    /// Base interface for all actions an entity can perform.
    /// An action is a component that represents a single, temporary task.
    /// </summary>
    public interface IAction : IComponent
    {
        /// <summary>
        /// The ID of the entity performing the action.
        /// </summary>
        public int ActorId { get; }

        /// <summary>
        /// A flag indicating if the action has been completed.
        /// </summary>
        public bool IsComplete { get; set; }
    }
}