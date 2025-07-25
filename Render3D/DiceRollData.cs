﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Dice
{
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
        /// Determines how the final results for this group are calculated (e.g., summed or returned individually).
        /// </summary>
        public DiceResultProcessing ResultProcessing { get; set; }
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