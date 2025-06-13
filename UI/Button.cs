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

            // Single hover calculation per frame
            IsHovered = Bounds.Contains(currentMouseState.Position);

            // Handle click detection
            if (IsHovered && 
                currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
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
            // Determine colors based on state
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

            // Draw background
            spriteBatch.Draw(pixel, Bounds, bgColor);
            
            // Draw text (centered)
            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPos = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) * 0.5f,
                Bounds.Y + (Bounds.Height - textSize.Y) * 0.5f
            );
            
            spriteBatch.DrawString(font, Text, textPos, textColor);
        }
    }
}