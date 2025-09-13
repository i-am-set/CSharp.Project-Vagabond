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
        /// Flavor text or a brief explanation of the move's effects.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The base power of the move, used in damage calculation.
        /// </summary>
        public int Power { get; set; }

        /// <summary>
        /// The fundamental type of the move, distinguishing magical spells from physical actions.
        /// </summary>
        public MoveType MoveType { get; set; }

        /// <summary>
        /// The type of damage the move inflicts upon impact.
        /// </summary>
        public ImpactType ImpactType { get; set; }

        /// <summary>
        /// A boolean indicating whether the move requires the user to make physical contact with the target.
        /// </summary>
        public bool MakesContact { get; set; }

        /// <summary>
        /// Defines the targeting behavior of the move.
        /// </summary>
        public TargetType Target { get; set; }

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