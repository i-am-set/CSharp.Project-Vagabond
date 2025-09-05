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
        public bool ClickOnPress { get; set; } = false;

        public event Action OnClick;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        private float _scrollPosition = 0f;
        private float _swayTimer = 0f;
        private bool _wasHoveredLastFrame = false;
        protected bool _isPressed = false; // State to track if the button was pressed down

        // Animation state for the squash effect
        private float _squashAnimationTimer = 0f;
        private const float SQUASH_ANIMATION_DURATION = 0.03f;

        private const float SWAY_SPEED = 3f;
        private const float SWAY_AMOUNT_X = 1f;

        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

#nullable enable
        public Button(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0.0f, bool enableHoverSway = true, bool clickOnPress = false)
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
            ClickOnPress = clickOnPress;
        }
#nullable restore

        public virtual void Update(MouseState currentMouseState)
        {
            Vector2 virtualMousePos = UseScreenCoordinates
                ? currentMouseState.Position.ToVector2()
                : Core.TransformMouse(currentMouseState.Position);

            UpdateHoverState(virtualMousePos);

            if (ClickOnPress)
            {
                // Old logic for immediate click on press
                if (UIInputManager.CanProcessMouseClick() && IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                {
                    TriggerClick();
                    UIInputManager.ConsumeMouseClick();
                }
            }
            else
            {
                // New logic for click on release
                bool mousePressedOverButton = IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
                bool mouseReleasedOverButton = IsHovered && currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;

                if (mousePressedOverButton)
                {
                    _isPressed = true;
                }

                if (mouseReleasedOverButton && _isPressed)
                {
                    if (UIInputManager.CanProcessMouseClick())
                    {
                        TriggerClick();
                        UIInputManager.ConsumeMouseClick();
                    }
                }

                // Reset pressed state if the mouse button is released anywhere
                if (currentMouseState.LeftButton == ButtonState.Released)
                {
                    _isPressed = false;
                }
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

        public virtual void ResetAnimationState()
        {
            _hoverAnimator.Reset();
            _isPressed = false;
            _squashAnimationTimer = 0f;
            _swayTimer = 0f;
            _wasHoveredLastFrame = false;
            IsHovered = false;
        }

        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform, bool forceHover = false)
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

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_isPressed && !ClickOnPress)
            {
                _squashAnimationTimer = Math.Min(_squashAnimationTimer + deltaTime, SQUASH_ANIMATION_DURATION);
            }
            else
            {
                _squashAnimationTimer = Math.Max(_squashAnimationTimer - deltaTime, 0);
            }

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            float swayOffsetX = 0f;

            if (isActivated && EnableHoverSway)
            {
                if (!_wasHoveredLastFrame)
                {
                    _swayTimer = 0f; // Reset timer on new hover to start animation from the beginning.
                }
                _swayTimer += deltaTime;
                swayOffsetX = (float)Math.Sin(_swayTimer * SWAY_SPEED) * SWAY_AMOUNT_X;
            }
            else
            {
                _swayTimer = 0f; // Reset if not hovered.
            }
            _wasHoveredLastFrame = isActivated;

            float totalXOffset = hopOffset + swayOffsetX;
            Vector2 textSize = font.MeasureString(Text);

            // Calculate squash scale based on animation timer
            Vector2 scale = Vector2.One;
            if (_squashAnimationTimer > 0)
            {
                float progress = _squashAnimationTimer / SQUASH_ANIMATION_DURATION;
                float targetScaleY = 1.0f / textSize.Y; // Target a 1-pixel height
                scale.Y = MathHelper.Lerp(1.0f, targetScaleY, progress);
            }

            // To correctly handle clipping while respecting the scene's transform,
            // we must restart the SpriteBatch, passing the transform matrix along.
            var originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            var originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;

            // The scene's SpriteBatch.Begin uses BlendState.AlphaBlend and SamplerState.PointClamp.
            // We must preserve these when restarting the batch.
            spriteBatch.End();
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, rasterizerState: _clipRasterizerState, transformMatrix: transform);

            // The ScissorRectangle is applied in the coordinate space of the render target,
            // which is what the transform matrix maps our virtual coordinates to.
            // Therefore, we can use the virtual-space `Bounds` directly.
            spriteBatch.GraphicsDevice.ScissorRectangle = Bounds;

            bool shouldScroll = OverflowScrollSpeed > 0 && textSize.X > Bounds.Width;
            if (shouldScroll)
            {
                _scrollPosition += deltaTime * OverflowScrollSpeed;
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
                Vector2 textOrigin = textSize / 2f;
                Vector2 textPosition;
                if (AlignLeft)
                {
                    // For left-align, origin needs to be adjusted to just the vertical center
                    textOrigin.X = 0;
                    textPosition = new Vector2(Bounds.Left + totalXOffset, Bounds.Center.Y);
                }
                else
                {
                    textPosition = new Vector2(Bounds.Center.X + totalXOffset, Bounds.Center.Y);
                }

                spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor, 0f, textOrigin, scale, SpriteEffects.None, 0f);

                // --- DIAGONAL STRIKETHROUGH LOGIC ---
                if (Strikethrough == StrikethroughType.Exhausted)
                {
                    Color strikethroughColor = _global.Palette_Red;
                    var pixel = ServiceLocator.Get<Texture2D>();

                    // Calculate diagonal properties based on the unscaled text size
                    float length = (float)Math.Sqrt(textSize.X * textSize.X + textSize.Y * textSize.Y);
                    float angle = (float)Math.Atan2(textSize.Y, textSize.X);

                    // Adjust position for non-centered origin if left-aligned
                    Vector2 strikethroughPos = textPosition;
                    if (AlignLeft)
                    {
                        strikethroughPos.Y -= textSize.Y / 2f;
                    }

                    spriteBatch.DrawSnapped(
                        texture: pixel,
                        position: strikethroughPos,
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

            // End our custom batch and restore the original state for the rest of the scene.
            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, rasterizerState: originalRasterizerState, transformMatrix: transform);
        }
    }
}