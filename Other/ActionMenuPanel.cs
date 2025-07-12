using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel that displays interactive menus for player actions during combat.
    /// </summary>
    public class ActionMenuPanel
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly Global _global;

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
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Updates the state of the menu buttons and rebuilds them if the UI state has changed.
        /// </summary>
        public void Update(GameTime gameTime, MouseState currentMouseState, BitmapFont font)
        {
            if (!_gameState.IsInCombat) return;

            // Rebuild buttons if the UI state has changed
            if (_gameState.UIState != _lastUIState)
            {
                RebuildButtons(font);
                _lastUIState = _gameState.UIState;
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
        private void RebuildButtons(BitmapFont font)
        {
            _buttons.Clear();
            int currentY = _bounds.Y + PADDING;

            switch (_gameState.UIState)
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
                    var attacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(_gameState.PlayerEntityId);
                    if (attacksComp == null) break;

                    foreach (var attack in attacksComp.Attacks)
                    {
                        string text = attack.Name;
                        var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                        var button = new Button(buttonBounds, text, attack.Name)
                        {
                            IsEnabled = true, // All attacks are always available now
                            CustomDefaultTextColor = _global.GameTextColor,
                            CustomHoverTextColor = _global.Palette_Yellow,
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
                CustomDefaultTextColor = _global.Palette_Red,
                CustomHoverTextColor = _global.Palette_Pink
            };
            backButton.OnClick += () => OnActionSelected?.Invoke("Back");
            _buttons.Add(backButton);
        }

        /// <summary>
        /// Draws the action menu panel, including its buttons and instructional text.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_gameState.IsInCombat) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Draw border and background
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg);

            // Draw buttons
            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            // Draw instructional text over the buttons if needed
            switch (_gameState.UIState)
            {
                case CombatUIState.SelectTarget:
                    DrawInstruction(spriteBatch, font, "Select a target...");
                    break;
                case CombatUIState.SelectMove:
                    DrawInstruction(spriteBatch, font, "Select a destination...");
                    break;
            }
        }

        private void DrawInstruction(SpriteBatch spriteBatch, BitmapFont font, string text)
        {
            var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING);
            spriteBatch.DrawString(font, text, position, Color.Yellow);
        }
    }
}