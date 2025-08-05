using Microsoft.Xna.Framework;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Defines the contract for a generic animation object that can be managed by the AnimationManager.
    /// </summary>
    public interface IAnimation
    {
        /// <summary>
        /// Updates the animation's internal state based on the elapsed time.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        void Update(GameTime gameTime);
    }
}