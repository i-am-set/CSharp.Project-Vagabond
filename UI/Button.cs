using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.UI
{
    public class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        public bool IsEnabled { get; set; } = true;
        public bool IsHovered { get; private set; }

        public event Action OnClick;

        private MouseState _previousMouseState;

        public Button(Rectangle bounds, string text)
        {
            Bounds = bounds;
            Text = text;
        }

        public void Update(MouseState currentMouseState)
        {
            if (!IsEnabled)
            {
                IsHovered = false;
                return;
            }

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            IsHovered = Bounds.Contains(virtualMousePos);

            if (IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TriggerClick();
            }

            _previousMouseState = currentMouseState;
        }

        public void TriggerClick()
        {
            if (IsEnabled)
            {
                OnClick?.Invoke();
            }
        }

        /// <summary>
        /// Draws the button.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            Draw(spriteBatch, font, false);
        }

        /// <summary>
        /// Draws the button, allowing hover state to be forced for keyboard navigation.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, BitmapFont font, bool forceHover)
        {
            Color textColor = (IsHovered || forceHover) ? Global.Instance.OptionHoverColor : Global.Instance.Palette_BrightWhite;
            if (!IsEnabled)
            {
                textColor = Global.Instance.Palette_Gray;
            }

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) / 2,
                Bounds.Y + (Bounds.Height - textSize.Y) / 2
            );

            spriteBatch.DrawString(font, Text, textPosition, textColor);
        }
    }
}