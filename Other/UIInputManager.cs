namespace ProjectVagabond.UI
{
    /// <summary>
    /// A static manager to handle UI input state on a frame-by-frame basis.
    /// This helps prevent a single input event (like a mouse click) from being
    /// processed by multiple UI elements in the same update loop, especially
    /// when UI layouts change dynamically.
    /// </summary>
    public static class UIInputManager
    {
        private static bool _mouseClickHandledThisFrame = false;

        /// <summary>
        /// Resets the input state for the beginning of a new frame.
        /// This should be called once per frame before any UI update logic.
        /// </summary>
        public static void ResetFrameState()
        {
            _mouseClickHandledThisFrame = false;
        }

        /// <summary>
        /// Checks if a mouse click can be processed by a UI element.
        /// </summary>
        /// <returns>True if a click has not yet been consumed this frame, otherwise false.</returns>
        public static bool CanProcessMouseClick()
        {
            return !_mouseClickHandledThisFrame;
        }

        /// <summary>
        /// Marks the mouse click as handled for the current frame, preventing
        /// other UI elements from processing the same click event.
        /// </summary>
        public static void ConsumeMouseClick()
        {
            _mouseClickHandledThisFrame = true;
        }
    }
}