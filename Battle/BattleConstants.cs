namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A static class to hold all "magic numbers" and constants for the battle system,
    /// making them easy to find and tune.
    /// </summary>
    public static class BattleConstants
    {
        public const float CRITICAL_HIT_MULTIPLIER = 1.5f;
        public const float CRITICAL_HIT_CHANCE = 0.0625f;
        public const float STRENGTH_UP_MULTIPLIER = 1.5f;
        public const float TENACITY_UP_MULTIPLIER = 0.66f;
        public const float RANDOM_VARIANCE_MIN = 0.85f;
        public const float RANDOM_VARIANCE_MAX = 1.00f;
        public const float GRAZE_MULTIPLIER = 0.25f;
        public const float MULTI_TARGET_MODIFIER = 0.75f;
    }
}