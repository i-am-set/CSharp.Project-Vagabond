using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A component that stores the current path an AI entity is following.
    /// This allows for smoother, more intentional movement instead of frame-by-frame recalculation.
    /// </summary>
    public class AIPathComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The list of waypoints the AI is currently following.
        /// </summary>
        public List<Vector2> Path { get; set; } = new List<Vector2>();

        /// <summary>
        /// The index of the next waypoint in the Path list to move towards.
        /// </summary>
        public int CurrentPathIndex { get; set; } = 0;

        /// <summary>
        /// A timer to control how often the AI recalculates its path to the target.
        /// </summary>
        public float RepathTimer { get; set; } = 0f;

        public bool HasPath() => Path != null && Path.Count > 0 && CurrentPathIndex < Path.Count;

        public void Clear()
        {
            Path.Clear();
            CurrentPathIndex = 0;
            RepathTimer = 0f;
        }

        public IComponent Clone()
        {
            // This is a runtime state component, so cloning just creates a fresh, empty one.
            return new AIPathComponent();
        }
    }
}