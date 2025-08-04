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
        private int _leftSelectedIndex = -1;
        private int _rightSelectedIndex = -1;

        public int LeftSelectedIndex => _leftSelectedIndex;
        public int RightSelectedIndex => _rightSelectedIndex;
        public HandType FocusedHand => _focusedHand;
        public Vector2 VirtualMousePosition { get; private set; }

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
            _leftSelectedIndex = -1; // Start with no card selected
            _rightSelectedIndex = -1; // Start with no card selected
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public int GetSelectedIndexForHand(HandType hand)
        {
            return hand == HandType.Left ? _leftSelectedIndex : _rightSelectedIndex;
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            VirtualMousePosition = Core.TransformMouse(mouseState.Position);

            HandleMouseInput(mouseState);
            HandleKeyboardInput(gameTime, keyboardState);

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        private void HandleMouseInput(MouseState mouseState)
        {
            bool mouseMoved = mouseState.Position != _previousMouseState.Position;
            bool isClick = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            // --- State: Selecting Actions ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                bool cardInteractionFound = false;

                // Check left menu for hover or click
                for (int i = 0; i < _leftActionMenu.Cards.Count; i++)
                {
                    if (_leftActionMenu.Cards[i].CurrentBounds.Contains(VirtualMousePosition))
                    {
                        cardInteractionFound = true;
                        if (mouseMoved)
                        {
                            _focusedHand = HandType.Left;
                            _leftSelectedIndex = i;
                        }
                        if (isClick)
                        {
                            _combatManager.SelectAction(HandType.Left, _leftActionMenu.Cards[i].Action.Id);
                            return; // A click is a terminal action for this input frame
                        }
                        break; // Found hover, no need to check other cards in this menu
                    }
                }

                // Check right menu if no interaction on left
                if (!cardInteractionFound)
                {
                    for (int i = 0; i < _rightActionMenu.Cards.Count; i++)
                    {
                        if (_rightActionMenu.Cards[i].CurrentBounds.Contains(VirtualMousePosition))
                        {
                            cardInteractionFound = true;
                            if (mouseMoved)
                            {
                                _focusedHand = HandType.Right;
                                _rightSelectedIndex = i;
                            }
                            if (isClick)
                            {
                                _combatManager.SelectAction(HandType.Right, _rightActionMenu.Cards[i].Action.Id);
                                return; // A click is a terminal action for this input frame
                            }
                            break; // Found hover
                        }
                    }
                }

                // If mouse moved but no card was hovered, handle deselection and focus change
                if (mouseMoved && !cardInteractionFound)
                {
                    // If the mouse is in an activation area but not over a card,
                    // focus that hand and deselect its current card.
                    if (_leftActionMenu.ActivationArea.Contains(VirtualMousePosition))
                    {
                        _focusedHand = HandType.Left;
                        _leftSelectedIndex = -1;
                    }
                    else if (_rightActionMenu.ActivationArea.Contains(VirtualMousePosition))
                    {
                        _focusedHand = HandType.Right;
                        _rightSelectedIndex = -1;
                    }
                    else // If the mouse is outside both activation areas, deselect everything.
                    {
                        _leftSelectedIndex = -1;
                        _rightSelectedIndex = -1;
                    }
                }
            }

            // --- Hand Cancellation Click (can happen in any state) ---
            if (isClick)
            {
                if (!string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId) && _leftHandRenderer.Bounds.Contains(VirtualMousePosition))
                {
                    _combatManager.CancelAction(HandType.Left);
                }
                else if (!string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId) && _rightHandRenderer.Bounds.Contains(VirtualMousePosition))
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
                    if (_leftSelectedIndex >= 0 && _leftSelectedIndex < _leftActionMenu.Cards.Count)
                    {
                        _combatManager.SelectAction(HandType.Left, _leftActionMenu.Cards[_leftSelectedIndex].Action.Id);
                    }
                }
                else
                {
                    if (_rightSelectedIndex >= 0 && _rightSelectedIndex < _rightActionMenu.Cards.Count)
                    {
                        _combatManager.SelectAction(HandType.Right, _rightActionMenu.Cards[_rightSelectedIndex].Action.Id);
                    }
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
                int count = _leftActionMenu.Cards.Count;
                if (count == 0) return;

                if (_leftSelectedIndex == -1)
                {
                    // If nothing is selected, start at the beginning or end
                    _leftSelectedIndex = (direction > 0) ? 0 : count - 1;
                }
                else
                {
                    // Cycle through the available cards
                    _leftSelectedIndex = (_leftSelectedIndex + direction + count) % count;
                }
            }
            else // Right Hand
            {
                int count = _rightActionMenu.Cards.Count;
                if (count == 0) return;

                if (_rightSelectedIndex == -1)
                {
                    // If nothing is selected, start at the beginning or end
                    _rightSelectedIndex = (direction > 0) ? 0 : count - 1;
                }
                else
                {
                    // Cycle through the available cards
                    _rightSelectedIndex = (_rightSelectedIndex + direction + count) % count;
                }
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