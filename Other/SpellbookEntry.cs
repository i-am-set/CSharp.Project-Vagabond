namespace ProjectVagabond
{
    /// <summary>
    /// Represents a single entry in the player's spellbook, containing a move
    /// and its remaining uses.
    /// </summary>
    public class SpellbookEntry
    {
        public const int MAX_USES = 3;

        public string MoveID { get; set; }
        public int RemainingUses { get; set; }

        public SpellbookEntry(string moveId, int uses = MAX_USES)
        {
            MoveID = moveId;
            RemainingUses = uses;
        }

        // Parameterless constructor for serialization if needed in the future.
        public SpellbookEntry() { }
    }
}