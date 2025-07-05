using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Defines the contract for all systems in the ECS.
    /// Systems contain logic that operates on entities with specific components.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Updates the system's logic for the given game time.
        /// </summary>
        /// <param name="gameTime">A snapshot of timing values.</param>
        void Update(GameTime gameTime);
    }
}