#nullable enable
using Microsoft.Xna.Framework;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// A static manager to handle UI input state on a frame-by-frame basis.
    /// This helps prevent a single input event (like a mouse click) from being
    /// processed by multiple UI elements in the same update loop, especially
    /// when UI layouts change dynamically. It also provides a global input buffer
    /// to prevent accidental clicks on UI that appears under the cursor.
    /// </summary>
    public static class UIInputManager
    {
        private static bool _mouseClickHandledThisFrame = false;
        private static float _inputBufferTimer = 0f;
        private const float INPUT_BUFFER_DURATION = 0.1f;

        /// <summary>
        /// Updates the manager's internal timers. Should be called once per frame.
        /// </summary>
        public static void Update(GameTime gameTime)
        {
            if (_inputBufferTimer > 0f)
            {
                _inputBufferTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

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
        /// <returns>True if a click has not yet been consumed this frame and the global input buffer is not active, otherwise false.</returns>
        public static bool CanProcessMouseClick()
        {
            return !_mouseClickHandledThisFrame && _inputBufferTimer <= 0f;
        }

        /// <summary>
        /// Marks the mouse click as handled for the current frame, preventing
        /// other UI elements from processing the same click event, and activates
        /// the global input buffer.
        /// </summary>
        public static void ConsumeMouseClick()
        {
            _mouseClickHandledThisFrame = true;
            _inputBufferTimer = INPUT_BUFFER_DURATION;
        }
    }
}
#nullable restore