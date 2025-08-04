using ProjectVagabond.Combat; // For ActionData
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
        /// The resolved ActionData for this combat action (could be a base spell or a synergy spell).
        /// </summary>
        public ActionData ActionData { get; }

        /// <summary>
        /// The effective speed of the caster for this specific action, used for tie-breaking initiative.
        /// For player hands, this is the hand's castSpeed. For synergies, it's the slower of the two hands.
        /// </summary>
        public float EffectiveCastSpeed { get; }

        /// <summary>
        /// Indicates which hand performed this action, if applicable (e.g., for player actions).
        /// </summary>
        public HandType SourceHand { get; }

        /// <summary>
        /// The ID of the action selected for the left hand, if this is a synergy action.
        /// </summary>
        public string OriginalLeftHandActionId { get; }

        /// <summary>
        /// The ID of the action selected for the right hand, if this is a synergy action.
        /// </summary>
        public string OriginalRightHandActionId { get; }

        /// <summary>
        /// Initializes a new instance of the CombatAction class for a single action.
        /// </summary>
        /// <param name="casterEntityId">The ID of the entity performing the action.</param>
        /// <param name="actionData">The resolved ActionData for this action.</param>
        /// <param name="effectiveCastSpeed">The effective speed of the caster for this action.</param>
        /// <param name="sourceHand">The hand performing the action (Left, Right, or None).</param>
        public CombatAction(int casterEntityId, ActionData actionData, float effectiveCastSpeed, HandType sourceHand = HandType.None)
        {
            CasterEntityId = casterEntityId;
            ActionData = actionData;
            EffectiveCastSpeed = effectiveCastSpeed;
            SourceHand = sourceHand;
            OriginalLeftHandActionId = null;
            OriginalRightHandActionId = null;
        }

        /// <summary>
        /// Initializes a new instance of the CombatAction class for a synergy action.
        /// </summary>
        /// <param name="casterEntityId">The ID of the entity performing the action.</param>
        /// <param name="synergyActionData">The ActionData for the resulting synergy spell.</param>
        /// <param name="effectiveCastSpeed">The effective speed of the synergy action (slower of the two hands).</param>
        /// <param name="originalLeftHandActionId">The ID of the action selected for the left hand.</param>
        /// <param name="originalRightHandActionId">The ID of the action selected for the right hand.</param>
        public CombatAction(int casterEntityId, ActionData synergyActionData, float effectiveCastSpeed, string originalLeftHandActionId, string originalRightHandActionId)
        {
            CasterEntityId = casterEntityId;
            ActionData = synergyActionData;
            EffectiveCastSpeed = effectiveCastSpeed;
            SourceHand = HandType.None; // Synergy actions are not tied to a single hand for execution
            OriginalLeftHandActionId = originalLeftHandActionId;
            OriginalRightHandActionId = originalRightHandActionId;
        }
    }
}