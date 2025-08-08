using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using ProjectVagabond.Combat;
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
        private readonly ActionHandUI _actionHandUI;

        // Input state
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // Drag & Drop State
        public CombatCard DraggedCard { get; private set; }
        private Vector2 _dragStartOffset;
        private HandType _potentialDropHand;

        public Vector2 VirtualMousePosition { get; private set; }

        public CombatInputHandler(CombatManager combatManager, HandRenderer leftHandRenderer, HandRenderer rightHandRenderer, ActionHandUI actionHandUI)
        {
            _combatManager = combatManager;
            _leftHandRenderer = leftHandRenderer;
            _rightHandRenderer = rightHandRenderer;
            _actionHandUI = actionHandUI;
        }

        /// <summary>
        /// Resets the input handler's state, typically at the start of a new combat.
        /// </summary>
        public void Reset()
        {
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
            DraggedCard = null;
            _potentialDropHand = HandType.None;
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            VirtualMousePosition = Core.TransformMouse(mouseState.Position);

            HandleMouseInput(mouseState);
            HandleKeyboardInput(gameTime, keyboardState);

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void HandleMouseInput(MouseState mouseState)
        {
            bool isClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool isClickPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            // --- State: Selecting Actions ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                // If we are currently dragging a card
                if (DraggedCard != null)
                {
                    HandleCardDrag(mouseState, isClickReleased);
                }
                // If we are not dragging, check if we should start dragging
                else if (isClickPressed)
                {
                    // Iterate backwards so cards on top are picked first
                    for (int i = _actionHandUI.Cards.Count - 1; i >= 0; i--)
                    {
                        var card = _actionHandUI.Cards[i];
                        if (card.CurrentBounds.Contains(VirtualMousePosition))
                        {
                            DraggedCard = card;
                            DraggedCard.IsBeingDragged = true;
                            // Set offset to snap the card's center to the mouse cursor
                            _dragStartOffset = new Vector2(card.CurrentBounds.Width / 2f, card.CurrentBounds.Height / 2f);

                            // Trigger "pick up" animation
                            DraggedCard.AnimateTo(
                                position: card.CurrentBounds.Position,
                                scale: 1.15f,
                                tint: Color.White,
                                rotation: 0f,
                                alpha: 1f,
                                shadowAlpha: 0.5f
                            );
                            break;
                        }
                    }
                }
            }

            // --- Hand Cancellation Click (can happen in any state) ---
            if (isClickPressed && DraggedCard == null)
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

        private void HandleCardDrag(MouseState mouseState, bool isClickReleased)
        {
            // Update card position to follow mouse
            DraggedCard.ForcePosition(VirtualMousePosition - _dragStartOffset);

            // Determine potential drop target (top 90% of the screen)
            float dropZoneHeight = Global.VIRTUAL_HEIGHT * 0.9f;
            var dropZone = new RectangleF(0, 0, Global.VIRTUAL_WIDTH, dropZoneHeight);

            if (dropZone.Contains(VirtualMousePosition))
            {
                _potentialDropHand = (VirtualMousePosition.X < Global.VIRTUAL_WIDTH / 2) ? HandType.Left : HandType.Right;
            }
            else
            {
                _potentialDropHand = HandType.None;
            }

            // Update hand renderers to show they are drop targets
            _leftHandRenderer.IsPotentialDropTarget = (_potentialDropHand == HandType.Left);
            _rightHandRenderer.IsPotentialDropTarget = (_potentialDropHand == HandType.Right);

            // Handle dropping the card
            if (isClickReleased)
            {
                if (_potentialDropHand == HandType.Left && string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId))
                {
                    _combatManager.SelectAction(HandType.Left, DraggedCard.Action.Id);
                }
                else if (_potentialDropHand == HandType.Right && string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId))
                {
                    _combatManager.SelectAction(HandType.Right, DraggedCard.Action.Id);
                }

                // Reset drag state
                DraggedCard.IsBeingDragged = false;
                DraggedCard = null;
                _potentialDropHand = HandType.None;
                _leftHandRenderer.IsPotentialDropTarget = false;
                _rightHandRenderer.IsPotentialDropTarget = false;
            }
        }

        private void HandleKeyboardInput(GameTime gameTime, KeyboardState keyboardState)
        {
            // --- State-Specific Input ---
            if (_combatManager.CurrentState == PlayerTurnState.Confirming)
            {
                HandleConfirmationInput(keyboardState);
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