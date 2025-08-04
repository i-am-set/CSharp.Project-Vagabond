using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

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
        Settings,
        Encounter,
        Combat
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
        protected readonly Core _core;

        protected MouseState previousMouseState;
        protected KeyboardState _previousKeyboardState;
        protected bool keyboardNavigatedLastFrame = false;
        protected bool firstTimeOpened = true;

        private const float INPUT_BLOCK_DURATION = 0.1f;
        private float _inputBlockTimer = 0f;

        protected SceneIntroAnimator _introAnimator;

        /// <summary>
        /// The input device used to navigate to this scene.
        /// </summary>
        public InputDevice LastUsedInputForNav { get; set; } = InputDevice.Mouse;

        /// <summary>
        /// Returns true if the scene is currently blocking input, e.g., for a short duration after entering.
        /// </summary>
        protected bool IsInputBlocked => _inputBlockTimer > 0;

        protected GameScene()
        {
            _core = ServiceLocator.Get<Core>();
        }

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
            _previousKeyboardState = Keyboard.GetState();
            _inputBlockTimer = INPUT_BLOCK_DURATION;

            _introAnimator = new SceneIntroAnimator();
            var contentBounds = GetAnimatedBounds();
            var animationBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            _introAnimator.Start(animationBounds, contentBounds);

            if (this.LastUsedInputForNav == InputDevice.Keyboard)
            {
                _core.IsMouseVisible = false;
            }
            else // Mouse was used to enter
            {
                _core.IsMouseVisible = true;
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
            _introAnimator?.Update(gameTime);

            if (_inputBlockTimer > 0)
            {
                _inputBlockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (keyboardNavigatedLastFrame)
            {
                keyboardNavigatedLastFrame = false;
            }
            else if (currentMouseState.Position != previousMouseState.Position)
            {
                if (!IsInputBlocked)
                {
                    _core.IsMouseVisible = true;
                }
            }

            // Update previous states at the end of the frame for the next frame's logic
            previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        /// <summary>
        /// Called every frame to draw the scene.
        /// </summary>
        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Get the transform from the animator. It will be Matrix.Identity if not animating.
            Matrix contentTransform = _introAnimator?.GetContentTransform() ?? Matrix.Identity;

            // The scene content is always drawn first, with the potential transformation.
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: contentTransform);
            DrawSceneContent(spriteBatch, font, gameTime);
            spriteBatch.End();

            // The animator, if active, draws its mask on top of the scene.
            if (_introAnimator != null && !_introAnimator.IsComplete)
            {
                // The animator's Draw method now only draws the mask and handles its own Begin/End.
                _introAnimator.Draw(spriteBatch, font, gameTime, null);
            }
        }

        /// <summary>
        /// When implemented in a derived class, draws the primary content of the scene.
        /// This is called by the base Draw method, potentially within an animation context.
        /// </summary>
        protected abstract void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime);


        /// <summary>
        /// Called every frame to draw full-screen effects underneath the main scene content.
        /// </summary>
        public virtual void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime) { }

        /// <summary>
        /// Called every frame to draw full-screen effects over the main scene content.
        /// </summary>
        public virtual void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime) { }

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
        /// When overridden in a derived class, provides the bounds for the main element to be animated in.
        /// </summary>
        protected abstract Rectangle GetAnimatedBounds();

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

                _core.IsMouseVisible = false;
                keyboardNavigatedLastFrame = true;
            }
        }
    }
}