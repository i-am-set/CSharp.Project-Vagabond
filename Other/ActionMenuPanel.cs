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
        private int _lastTurnEntityId = -1;

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

        public void Update(GameTime gameTime, MouseState currentMouseState, BitmapFont font)
        {
            if (!_gameState.IsInCombat) return;

            // Rebuild buttons if the UI state OR the current turn entity has changed.
            if (_gameState.UIState != _lastUIState || _gameState.CurrentTurnEntityId != _lastTurnEntityId)
            {
                RebuildButtons(font);
                _lastUIState = _gameState.UIState;
                _lastTurnEntityId = _gameState.CurrentTurnEntityId;
            }

            foreach (var button in _buttons)
            {
                button.Update(currentMouseState);
            }
        }

        private void RebuildButtons(BitmapFont font)
        {
            _buttons.Clear();

            if (_gameState.CurrentTurnEntityId != _gameState.PlayerEntityId)
            {
                return;
            }

            int currentY = _bounds.Y + PADDING;

            switch (_gameState.UIState)
            {
                case CombatUIState.Default:
                    var mainOptions = new List<string> { "Attack", "Move", "End Turn" };
                    var turnStats = _componentStore.GetComponent<TurnStatsComponent>(_gameState.PlayerEntityId);

                    foreach (var option in mainOptions)
                    {
                        var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                        var button = new Button(buttonBounds, option);

                        if (option == "Move")
                        {
                            button.IsEnabled = _gameState.CanPlayerMoveInCombat();
                        }
                        else if (option == "Attack")
                        {
                            button.IsEnabled = turnStats?.HasPrimaryAction ?? false;
                        }

                        button.OnClick += () => OnActionSelected?.Invoke(option);
                        _buttons.Add(button);
                        currentY += BUTTON_HEIGHT;
                    }
                    break;

                case CombatUIState.SelectAttack:
                    var attacksComp = _componentStore.GetComponent<AvailableAttacksComponent>(_gameState.PlayerEntityId);
                    if (attacksComp != null)
                    {
                        foreach (var attack in attacksComp.Attacks)
                        {
                            var buttonBounds = new Rectangle(_bounds.X + PADDING, currentY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);
                            var button = new Button(buttonBounds, attack.Name);
                            button.OnClick += () => OnActionSelected?.Invoke(attack.Name);
                            _buttons.Add(button);
                            currentY += BUTTON_HEIGHT;
                        }
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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_gameState.IsInCombat) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            var borderRect = new Rectangle(_bounds.X - BORDER_THICKNESS, _bounds.Y - BORDER_THICKNESS, _bounds.Width + (BORDER_THICKNESS * 2), _bounds.Height + (BORDER_THICKNESS * 2));
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg);

            // Only draw buttons and instructions if it's the player's turn.
            if (_gameState.CurrentTurnEntityId == _gameState.PlayerEntityId)
            {
                foreach (var button in _buttons)
                {
                    button.Draw(spriteBatch, font, gameTime);
                }

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
        }

        private void DrawInstruction(SpriteBatch spriteBatch, BitmapFont font, string text)
        {
            var position = new Vector2(_bounds.X + PADDING, _bounds.Y + PADDING);
            spriteBatch.DrawString(font, text, position, Color.Yellow);
        }
    }
}