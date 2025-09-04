namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Defines whether an element is used for offensive (moves) or defensive (combatants) purposes.
    /// </summary>
    public enum ElementType
    {
        Offensive,
        Defensive
    }

    /// <summary>
    /// Represents a single element definition, loaded from a data file.
    /// </summary>
    public class ElementDefinition
    {
        /// <summary>
        /// The unique integer key for the element.
        /// </summary>
        public int ElementID { get; set; }

        /// <summary>
        /// The display name of the element (e.g., "Fire").
        /// </summary>
        public string ElementName { get; set; }

        /// <summary>
        /// The type of the element (Offensive or Defensive).
        /// </summary>
        public ElementType Type { get; set; }
    }

    /// <summary>
    /// Represents a single rule for how two elements interact, loaded from a data file.
    /// </summary>
    public class ElementalInteraction
    {
        /// <summary>
        /// The unique integer key for the interaction rule.
        /// </summary>
        public int InteractionID { get; set; }

        /// <summary>
        /// The foreign key to the attacking element's ElementID.
        /// </summary>
        public int AttackingElementID { get; set; }

        /// <summary>
        /// The foreign key to the defending element's ElementID.
        /// </summary>
        public int DefendingElementID { get; set; }

        /// <summary>
        /// The damage multiplier to apply for this interaction.
        /// </summary>
        public float Multiplier { get; set; }
    }
}