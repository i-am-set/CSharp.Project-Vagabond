namespace ProjectVagabond
{
    /// <summary>
    /// A central place to define event argument classes for the EventBus.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// Published when a message should be added to the main terminal output.
        /// </summary>
        public struct TerminalMessagePublished
        {
            public string Message { get; set; }
            public Microsoft.Xna.Framework.Color? BaseColor { get; set; }
        }

        /// <summary>
        /// Published when a message should be added to the combat log.
        /// </summary>
        public struct CombatLogMessagePublished
        {
            public string Message { get; set; }
        }

        /// <summary>
        /// Published when the game state changes to or from combat.
        /// </summary>
        public struct CombatStateChanged
        {
            public bool IsInCombat { get; set; }
        }

        /// <summary>
        /// Published when the player's action queue is modified.
        /// </summary>
        public struct ActionQueueChanged
        {
            // This event can be expanded with more data if needed,
            // but for now, its existence is enough to signal a refresh.
        }

        /// <summary>
        /// Published when an entity's health is reduced.
        /// </summary>
        public struct EntityTookDamage
        {
            public int EntityId { get; set; }
            public int DamageAmount { get; set; }
        }
    }
}
