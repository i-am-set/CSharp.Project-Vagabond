namespace ProjectVagabond
{
    public class ActiveStatusEffect
    {
        public StatusEffect BaseEffect { get; }
        public float Duration { get; set; }
        public float TimeSinceLastTick { get; set; }
        public int SourceEntityId { get; }

        public ActiveStatusEffect(StatusEffect baseEffect, float duration, int sourceEntityId)
        {
            BaseEffect = baseEffect;
            Duration = duration;
            SourceEntityId = sourceEntityId;
            TimeSinceLastTick = 0f;
        }
    }
}