using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A dedicated scene for handling transitions between other scenes.
    /// It ensures a solid black background during loading and transition animations.
    /// </summary>
    public class TransitionScene : GameScene
    {
        private readonly SceneManager _sceneManager;

        public TransitionScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            // This scene covers the entire screen, so its animated bounds are the full virtual resolution.
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            // We don't call base.Enter() here because we don't want the input block timer
            // or mouse positioning logic for this scene. It's purely visual.
            // The SceneManager will handle starting its animators.
        }

        public override void Update(GameTime gameTime)
        {
            // This scene doesn't have its own complex logic, it just exists to be drawn.
            // The SceneManager's Update loop will handle updating its animators.
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Draw a solid black background to cover the default palette background
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);
        }

        // This scene does not draw any underlay or overlay content itself.
        // The SceneManager's DrawOverlay handles the transition animations.
    }
}