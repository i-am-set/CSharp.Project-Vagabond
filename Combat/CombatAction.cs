﻿using ProjectVagabond.Combat; // For ActionData
using System.Collections.Generic; // For List

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines which hand is performing an action.
    /// </summary>
    public enum HandType
    {
        Left,
        Right,
        None // For non-player actions or actions not tied to a specific hand
    }

    /// <summary>
    /// Represents a single action to be executed during a combat turn.
    /// This object is created after player/enemy selections and potential synergies are resolved.
    /// </summary>
    public class CombatAction
    {
        /// <summary>
        /// The ID of the entity performing this action.
        /// </summary>
        public int CasterEntityId { get; }

        /// <summary>
        /// The resolved ActionData for this combat action.
        /// </summary>
        public ActionData ActionData { get; }

        /// <summary>
        /// The calculated speed of this action for the turn, after rolling.
        /// This needs a public setter so it can be assigned in the ActionExecutionState.
        /// </summary>
        public int TurnSpeed { get; set; }

        /// <summary>
        /// A list of entity IDs that are the target(s) of this action.
        /// Can be empty for self-cast actions.
        /// </summary>
        public List<int> TargetEntityIds { get; }

        /// <summary>
        /// Initializes a new instance of the CombatAction class.
        /// </summary>
        /// <param name="casterEntityId">The ID of the entity performing the action.</param>
        /// <param name="actionData">The resolved ActionData for this action.</param>
        /// <param name="targetEntityIds">A list of entity IDs targeted by the action.</param>
        public CombatAction(int casterEntityId, ActionData actionData, List<int> targetEntityIds)
        {
            CasterEntityId = casterEntityId;
            ActionData = actionData;
            TargetEntityIds = targetEntityIds ?? new List<int>();
            TurnSpeed = 0; // Default to 0; will be calculated before execution.
        }
    }
}
