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
        /// <summary>
        /// The minimum distance the mouse must move (in virtual pixels) after clicking a card
        /// before a drag operation officially begins.
        /// </summary>
        public const float DRAG_START_THRESHOLD = 8f;

        // Input state
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private Vector2 _previousVirtualMousePosition;

        // Drag & Drop State
        public CombatCard DraggedCard { get; private set; }
        public CombatCard HeldCard { get; private set; } // Card being held on click, but not yet dragged
        private Vector2 _dragStartOffset;
        public Vector2 DragStartPosition { get; private set; } // Mouse position where the click started
        public HandType PotentialDropHand { get; private set; }
        public HandType InvalidDropHand { get; private set; }
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
            _previousVirtualMousePosition = Core.TransformMouse(_previousMouseState.Position);
            DraggedCard = null;
            HeldCard = null;
            PotentialDropHand = HandType.None;
            InvalidDropHand = HandType.None;
        }

        public void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            VirtualMousePosition = Core.TransformMouse(mouseState.Position);

            HandleMouseInput(gameTime, mouseState, keyboardState);
            HandleKeyboardInput(gameTime, keyboardState);

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
            _previousVirtualMousePosition = VirtualMousePosition;
        }

        private void HandleMouseInput(GameTime gameTime, MouseState mouseState, KeyboardState keyboardState)
        {
            bool isClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool isClickPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool isClickHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool isRightClickPressed = mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            // --- State: Selecting Actions ---
            if (_combatManager.CurrentState == PlayerTurnState.Selecting)
            {
                // Check for cancellation first (right click or escape)
                if (isRightClickPressed || (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape)))
                {
                    if (DraggedCard != null || HeldCard != null)
                    {
                        CancelDrag();
                        return; // End processing for this frame
                    }
                }

                // Handle an ongoing drag
                if (DraggedCard != null)
                {
                    HandleCardDrag(gameTime, mouseState, isClickReleased);
                }
                // Handle a held card (primed for dragging)
                else if (HeldCard != null)
                {
                    if (isClickHeld) // Mouse is still down
                    {
                        float distSquared = Vector2.DistanceSquared(VirtualMousePosition, DragStartPosition);
                        if (distSquared > DRAG_START_THRESHOLD * DRAG_START_THRESHOLD)
                        {
                            // Threshold exceeded, start the actual drag
                            DraggedCard = HeldCard;
                            HeldCard = null; // No longer just held

                            DraggedCard.IsBeingDragged = true;
                            DraggedCard.StartDragSway();
                            _dragStartOffset = new Vector2(DraggedCard.CurrentBounds.Width / 2f, DraggedCard.CurrentBounds.Height / 2f);

                            // Initial animation values when drag begins
                            DraggedCard.AnimateTo(
                                position: DraggedCard.CurrentBounds.Position,
                                scale: 1.2f,
                                tint: Color.White,
                                rotation: 0f, // Ensure card is upright when dragged
                                alpha: 1f,
                                shadowAlpha: 0.5f
                            );
                        }
                    }

                    if (isClickReleased) // Click was released without moving enough to drag
                    {
                        HeldCard = null;
                    }
                }
                // Handle a new click to start holding a card
                else if (isClickPressed)
                {
                    // Iterate backwards so cards on top are picked first
                    for (int i = _actionHandUI.Cards.Count - 1; i >= 0; i--)
                    {
                        var card = _actionHandUI.Cards[i];
                        if (card.CurrentBounds.Contains(VirtualMousePosition))
                        {
                            HeldCard = card;
                            DragStartPosition = VirtualMousePosition;
                            break;
                        }
                    }
                }
            }


            // --- Hand Cancellation Click (can happen in any state) ---
            if (isClickPressed && DraggedCard == null && HeldCard == null)
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
            if (DraggedCard != null)
            {
                DraggedCard.StopDragSway();
                DraggedCard.IsBeingDragged = false;
            }

            DraggedCard = null;
            HeldCard = null;
            PotentialDropHand = HandType.None;
            InvalidDropHand = HandType.None;
            _leftHandRenderer.IsPotentialDropTarget = false;
            _rightHandRenderer.IsPotentialDropTarget = false;
            _leftHandRenderer.IsInvalidDropTarget = false;
            _rightHandRenderer.IsInvalidDropTarget = false;
        }


        private void HandleCardDrag(GameTime gameTime, MouseState mouseState, bool isClickReleased)
        {
            Vector2 velocity = VirtualMousePosition - _previousVirtualMousePosition;

            DraggedCard.SetDragVelocity(velocity);
            DraggedCard.ForcePosition(VirtualMousePosition - _dragStartOffset);
            DraggedCard.Update(gameTime);


            var core = ServiceLocator.Get<Core>();
            Rectangle actualScreenVirtualBounds = core.GetActualScreenVirtualBounds();

            // Determine potential drop target based on the visible screen area
            float dropZoneHeight = actualScreenVirtualBounds.Height * DROP_ZONE_TOP_PERCENTAGE;
            var dropZone = new RectangleF(actualScreenVirtualBounds.X, actualScreenVirtualBounds.Y, actualScreenVirtualBounds.Width, dropZoneHeight);

            PotentialDropHand = HandType.None;
            InvalidDropHand = HandType.None;

            if (dropZone.Contains(VirtualMousePosition))
            {
                if (VirtualMousePosition.X < actualScreenVirtualBounds.Center.X)
                {
                    if (string.IsNullOrEmpty(_combatManager.LeftHand.SelectedActionId))
                    {
                        PotentialDropHand = HandType.Left;
                    }
                    else
                    {
                        InvalidDropHand = HandType.Left;
                    }
                }
                else // Right side
                {
                    if (string.IsNullOrEmpty(_combatManager.RightHand.SelectedActionId))
                    {
                        PotentialDropHand = HandType.Right;
                    }
                    else
                    {
                        InvalidDropHand = HandType.Right;
                    }
                }
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
            _leftHandRenderer.IsInvalidDropTarget = (InvalidDropHand == HandType.Left);
            _rightHandRenderer.IsInvalidDropTarget = (InvalidDropHand == HandType.Right);

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

                // Reset drag state regardless of success
                CancelDrag();
            }
        }

        private void HandleKeyboardInput(GameTime gameTime, KeyboardState keyboardState)
        {
            // Drag cancellation is handled in HandleMouseInput to ensure correct priority
            if (DraggedCard != null || HeldCard != null) return;

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