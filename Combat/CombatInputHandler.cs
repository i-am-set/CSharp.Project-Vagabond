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
        private readonly HapticsManager _hapticsManager;

        // --- TUNING CONSTANTS ---
        /// <summary>
        /// Defines the vertical portion of the screen that acts as the drop zone for cards.
        /// 0.9f means the top 90% of the screen is the drop zone.
        /// </summary>
        public const float DROP_ZONE_TOP_PERCENTAGE = 0.9f;

        // Input state
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // Drag & Drop State
        public CombatCard DraggedCard { get; private set; }
        private Vector2 _dragStartOffset;
        public HandType PotentialDropHand { get; private set; }
        private HandType _previousPotentialDropHand = HandType.None;

        public Vector2 VirtualMousePosition { get; private set; }

        public CombatInputHandler(CombatManager combatManager, HandRenderer leftHandRenderer, HandRenderer rightHandRenderer, ActionHandUI actionHandUI)
        {
            _combatManager = combatManager;
            _leftHandRenderer = leftHandRenderer;
            _rightHandRenderer = rightHandRenderer;
            _actionHandUI = actionHandUI;
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        /// <summary>
        /// Resets the input handler's state, typically at the start of a new combat.
        /// </summary>
        public void Reset()
        {
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
            DraggedCard = null;
            PotentialDropHand = HandType.None;
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            VirtualMousePosition = Core.TransformMouse(mouseState.Position);

            HandleMouseInput(mouseState, keyboardState);
            HandleKeyboardInput(gameTime, keyboardState);

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void HandleMouseInput(MouseState mouseState, KeyboardState keyboardState)
        {
            bool isClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool isClickPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool isRightClickPressed = mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            // --- State: Selecting Actions ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                // If we are currently dragging a card
                if (DraggedCard != null)
                {
                    // Check for drag cancellation first
                    if (isRightClickPressed || (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape)))
                    {
                        CancelDrag();
                        return;
                    }
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
                            DraggedCard.StartDragSway();
                            // Set offset to snap the card's center to the mouse cursor
                            _dragStartOffset = new Vector2(card.CurrentBounds.Width / 2f, card.CurrentBounds.Height / 2f);

                            // Trigger "pick up" animation with overshoot
                            DraggedCard.AnimateTo(
                                position: card.CurrentBounds.Position,
                                scale: 1.2f, // Overshoot scale
                                tint: Color.White,
                                rotation: (float)(new Random().NextDouble() * 0.1 - 0.05), // Slight random tilt
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

        private void CancelDrag()
        {
            if (DraggedCard == null) return;

            DraggedCard.StopDragSway();
            DraggedCard.IsBeingDragged = false;
            DraggedCard = null;
            PotentialDropHand = HandType.None;
            _leftHandRenderer.IsPotentialDropTarget = false;
            _rightHandRenderer.IsPotentialDropTarget = false;
        }


        private void HandleCardDrag(MouseState mouseState, bool isClickReleased)
        {
            // Update card position to follow mouse
            DraggedCard.ForcePosition(VirtualMousePosition - _dragStartOffset);

            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Determine potential drop target based on the visible screen area
            float dropZoneHeight = actualScreenVirtualBounds.Height * DROP_ZONE_TOP_PERCENTAGE;
            var dropZone = new RectangleF(actualScreenVirtualBounds.X, actualScreenVirtualBounds.Y, actualScreenVirtualBounds.Width, dropZoneHeight);

            if (dropZone.Contains(VirtualMousePosition))
            {
                if (VirtualMousePosition.X < actualScreenVirtualBounds.Center.X)
                {
                    // Only a valid target if the left hand is empty
                    PotentialDropHand = string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId) ? HandType.Left : HandType.None;
                }
                else // Right side
                {
                    // Only a valid target if the right hand is empty
                    PotentialDropHand = string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId) ? HandType.Right : HandType.None;
                }
            }
            else
            {
                PotentialDropHand = HandType.None;
            }

            // Trigger haptic feedback when entering a drop zone
            if (PotentialDropHand != HandType.None && _previousPotentialDropHand == HandType.None)
            {
                _hapticsManager.TriggerWobble(0.5f, 0.1f, 20f);
            }
            _previousPotentialDropHand = PotentialDropHand;


            // Update hand renderers to show they are drop targets
            _leftHandRenderer.IsPotentialDropTarget = (PotentialDropHand == HandType.Left);
            _rightHandRenderer.IsPotentialDropTarget = (PotentialDropHand == HandType.Right);

            // Handle dropping the card
            if (isClickReleased)
            {
                if (PotentialDropHand == HandType.Left)
                {
                    _combatManager.SelectAction(HandType.Left, DraggedCard.Action.Id);
                    _hapticsManager.TriggerShake(1.5f, 0.15f);
                }
                else if (PotentialDropHand == HandType.Right)
                {
                    _combatManager.SelectAction(HandType.Right, DraggedCard.Action.Id);
                    _hapticsManager.TriggerShake(1.5f, 0.15f);
                }

                // Reset drag state
                CancelDrag();
            }
        }

        private void HandleKeyboardInput(GameTime gameTime, KeyboardState keyboardState)
        {
            // Drag cancellation is handled in HandleMouseInput to ensure correct priority
            if (DraggedCard != null) return;

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_combatManager.CurrentState == PlayerTurnState.Confirming)
                {
                    _combatManager.CancelTurn();
                    return;
                }

                if (_combatManager.CurrentState == PlayerTurnState.Selecting)
                {
                    bool leftSelected = !string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId);
                    bool rightSelected = !string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId);

                    if (leftSelected && !rightSelected)
                    {
                        _combatManager.CancelAction(HandType.Left);
                    }
                    else if (!leftSelected && rightSelected)
                    {
                        _combatManager.CancelAction(HandType.Right);
                    }
                }
            }

            if (_combatManager.CurrentState == PlayerTurnState.Confirming)
            {
                if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
                {
                    _combatManager.ConfirmTurn();
                }
            }
        }
    }
}