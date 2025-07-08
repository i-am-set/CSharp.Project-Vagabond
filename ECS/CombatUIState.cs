namespace ProjectVagabond
{
    /// <summary>
    /// Manages which menu is currently active for the player during combat.
    /// </summary>
    public enum CombatUIState
    {
        /// <summary>
        /// The main action menu is shown (Attack, Skill, etc.).
        /// </summary>
        Default,
        /// <summary>
        /// The player is choosing from a list of attacks.
        /// </summary>
        SelectAttack,
        /// <summary>
        /// The player is choosing an enemy to target.
        /// </summary>
        SelectTarget,
        /// <summary>
        /// Input is disabled (e.g., during an animation).
        /// </summary>
        Busy
    }
}