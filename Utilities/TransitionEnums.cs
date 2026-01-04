namespace ProjectVagabond.Transitions
{
    public enum TransitionType
    {
        None,
        Shutters,
        Diamonds,      // The grid-based diamond wipe
        BigBlocksEase, // The "good" block transition
        SpinningSquare, // New: Rotates and expands
        Curtain,        // New: Slides in from sides
        CenterDiamond   // New: Single large diamond expansion
    }

    public enum TransitionState
    {
        Idle,
        Out,        // Scene is visible, effect is covering it up
        Hold,       // Screen is fully black, waiting for scene swap
        In
    }
}