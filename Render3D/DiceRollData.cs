using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// The geometric type of the die.
    /// </summary>
    public enum DieType
    {
        D6,
        D4
    }

    /// <summary>
    /// Defines how the results of a dice group should be processed.
    /// </summary>
    public enum DiceResultProcessing
    {
        /// <summary>
        /// The face values of all dice in the group will be added together into a single integer result.
        /// </summary>
        Sum,

        /// <summary>
        /// Each die's face value will be returned as a separate integer in the result list.
        /// </summary>
        IndividualValues
    }

    /// <summary>
    /// Represents a single, distinct set of dice within a larger roll request.
    /// </summary>
    public class DiceGroup
    {
        /// <summary>
        /// A unique identifier for this group (e.g., "damage", "poison_ticks").
        /// The calling system will use this ID to retrieve its results.
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// The number of dice to roll in this group.
        /// </summary>
        public int NumberOfDice { get; set; }

        /// <summary>
        /// The color to tint the visual models of the dice in this group.
        /// </summary>
        public Color Tint { get; set; }

        /// <summary>
        /// A multiplier for the visual and physical scale of the dice in this group. Defaults to 1.0f.
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// The type of die to roll (e.g., D6, D4). This determines the physics shape and result calculation.
        /// </summary>
        public DieType DieType { get; set; } = DieType.D6;

        /// <summary>
        /// Determines how the final results for this group are calculated (e.g., summed or returned individually).
        /// </summary>
        public DiceResultProcessing ResultProcessing { get; set; }

        /// <summary>
        /// A multiplier to apply to the final sum of this group. Defaults to 1.0.
        /// </summary>
        public float Multiplier { get; set; } = 1.0f;

        /// <summary>
        /// A flat modifier to add to the final sum of this group. Defaults to 0.
        /// </summary>
        public int Modifier { get; set; } = 0;
    }

    /// <summary>
    /// Contains the structured results of a completed dice roll, organized by group.
    /// </summary>
    public class DiceRollResult
    {
        /// <summary>
        /// A dictionary mapping each GroupId from the roll request to its final, processed result(s).
        /// If a group's processing was 'Sum', this list will contain a single integer.
        /// If it was 'IndividualValues', this list will contain a value for each die.
        /// </summary>
        public Dictionary<string, List<int>> ResultsByGroup { get; set; } = new Dictionary<string, List<int>>();
    }
}