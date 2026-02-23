using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
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
        public bool MouseMovedThisFrame { get; private set; }

        // Threshold for detecting ANY movement (responsiveness)
        private const float MOUSE_MOVE_THRESHOLD = 0.5f;
        // Threshold for switching FROM Gamepad/Keyboard TO Mouse (drift prevention)
        private const float MOUSE_WAKE_THRESHOLD = 10.0f;

        private const float STICK_THRESHOLD = 0.5f;

        public bool NavigateUp { get; private set; }
        public bool NavigateDown { get; private set; }
        public bool NavigateLeft { get; private set; }
        public bool NavigateRight { get; private set; }
        public bool Confirm { get; private set; }
        public bool Back { get; private set; }

        private bool _mouseClickConsumed;
        private bool _ignoreMouseUntilMovement;

        public void Update()
        {
            _mouseClickConsumed = false;

            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            _previousGamePadState = _currentGamePadState;
            _currentGamePadState = GamePad.GetState(PlayerIndex.One);

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            float mouseDistance = Vector2.Distance(new Vector2(_currentMouseState.X, _currentMouseState.Y), new Vector2(_previousMouseState.X, _previousMouseState.Y));
            MouseMovedThisFrame = mouseDistance > MOUSE_MOVE_THRESHOLD;

            DetectInputDevice(mouseDistance);
            UpdateAbstractInputs();
        }

        public void ConsumeMouseClick()
        {
            _mouseClickConsumed = true;
        }

        public bool IsMouseClickAvailable()
        {
            return !_mouseClickConsumed;
        }

        public MouseState GetEffectiveMouseState()
        {
            if (!IsMouseActive)
            {
                return new MouseState(-9999, -9999, _currentMouseState.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            }
            return _currentMouseState;
        }

        private void DetectInputDevice(float mouseDistance)
        {
            bool isSignificantMovement = mouseDistance > MOUSE_WAKE_THRESHOLD;
            bool isClick = _currentMouseState.LeftButton == ButtonState.Pressed || _currentMouseState.RightButton == ButtonState.Pressed;

            // Check for Keyboard activity
            bool keyboardActive = false;
            if (_currentKeyboardState.GetPressedKeyCount() > 0 && _currentKeyboardState != _previousKeyboardState)
            {
                keyboardActive = true;
            }

            // Check for Gamepad activity
            bool gamepadActive = false;
            if (_currentGamePadState.IsConnected && _currentGamePadState.PacketNumber != _previousGamePadState.PacketNumber)
            {
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
                    gamepadActive = true;
                }
            }

            // Prioritize explicit controller/keyboard input
            if (keyboardActive)
            {
                CurrentInputDevice = InputDeviceType.Keyboard;
                IsMouseActive = false;
                _ignoreMouseUntilMovement = true;
            }
            else if (gamepadActive)
            {
                CurrentInputDevice = InputDeviceType.Gamepad;
                IsMouseActive = false;
                _ignoreMouseUntilMovement = true;
            }
            else
            {
                // Only switch back to mouse if we overcome the ignore flag
                if (_ignoreMouseUntilMovement)
                {
                    if (isSignificantMovement || isClick)
                    {
                        _ignoreMouseUntilMovement = false;
                        CurrentInputDevice = InputDeviceType.Mouse;
                        IsMouseActive = true;
                    }
                }
                else
                {
                    // Normal mouse behavior
                    if (CurrentInputDevice != InputDeviceType.Mouse)
                    {
                        if (isSignificantMovement || isClick)
                        {
                            CurrentInputDevice = InputDeviceType.Mouse;
                            IsMouseActive = true;
                        }
                    }
                    else
                    {
                        // If already mouse, keep it mouse on any movement
                        if (MouseMovedThisFrame || isClick)
                        {
                            CurrentInputDevice = InputDeviceType.Mouse;
                            IsMouseActive = true;
                        }
                    }
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

        private bool IsStickJustMoved(Vector2 direction)
        {
            Vector2 currentStick = _currentGamePadState.ThumbSticks.Left;
            Vector2 prevStick = _previousGamePadState.ThumbSticks.Left;

            float currentDot = Vector2.Dot(currentStick, direction);
            float prevDot = Vector2.Dot(prevStick, direction);

            return currentDot > STICK_THRESHOLD && prevDot <= STICK_THRESHOLD;
        }
    }
}