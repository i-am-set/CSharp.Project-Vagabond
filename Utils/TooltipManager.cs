// TooltipManager.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond
{
    public class TooltipManager
    {
        private object _currentRequester;
        private string _tooltipText;
        private Vector2 _cursorPosition;

        private float _hoverTimer;
        private bool _isTooltipVisible;

        private const float HOVER_DELAY = 0.5f; // Time in seconds before tooltip appears

        /// <summary>
        /// Requests a tooltip to be shown for a specific UI element.
        /// The manager will start a timer and show the tooltip after a short delay.
        /// </summary>
        /// <param name="requester">The object requesting the tooltip (e.g., 'this' from the calling class).</param>
        /// <param name="text">The text to display in the tooltip.</param>
        /// <param name="cursorPosition">The current position of the mouse cursor in virtual coordinates.</param>
        public void Request(object requester, string text, Vector2 cursorPosition)
        {
            if (requester != _currentRequester)
            {
                _hoverTimer = 0f;
                _isTooltipVisible = false;
                _currentRequester = requester;
            }
            _tooltipText = text;
            _cursorPosition = cursorPosition;
        }

        /// <summary>
        /// Cancels a tooltip request from a specific requester.
        /// This should be called when the cursor is no longer hovering over the element.
        /// </summary>
        /// <param name="requester">The object that initially requested the tooltip.</param>
        public void CancelRequest(object requester)
        {
            if (requester == _currentRequester)
            {
                _currentRequester = null;
                _tooltipText = null;
                _hoverTimer = 0f;
                _isTooltipVisible = false;
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_currentRequester != null)
            {
                _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_hoverTimer >= HOVER_DELAY)
                {
                    _isTooltipVisible = true;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_isTooltipVisible || string.IsNullOrEmpty(_tooltipText))
            {
                return;
            }

            var pixel = Core.Pixel;
            var font = Global.Instance.DefaultFont;
            Vector2 textSize = font.MeasureString(_tooltipText);

            const int paddingX = 8;
            const int paddingY = 4;
            int tooltipWidth = (int)textSize.X + paddingX;
            int tooltipHeight = (int)textSize.Y + paddingY;

            // --- Smart Positioning ---
            float finalX = _cursorPosition.X;
            float finalY = _cursorPosition.Y;

            // Position below cursor by default
            finalY += 20; 

            // If it goes off the bottom, position it above the cursor instead
            if (finalY + tooltipHeight > Global.VIRTUAL_HEIGHT)
            {
                finalY = _cursorPosition.Y - tooltipHeight - 5;
            }

            // If it goes off the right, shift it left
            if (finalX + tooltipWidth > Global.VIRTUAL_WIDTH)
            {
                finalX = Global.VIRTUAL_WIDTH - tooltipWidth;
            }

            Rectangle tooltipBg = new Rectangle((int)finalX, (int)finalY, tooltipWidth, tooltipHeight);
            Vector2 textPosition = new Vector2(tooltipBg.X + (paddingX / 2), tooltipBg.Y + (paddingY / 2));

            // Draw background and border
            spriteBatch.Draw(pixel, tooltipBg, Global.Instance.ToolTipBGColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor);

            // Draw text
            spriteBatch.DrawString(font, _tooltipText, textPosition, Global.Instance.ToolTipTextColor);
        }
    }
}