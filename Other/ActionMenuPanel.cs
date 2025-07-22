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
        private readonly List<Button> _actionButtons = new List<Button>();
        private readonly Button _backButton;
        private readonly Button _endTurnButton;
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

            // Create persistent buttons once to preserve their internal state (like previousMouseState)
            _backButton = new Button(Rectangle.Empty, "Back")
            {
                CustomDefaultTextColor = _global.Palette_Red,
                CustomHoverTextColor = _global.Palette_Pink
            };
            _backButton.OnClick += () => OnActionSelected?.Invoke("Back");

            _endTurnButton = new Button(Rectangle.Empty, "End Turn");
            _endTurnButton.OnClick += () => OnActionSelected?.Invoke("End Turn");
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

            foreach (var button in _actionButtons)
            {
                button.Update(currentMouseState);
            }
            _backButton.Update(currentMouseState);
            _endTurnButton.Update(currentMouseState);
        }

        private void RebuildButtons(BitmapFont font)
        {
            _actionButtons.Clear();

            if (_gameState.CurrentTurnEntityId != _gameState.PlayerEntityId)
            {
                return;
            }

            int currentY = _bounds.Y + PADDING;
            int bottomButtonY = _bounds.Bottom - PADDING - BUTTON_HEIGHT;
            var bottomButtonBounds = new Rectangle(_bounds.X + PADDING, bottomButtonY, _bounds.Width - (PADDING * 2), BUTTON_HEIGHT);

            // Default visibility
            _backButton.IsEnabled = false;
            _endTurnButton.IsEnabled = false;

            switch (_gameState.UIState)
            {
                case CombatUIState.Default:
                    var mainOptions = new List<string> { "Attack", "Move", "Flee" };
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
                        else if (option == "Flee")
                        {
                            button.IsEnabled = turnStats?.IsPristineForTurn ?? false;
                        }

                        button.OnClick += () => OnActionSelected?.Invoke(option);
                        _actionButtons.Add(button);
                        currentY += BUTTON_HEIGHT;
                    }

                    _endTurnButton.Bounds = bottomButtonBounds;
                    _endTurnButton.IsEnabled = true;
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
                            _actionButtons.Add(button);
                            currentY += BUTTON_HEIGHT;
                        }
                    }
                    _backButton.Bounds = bottomButtonBounds;
                    _backButton.IsEnabled = true;
                    break;

                case CombatUIState.SelectTarget:
                case CombatUIState.SelectMove:
                    _backButton.Bounds = bottomButtonBounds;
                    _backButton.IsEnabled = true;
                    break;
            }
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
                foreach (var button in _actionButtons)
                {
                    button.Draw(spriteBatch, font, gameTime);
                }
                if (_backButton.IsEnabled) _backButton.Draw(spriteBatch, font, gameTime);
                if (_endTurnButton.IsEnabled) _endTurnButton.Draw(spriteBatch, font, gameTime);

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
            spriteBatch.DrawString(font, text, position, _global.CombatInstructionColor);
        }
    }
}