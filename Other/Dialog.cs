using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;

namespace ProjectVagabond.UI
{
    public abstract class Dialog
    {
        protected readonly GameScene _currentGameScene;
        protected readonly Global _global;
        protected readonly Core _core;

        public bool IsActive { get; protected set; }
        protected Rectangle _dialogBounds;

        protected KeyboardState _previousKeyboardState;
        protected MouseState _previousMouseState;

        public Dialog(GameScene currentGameScene)
        {
            _currentGameScene = currentGameScene;
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
        }

        public virtual void Hide()
        {
            IsActive = false;
        }

        public abstract void Update(GameTime gameTime);

        public void DrawOverlay(SpriteBatch spriteBatch)
        {
            if (!IsActive) return;

            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var pixel = ServiceLocator.Get<Texture2D>();
            var screenBounds = new Rectangle(0, 0, graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(pixel, screenBounds, Color.Black * 0.7f);
            spriteBatch.End();
        }

        public abstract void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime);

        protected bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        protected void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}