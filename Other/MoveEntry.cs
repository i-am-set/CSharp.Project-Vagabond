namespace ProjectVagabond
{
    /// <summary>
    /// Represents a single move entry in the player's list (Spell or Action), containing the MoveID
    /// and a persistent counter for how many times it has been used.
    /// </summary>
    public class MoveEntry
    {
        public string MoveID { get; set; }
        public int TimesUsed { get; set; }

        public MoveEntry(string moveId, int timesUsed = 0)
        {
            MoveID = moveId;
            TimesUsed = timesUsed;
        }

        // Parameterless constructor for serialization if needed in the future.
        public MoveEntry() { }

        /// <summary>
        /// Creates a shallow copy of the MoveEntry.
        /// </summary>
        public MoveEntry Clone()
        {
            return (MoveEntry)this.MemberwiseClone();
        }
    }
}