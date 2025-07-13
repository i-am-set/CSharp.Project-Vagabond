namespace ProjectVagabond
{
    /// <summary>
    /// A marker action that signifies the end of an entity's turn.
    /// </summary>
    public class EndTurnAction : IAction
    {
        public int ActorId { get; }
        public bool IsComplete { get; set; }

        public EndTurnAction(int actorId)
        {
            ActorId = actorId;
            IsComplete = false;
        }
    }
}