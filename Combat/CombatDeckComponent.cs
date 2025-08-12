using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A component that manages an entity's deck of actions for combat.
    /// This is a runtime component, initialized at the start of combat.
    /// </summary>
    public class CombatDeckComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The list of action IDs available to be drawn.
        /// </summary>
        public List<string> DrawPile { get; set; } = new List<string>();

        /// <summary>
        /// The list of action IDs currently in the entity's hand.
        /// </summary>
        public List<string> Hand { get; set; } = new List<string>();

        /// <summary>
        /// The list of action IDs that have been used or discarded.
        /// </summary>
        public List<string> DiscardPile { get; set; } = new List<string>();

        public IComponent Clone()
        {
            // This is a runtime state component. Cloning creates a fresh, empty instance
            // which will be populated at the start of combat.
            return new CombatDeckComponent();
        }
    }
}