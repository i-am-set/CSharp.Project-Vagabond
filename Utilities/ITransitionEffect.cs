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
        /// <param name="screenSize">The actual pixel dimensions of the window/screen.</param>
        /// <param name="contentScale">The ratio of Screen Height to Virtual Height, used to scale transition elements (like Diamonds) to match the game's aesthetic size.</param>
        void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale);
    }
}