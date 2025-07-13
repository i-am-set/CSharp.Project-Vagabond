namespace ProjectVagabond
{
    /// <summary>
    /// An action representing an entity attacking a target.
    /// </summary>
    public class AttackAction : IAction
    {
        public int ActorId { get; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// The entity ID of the target.
        /// </summary>
        public int TargetId { get; }

        /// <summary>
        /// The name of the attack, matching an entry in the attacker's AvailableAttacksComponent.
        /// </summary>
        public string AttackName { get; }

        public AttackAction(int actorId, int targetId, string attackName)
        {
            ActorId = actorId;
            TargetId = targetId;
            AttackName = attackName;
            IsComplete = false;
        }
    }
}