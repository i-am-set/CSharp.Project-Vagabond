using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;

namespace ProjectVagabond
{
    public class TooltipManager
    {
        private readonly Global _global;

        private string _text = "";
        private Vector2 _anchorPosition;
        private bool _isVisible = false;
        private float _timer = 0f;
        private float _timeToAppear = 0.5f;

        private object _currentRequestor = null;
        private bool _requestThisFrame = false;

        public TooltipManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        /// <summary>
        /// Requests a tooltip to be shown for a specific UI element or game object.
        /// This should be called every frame that the object is being hovered.
        /// </summary>
        /// <param name="requestor">A unique object identifying the source of the tooltip (e.g., a Rectangle, a string ID).</param>
        /// <param name="text">The text to display in the tooltip.</param>
        /// <param name="anchorPosition">The position to anchor the tooltip to (e.g., mouse position).</param>
        /// <param name="delay">The time in seconds to wait before showing the tooltip.</param>
        public void RequestTooltip(object requestor, string text, Vector2 anchorPosition, float delay = Global.TOOLTIP_AVERAGE_POPUP_TIME)
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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!_isVisible || string.IsNullOrEmpty(_text))
            {
                return;
            }

            Texture2D pixel = ServiceLocator.Get<Texture2D>();
            Vector2 textSize = font.MeasureString(_text);

            const int paddingX = 8;
            const int paddingY = 4;
            int tooltipWidth = (int)textSize.X + paddingX;
            int tooltipHeight = (int)textSize.Y + paddingY;

            // Position the tooltip above the anchor, centered horizontally.
            Vector2 finalTopLeftPosition = new Vector2(
                _anchorPosition.X - tooltipWidth / 2f,
                _anchorPosition.Y - tooltipHeight - 2 // 2 pixels of space
            );

            // Clamp to screen bounds
            if (finalTopLeftPosition.X < 0) finalTopLeftPosition.X = 0;
            if (finalTopLeftPosition.Y < 0) finalTopLeftPosition.Y = 0;
            if (finalTopLeftPosition.X + tooltipWidth > Global.VIRTUAL_WIDTH)
            {
                finalTopLeftPosition.X = Global.VIRTUAL_WIDTH - tooltipWidth;
            }
            if (finalTopLeftPosition.Y + tooltipHeight > Global.VIRTUAL_HEIGHT)
            {
                finalTopLeftPosition.Y = Global.VIRTUAL_HEIGHT - tooltipHeight;
            }

            Rectangle tooltipBg = new Rectangle((int)finalTopLeftPosition.X, (int)finalTopLeftPosition.Y, tooltipWidth, tooltipHeight);
            Vector2 textPosition = new Vector2(tooltipBg.X + (paddingX / 2), tooltipBg.Y + (paddingY / 2));

            spriteBatch.DrawSnapped(pixel, tooltipBg, _global.ToolTipBGColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), _global.ToolTipBorderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), _global.ToolTipBorderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), _global.ToolTipBorderColor);
            spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), _global.ToolTipBorderColor);

            spriteBatch.DrawStringSnapped(font, _text, textPosition, _global.ToolTipTextColor);
        }
    }
}