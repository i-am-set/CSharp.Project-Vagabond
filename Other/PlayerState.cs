using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds all dynamic, persistent data for the player character.
    /// This acts as the single source of truth for the player's state,
    /// separate from their static archetype definition.
    /// </summary>
    public class PlayerState
    {
        /// <summary>
        /// Represents the player's spellbook. The size of the list is the number of
        /// spell pages the player has. A null or empty string indicates an empty page.
        /// </summary>
        public List<string> SpellbookPages { get; set; } = new List<string>();

        // Future properties like inventory, stats, quest flags, etc., would go here.
    }
}