using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond
{
    public class TooltipManager
    {
        private string _text = "";
        private Vector2 _anchorPosition;
        private bool _isVisible = false;
        private float _timer = 0f;
        private float _timeToAppear = 0.5f;

        private object _currentRequestor = null;
        private bool _requestThisFrame = false;

        /// <summary>
        /// Requests a tooltip to be shown for a specific UI element or game object.
        /// This should be called every frame that the object is being hovered.
        /// </summary>
        /// <param name="requestor">A unique object identifying the source of the tooltip (e.g., a Rectangle, a string ID).</param>
        /// <param name="text">The text to display in the tooltip.</param>
        /// <param name="anchorPosition">The position to anchor the tooltip to (e.g., mouse position).</param>
        /// <param name="delay">The time in seconds to wait before showing the tooltip.</param>
        public void RequestTooltip(object requestor, string text, Vector2 anchorPosition, float delay = 0.5f)
        {
            if (requestor == null) return;

            if (_currentRequestor == null || !_currentRequestor.Equals(requestor))
            {
                _timer = 0f;
                _isVisible = false;
                _currentRequestor = requestor;
            }

            _text = text;
            _anchorPosition = anchorPosition;
            _timeToAppear = delay;
            _requestThisFrame = true;
        }

        public void Update(GameTime gameTime)
        {
            if (_requestThisFrame)
            {
                _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_timer >= _timeToAppear)
                {
                    _isVisible = true;
                }
            }
            else
            {
                _isVisible = false;
                _timer = 0f;
                _currentRequestor = null;
            }

            _requestThisFrame = false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_isVisible || string.IsNullOrEmpty(_text))
            {
                return;
            }

            Texture2D pixel = Core.Pixel;
            BitmapFont font = Global.Instance.DefaultFont;
            Vector2 textSize = font.MeasureString(_text);

            const int paddingX = 8;
            const int paddingY = 4;
            int tooltipWidth = (int)textSize.X + paddingX;
            int tooltipHeight = (int)textSize.Y + paddingY;

            Vector2 finalTopLeftPosition = new Vector2(_anchorPosition.X + 15, _anchorPosition.Y + 15);

            if (finalTopLeftPosition.X + tooltipWidth > Global.VIRTUAL_WIDTH)
            {
                finalTopLeftPosition.X = _anchorPosition.X - tooltipWidth - 5;
            }

            if (finalTopLeftPosition.Y + tooltipHeight > Global.VIRTUAL_HEIGHT)
            {
                finalTopLeftPosition.Y = _anchorPosition.Y - tooltipHeight - 5;
            }

            Rectangle tooltipBg = new Rectangle(
                (int)finalTopLeftPosition.X,
                (int)finalTopLeftPosition.Y,
                tooltipWidth,
                tooltipHeight
            );

            Vector2 textPosition = new Vector2(
                finalTopLeftPosition.X + (paddingX / 2),
                finalTopLeftPosition.Y + (paddingY / 2)
            );

            spriteBatch.Draw(pixel, tooltipBg, Global.Instance.ToolTipBGColor * 0.9f);

            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor); // Top
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), Global.Instance.ToolTipBorderColor); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor); // Left
            spriteBatch.Draw(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), Global.Instance.ToolTipBorderColor); // Right

            spriteBatch.DrawString(font, _text, textPosition, Global.Instance.ToolTipTextColor);
        }
    }
}