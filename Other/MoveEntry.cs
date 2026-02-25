namespace ProjectVagabond.Battle
{
    public class MoveEntry
    {
        public string MoveID { get; set; }
        public int TimesUsed { get; set; }
        public int TurnsUntilReady { get; set; }

        public MoveEntry() { }
        public MoveEntry(string moveId, int timesUsed)
        {
            MoveID = moveId;
            TimesUsed = timesUsed;
            TurnsUntilReady = 0;
        }

        public MoveEntry Clone()
        {
            return new MoveEntry
            {
                MoveID = this.MoveID,
                TimesUsed = this.TimesUsed,
                TurnsUntilReady = this.TurnsUntilReady
            };
        }
    }
}