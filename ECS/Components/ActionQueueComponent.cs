using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds the queue of actions for an entity to perform.
    /// </summary>
    public class ActionQueueComponent : IComponent
    {
        public List<PendingAction> ActionQueue { get; } = new List<PendingAction>();
    }
}