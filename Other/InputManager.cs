using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace ProjectVagabond
{
    public enum InputDeviceType
    {
        Keyboard,
        Gamepad,
        Mouse
    }

    public class InputManager
    {
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        private GamePadState _currentGamePadState;
        private GamePadState _previousGamePadState;

        private MouseState _currentMouseState;
        private MouseState _previousMouseState;

        public InputDeviceType CurrentInputDevice { get; private set; } = InputDeviceType.Mouse;
        public bool IsMouseActive { get; private set; } = true;

        private const float MOUSE_MOVE_THRESHOLD = 2.0f;
        private const float STICK_THRESHOLD = 0.5f;

        public bool NavigateUp { get; private set; }
        public bool NavigateDown { get; private set; }
        public bool NavigateLeft { get; private set; }
        public bool NavigateRight { get; private set; }
        public bool Confirm { get; private set; }
        public bool Back { get; private set; }

        public void Update()
        {
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            _previousGamePadState = _currentGamePadState;
            _currentGamePadState = GamePad.GetState(PlayerIndex.One);

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            DetectInputDevice();
            UpdateAbstractInputs();
        }

        public MouseState GetEffectiveMouseState()
        {
            if (!IsMouseActive)
            {
                // Return a state with the mouse far off-screen so hit tests fail
                return new MouseState(-9999, -9999, _currentMouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            }
            return _currentMouseState;
        }

        private void DetectInputDevice()
        {
            // Check for Mouse Movement or Clicks
            if (Vector2.Distance(new Vector2(_currentMouseState.X, _currentMouseState.Y), new Vector2(_previousMouseState.X, _previousMouseState.Y)) > MOUSE_MOVE_THRESHOLD ||
                _currentMouseState.LeftButton == ButtonState.Pressed ||
                _currentMouseState.RightButton == ButtonState.Pressed)
            {
                CurrentInputDevice = InputDeviceType.Mouse;
                IsMouseActive = true;
            }

            // Check for Keyboard Input
            if (_currentKeyboardState.GetPressedKeyCount() > 0)
            {
                // Ignore keys that might be held down from previous context if needed, 
                // but generally any key press switches context
                if (_currentKeyboardState != _previousKeyboardState)
                {
                    CurrentInputDevice = InputDeviceType.Keyboard;
                    IsMouseActive = false;
                }
            }

            // Check for Gamepad Input
            if (_currentGamePadState.IsConnected && _currentGamePadState.PacketNumber != _previousGamePadState.PacketNumber)
            {
                // Simple check: if any button is pressed or sticks moved significantly
                bool buttonPressed = _currentGamePadState.Buttons.A == ButtonState.Pressed ||
                                     _currentGamePadState.Buttons.B == ButtonState.Pressed ||
                                     _currentGamePadState.Buttons.X == ButtonState.Pressed ||
                                     _currentGamePadState.Buttons.Y == ButtonState.Pressed ||
                                     _currentGamePadState.DPad.Up == ButtonState.Pressed ||
                                     _currentGamePadState.DPad.Down == ButtonState.Pressed ||
                                     _currentGamePadState.DPad.Left == ButtonState.Pressed ||
                                     _currentGamePadState.DPad.Right == ButtonState.Pressed;

                bool stickMoved = _currentGamePadState.ThumbSticks.Left.Length() > 0.2f;

                if (buttonPressed || stickMoved)
                {
                    CurrentInputDevice = InputDeviceType.Gamepad;
                    IsMouseActive = false;
                }
            }
        }

        private void UpdateAbstractInputs()
        {
            NavigateUp = IsKeyPressed(Keys.Up) || IsKeyPressed(Keys.W) || IsButtonJustPressed(Buttons.DPadUp) || IsStickJustMoved(Vector2.UnitY);
            NavigateDown = IsKeyPressed(Keys.Down) || IsKeyPressed(Keys.S) || IsButtonJustPressed(Buttons.DPadDown) || IsStickJustMoved(-Vector2.UnitY);
            NavigateLeft = IsKeyPressed(Keys.Left) || IsKeyPressed(Keys.A) || IsButtonJustPressed(Buttons.DPadLeft) || IsStickJustMoved(-Vector2.UnitX);
            NavigateRight = IsKeyPressed(Keys.Right) || IsKeyPressed(Keys.D) || IsButtonJustPressed(Buttons.DPadRight) || IsStickJustMoved(Vector2.UnitX);

            Confirm = IsKeyPressed(Keys.Space) || IsKeyPressed(Keys.Enter) || IsButtonJustPressed(Buttons.A);
            Back = IsKeyPressed(Keys.Escape) || IsButtonJustPressed(Buttons.B);
        }

        private bool IsKeyPressed(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private bool IsButtonJustPressed(Buttons button)
        {
            return _currentGamePadState.IsButtonDown(button) && _previousGamePadState.IsButtonUp(button);
        }

        // Helper to detect "Just Moved" for analog sticks to simulate discrete navigation
        private bool IsStickJustMoved(Vector2 direction)
        {
            Vector2 currentStick = _currentGamePadState.ThumbSticks.Left;
            Vector2 prevStick = _previousGamePadState.ThumbSticks.Left;

            // Dot product to check alignment with direction
            float currentDot = Vector2.Dot(currentStick, direction);
            float prevDot = Vector2.Dot(prevStick, direction);

            return currentDot > STICK_THRESHOLD && prevDot <= STICK_THRESHOLD;
        }
    }
}