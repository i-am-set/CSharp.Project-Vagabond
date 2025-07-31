﻿using Microsoft.Xna.Framework;
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
    /// A UI panel responsible for drawing a scrolling, interactive initiative list during combat.
    /// </summary>
    public class TurnOrderPanel
    {
        private readonly GameState _gameState;
        private readonly Global _global;

        private readonly Rectangle _bounds;
        private int _maxVisibleItems;
        private int _scrollStartIndex = 0;

        private readonly List<Button> _buttons = new List<Button>();
        private List<int> _lastInitiativeOrder = new List<int>();
        private int _lastTurnEntityId = -1;

        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;
        private const int TITLE_AREA_HEIGHT = 18;

        public event Action<int> OnTargetSelected;

        public TurnOrderPanel(Rectangle bounds)
        {
            _bounds = bounds;
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Updates the turn order panel, rebuilding buttons if the state has changed.
        /// </summary>
        public void Update(GameTime gameTime, MouseState currentMouseState, BitmapFont font)
        {
            if (!_gameState.IsInCombat)
            {
                if (_buttons.Count > 0) _buttons.Clear();
                return;
            }

            // Check if the initiative order or current turn has changed, requiring a rebuild.
            if (!_gameState.InitiativeOrder.SequenceEqual(_lastInitiativeOrder) || _gameState.CurrentTurnEntityId != _lastTurnEntityId)
            {
                RebuildButtons(font);
                _lastInitiativeOrder = new List<int>(_gameState.InitiativeOrder);
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
            var initiativeOrder = _gameState.InitiativeOrder;
            if (initiativeOrder == null || initiativeOrder.Count == 0 || font == null) return;

            int lineHeight = font.LineHeight + 4;
            _maxVisibleItems = (_bounds.Height - TITLE_AREA_HEIGHT - PADDING) / lineHeight;


            // Update scroll position to keep the current turn visible
            int currentIndex = initiativeOrder.IndexOf(_gameState.CurrentTurnEntityId);
            if (currentIndex != -1)
            {
                if (currentIndex < _scrollStartIndex) _scrollStartIndex = currentIndex;
                else if (currentIndex >= _scrollStartIndex + _maxVisibleItems) _scrollStartIndex = currentIndex - _maxVisibleItems + 1;
            }

            var displayNames = EntityNamer.GetUniqueNames(initiativeOrder);
            float currentY = _bounds.Y + TITLE_AREA_HEIGHT + 2;

            int itemsToDraw = System.Math.Min(_maxVisibleItems, initiativeOrder.Count - _scrollStartIndex);

            for (int i = 0; i < itemsToDraw; i++)
            {
                int listIndex = _scrollStartIndex + i;
                int entityId = initiativeOrder[listIndex];
                string name = displayNames[entityId];

                var buttonBounds = new Rectangle((int)_bounds.X + PADDING, (int)currentY + (i * lineHeight), _bounds.Width - (PADDING * 2), lineHeight);

                var button = new Button(buttonBounds, name, $"SelectTarget_{entityId}")
                {
                    AlignLeft = true,
                    OverflowScrollSpeed = 30f,
                    CustomDefaultTextColor = (entityId == _gameState.PlayerEntityId) ? _global.Palette_Yellow : Color.LightGray,
                    CustomHoverTextColor = _global.Palette_Pink
                };

                // Can't target self
                if (entityId == _gameState.PlayerEntityId)
                {
                    button.IsEnabled = false;
                    button.CustomDisabledTextColor = _global.Palette_Yellow;
                }

                int capturedId = entityId; // Capture the ID for the lambda
                button.OnClick += () => OnTargetSelected?.Invoke(capturedId);
                _buttons.Add(button);
            }
        }

        /// <summary>
        /// Draws the turn order display if the game is in combat.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_gameState.IsInCombat || font == null) return;

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // --- Draw Panel Border and Background ---
            var borderRect = new Rectangle(_bounds.X - BORDER_THICKNESS, _bounds.Y - BORDER_THICKNESS, _bounds.Width + (BORDER_THICKNESS * 2), _bounds.Height + (BORDER_THICKNESS * 2));
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg * 0.9f);

            // --- Draw Title and Divider ---
            string title = "TURN ORDER";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2(_bounds.Center.X - titleSize.X / 2, _bounds.Y + PADDING);
            spriteBatch.DrawString(font, title, titlePos, _global.Palette_Gray);
            int dividerY = (int)titlePos.Y + font.LineHeight + 2;
            spriteBatch.Draw(pixel, new Rectangle(_bounds.X + PADDING, dividerY, _bounds.Width - (PADDING * 2), 1), _global.Palette_Gray);

            // --- Draw Buttons and Turn Numbers ---
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                int listIndex = _scrollStartIndex + i;

                if (listIndex >= _gameState.InitiativeOrder.Count)
                {
                    break; // Stop drawing if the initiative order has shrunk mid-frame.
                }

                int entityId = _gameState.InitiativeOrder[listIndex];

                string indicator = (entityId == _gameState.CurrentTurnEntityId) ? ">" : (listIndex + 1).ToString();
                Vector2 indicatorPos = new Vector2(button.Bounds.X, button.Bounds.Y + (button.Bounds.Height - font.LineHeight) / 2);
                spriteBatch.DrawString(font, indicator, indicatorPos, entityId == _gameState.CurrentTurnEntityId ? _global.Palette_BrightWhite : _global.Palette_Gray);

                var textBounds = button.Bounds;
                int indicatorWidth = (int)font.MeasureString("> ").Width;
                textBounds.X += indicatorWidth;
                textBounds.Width -= indicatorWidth;

                var originalBounds = button.Bounds;
                button.Bounds = textBounds;
                button.Draw(spriteBatch, font, gameTime);
                button.Bounds = originalBounds;
            }
        }
    }
}