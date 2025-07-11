using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.UI
{
    public class Button
    {
        protected readonly Global _global;

        public Rectangle Bounds { get; set; }
        public string Text { get; set; }
        public string Function { get; set; }
        public Color? CustomDefaultTextColor { get; set; }
        public Color? CustomHoverTextColor { get; set; }
        public Color? CustomDisabledTextColor { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsHovered { get; set; }
        public bool UseScreenCoordinates { get; set; } = false;
        public bool AlignLeft { get; set; } = false;
        public float OverflowScrollSpeed { get; set; } = 0f;

        public event Action OnClick;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        private float _scrollPosition = 0f;

#nullable enable
        public Button(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0.0f)
        {
            _global = ServiceLocator.Get<Global>();

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
            AlignLeft = alignLeft;
            OverflowScrollSpeed = overflowScrollSpeed;
        }
#nullable restore

        public virtual void Update(MouseState currentMouseState)
        {
            Vector2 virtualMousePos = UseScreenCoordinates
                ? currentMouseState.Position.ToVector2()
                : Core.TransformMouse(currentMouseState.Position);

            UpdateHoverState(virtualMousePos);

            if (IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TriggerClick();
            }

            _previousMouseState = currentMouseState;
        }

        public void UpdateHoverState(Vector2 virtualMousePos)
        {
            if (!IsEnabled)
            {
                IsHovered = false;
                return;
            }
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
                textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            }
            else
            {
                textColor = isActivated
                    ? (CustomHoverTextColor ?? _global.ButtonHoverColor)
                    : (CustomDefaultTextColor ?? _global.Palette_BrightWhite);
            }

            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            Vector2 textSize = font.MeasureString(Text);

            var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            var clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: clipRasterizerState);

            spriteBatch.GraphicsDevice.ScissorRectangle = Bounds;

            bool shouldScroll = OverflowScrollSpeed > 0 && textSize.X > Bounds.Width;
            if (shouldScroll)
            {
                _scrollPosition += (float)gameTime.ElapsedGameTime.TotalSeconds * OverflowScrollSpeed;
                string scrollingText = Text + "  ";
                Vector2 scrollingTextSize = font.MeasureString(scrollingText);
                if (_scrollPosition > scrollingTextSize.X)
                {
                    _scrollPosition -= scrollingTextSize.X;
                }
                Vector2 scrollTextPosition = new Vector2(Bounds.X - _scrollPosition, Bounds.Y + (Bounds.Height - textSize.Y) / 2);
                spriteBatch.DrawString(font, scrollingText, scrollTextPosition, textColor);
                spriteBatch.DrawString(font, scrollingText, new Vector2(scrollTextPosition.X + scrollingTextSize.X, scrollTextPosition.Y), textColor);
            }
            else
            {
                Vector2 textPosition;
                if (AlignLeft)
                {
                    textPosition = new Vector2(Bounds.X + xOffset, Bounds.Y + (Bounds.Height - textSize.Y) / 2);
                }
                else
                {
                    textPosition = new Vector2(Bounds.X + (Bounds.Width - textSize.X) / 2 + xOffset, Bounds.Y + (Bounds.Height - textSize.Y) / 2);
                }
                spriteBatch.DrawString(font, Text, textPosition, textColor);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: originalRasterizerState);
        }
    }
}