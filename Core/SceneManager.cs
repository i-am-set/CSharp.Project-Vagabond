using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Linq;
using ProjectVagabond.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;

namespace ProjectVagabond
{
    public class SceneManager
    {
        private readonly Dictionary<GameSceneState, GameScene> _scenes = new Dictionary<GameSceneState, GameScene>();
        private GameScene? _currentScene;
        private GameScene? _modalScene;

        // Dependencies
        private TransitionManager _transitionManager;

        public GameScene? CurrentActiveScene => _currentScene;
        public bool IsModalActive => _modalScene != null;

        // Legacy flags kept for compatibility, but logic is now driven by TransitionManager
        public bool IsLoadingBetweenScenes => false;
        public bool IsHoldingBlack => _transitionManager.IsScreenObscured;

        public InputDevice LastInputDevice { get; set; } = InputDevice.Mouse;

        public SceneManager() { }

        public void AddScene(GameSceneState state, GameScene scene)
        {
            _scenes[state] = scene;
            scene.Initialize();
        }

        public GameScene? GetScene(GameSceneState state)
        {
            _scenes.TryGetValue(state, out var scene);
            return scene;
        }

        public void ShowModal(GameSceneState state)
        {
            if (IsModalActive || !_scenes.TryGetValue(state, out var newModal)) return;
            _modalScene = newModal;
            _modalScene.LastInputDevice = _currentScene?.LastInputDevice ?? InputDevice.Mouse;
            _modalScene.Enter();
        }

        public void HideModal()
        {
            if (!IsModalActive) return;
            _modalScene?.Exit();
            _modalScene = null;

            _currentScene?.ResetInputState();
        }

        public void ResetInputState()
        {
            _currentScene?.ResetInputState();
            _modalScene?.ResetInputState();
        }

        /// <summary>
        /// Changes the scene using the new TransitionManager with specific Out and In effects.
        /// Supports baked-in loading screens.
        /// </summary>
        /// <param name="state">Target scene.</param>
        /// <param name="outTransition">Effect to use when leaving current scene.</param>
        /// <param name="inTransition">Effect to use when entering new scene.</param>
        /// <param name="transitionDelay">Time in seconds to hold the black screen before revealing the new scene.</param>
        /// <param name="loadingTasks">Optional loading tasks. If provided, the transition will hold until tasks complete.</param>
        public void ChangeScene(GameSceneState state, TransitionType outTransition, TransitionType inTransition, float transitionDelay = 0f, List<LoadingTask>? loadingTasks = null)
        {
            _transitionManager ??= ServiceLocator.Get<TransitionManager>();

            if (_transitionManager.IsTransitioning) return;
            HideModal();

            // If we have loading tasks, we tell the TransitionManager to HOLD manually.
            // It will wait for us to set ManualHold = false.
            bool hasLoading = loadingTasks != null && loadingTasks.Any();

            _transitionManager.StartTransition(
                outTransition,
                inTransition,
                onMidpoint: () => PerformSceneSwapOrLoad(state, loadingTasks),
                holdDuration: transitionDelay
            );

            if (hasLoading)
            {
                _transitionManager.ManualHold = true;
            }
        }

        // Overload for backward compatibility (Defaults to Fade/Fade, No Delay)
        public void ChangeScene(GameSceneState state, List<LoadingTask>? loadingTasks = null)
        {
            ChangeScene(state, TransitionType.Diamonds, TransitionType.Diamonds, 0f, loadingTasks);
        }

        private void PerformSceneSwapOrLoad(GameSceneState state, List<LoadingTask>? loadingTasks)
        {
            HideModal();

            // If we have loading tasks, start the loading screen.
            // The actual scene swap will happen when loading finishes.
            if (loadingTasks != null && loadingTasks.Any())
            {
                var loadingScreen = ServiceLocator.Get<LoadingScreen>();
                loadingScreen.Clear();
                foreach (var task in loadingTasks)
                {
                    loadingScreen.AddTask(task);
                }

                // Hook up completion
                loadingScreen.OnComplete += () =>
                {
                    // 1. Swap Scene
                    SwapSceneInternal(state);

                    // 2. Release Transition Hold
                    _transitionManager.ManualHold = false;
                };

                loadingScreen.Start();
            }
            else
            {
                // No loading, swap immediately
                SwapSceneInternal(state);
            }
        }

        private void SwapSceneInternal(GameSceneState state)
        {
            if (_scenes.TryGetValue(state, out var newScene))
            {
                _currentScene?.Exit();
                _currentScene = newScene;
                _currentScene.LastInputDevice = this.LastInputDevice;
                _currentScene.Enter();
            }
        }

        public void Update(GameTime gameTime)
        {
            UIInputManager.Update(gameTime);
            UIInputManager.ResetFrameState();

            if (IsModalActive)
            {
                _modalScene?.Update(gameTime);
                return;
            }

            _transitionManager ??= ServiceLocator.Get<TransitionManager>();

            // Only update the scene if the screen is NOT obscured by a transition/loading
            if (!_transitionManager.IsScreenObscured)
            {
                _currentScene?.Update(gameTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_currentScene != null && _currentScene.GetType() != typeof(TransitionScene))
            {
                // Draw scene with standard transform
                _currentScene?.Draw(spriteBatch, font, gameTime, transform);

                // Draw particles
                var particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
                particleSystemManager.Draw(spriteBatch, transform);
            }

            if (IsModalActive)
            {
                _modalScene?.DrawUnderlay(spriteBatch, font, gameTime);
                _modalScene?.Draw(spriteBatch, font, gameTime, transform);
            }
        }

        public void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_currentScene != null && _currentScene.GetType() != typeof(TransitionScene))
            {
                _currentScene?.DrawUnderlay(spriteBatch, font, gameTime);
            }
            if (IsModalActive)
            {
                _modalScene?.DrawUnderlay(spriteBatch, font, gameTime);
            }
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            _currentScene?.DrawOverlay(spriteBatch, font, gameTime);
            if (IsModalActive)
            {
                _modalScene?.DrawOverlay(spriteBatch, font, gameTime);
            }
        }

        public void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsModalActive)
            {
                _currentScene?.DrawFullscreenUI(spriteBatch, font, gameTime, transform);
            }
            if (IsModalActive)
            {
                _modalScene?.DrawFullscreenUI(spriteBatch, font, gameTime, transform);
            }
        }
    }
}