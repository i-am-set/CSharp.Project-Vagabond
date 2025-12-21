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
        void Draw(SpriteBatch spriteBatch);
    }
}