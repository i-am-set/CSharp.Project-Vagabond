using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond
{
    public enum FadeState
    {
        Idle,
        FadingOut,
        HoldingBlack,
        FadingIn
    }

    public class SceneManager
    {
        private readonly Dictionary<GameSceneState, GameScene> _scenes = new Dictionary<GameSceneState, GameScene>();
        private GameScene _currentScene;
        private GameSceneState _nextSceneState;

        private SceneOutroAnimator _outroAnimator;
        private bool _isTransitioning = false;
        private bool _isHoldingBlack = false;
        private float _holdTimer = 0f;
        private const float HOLD_DURATION = 0.5f;

        /// <summary>
        /// The currently active scene.
        /// </summary>
        public GameScene CurrentActiveScene => _currentScene;

        /// <summary>
        /// The last input device used to trigger a major action, like changing a scene.
        /// </summary>
        public InputDevice LastInputDevice { get; set; } = InputDevice.Mouse;

        public SceneManager() { }

        /// <summary>
        /// Adds a scene to the manager and initializes it.
        /// </summary>
        public void AddScene(GameSceneState state, GameScene scene)
        {
            _scenes[state] = scene;
            scene.Initialize();
        }

        /// <summary>
        /// Retrieves a scene instance from the manager.
        /// </summary>
        /// <param name="state">The state of the scene to retrieve.</param>
        /// <returns>The GameScene instance, or null if not found.</returns>
        public GameScene GetScene(GameSceneState state)
        {
            _scenes.TryGetValue(state, out var scene);
            return scene;
        }

        /// <summary>
        /// Changes the currently active scene via an outro/intro animation sequence.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        public void ChangeScene(GameSceneState state)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            _nextSceneState = state;

            _outroAnimator = new SceneOutroAnimator();
            _outroAnimator.OnComplete += HandleOutroComplete;
            _outroAnimator.Start(new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT));
        }

        private void HandleOutroComplete()
        {
            if (_outroAnimator != null)
            {
                _outroAnimator.OnComplete -= HandleOutroComplete;
                _outroAnimator = null;
            }
            _isHoldingBlack = true;
            _holdTimer = 0f;
        }

        /// <summary>
        /// Performs the actual scene switch.
        /// </summary>
        private void SwitchToScene(GameSceneState state)
        {
            if (_scenes.TryGetValue(state, out var newScene))
            {
                _currentScene?.Exit();
                _currentScene = newScene;
                _currentScene.LastUsedInputForNav = this.LastInputDevice;
                _currentScene.Enter();
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_isTransitioning)
            {
                _outroAnimator?.Update(gameTime);
                if (_isHoldingBlack)
                {
                    _holdTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_holdTimer >= HOLD_DURATION)
                    {
                        _isHoldingBlack = false;
                        _isTransitioning = false;
                        SwitchToScene(_nextSceneState);
                    }
                }
            }
            else
            {
                _currentScene?.Update(gameTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.Draw(spriteBatch, font, gameTime);
        }

        public void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.DrawUnderlay(spriteBatch, font, gameTime);
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.DrawOverlay(spriteBatch, font, gameTime);

            if (_isTransitioning && _outroAnimator != null)
            {
                _outroAnimator.Draw(spriteBatch, font, gameTime, null);
            }

            if (_isHoldingBlack)
            {
                spriteBatch.Begin();
                spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);
                spriteBatch.End();
            }
        }
    }
}