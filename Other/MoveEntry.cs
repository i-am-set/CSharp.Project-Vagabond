namespace ProjectVagabond.Battle
{
    public class MoveEntry
    {
        public CompiledMove CompiledMove { get; set; }
        public int TimesUsed { get; set; }
        public int TurnsUntilReady { get; set; }

        public MoveEntry() { }

        public MoveEntry(CompiledMove compiledMove, int timesUsed)
        {
            CompiledMove = compiledMove;
            TimesUsed = timesUsed;
            TurnsUntilReady = 0;
        }

        public MoveEntry Clone()
        {
            return new MoveEntry
            {
                CompiledMove = this.CompiledMove,
                TimesUsed = this.TimesUsed,
                TurnsUntilReady = this.TurnsUntilReady
            };
        }
    }
}