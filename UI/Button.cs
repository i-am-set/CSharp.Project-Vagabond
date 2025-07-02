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
        public string Function { get; set; }
        public Color? CustomDefaultTextColor { get; set; }
        public Color? CustomHoverTextColor { get; set; }
        public Color? CustomDisabledTextColor { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsHovered { get; set; }

        public event Action OnClick;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();

        #nullable enable
        public Button(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null)
        {
            if (function == null)
            {
                function = text;
            }

            Bounds = bounds;
            Text = text;
            Function = function;
            CustomDefaultTextColor = customDefaultTextColor;
            CustomHoverTextColor = customHoverTextColor;
            CustomDisabledTextColor = customDisabledTextColor;
        }
        #nullable restore

        public virtual void Update(MouseState currentMouseState)
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
                if (CustomDisabledTextColor.HasValue)
                {
                    textColor = CustomDisabledTextColor.Value;
                } else
                {
                    textColor = Global.Instance.ButtonDisableColor;
                }
            }
            else
            {
                if (isActivated)
                {
                    if (CustomHoverTextColor.HasValue)
                    {
                        textColor = CustomHoverTextColor.Value;
                    } else
                    {
                        textColor = Global.Instance.ButtonHoverColor;
                    }
                } 
                else
                {
                    if (CustomDefaultTextColor.HasValue)
                    {
                        textColor = CustomDefaultTextColor.Value;
                    }else
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