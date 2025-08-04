namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Represents one of the player's hands in combat, encapsulating its state and stats.
    /// </summary>
    public class PlayerHand
    {
        /// <summary>
        /// The type of hand (Left or Right).
        /// </summary>
        public HandType Hand { get; }

        /// <summary>
        /// The speed at which this hand executes actions. Used for tie-breaking in turn resolution.
        /// </summary>
        public float CastSpeed { get; set; }

        /// <summary>
        /// The unique ID of the action currently selected for this hand. Null if no action is selected.
        /// </summary>
        public string SelectedActionId { get; private set; }

        /// <summary>
        /// Initializes a new instance of the PlayerHand class.
        /// </summary>
        /// <param name="hand">The type of hand (Left or Right).</param>
        /// <param name="castSpeed">The base casting speed for this hand.</param>
        public PlayerHand(HandType hand, float castSpeed)
        {
            Hand = hand;
            CastSpeed = castSpeed;
        }

        /// <summary>
        /// Assigns an action to this hand.
        /// </summary>
        /// <param name="actionId">The unique ID of the action to select.</param>
        public void SelectAction(string actionId)
        {
            SelectedActionId = actionId;
        }

        /// <summary>
        /// Clears the currently selected action for this hand.
        /// </summary>
        public void ClearSelection()
        {
            SelectedActionId = null;
        }
    }
}