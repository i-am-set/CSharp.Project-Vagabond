using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.Scenes;

namespace ProjectVagabond.UI
{
    public abstract class Dialog
    {
        public bool IsActive { get; protected set; }
        protected GameScene _currentGameScene;
        protected Rectangle _dialogBounds;

        protected KeyboardState _previousKeyboardState;
        protected MouseState _previousMouseState;

        public Dialog(GameScene currentGameScene)
        {
            _currentGameScene = currentGameScene;
        }

        public virtual void Hide()
        {
            IsActive = false;
        }

        public abstract void Update(GameTime gameTime);

        /// <summary>
        /// The main drawing method for the dialog. It handles drawing the overlay and then the dialog content on top of it.
        /// </summary>
        public void Draw(GameTime gameTime)
        {
            if (!IsActive) return;

            DrawOverlay(gameTime);

            DrawContent(gameTime);
        }

        /// <summary>
        /// Draws the specific content of the dialog. Must be implemented by derived classes.
        /// </summary>
        protected abstract void DrawContent(GameTime gameTime);

        /// <summary>
        /// Draws the semi-transparent background that darkens the main screen.
        /// </summary>
        private void DrawOverlay(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var graphicsDevice = Global.Instance.CurrentGraphics.GraphicsDevice;
            var pixel = Core.Pixel;
            var screenBounds = new Rectangle(0, 0, graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(pixel, screenBounds, Color.Black * 0.7f);
            spriteBatch.End();
        }

        protected bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        protected static void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}