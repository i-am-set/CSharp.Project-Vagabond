namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents an action that is queued to execute on a future turn.
    /// </summary>
    public class DelayedAction
    {
        public QueuedAction Action { get; set; }
        public int TurnsRemaining { get; set; }
    }
}