using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Linq;

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
        private const float HOLD_DURATION = 0.1f;

        private bool _loadIsPending = false;
        private List<LoadingTask> _pendingLoadingTasks;

        /// <summary>
        /// The currently active scene.
        /// </summary>
        public GameScene CurrentActiveScene => _currentScene;

        /// <summary>
        /// True when the manager is transitioning between scenes and a loading operation is active.
        /// This is used by the Core loop to suppress drawing of the old scene and background.
        /// </summary>
        public bool IsLoadingBetweenScenes => _isTransitioning && _loadIsPending && _outroAnimator == null;

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
        /// <param name="loadingTasks">An optional list of tasks to execute during the transition.</param>
        public void ChangeScene(GameSceneState state, List<LoadingTask> loadingTasks = null)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            _nextSceneState = state;
            _pendingLoadingTasks = loadingTasks;
            _loadIsPending = _pendingLoadingTasks != null && _pendingLoadingTasks.Any();

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

            if (_loadIsPending)
            {
                var loadingScreen = ServiceLocator.Get<LoadingScreen>();
                loadingScreen.Clear();
                foreach (var task in _pendingLoadingTasks)
                {
                    loadingScreen.AddTask(task);
                }

                loadingScreen.OnComplete += HandleLoadingComplete;
                loadingScreen.Start();
            }
            else
            {
                _isHoldingBlack = true;
                _holdTimer = 0f;
            }
        }

        private void HandleLoadingComplete()
        {
            ServiceLocator.Get<LoadingScreen>().OnComplete -= HandleLoadingComplete;
            _loadIsPending = false;
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

                // If loading is pending, the SceneManager's state machine is effectively paused.
                // The Core loop is updating the LoadingScreen.
                // The callback from the loading screen will set _loadIsPending to false, allowing the transition to continue.
                if (_loadIsPending)
                {
                    return; // Do nothing until loading is complete.
                }

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
            // If we are in the middle of a transition that requires loading,
            // and the outro is complete, don't draw the old scene. This results
            // in a black background for the loading screen.
            if (IsLoadingBetweenScenes)
            {
                return;
            }
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

            if (_isHoldingBlack && !_loadIsPending)
            {
                spriteBatch.Begin();
                spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);
                spriteBatch.End();
            }
        }
    }
}