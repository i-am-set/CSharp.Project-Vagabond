namespace ProjectVagabond.Transitions
{
    public enum TransitionType
    {
        None,
        Shutter,       // Vertical Curtain (Top/Bottom)
        Curtain,       // Horizontal Curtain (Left/Right)
        Aperture,      // All sides closing in
        Diamonds,      // Grid-based diamond wipe
        BigBlocksEase, // Block transition
        SpinningSquare,// Rotates and expands
        CenterSquare,  // Expands without rotation
        CenterDiamond  // Expands rotated 45 degrees
    }

    public enum TransitionState
    {
        Idle,
        Out,        // Scene is visible, effect is covering it up
        Hold,       // Screen is fully black, waiting for scene swap
        In
    }
}