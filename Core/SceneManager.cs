#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Linq;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;

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
        private GameScene? _currentScene;
        private GameScene? _modalScene;
        private GameSceneState _nextSceneState;

        private SceneOutroAnimator? _outroAnimator;
        private SceneIntroAnimator? _introAnimator;
        private bool _isTransitioning = false;
        private bool _isHoldingBlack = false;
        private float _holdTimer = 0f;
        private const float HOLD_DURATION = 0.5f;

        private bool _loadIsPending = false;
        private List<LoadingTask>? _pendingLoadingTasks;
        private Action? _onTransitionCompleteAction;

        /// <summary>
        /// The currently active scene.
        /// </summary>
        public GameScene? CurrentActiveScene => _currentScene;
        public bool IsModalActive => _modalScene != null;

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

        public SceneManager()
        {
            EventBus.Subscribe<GameEvents.NarrativeChoiceRequested>(OnNarrativeChoiceRequested);
            EventBus.Subscribe<GameEvents.RewardChoiceRequested>(OnRewardChoiceRequested);
        }

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
        public GameScene? GetScene(GameSceneState state)
        {
            _scenes.TryGetValue(state, out var scene);
            return scene;
        }

        public void ShowModal(GameSceneState state)
        {
            if (IsModalActive || !_scenes.TryGetValue(state, out var newModal)) return;

            _modalScene = newModal;
            _modalScene.LastUsedInputForNav = _currentScene?.LastUsedInputForNav ?? InputDevice.Mouse;
            _modalScene.Enter();
        }

        public void HideModal()
        {
            if (!IsModalActive) return;
            _modalScene?.Exit();
            _modalScene = null;
        }

        /// <summary>
        /// Changes the currently active scene via an outro/intro animation sequence.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        /// <param name="loadingTasks">An optional list of tasks to execute during the transition.</param>
        public void ChangeScene(GameSceneState state, List<LoadingTask>? loadingTasks = null)
        {
            ChangeScene(state, loadingTasks, null);
        }

        /// <summary>
        /// Changes the currently active scene via an outro/intro animation sequence, with an action to execute upon completion.
        /// </summary>
        /// <param name="state">The state of the scene to switch to.</param>
        /// <param name="loadingTasks">An optional list of tasks to execute during the transition.</param>
        /// <param name="onComplete">An action to invoke after the new scene's Enter() method is called.</param>
        public void ChangeScene(GameSceneState state, List<LoadingTask>? loadingTasks, Action? onComplete)
        {
            if (_isTransitioning) return;
            HideModal(); // Hide any active modal before changing scenes.

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

            if (_loadIsPending && _pendingLoadingTasks != null)
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

            // If a modal is active, it gets exclusive update priority.
            // The underlying scene and any transitions are paused.
            if (IsModalActive)
            {
                _modalScene?.Update(gameTime);
                return;
            }

            // Phase 1: Outro Animation. This is the highest priority.
            if (_outroAnimator != null)
            {
                _outroAnimator.Update(gameTime);
                _currentScene?.Update(gameTime); // Update the old scene while it animates out.

                // If the animator just finished, null it out. The event handler has already
                // switched the scene and started the next phase.
                if (_outroAnimator.IsComplete)
                {
                    _outroAnimator = null;
                }
                return; // Nothing else happens this frame.
            }

            // Phase 2: Intro Animation. This runs after the "in-between" phase.
            if (_introAnimator != null)
            {
                _introAnimator.Update(gameTime);
                _currentScene?.Update(gameTime); // Update the new scene while it animates in.

                if (_introAnimator.IsComplete)
                {
                    _isTransitioning = false; // The entire transition process is now finished.
                    _introAnimator = null;
                }
                return; // Nothing else happens this frame.
            }

            // Phase 3: "In-between" logic (loading, holding black). This only runs if no animators are active.
            if (_isTransitioning)
            {
                if (_loadIsPending)
                {
                    // The LoadingScreen is updated by Core, but we need to update the TransitionScene.
                    _currentScene?.Update(gameTime);
                }
                else if (_isHoldingBlack)
                {
                    _holdTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_holdTimer >= HOLD_DURATION)
                    {
                        _isHoldingBlack = false;
                        // The "in-between" phase is over. Switch to the new scene, which will create the intro animator for the next phase.
                        SwitchToSceneInternal(_nextSceneState);
                    }
                }
                return; // Nothing else happens this frame.
            }

            // Phase 4: No transition is active, just a normal scene update.
            _currentScene?.Update(gameTime);
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

            if (IsModalActive)
            {
                _modalScene?.DrawUnderlay(spriteBatch, font, gameTime);
                _modalScene?.Draw(spriteBatch, font, gameTime, transform);
            }
        }

        public void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Only draw underlay if it's NOT the TransitionScene.
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
            // Draw the loading screen if active.
            if (ServiceLocator.Get<LoadingScreen>().IsActive)
            {
                ServiceLocator.Get<LoadingScreen>().Draw(spriteBatch, font, ServiceLocator.Get<GraphicsDevice>().PresentationParameters.Bounds);
            }

            // The current scene should always draw its overlay content.
            _currentScene?.DrawOverlay(spriteBatch, font, gameTime);

            if (IsModalActive)
            {
                _modalScene?.DrawOverlay(spriteBatch, font, gameTime);
            }
        }

        public void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Only draw the current scene's fullscreen UI if no modal is active.
            if (!IsModalActive)
            {
                _currentScene?.DrawFullscreenUI(spriteBatch, font, gameTime, transform);
            }

            if (IsModalActive)
            {
                _modalScene?.DrawFullscreenUI(spriteBatch, font, gameTime, transform);
            }
        }

        private void OnNarrativeChoiceRequested(GameEvents.NarrativeChoiceRequested e)
        {
            var narrativeScene = GetScene(GameSceneState.NarrativeChoice) as NarrativeChoiceScene;
            if (narrativeScene != null)
            {
                narrativeScene.Show(e.Prompt, e.Choices);
                ShowModal(GameSceneState.NarrativeChoice);
            }
        }

        private void OnRewardChoiceRequested(GameEvents.RewardChoiceRequested e)
        {
            var choiceScene = GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            if (choiceScene != null)
            {
                if (e.RewardType == "Spell")
                {
                    choiceScene.Show(ChoiceType.Spell, e.Count, e.GameStage);
                    ShowModal(GameSceneState.ChoiceMenu);
                }
                // TODO: Handle other reward types like "Ability" and "Item".
            }
        }
    }
}
#nullable restore