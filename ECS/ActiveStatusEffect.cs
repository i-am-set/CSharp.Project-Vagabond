namespace ProjectVagabond
{
    public class ActiveStatusEffect
    {
        public StatusEffect BaseEffect { get; }
        public float Duration { get; set; }
        public float TimeSinceLastTick { get; set; }
        public int SourceEntityId { get; }
        public int Amount { get; set; }

        public ActiveStatusEffect(StatusEffect baseEffect, float duration, int sourceEntityId, int amount)
        {
            BaseEffect = baseEffect;
            Duration = duration;
            SourceEntityId = sourceEntityId;
            Amount = amount;
            TimeSinceLastTick = 0f;
        }
    }
}