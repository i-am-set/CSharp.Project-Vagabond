﻿﻿﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.UI
{
    public class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; set;  }
        public Color? CustomTextColor { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsHovered { get; private set; }

        public event Action OnClick;

        private MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();

        public Button(Rectangle bounds, string text)
        {
            Bounds = bounds;
            Text = text;
            CustomTextColor = null;
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

        public void UpdateHoverState(MouseState currentMouseState)
        {
            if (!IsEnabled)
            {
                IsHovered = false;
                return;
            }

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            IsHovered = Bounds.Contains(virtualMousePos);
        }

        public void TriggerClick()
        {
            if (IsEnabled)
            {
                OnClick?.Invoke();
            }
        }

        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            Draw(spriteBatch, font, gameTime, false);
        }

        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, bool forceHover)
        {
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (!IsEnabled)
            {
                textColor = Global.Instance.Palette_Gray;
            }
            else
            {
                if (isActivated)
                {
                    textColor = Global.Instance.OptionHoverColor;
                } else
                {
                    if (CustomTextColor.HasValue)
                    {
                        textColor = CustomTextColor.Value;
                    }
                    else
                    {
                        textColor = Global.Instance.Palette_BrightWhite;
                    }
                }
            }

            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) / 2 + xOffset,
                Bounds.Y + (Bounds.Height - textSize.Y) / 2
            );

            spriteBatch.DrawString(font, Text, textPosition, textColor);
        }
    }
}
