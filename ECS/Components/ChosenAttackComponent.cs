namespace ProjectVagabond
{
    /// <summary>
    /// A temporary component that holds the attack an entity has decided to make this turn.
    /// </summary>
    public class ChosenAttackComponent : IComponent
    {
        /// <summary>
        /// The entity ID of the target.
        /// </summary>
        public int TargetId { get; set; }

        /// <summary>
        /// The name of the attack, matching an entry in the attacker's AvailableAttacksComponent.
        /// </summary>
        public string AttackName { get; set; }
    }
}