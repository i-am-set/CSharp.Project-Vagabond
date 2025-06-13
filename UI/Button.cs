using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ProjectVagabond.UI
{
    public class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; set; }
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

            IsHovered = Bounds.Contains(currentMouseState.Position);

            if (IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                OnClick?.Invoke();
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

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
        {
            Color bgColor;
            Color textColor = Global.Instance.palette_BrightWhite;

            if (!IsEnabled)
            {
                bgColor = Global.Instance.palette_DarkGray;
                textColor = Global.Instance.palette_Gray;
            }
            else if (IsHovered)
            {
                bgColor = Global.Instance.palette_LightGray;
            }
            else
            {
                bgColor = Global.Instance.palette_Gray;
            }

            spriteBatch.Draw(pixel, Bounds, bgColor);

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition = new Vector2(
                (int)(Bounds.X + (Bounds.Width - textSize.X) / 2),
                (int)(Bounds.Y + (Bounds.Height - textSize.Y) / 2)
            );

            spriteBatch.DrawString(font, Text, textPosition, textColor);
        }
    }
}