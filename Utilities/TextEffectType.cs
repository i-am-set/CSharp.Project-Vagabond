namespace ProjectVagabond.Utils
{
    public enum TextEffectType
    {
        None,
        Wave,           // Standard Sine Wave (Y offset)
        Shake,          // Random Jitter
        PopWave,        // Wave + Scaling (Balatro style)
        Wobble,         // Sine Wave Rotation
        Rainbow,        // Color Cycle
        Nervous         // Fast, small shake + slight rotation
    }
}
