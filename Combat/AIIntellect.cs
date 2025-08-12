namespace ProjectVagabond
{
    /// <summary>
    /// Defines the intelligence tiers for AI decision-making in combat.
    /// </summary>
    public enum AIIntellect
    {
        /// <summary>
        /// The AI chooses its actions randomly from its available hand.
        /// </summary>
        Dumb,
        /// <summary>
        /// The AI prioritizes damage but may make suboptimal choices.
        /// </summary>
        Normal,
        /// <summary>
        /// The AI evaluates outcomes and its own resources to choose the best possible action.
        /// </summary>
        Optimal
    }
}