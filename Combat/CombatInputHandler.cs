﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.FSM;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Processes mouse and keyboard input for the combat scene, translating it into
    /// commands for the CombatManager.
    /// </summary>
    public class CombatInputHandler
    {
        private readonly CombatManager _combatManager;
        private readonly ActionHandUI _actionHandUI;
        private readonly HapticsManager _hapticsManager;
        private readonly CombatScene _combatScene;

        // --- TUNING CONSTANTS ---
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
        public Vector2 DragStartPosition { get; private set; } // Mouse position where the click started
        public int? PotentialTargetId { get; private set; }
        private int? _previousPotentialTargetId = null;

        public Vector2 VirtualMousePosition { get; private set; }

        public CombatInputHandler(CombatManager combatManager, ActionHandUI actionHandUI, CombatScene combatScene)
        {
            _combatManager = combatManager;
            _actionHandUI = actionHandUI;
            _combatScene = combatScene;
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
            PotentialTargetId = null;
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
            if (_combatManager.FSM.CurrentState is ActionSelectionState)
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
        }

        private void CancelDrag()
        {
            if (DraggedCard != null)
            {
                DraggedCard.StopDragSway();
                DraggedCard.IsBeingDragged = false;
            }

            // Clear targeting visuals
            if (PotentialTargetId.HasValue)
            {
                _combatScene.SetEntityTargeted(PotentialTargetId.Value, false);
            }
            _combatScene.SetAllEnemiesTargeted(false);


            DraggedCard = null;
            HeldCard = null;
            PotentialTargetId = null;
        }


        private void HandleCardDrag(GameTime gameTime, MouseState mouseState, bool isClickReleased)
        {
            Vector2 velocity = VirtualMousePosition - _previousVirtualMousePosition;

            DraggedCard.SetDragVelocity(velocity);
            // The card is now responsible for centering itself on the cursor position.
            DraggedCard.ForcePosition(VirtualMousePosition);

            // Inform the card of its status so it can adjust its scale
            bool isInsidePlayArea = _combatScene.PlayArea.Contains(VirtualMousePosition);
            DraggedCard.SetDragPlayAreaStatus(isInsidePlayArea);

            DraggedCard.Update(gameTime);

            // --- Targeting Logic ---
            PotentialTargetId = null;
            _combatScene.SetAllEnemiesTargeted(false); // Clear previous AoE highlight

            var actionType = DraggedCard.Action.TargetType;

            // Only check for targets if the card is in the valid play area.
            if (isInsidePlayArea)
            {
                if (actionType == TargetType.AllEnemies)
                {
                    _combatScene.SetAllEnemiesTargeted(true);
                }
                else if (actionType == TargetType.SingleEnemy)
                {
                    PotentialTargetId = _combatScene.FindClosestEnemyTo(VirtualMousePosition)?.EntityId;
                }
                // For Self-cast, we don't need a specific target ID. Being in the play area is enough.
            }


            // Update visual indicators for the potential target
            if (_previousPotentialTargetId.HasValue && _previousPotentialTargetId != PotentialTargetId)
            {
                _combatScene.SetEntityTargeted(_previousPotentialTargetId.Value, false);
            }
            if (PotentialTargetId.HasValue)
            {
                _combatScene.SetEntityTargeted(PotentialTargetId.Value, true);
            }

            // Trigger haptic feedback when entering a target
            if (PotentialTargetId.HasValue && !_previousPotentialTargetId.HasValue)
            {
                _hapticsManager.TriggerWobble(0.5f, 0.1f, 20f);
            }
            _previousPotentialTargetId = PotentialTargetId;

            // Handle dropping the card
            if (isClickReleased)
            {
                bool actionPlayed = false;

                // Only proceed if the card is dropped within the valid play area.
                if (isInsidePlayArea)
                {
                    if (actionType == TargetType.SingleEnemy && PotentialTargetId.HasValue)
                    {
                        _combatManager.AddPlayerAction(DraggedCard.Action.Id, new List<int> { PotentialTargetId.Value });
                        actionPlayed = true;
                    }
                    else if (actionType == TargetType.AllEnemies)
                    {
                        _combatManager.AddPlayerAction(DraggedCard.Action.Id, _combatScene.GetAllEnemyIds());
                        actionPlayed = true;
                    }
                    else if (actionType == TargetType.Self)
                    {
                        _combatManager.AddPlayerAction(DraggedCard.Action.Id, new List<int>()); // Empty list for self-cast
                        actionPlayed = true;
                    }
                }

                if (actionPlayed)
                {
                    _hapticsManager.TriggerShake(1.5f, 0.15f);
                }

                // Reset drag state regardless of success or cancellation.
                CancelDrag();
            }
        }

        private void HandleKeyboardInput(GameTime gameTime, KeyboardState keyboardState)
        {
            // Drag cancellation is handled in HandleMouseInput to ensure correct priority
            if (DraggedCard != null || HeldCard != null) return;

            // No keyboard shortcuts for turn confirmation in a single-action system yet.
        }
    }
}
