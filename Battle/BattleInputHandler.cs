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
        public event Action<MoveData, MoveEntry, BattleCombatant> OnMoveTargetSelected;
        public event Action<ConsumableItemData, BattleCombatant> OnItemTargetSelected;
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

        public void Update(
            GameTime gameTime,
            BattleUIManager uiManager,
            BattleRenderer renderer)
        {
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                OnBackRequested?.Invoke();
            }

            // --- HOVER DETECTION (Always Active) ---
            // We calculate the hovered target every frame so that tooltips (like Stat Changes)
            // can work even in the default Action Selection menu.
            var currentTargets = renderer.GetCurrentTargets();
            _hoveredTargetIndex = -1;

            // Check UI Buttons First for HOVER mapping (e.g. Targeting Buttons)
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

            // Inform UI Manager about Sprite Hover
            if (_hoveredTargetIndex != -1)
            {
                uiManager.CombatantHoveredViaSprite = currentTargets[_hoveredTargetIndex].Combatant;
            }
            else
            {
                uiManager.CombatantHoveredViaSprite = null;
            }

            // --- CLICK HANDLING (State Dependent) ---
            if (uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting)
            {
                // Right Click to Go Back
                if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                {
                    OnBackRequested?.Invoke();
                }

                // Handle Click on Sprites (UI Buttons handle their own clicks in BattleUIManager)
                if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    // Only process sprite clicks if we aren't hovering a UI button
                    if (_hoveredTargetIndex != -1 && uiHoveredCombatant == null)
                    {
                        var selectedTarget = currentTargets[_hoveredTargetIndex].Combatant;
                        var battleManager = ServiceLocator.Get<BattleManager>();
                        var actor = battleManager.CurrentActingCombatant;

                        if (uiManager.UIState == BattleUIState.Targeting)
                        {
                            var move = uiManager.MoveForTargeting;
                            if (move != null && actor != null)
                            {
                                var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, battleManager.AllCombatants);
                                if (validTargets.Contains(selectedTarget))
                                {
                                    OnMoveTargetSelected?.Invoke(move, uiManager.SpellForTargeting, selectedTarget);
                                    UIInputManager.ConsumeMouseClick();
                                }
                            }
                        }
                        else if (uiManager.UIState == BattleUIState.ItemTargeting)
                        {
                            var item = uiManager.ItemForTargeting;
                            if (item != null && actor != null)
                            {
                                var validTargets = TargetingHelper.GetValidTargets(actor, item.Target, battleManager.AllCombatants);
                                if (validTargets.Contains(selectedTarget))
                                {
                                    OnItemTargetSelected?.Invoke(item, selectedTarget);
                                    UIInputManager.ConsumeMouseClick();
                                }
                            }
                        }
                    }
                }
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}