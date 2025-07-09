using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel that displays interactive menus for player actions during combat.
    /// </summary>
    public class ActionMenuPanel
    {
        private readonly Rectangle _bounds;
        private readonly List<Button> _buttons = new List<Button>();
        private CombatUIState _lastUIState = CombatUIState.Busy;

        private const int PADDING = 10;
        private const int BORDER_THICKNESS = 2;
        private const int BUTTON_HEIGHT = 18;

        public event Action<string> OnActionSelected;

        public ActionMenuPanel(Rectangle bounds)
        {
            _bounds = bounds;
        }

        /// <summary>
        /// Updates the state of the menu buttons and rebuilds them if the UI state has changed.
        /// </summary>
        public void Update(GameTime gameTime, MouseState currentMouseState)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            // Rebuild buttons if the UI state has changed
            if (gameState.UIState != _lastUIState)
            {
                RebuildButtons(gameState);
                _lastUIState = gameState.UIState;
            }

            // Update all current buttons
            foreach (var button in _buttons)
            {
                button.Update(currentMouseState);
            }
        }

        /// <summary>
        /// Rebuilds the list of buttons based on the current combat UI state.
        /// </summary>
        private void RebuildButtons(GameState gameState)
        {
            _buttons.Clear();
            int currentY = _bounds.Y + PADDING;

            switch (gameState.UIState)
            {
                case CombatUIState.Default:
                    var mainOptions = new List<string> { "Attack", "Skills", "Move", "Item", "End Turn" };
                    foreach (var option in mainOptions)
                    {
                        var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                        var button = new Button(buttonBounds, option);
                        button.OnClick += () => OnActionSelected?.Invoke(option);
                        _buttons.Add(button);
                        currentY += BUTTON_HEIGHT;
                    }
                    break;

                case CombatUIState.SelectAttack:
                    var attacksComp = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(gameState.PlayerEntityId);
                    var combatStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(gameState.PlayerEntityId);
                    if (attacksComp == null || combatStats == null) break;

                    foreach (var attack in attacksComp.Attacks)
                    {
                        bool canAfford = combatStats.ActionPoints >= attack.ActionPointCost;
                        string text = $"{attack.Name} (Cost: {attack.ActionPointCost} AP)";
                        var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                        var button = new Button(buttonBounds, text, attack.Name)
                        {
                            IsEnabled = canAfford,
                            CustomDefaultTextColor = Global.Instance.GameTextColor,
                            CustomHoverTextColor = Global.Instance.Palette_Yellow,
                            CustomDisabledTextColor = Color.DarkGray
                        };
                        button.OnClick += () => OnActionSelected?.Invoke(attack.Name);
                        _buttons.Add(button);
                        currentY += BUTTON_HEIGHT;
                    }
                    AddBackButton();
                    break;

                case CombatUIState.SelectSkill:
                    var skillOptions = new List<string> { "Block", "Power Strike" };
                    foreach (var option in skillOptions)
                    {
                        var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                        var button = new Button(buttonBounds, option) { IsEnabled = false }; // Disabled for now
                        _buttons.Add(button);
                        currentY += BUTTON_HEIGHT;
                    }
                    AddBackButton();
                    break;

                case CombatUIState.SelectTarget:
                case CombatUIState.SelectMove:
                    AddBackButton();
                    break;
            }
        }

        private void AddBackButton()
        {
            int backButtonY = _bounds.Bottom - PADDING - BUTTON_HEIGHT;
            var backButtonBounds = new Rectangle(_bounds.X + PADDING, backButtonY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
            var backButton = new Button(backButtonBounds, "Back")
            {
                CustomDefaultTextColor = Global.Instance.Palette_Red,
                CustomHoverTextColor = Global.Instance.Palette_Pink
            };
            backButton.OnClick += () => OnActionSelected?.Invoke("Back");
            _buttons.Add(backButton);
        }

        /// <summary>
        /// Draws the action menu panel, including its buttons and instructional text.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            var font = Global.Instance.DefaultFont;

            // Draw border and background
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);
            spriteBatch.Draw(Core.Pixel, _bounds, Global.Instance.TerminalBg);

            // Draw buttons
            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            // Draw instructional text over the buttons if needed
            switch (gameState.UIState)
            {
                case CombatUIState.SelectTarget:
                    DrawInstruction(spriteBatch, "Select a target...");
                    break;
                case CombatUIState.SelectMove:
                    DrawInstruction(spriteBatch, "Select a destination...");
                    break;
            }
        }

        private void DrawInstruction(SpriteBatch spriteBatch, string text)
        {
            var font = Global.Instance.DefaultFont;
            var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING);
            spriteBatch.DrawString(font, text, position, Color.Yellow);
        }
    }
}