namespace ProjectVagabond
{
    /// <summary>
    /// Manages which menu or interaction mode is currently active for the player in combat.
    /// </summary>
    public enum CombatUIState
    {
        /// <summary>
        /// The main action menu is shown (Attack, Skills, Move, etc.).
        /// </summary>
        Default,
        /// <summary>
        /// The player is choosing from a list of available attacks.
        /// </summary>
        SelectAttack,
        /// <summary>
        /// The player is choosing from a list of available skills.
        /// </summary>
        SelectSkill,
        /// <summary>
        /// The player is choosing a tile on the local map to move to.
        /// </summary>
        SelectMove,
        /// <summary>
        /// The player is choosing an enemy to target with an action.
        /// </summary>
        SelectTarget,
        /// <summary>
        /// Player input is disabled (e.g., during an enemy's turn or an animation).
        /// </summary>
        Busy
    }
}