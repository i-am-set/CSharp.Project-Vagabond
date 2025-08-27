using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Defines the reason a button might have a strikethrough.
    /// </summary>
    public enum StrikethroughType
    {
        None,
        /// <summary>
        /// Disabled because the action has been used up for the turn. Uses a distinct red color.
        /// </summary>
        Exhausted
    }

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
        public StrikethroughType Strikethrough { get; set; } = StrikethroughType.None;
        public bool EnableHoverSway { get; set; } = true;

        public event Action OnClick;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        private float _scrollPosition = 0f;
        private float _swayTimer = 0f;
        private bool _wasHoveredLastFrame = false;

        private const float SWAY_SPEED = 3f;
        private const float SWAY_AMOUNT_X = 1f;
        private const float SWAY_AMOUNT_Y = 1f;

        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

#nullable enable
        public Button(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0.0f, bool enableHoverSway = true)
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
            EnableHoverSway = enableHoverSway;
        }
#nullable restore

        public virtual void Update(MouseState currentMouseState)
        {
            Vector2 virtualMousePos = UseScreenCoordinates
                ? currentMouseState.Position.ToVector2()
                : Core.TransformMouse(currentMouseState.Position);

            UpdateHoverState(virtualMousePos);

            if (UIInputManager.CanProcessMouseClick() && IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TriggerClick();
                UIInputManager.ConsumeMouseClick();
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

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            float swayOffsetX = 0f;
            float swayOffsetY = 0f;

            if (isActivated && EnableHoverSway)
            {
                if (!_wasHoveredLastFrame)
                {
                    _swayTimer = 0f; // Reset timer on new hover to start animation from the beginning.
                }
                _swayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                // Use sine waves with different frequencies for a figure-eight motion
                swayOffsetX = (float)Math.Sin(_swayTimer * SWAY_SPEED) * SWAY_AMOUNT_X;
                swayOffsetY = (float)Math.Sin(_swayTimer * SWAY_SPEED * 2) * SWAY_AMOUNT_Y;
            }
            else
            {
                _swayTimer = 0f; // Reset if not hovered.
            }
            _wasHoveredLastFrame = isActivated;

            float totalXOffset = hopOffset + swayOffsetX;

            Vector2 textSize = font.MeasureString(Text);

            var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            var clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _clipRasterizerState);

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
                spriteBatch.DrawStringSnapped(font, scrollingText, scrollTextPosition, textColor);
                spriteBatch.DrawStringSnapped(font, scrollingText, new Vector2(scrollTextPosition.X + scrollingTextSize.X, scrollTextPosition.Y), textColor);
            }
            else
            {
                Vector2 textPosition;
                if (AlignLeft)
                {
                    textPosition = new Vector2(Bounds.X + totalXOffset, Bounds.Y + (Bounds.Height - textSize.Y) / 2 + swayOffsetY);
                }
                else
                {
                    textPosition = new Vector2(Bounds.X + (Bounds.Width - textSize.X) / 2 + totalXOffset, Bounds.Y + (Bounds.Height - textSize.Y) / 2 + swayOffsetY);
                }
                spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor);

                // --- DIAGONAL STRIKETHROUGH LOGIC ---
                if (Strikethrough == StrikethroughType.Exhausted)
                {
                    Color strikethroughColor = _global.Palette_Red;
                    var pixel = ServiceLocator.Get<Texture2D>();

                    // Calculate diagonal properties
                    float length = (float)Math.Sqrt(textSize.X * textSize.X + textSize.Y * textSize.Y);
                    float angle = (float)Math.Atan2(textSize.Y, textSize.X);

                    // Draw the rotated line starting from the top-left of the text
                    spriteBatch.DrawSnapped(
                        texture: pixel,
                        position: textPosition,
                        sourceRectangle: null,
                        color: strikethroughColor,
                        rotation: angle,
                        origin: new Vector2(0, 0.5f), // Center the line vertically on the start point
                        scale: new Vector2(length, 1), // Scale the 1x1 pixel to the correct length and 1px thickness
                        effects: SpriteEffects.None,
                        layerDepth: 0
                    );
                }
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: originalRasterizerState);
        }
    }
}