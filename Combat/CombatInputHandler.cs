using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.Combat.UI;
using System;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Processes mouse and keyboard input for the combat scene, translating it into
    /// commands for the CombatManager.
    /// </summary>
    public class CombatInputHandler
    {
        private readonly CombatManager _combatManager;
        private readonly HandRenderer _leftHandRenderer;
        private readonly HandRenderer _rightHandRenderer;
        private readonly ActionMenu _leftActionMenu;
        private readonly ActionMenu _rightActionMenu;

        // --- TUNING CONSTANTS ---
        private const float KEY_INITIAL_DELAY = 0.4f;
        private const float KEY_REPEAT_DELAY = 0.1f;

        // Input state
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private float _keyRepeatTimer = 0f;

        // Selection state
        private HandType _focusedHand = HandType.Left;
        private int _leftSelectedIndex = 0;
        private int _rightSelectedIndex = 0;

        public int LeftSelectedIndex => _leftSelectedIndex;
        public int RightSelectedIndex => _rightSelectedIndex;
        public HandType FocusedHand => _focusedHand;

        public CombatInputHandler(CombatManager combatManager, HandRenderer leftHandRenderer, HandRenderer rightHandRenderer, ActionMenu leftActionMenu, ActionMenu rightActionMenu)
        {
            _combatManager = combatManager;
            _leftHandRenderer = leftHandRenderer;
            _rightHandRenderer = rightHandRenderer;
            _leftActionMenu = leftActionMenu;
            _rightActionMenu = rightActionMenu;
        }

        /// <summary>
        /// Resets the input handler's state, typically at the start of a new combat.
        /// </summary>
        public void Reset()
        {
            _focusedHand = HandType.Left;
            _leftSelectedIndex = 0;
            _rightSelectedIndex = 0;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(mouseState.Position);

            HandleMouseInput(mouseState, virtualMousePos);
            HandleKeyboardInput(gameTime, keyboardState);

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        private void HandleMouseInput(MouseState mouseState, Vector2 virtualMousePos)
        {
            bool mouseMoved = mouseState.Position != _previousMouseState.Position;

            // --- Menu Hover Logic (Mouse Priority) ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                // Check left menu
                for (int i = 0; i < _leftActionMenu.TileBounds.Count; i++)
                {
                    if (_leftActionMenu.TileBounds[i].Contains(virtualMousePos))
                    {
                        if (mouseMoved)
                        {
                            _focusedHand = HandType.Left;
                            _leftSelectedIndex = i;
                        }
                        if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                        {
                            _combatManager.SelectAction(HandType.Left, _leftActionMenu.Actions[i].Id);
                        }
                        return; // Mouse is over a menu, no need to check other inputs
                    }
                }

                // Check right menu
                for (int i = 0; i < _rightActionMenu.TileBounds.Count; i++)
                {
                    if (_rightActionMenu.TileBounds[i].Contains(virtualMousePos))
                    {
                        if (mouseMoved)
                        {
                            _focusedHand = HandType.Right;
                            _rightSelectedIndex = i;
                        }
                        if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                        {
                            _combatManager.SelectAction(HandType.Right, _rightActionMenu.Actions[i].Id);
                        }
                        return;
                    }
                }
            }

            // --- Hand Cancellation Click ---
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (!string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId) && _leftHandRenderer.Bounds.Contains(virtualMousePos))
                {
                    _combatManager.CancelAction(HandType.Left);
                }
                else if (!string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId) && _rightHandRenderer.Bounds.Contains(virtualMousePos))
                {
                    _combatManager.CancelAction(HandType.Right);
                }
            }
        }

        private void HandleKeyboardInput(GameTime gameTime, KeyboardState keyboardState)
        {
            // --- State-Specific Input ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                HandleMenuNavigation(gameTime, keyboardState);
            }
            else if (_combatManager.CurrentState == PlayerTurnState.Confirming)
            {
                HandleConfirmationInput(keyboardState);
            }
        }

        private void HandleMenuNavigation(GameTime gameTime, KeyboardState keyboardState)
        {
            // --- Focus Switching ---
            if (keyboardState.IsKeyDown(Keys.Tab) && _previousKeyboardState.IsKeyUp(Keys.Tab))
            {
                _focusedHand = (_focusedHand == HandType.Left) ? HandType.Right : HandType.Left;
            }

            // --- Selection ---
            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_focusedHand == HandType.Left)
                {
                    _combatManager.SelectAction(HandType.Left, _leftActionMenu.Actions[_leftSelectedIndex].Id);
                }
                else
                {
                    _combatManager.SelectAction(HandType.Right, _rightActionMenu.Actions[_rightSelectedIndex].Id);
                }
            }

            // --- Navigation (with repeat delay) ---
            bool leftPressed = keyboardState.IsKeyDown(Keys.Left);
            bool rightPressed = keyboardState.IsKeyDown(Keys.Right);

            if (leftPressed || rightPressed)
            {
                bool isInitialPress = (leftPressed && _previousKeyboardState.IsKeyUp(Keys.Left)) ||
                                      (rightPressed && _previousKeyboardState.IsKeyUp(Keys.Right));

                _keyRepeatTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (isInitialPress || _keyRepeatTimer <= 0)
                {
                    _keyRepeatTimer = isInitialPress ? KEY_INITIAL_DELAY : KEY_REPEAT_DELAY;
                    int direction = rightPressed ? 1 : -1;
                    NavigateFocusedMenu(direction);
                }
            }
            else
            {
                _keyRepeatTimer = 0;
            }
        }

        private void NavigateFocusedMenu(int direction)
        {
            if (_focusedHand == HandType.Left)
            {
                _leftSelectedIndex = (_leftSelectedIndex + direction + _leftActionMenu.Actions.Count) % _leftActionMenu.Actions.Count;
            }
            else
            {
                _rightSelectedIndex = (_rightSelectedIndex + direction + _rightActionMenu.Actions.Count) % _rightActionMenu.Actions.Count;
            }
        }

        private void HandleConfirmationInput(KeyboardState keyboardState)
        {
            // Confirm turn
            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                _combatManager.ConfirmTurn();
            }

            // Cancel turn
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                _combatManager.CancelTurn();
            }
        }
    }
}