namespace ProjectVagabond
{
    /// <summary>
    /// Represents a single entry in the player's spellbook, containing a move
    /// and a persistent counter for how many times it has been used.
    /// </summary>
    public class SpellbookEntry
    {
        public string MoveID { get; set; }
        public int TimesUsed { get; set; }

        public SpellbookEntry(string moveId, int timesUsed = 0)
        {
            MoveID = moveId;
            TimesUsed = timesUsed;
        }

        // Parameterless constructor for serialization if needed in the future.
        public SpellbookEntry() { }

        /// <summary>
        /// Creates a shallow copy of the SpellbookEntry. This is sufficient because
        /// its properties (string, int) are value types or immutable.
        /// </summary>
        public SpellbookEntry Clone()
        {
            return (SpellbookEntry)this.MemberwiseClone();
        }
    }
}