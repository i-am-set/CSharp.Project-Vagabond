using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A simple scene that waits for a specified duration and then transitions to another scene.
    /// Used to create a "hard cut" effect where the background is visible for a moment.
    /// </summary>
    public class TransitionScene : GameScene
    {
        private SceneManager _sceneManager;
        private GameSceneState _nextSceneState;
        private float _delay;
        private float _timer;

        public override void Initialize()
        {
            base.Initialize();
            _sceneManager = ServiceLocator.Get<SceneManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return Rectangle.Empty;
        }

        public void SetTransition(GameSceneState nextScene, float delay)
        {
            _nextSceneState = nextScene;
            _delay = delay;
        }

        public override void Enter()
        {
            // We don't call base.Enter() because we don't want the intro animator for this scene.
            _timer = 0f;
        }

        public override void Update(GameTime gameTime)
        {
            // We don't call base.Update() for the same reason.
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= _delay)
            {
                _sceneManager.ChangeScene(_nextSceneState);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // This scene is intentionally blank. The background is drawn by Core.cs.
        }
    }
}