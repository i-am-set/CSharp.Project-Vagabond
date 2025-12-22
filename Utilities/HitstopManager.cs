using Microsoft.Xna.Framework;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Manages "Frame Freezing" (Hitstop) to add impact to combat.
    /// When active, it consumes the game's delta time, effectively pausing logic
    /// while allowing rendering and specific "juice" effects (like shake) to continue.
    /// </summary>
    public class HitstopManager
    {
        private float _timer;
        public bool IsActive => _timer > 0f;

        /// <summary>
        /// Triggers a frame freeze for the specified duration in seconds.
        /// If a freeze is already active, it extends it only if the new duration is longer.
        /// </summary>
        public void Trigger(float duration)
        {
            if (duration > _timer)
            {
                _timer = duration;
            }
        }

        /// <summary>
        /// Updates the hitstop timer.
        /// </summary>
        /// <param name="realDeltaTime">The actual elapsed time since the last frame.</param>
        /// <returns>
        /// Returns a time scale multiplier (0.0f if frozen, 1.0f if normal).
        /// Multiply your game logic delta time by this value.
        /// </returns>
        public float Update(float realDeltaTime)
        {
            if (_timer > 0f)
            {
                _timer -= realDeltaTime;
                if (_timer < 0f) _timer = 0f;
                return 0f; // Time is frozen
            }
            return 1f; // Time flows normally
        }

        public void Reset()
        {
            _timer = 0f;
        }
    }
}
