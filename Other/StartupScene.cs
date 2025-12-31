using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Transitions;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Scenes
{
    public class StartupScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly Global _global;
        private float _timer;
        private const float DURATION = 1.5f; // 1.5 seconds of splash screen
        private bool _transitionTriggered;

        public StartupScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
            _timer = 0f;
            _transitionTriggered = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!_transitionTriggered)
            {
                _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_timer >= DURATION)
                {
                    _transitionTriggered = true;
                    _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.Diamonds);
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            // Draw Solid Black Background
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);
        }
    }
}