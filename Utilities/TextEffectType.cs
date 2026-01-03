namespace ProjectVagabond.Utils
{
    public enum TextEffectType
    {
        None,
        Wave,           // Standard Sine Wave (Y offset)
        Shake,          // Random Jitter
        PopWave,        // Wave + Scaling (Balatro style)
        Wobble,         // Sine Wave Rotation
        Nervous,        // Fast, small shake + slight rotation
        Rainbow,        // Color Cycle (No movement)
        RainbowWave,    // Color Cycle + Wave
        Pop,            // Scaling Pulse (No movement)
        Bounce,         // Bouncing ball motion (Absolute Sine)
        Drift,          // Horizontal Sine Wave
        Glitch,         // Chaotic offsets and color tints
        Flicker         // Opacity pulsing
    }
}
