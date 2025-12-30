using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.Transitions
{
    public interface ITransitionEffect
    {
        /// <summary>
        /// True if the effect has finished its current phase (Out or In).
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Resets the effect to start a specific phase.
        /// </summary>
        /// <param name="isTransitioningOut">True if covering the screen (Out), False if revealing (In).</param>
        void Start(bool isTransitioningOut);

        void Update(GameTime gameTime);

        /// <summary>
        /// Draws the transition effect.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to draw with.</param>
        /// <param name="bounds">The full screen bounds to cover.</param>
        /// <param name="scale">The current game scale factor, used to size retro elements correctly.</param>
        void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale);
    }
}