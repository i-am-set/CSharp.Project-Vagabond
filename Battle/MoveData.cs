using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a single combat move.
    /// </summary>
    public class MoveData
    {
        /// <summary>
        /// A unique string identifier for the move (e.g., "Tackle", "Fireball").
        /// </summary>
        public string MoveID { get; set; }

        /// <summary>
        /// The display name of the move.
        /// </summary>
        public string MoveName { get; set; }

        /// <summary>
        /// The base power of the move, used in damage calculation.
        /// </summary>
        public int Power { get; set; }

        /// <summary>
        /// Whether the move is Physical or Magical.
        /// </summary>
        public DamageType DamageType { get; set; }

        /// <summary>
        /// The base accuracy of the move (1-100). A value of -1 represents a "True Hit" that never misses.
        /// </summary>
        public int Accuracy { get; set; }

        /// <summary>
        /// The move's priority for sorting the action queue. Higher values go first.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// A list of element IDs associated with this move's attack type.
        /// </summary>
        public List<int> OffensiveElementIDs { get; set; } = new List<int>();
    }
}