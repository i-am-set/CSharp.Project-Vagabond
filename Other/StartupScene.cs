using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class StartupScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly Global _global;
        private readonly TransitionManager _transitionManager;
        private float _timer;
        private const float DURATION = 1.5f; // 1.5 seconds of splash screen
        private bool _transitionTriggered;

        public StartupScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _global = ServiceLocator.Get<Global>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
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
                    // Use random transition
                    var transitionOut = _transitionManager.GetRandomTransition();
                    var transitionIn = _transitionManager.GetRandomTransition();
                    _sceneManager.ChangeScene(GameSceneState.MainMenu, transitionOut, transitionIn);
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
