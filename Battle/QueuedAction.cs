namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents a single, chosen action waiting to be executed in the action queue.
    /// It captures the state of the actor and target at the moment the action is chosen.
    /// </summary>
    public class QueuedAction
    {
        /// <summary>
        /// The combatant performing the action.
        /// </summary>
        public BattleCombatant Actor { get; set; }

        /// <summary>
        /// The move that was selected.
        /// </summary>
        public MoveData ChosenMove { get; set; }

        /// <summary>
        /// The combatant being targeted by the move.
        /// </summary>
        public BattleCombatant Target { get; set; }

        /// <summary>
        /// A copy of the move's priority, used for sorting the action queue.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// A copy of the actor's agility, used for tie-breaking in the action queue.
        /// </summary>
        public int ActorAgility { get; set; }
    }
}