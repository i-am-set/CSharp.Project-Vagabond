using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Editor
{
    /// <summary>
    /// Defines the contract for a scene that can provide context for animation playback.
    /// This decouples the ActionAnimator from specific scene implementations like CombatScene.
    /// </summary>
    public interface IAnimationPlaybackContext
    {
        /// <summary>
        /// A dictionary of named anchor points for positioning animated elements.
        /// </summary>
        Dictionary<string, Vector2> AnimationAnchors { get; }
    }
}