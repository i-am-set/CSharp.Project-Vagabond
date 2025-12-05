using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
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

            if (uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting)
            {
                // Right Click to Go Back
                if (currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                {
                    OnBackRequested?.Invoke();
                }

                var currentTargets = renderer.GetCurrentTargets();
                _hoveredTargetIndex = -1;
                for (int i = 0; i < currentTargets.Count; i++)
                {
                    if (currentTargets[i].Bounds.Contains(virtualMousePos))
                    {
                        _hoveredTargetIndex = i;
                        break;
                    }
                }

                if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed)
                {
                    if (_hoveredTargetIndex != -1)
                    {
                        var selectedTarget = currentTargets[_hoveredTargetIndex].Combatant;
                        if (uiManager.UIState == BattleUIState.Targeting)
                        {
                            OnMoveTargetSelected?.Invoke(uiManager.MoveForTargeting, uiManager.SpellForTargeting, selectedTarget);
                        }
                        else
                        {
                            OnItemTargetSelected?.Invoke(uiManager.ItemForTargeting, selectedTarget);
                        }
                        UIInputManager.ConsumeMouseClick();
                    }
                }
            }
            else
            {
                _hoveredTargetIndex = -1;
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}