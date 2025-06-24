using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// Enum to identify each distinct game scene.
    /// </summary>
    public enum GameSceneState
    {
        MainMenu,
        TerminalMap,
        Dialogue,
        Combat,
        Settings
    }

    /// <summary>
    /// Defines the input device used for navigation.
    /// </summary>
    public enum InputDevice
    {
        Keyboard,
        Mouse
    }

    /// <summary>
    /// Abstract base class for all game scenes.
    /// </summary>
    public abstract class GameScene
    {
        protected MouseState previousMouseState;
        protected bool keyboardNavigatedLastFrame = false;
        protected bool firstTimeOpened = true;

        private const float INPUT_BLOCK_DURATION = 0.1f;
        private float _inputBlockTimer = 0f;

        /// <summary>
        /// The input device used to navigate to this scene.
        /// </summary>
        public InputDevice LastUsedInputForNav { get; set; } = InputDevice.Mouse;

        /// <summary>
        /// Returns true if the scene is currently blocking input, e.g., for a short duration after entering.
        /// </summary>
        protected bool IsInputBlocked => _inputBlockTimer > 0;

        /// <summary>
        /// Called once when the scene is first added to the SceneManager.
        /// Use for one-time setup.
        /// </summary>
        public virtual void Initialize()
        {
            firstTimeOpened = true;
        }

        /// <summary>
        /// Called every time the scene becomes the active scene.
        /// Use for resetting state.
        /// </summary>
        public virtual void Enter()
        {
            previousMouseState = Mouse.GetState();
            _inputBlockTimer = INPUT_BLOCK_DURATION;

            if (this.LastUsedInputForNav == InputDevice.Keyboard)
            {
                Core.Instance.IsMouseVisible = false;
            }
            else // Mouse was used to enter
            {
                Core.Instance.IsMouseVisible = true;
                keyboardNavigatedLastFrame = false;
            }
        }

        /// <summary>
        /// Called every time the scene is no longer the active scene.
        /// </summary>
        public virtual void Exit() { }

        /// <summary>
        /// Called every frame to update the scene's logic.
        /// </summary>
        public virtual void Update(GameTime gameTime)
        {
            if (_inputBlockTimer > 0)
            {
                _inputBlockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            var currentMouseState = Mouse.GetState();

            if (keyboardNavigatedLastFrame)
            {
                keyboardNavigatedLastFrame = false;
            }
            else if (currentMouseState.Position != previousMouseState.Position)
            {
                if (!IsInputBlocked)
                {
                    Core.Instance.IsMouseVisible = true;
                }
            }

        }

        /// <summary>
        /// Called every frame to draw the scene.
        /// </summary>
        public abstract void Draw(GameTime gameTime);

        /// <summary>
        /// Called every frame to draw full-screen effects underneath the main scene content.
        /// </summary>
        public virtual void DrawUnderlay(GameTime gameTime) { }

        public void ResetInputBlockTimer()
        {
            _inputBlockTimer = INPUT_BLOCK_DURATION;
        }

        /// <summary>
        /// When overridden in a derived class, provides the screen bounds of the first selectable UI element.
        /// Returns null if there are no selectable elements.
        /// </summary>
        /// <returns>A nullable Rectangle representing the bounds of the first element.</returns>
        protected virtual Rectangle? GetFirstSelectableElementBounds()
        {
            return null;
        }

        /// <summary>
        /// Checks for a selectable element and moves the mouse to its center.
        /// This should be called in the child's Enter() method after its UI is initialized.
        /// </summary>
        protected void PositionMouseOnFirstSelectable()
        {
            var firstElementBounds = GetFirstSelectableElementBounds();
            if (firstElementBounds.HasValue)
            {
                Point screenPos = Core.TransformVirtualToScreen(firstElementBounds.Value.Center);
                Mouse.SetPosition(screenPos.X, screenPos.Y);

                Core.Instance.IsMouseVisible = false;
                keyboardNavigatedLastFrame = true;
            }
        }

        public virtual void DrawOverlay(GameTime gameTime) { }
    }
}