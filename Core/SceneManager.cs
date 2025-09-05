using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Linq;
using ProjectVagabond.UI;

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
        private SceneIntroAnimator _introAnimator;
        private bool _isTransitioning = false;
        private bool _isHoldingBlack = false;
        private float _holdTimer = 0f;
        private const float HOLD_DURATION = 0.1f;

        private bool _loadIsPending = false;
        private List<LoadingTask> _pendingLoadingTasks;
        private Action _onTransitionCompleteAction;

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
        /// True when the manager is holding a black screen between the outro and intro animations.
        /// </summary>
        public bool IsHoldingBlack => _isHoldingBlack;

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
            ChangeScene(state, loadingTasks, null);
        }

        /// <summary>
        /// Changes the currently active scene via an outro/intro animation sequence, with an action to execute upon completion.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        /// <param name="loadingTasks">An optional list of tasks to execute during the transition.</param>
        /// <param name="onComplete">An action to invoke after the new scene's Enter() method is called.</param>
        public void ChangeScene(GameSceneState state, List<LoadingTask> loadingTasks, Action onComplete)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            _nextSceneState = state;
            _pendingLoadingTasks = loadingTasks;
            _onTransitionCompleteAction = onComplete;
            _loadIsPending = _pendingLoadingTasks != null && _pendingLoadingTasks.Any();
            _introAnimator = null; // Clear any existing intro animator

            _outroAnimator = new SceneOutroAnimator();
            _outroAnimator.OnComplete += HandleOutroComplete;
            _outroAnimator.Start();
        }


        private void HandleOutroComplete()
        {
            if (_outroAnimator != null)
            {
                _outroAnimator.OnComplete -= HandleOutroComplete;
            }

            // Now that the old scene has faded out, switch to the TransitionScene
            // to handle the black screen and loading process.
            SwitchToSceneInternal(GameSceneState.Transition);

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
        private void SwitchToSceneInternal(GameSceneState state)
        {
            if (_scenes.TryGetValue(state, out var newScene))
            {
                _currentScene?.Exit();
                _currentScene = newScene;
                _currentScene.LastUsedInputForNav = this.LastInputDevice;
                _currentScene.Enter();

                // Invoke the completion action after the scene has been entered.
                // This is only for the *final* scene, not the TransitionScene itself.
                if (state != GameSceneState.Transition)
                {
                    _onTransitionCompleteAction?.Invoke();
                    _onTransitionCompleteAction = null; // Clear the action so it doesn't run again.
                }

                // Start the intro animation for the new scene
                // This is only for the *final* scene, not the TransitionScene itself.
                if (state != GameSceneState.Transition)
                {
                    _introAnimator = new SceneIntroAnimator();
                    _introAnimator.Start();
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            UIInputManager.ResetFrameState();

            // Handle outro animation first, as it blocks the scene switch.
            if (_outroAnimator != null && !_outroAnimator.IsComplete)
            {
                _outroAnimator.Update(gameTime);
                _currentScene?.Update(gameTime); // Continue updating the old scene during its outro.
                return;
            }

            // Handle intro animation if not transitioning.
            if (!_isTransitioning)
            {
                _introAnimator?.Update(gameTime);
                _currentScene?.Update(gameTime);
                return;
            }

            // --- At this point, outro is complete, and we are in the 'black' part of the transition ---

            // If loading is pending, the SceneManager's state machine is effectively paused.
            // The Core loop is updating the LoadingScreen.
            // The callback from the loading screen will set _loadIsPending to false, allowing the transition to continue.
            if (_loadIsPending)
            {
                // Update the TransitionScene while loading.
                _currentScene?.Update(gameTime);
                return; // Do nothing until loading is complete.
            }

            if (_isHoldingBlack)
            {
                _holdTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_holdTimer >= HOLD_DURATION)
                {
                    _isHoldingBlack = false;
                    _isTransitioning = false; // Transition is now over, we are entering the new scene.
                    _outroAnimator = null; // Clean up the completed outro animator.
                    // Switch to the final target scene.
                    SwitchToSceneInternal(_nextSceneState);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Only draw the current scene if it's NOT the TransitionScene.
            // The TransitionScene is handled by DrawOverlay.
            if (_currentScene != null && _currentScene.GetType() != typeof(TransitionScene))
            {
                Matrix baseTransform = transform;
                Matrix contentTransform = Matrix.Identity;

                // Prioritize outro transform, then intro transform. They won't be active at the same time
                // under the new Update logic.
                if (_outroAnimator != null && !_outroAnimator.IsComplete)
                {
                    contentTransform = _outroAnimator.GetContentTransform();
                }
                else if (_introAnimator != null && !_introAnimator.IsComplete)
                {
                    contentTransform = _introAnimator.GetContentTransform();
                }

                // The animation should happen in virtual space, then the whole thing is transformed to screen space.
                Matrix finalTransform = contentTransform * baseTransform;

                _currentScene?.Draw(spriteBatch, font, gameTime, finalTransform);
            }
        }

        public void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Only draw underlay if it's NOT the TransitionScene.
            if (_currentScene != null && _currentScene.GetType() != typeof(TransitionScene))
            {
                _currentScene?.DrawUnderlay(spriteBatch, font, gameTime);
            }
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // The animators no longer draw anything themselves.
            // This method is now only for the loading screen.

            // Draw the loading screen if active.
            if (ServiceLocator.Get<LoadingScreen>().IsActive)
            {
                ServiceLocator.Get<LoadingScreen>().Draw(spriteBatch, font, ServiceLocator.Get<GraphicsDevice>().PresentationParameters.Bounds);
            }
        }
    }
}