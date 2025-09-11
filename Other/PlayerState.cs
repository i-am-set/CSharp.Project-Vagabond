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
        /// A list of MoveIDs for the combat actions the player currently has available.
        /// This list can be modified at runtime to learn or forget moves.
        /// </summary>
        public List<string> CurrentActionMoveIDs { get; set; } = new List<string>();

        // Future properties like inventory, stats, quest flags, etc., would go here.
    }
}