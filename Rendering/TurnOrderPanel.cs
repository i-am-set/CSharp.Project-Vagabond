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
    /// A UI panel responsible for drawing a scrolling, interactive initiative list during combat.
    /// </summary>
    public class TurnOrderPanel
    {
        private readonly Vector2 _position;
        private readonly int _width;
        private readonly int _maxVisibleItems;
        private int _scrollStartIndex = 0;

        private readonly List<Button> _buttons = new List<Button>();
        private List<int> _lastInitiativeOrder = new List<int>();
        private int _lastTurnEntityId = -1;

        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;
        private const int TITLE_AREA_HEIGHT = 18;
        private const int TURN_NUMBER_OFFSET = 15;

        public event Action<int> OnTargetSelected;

        public TurnOrderPanel(Vector2 position, int width, int maxVisibleItems)
        {
            _position = position;
            _width = width;
            _maxVisibleItems = maxVisibleItems;
        }

        /// <summary>
        /// Updates the turn order panel, rebuilding buttons if the state has changed.
        /// </summary>
        public void Update(GameTime gameTime, MouseState currentMouseState)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat)
            {
                if (_buttons.Count > 0) _buttons.Clear();
                return;
            }

            // Check if the initiative order or current turn has changed, requiring a rebuild.
            if (!gameState.InitiativeOrder.SequenceEqual(_lastInitiativeOrder) || gameState.CurrentTurnEntityId != _lastTurnEntityId)
            {
                RebuildButtons(gameState);
                _lastInitiativeOrder = new List<int>(gameState.InitiativeOrder);
                _lastTurnEntityId = gameState.CurrentTurnEntityId;
            }

            foreach (var button in _buttons)
            {
                button.Update(currentMouseState);
            }
        }

        private void RebuildButtons(GameState gameState)
        {
            _buttons.Clear();
            var initiativeOrder = gameState.InitiativeOrder;
            if (initiativeOrder == null || initiativeOrder.Count == 0) return;

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            // Update scroll position to keep the current turn visible
            int currentIndex = initiativeOrder.IndexOf(gameState.CurrentTurnEntityId);
            if (currentIndex != -1)
            {
                if (currentIndex < _scrollStartIndex) _scrollStartIndex = currentIndex;
                else if (currentIndex >= _scrollStartIndex + _maxVisibleItems) _scrollStartIndex = currentIndex - _maxVisibleItems + 1;
            }

            var displayNames = EntityNamer.GetUniqueNames(initiativeOrder);
            int lineHeight = font.LineHeight + 4;
            float currentY = _position.Y + TITLE_AREA_HEIGHT + 2;

            int itemsToDraw = System.Math.Min(_maxVisibleItems, initiativeOrder.Count - _scrollStartIndex);

            for (int i = 0; i < itemsToDraw; i++)
            {
                int listIndex = _scrollStartIndex + i;
                int entityId = initiativeOrder[listIndex];
                string name = displayNames[entityId];

                var buttonBounds = new Rectangle((int)_position.X + PADDING, (int)currentY + (i * lineHeight), _width - (PADDING * 2), lineHeight);

                var button = new Button(buttonBounds, name, $"SelectTarget_{entityId}")
                {
                    AlignLeft = true,
                    OverflowScrollSpeed = 30f,
                    CustomDefaultTextColor = (entityId == gameState.PlayerEntityId) ? Color.Yellow : Color.LightGray,
                    CustomHoverTextColor = Global.Instance.Palette_Pink
                };

                // Can't target self
                if (entityId == gameState.PlayerEntityId)
                {
                    button.IsEnabled = false;
                    button.CustomDisabledTextColor = Color.Yellow;
                }

                int capturedId = entityId; // Capture the ID for the lambda
                button.OnClick += () => OnTargetSelected?.Invoke(capturedId);
                _buttons.Add(button);
            }
        }

        /// <summary>
        /// Draws the turn order display if the game is in combat.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            // --- Calculate Panel Dimensions with Fixed Height ---
            int lineHeight = font.LineHeight + 4;
            // The height is now fixed based on the max number of visible items.
            int listHeight = _maxVisibleItems * lineHeight;
            int totalHeight = TITLE_AREA_HEIGHT + listHeight + PADDING;
            var bounds = new Rectangle((int)_position.X, (int)_position.Y, _width, totalHeight);

            // --- Draw Panel Border and Background ---
            var borderRect = new Rectangle(bounds.X - BORDER_THICKNESS, bounds.Y - BORDER_THICKNESS, bounds.Width + (BORDER_THICKNESS * 2), bounds.Height + (BORDER_THICKNESS * 2));
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);
            spriteBatch.Draw(Core.Pixel, bounds, Global.Instance.TerminalBg * 0.9f);

            // --- Draw Title and Divider ---
            string title = "TURN ORDER";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2(bounds.Center.X - titleSize.X / 2, bounds.Y + PADDING);
            spriteBatch.DrawString(font, title, titlePos, Global.Instance.Palette_Gray);
            int dividerY = (int)titlePos.Y + font.LineHeight + 2;
            spriteBatch.Draw(Core.Pixel, new Rectangle(bounds.X + PADDING, dividerY, bounds.Width - (PADDING * 2), 1), Global.Instance.Palette_Gray);

            // --- Draw Buttons and Turn Numbers ---
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                int listIndex = _scrollStartIndex + i;
                int entityId = gameState.InitiativeOrder[listIndex];

                // Draw the turn number to the left of the box
                string turnNumber = (listIndex + 1).ToString();
                Vector2 turnNumberSize = font.MeasureString(turnNumber);
                Vector2 turnNumberPos = new Vector2(
                    button.Bounds.X - TURN_NUMBER_OFFSET - PADDING,
                    button.Bounds.Center.Y - turnNumberSize.Y / 2
                );
                spriteBatch.DrawString(font, turnNumber, turnNumberPos, Global.Instance.Palette_Gray);

                // Draw the turn indicator separately from the button text
                if (entityId == gameState.CurrentTurnEntityId)
                {
                    string indicator = ">";
                    Vector2 indicatorPos = new Vector2(
                        button.Bounds.X,
                        button.Bounds.Y + (button.Bounds.Height - font.LineHeight) / 2
                    );
                    spriteBatch.DrawString(font, indicator, indicatorPos, button.CustomDefaultTextColor ?? Color.White);
                }

                // Adjust button text bounds to make space for the indicator
                var textBounds = button.Bounds;
                int indicatorWidth = (int)font.MeasureString("> ").Width;
                textBounds.X += indicatorWidth;
                textBounds.Width -= indicatorWidth;

                // Temporarily set the button's bounds for drawing, then restore
                var originalBounds = button.Bounds;
                button.Bounds = textBounds;
                button.Draw(spriteBatch, font, gameTime);
                button.Bounds = originalBounds;
            }
        }
    }
}