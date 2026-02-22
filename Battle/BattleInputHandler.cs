using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class BattleInputHandler
    {
        public event Action OnBackRequested;
        private int _hoveredTargetIndex = -1;
        public int HoveredTargetIndex => _hoveredTargetIndex;

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        public BattleInputHandler()
        {
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Reset()
        {
            _hoveredTargetIndex = -1;
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void ResetHover(BattleUIManager uiManager)
        {
            _hoveredTargetIndex = -1;
            uiManager.CombatantHoveredViaSprite = null;
        }

        public void Update(
            GameTime gameTime,
            BattleUIManager uiManager,
            BattleRenderer renderer)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            if (battleManager.CurrentPhase != BattleManager.BattlePhase.ActionSelection)
            {
                _hoveredTargetIndex = -1;
                uiManager.CombatantHoveredViaSprite = null;
                return;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                OnBackRequested?.Invoke();
            }

            // --- HOVER DETECTION ---
            var currentTargets = renderer.GetCurrentTargets();
            _hoveredTargetIndex = -1;

            // Check UI Buttons First
            var uiHoveredCombatant = uiManager.HoveredCombatantFromUI;
            if (uiHoveredCombatant != null)
            {
                for (int i = 0; i < currentTargets.Count; i++)
                {
                    if (currentTargets[i].Combatant == uiHoveredCombatant)
                    {
                        _hoveredTargetIndex = i;
                        break;
                    }
                }
            }
            else
            {
                // Fallback to Sprite Hover
                for (int i = 0; i < currentTargets.Count; i++)
                {
                    if (currentTargets[i].Bounds.Contains(virtualMousePos))
                    {
                        _hoveredTargetIndex = i;
                        break;
                    }
                }
            }

            if (_hoveredTargetIndex != -1)
            {
                uiManager.CombatantHoveredViaSprite = currentTargets[_hoveredTargetIndex].Combatant;
                if (uiManager.UIState == BattleUIState.Targeting)
                {
                    ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickable);
                }
                else if (uiManager.UIState == BattleUIState.Default)
                {
                    ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickableHint);
                }
            }
            else
            {
                uiManager.CombatantHoveredViaSprite = null;
            }

            // --- CLICK HANDLING ---
            // Right Click to Go Back
            if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
            {
                if (uiManager.UIState == BattleUIState.Targeting)
                {
                    OnBackRequested?.Invoke();
                }
            }

            // Handle Click on Sprites
            var inputManager = ServiceLocator.Get<InputManager>();
            if (inputManager.IsMouseClickAvailable() && currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                if (_hoveredTargetIndex != -1 && uiHoveredCombatant == null)
                {
                    var selectedTarget = currentTargets[_hoveredTargetIndex].Combatant;

                    // Route click to UI Manager
                    uiManager.HandleSpriteClick(selectedTarget);
                    inputManager.ConsumeMouseClick();
                }
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}