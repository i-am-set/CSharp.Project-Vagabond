﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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

            // Transform mouse coordinates from screen space to virtual space
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // Single hover calculation per frame using virtual coordinates
            IsHovered = Bounds.Contains(virtualMousePos);

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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            // Determine colors based on state
            Color bgColor;
            Color textColor = Global.Instance.palette_BrightWhite;
            
            if (!IsEnabled)
            {
                bgColor = Color.Transparent;
                textColor = Global.Instance.palette_Gray;
            }
            else if (IsHovered)
            {
                bgColor = Color.Transparent;
                textColor = Global.Instance.palette_LightYellow;
            }
            else
            {
                bgColor = Color.Transparent;
            }

            // Draw background
            spriteBatch.Draw(Core.Pixel, Bounds, bgColor);
            
            // Draw text (centered)
            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPos = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) * 0.5f,
                Bounds.Y + (Bounds.Height - textSize.Y) * 0.5f
            );
            
            spriteBatch.DrawString(font, Text, textPos, textColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}