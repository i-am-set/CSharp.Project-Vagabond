namespace ProjectVagabond.Transitions
{
    public enum TransitionType
    {
        None,
        Fade,
        Shutters,
        Diamonds,
        Blocks,
        BigBlocksEase,
        Pixels
    }

    public enum TransitionState
    {
        Idle,
        Out,        // Scene is visible, effect is covering it up
        Hold,       // Screen is fully black, waiting for scene swap
        In          // New scene is visible, effect is revealing it
    }
}