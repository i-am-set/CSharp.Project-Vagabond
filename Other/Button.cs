#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Defines the type of animation to play when a button is hovered.
    /// </summary>
    public enum HoverAnimationType
    {
        /// <summary>
        /// A quick "hop" to the right and back.
        /// </summary>
        Hop,
        /// <summary>
        /// Slides to the right and holds the position until unhovered.
        /// </summary>
        SlideAndHold
    }

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
        public bool IsPressed => _isPressed;
        public bool UseScreenCoordinates { get; set; } = false;
        public bool AlignLeft { get; set; } = false;
        public float OverflowScrollSpeed { get; set; } = 0f;
        public StrikethroughType Strikethrough { get; set; } = StrikethroughType.None;
        public bool EnableHoverSway { get; set; } = true;
        public BitmapFont? Font { get; set; }
        public Vector2 TextRenderOffset { get; set; } = Vector2.Zero;
        public Color? DebugColor { get; set; }
        public HoverAnimationType HoverAnimation { get; set; } = HoverAnimationType.Hop;


        public event Action? OnClick;
        public event Action? OnRightClick;

        public bool HasRightClickHint { get; set; } = false;
        public bool HasLeftClickAction => OnClick != null;
        public bool HasRightClickAction => OnRightClick != null || HasRightClickHint;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        protected bool _isPressed = false;

        // Sprite-based properties
        private readonly Texture2D? _spriteSheet;
        private readonly Rectangle? _defaultSourceRect;
        private readonly Rectangle? _hoverSourceRect;
        private readonly Rectangle? _clickedSourceRect;
        private readonly Rectangle? _disabledSourceRect;

        // Animation state
        private float _squashAnimationTimer = 0f;
        private const float SQUASH_ANIMATION_DURATION = 0.03f;
        private const int LEFT_ALIGN_PADDING = 4;
        private static readonly Random _random = new Random();
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // Slide and Hold animation state
        private float _slideOffset = 0f;
        private const float SLIDE_TARGET_OFFSET = -1f;
        private const float SLIDE_SPEED = 80f;

        // Feedback Animation State
        protected float _shakeTimer = 0f;
        private const float SHAKE_DURATION = 0.3f;
        private const float SHAKE_MAGNITUDE = 2f;
        private const float SHAKE_FREQUENCY = 40f;

        protected float _flashTimer = 0f;
        protected float _flashDuration = 0f;
        protected Color _flashColor;

        // Text-based constructor
        public Button(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0.0f, bool enableHoverSway = true, BitmapFont? font = null)
        {
            _global = ServiceLocator.Get<Global>();
            if (function == null) function = text;

            Bounds = bounds;
            Text = text;
            Function = function;
            CustomDefaultTextColor = customDefaultTextColor;
            CustomHoverTextColor = customHoverTextColor;
            CustomDisabledTextColor = customDisabledTextColor;
            AlignLeft = alignLeft;
            OverflowScrollSpeed = overflowScrollSpeed;
            EnableHoverSway = enableHoverSway;
            Font = font;
        }

        // Sprite-based constructor
        public Button(Rectangle bounds, Texture2D? spriteSheet, Rectangle? defaultSourceRect, Rectangle? hoverSourceRect, Rectangle? clickedSourceRect, Rectangle? disabledSourceRect, string? function = null, bool enableHoverSway = true, Color? debugColor = null)
        {
            _global = ServiceLocator.Get<Global>();
            Bounds = bounds;
            Text = "";
            Function = function ?? "";
            EnableHoverSway = enableHoverSway;
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect;
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            DebugColor = debugColor;
        }

        public virtual void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            if (!IsEnabled)
            {
                IsHovered = false;
                _isPressed = false;
                _previousMouseState = currentMouseState;
                return;
            }

            Vector2 virtualMousePos = UseScreenCoordinates
                ? currentMouseState.Position.ToVector2()
                : Core.TransformMouse(currentMouseState.Position);

            if (worldTransform.HasValue)
            {
                var inverseTransform = Matrix.Invert(worldTransform.Value);
                virtualMousePos = Vector2.Transform(virtualMousePos, inverseTransform);
            }

            UpdateHoverState(virtualMousePos);

            bool mousePressedThisFrame = currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool mouseIsDown = currentMouseState.LeftButton == ButtonState.Pressed;

            // Click-on-press is now the only behavior.
            if (UIInputManager.CanProcessMouseClick() && IsHovered && mousePressedThisFrame)
            {
                TriggerClick();
                UIInputManager.ConsumeMouseClick();
            }

            // Visual pressed state is active as long as mouse is down over the button.
            _isPressed = IsHovered && mouseIsDown;

            bool rightMouseReleasedOverButton = IsHovered && currentMouseState.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
            if (rightMouseReleasedOverButton)
            {
                if (UIInputManager.CanProcessMouseClick())
                {
                    OnRightClick?.Invoke();
                    UIInputManager.ConsumeMouseClick();
                }
            }

            // Update cursor state
            var cursorManager = ServiceLocator.Get<CursorManager>();
            if (IsHovered)
            {
                cursorManager.SetState(_isPressed ? CursorState.Click : CursorState.HoverClickable);
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

        public virtual void TriggerShake()
        {
            _shakeTimer = SHAKE_DURATION;
        }

        public virtual void TriggerFlash(Color color, float duration = 0.4f)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTimer = duration;
        }

        public virtual void ResetAnimationState()
        {
            _hoverAnimator.Reset();
            _isPressed = false;
            _squashAnimationTimer = 0f;
            IsHovered = false;
            _slideOffset = 0f;
            _shakeTimer = 0f;
            _flashTimer = 0f;
        }

        protected (Vector2 shakeOffset, Color? flashTint) UpdateFeedbackAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 shakeOffset = Vector2.Zero;
            Color? flashTint = null;

            if (_shakeTimer > 0)
            {
                _shakeTimer -= dt;
                float progress = 1f - (_shakeTimer / SHAKE_DURATION);
                float magnitude = SHAKE_MAGNITUDE * (1f - Easing.EaseOutQuad(progress));
                shakeOffset.X = MathF.Sin(_shakeTimer * SHAKE_FREQUENCY) * magnitude;
            }

            if (_flashTimer > 0)
            {
                _flashTimer -= dt;
                float progress = 1f - (_flashTimer / _flashDuration);
                float alpha = 1.0f - Easing.EaseInQuad(progress);
                flashTint = new Color(_flashColor, alpha);
            }

            return (shakeOffset, flashTint);
        }

        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_spriteSheet != null)
            {
                DrawSprite(spriteBatch, gameTime, transform, forceHover, horizontalOffset, verticalOffset, tintColorOverride);
            }
            else
            {
                DrawText(spriteBatch, defaultFont, gameTime, transform, forceHover, horizontalOffset, verticalOffset, tintColorOverride);
            }
        }

        private void DrawSprite(SpriteBatch spriteBatch, GameTime gameTime, Matrix transform, bool forceHover, float? horizontalOffset, float? verticalOffset, Color? tintColorOverride)
        {
            Rectangle? sourceRectToDraw = _defaultSourceRect;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (!IsEnabled && _disabledSourceRect.HasValue) sourceRectToDraw = _disabledSourceRect;
            else if (_isPressed && _clickedSourceRect.HasValue) sourceRectToDraw = _clickedSourceRect;
            else if (isActivated && _hoverSourceRect.HasValue) sourceRectToDraw = _hoverSourceRect;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_isPressed) _squashAnimationTimer = Math.Min(_squashAnimationTimer + deltaTime, SQUASH_ANIMATION_DURATION);
            else _squashAnimationTimer = Math.Max(_squashAnimationTimer - deltaTime, 0);

            Vector2 scale = Vector2.One;
            Vector2 shakeOffset = Vector2.Zero;
            if (_squashAnimationTimer > 0)
            {
                float progress = _squashAnimationTimer / SQUASH_ANIMATION_DURATION;
                scale.Y = MathHelper.Lerp(1.0f, 1.5f / Bounds.Height, progress);
                shakeOffset.X = MathF.Round((float)(_random.NextDouble() * 2 - 1) * SHAKE_MAGNITUDE);
            }

            var position = new Vector2(Bounds.Center.X + (horizontalOffset ?? 0f), Bounds.Center.Y + (verticalOffset ?? 0f)) + shakeOffset;

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                var origin = sourceRectToDraw.Value.Size.ToVector2() / 2f;
                spriteBatch.DrawSnapped(_spriteSheet, position, sourceRectToDraw, tintColorOverride ?? Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }
            else if (DebugColor.HasValue)
            {
                var debugRect = new Rectangle((int)position.X - Bounds.Width / 2, (int)position.Y - Bounds.Height / 2, Bounds.Width, Bounds.Height);
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), debugRect, DebugColor.Value);
            }
        }

        private void DrawText(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover, float? horizontalOffset, float? verticalOffset, Color? tintColorOverride)
        {
            BitmapFont font = this.Font ?? defaultFont;
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (tintColorOverride.HasValue)
            {
                textColor = tintColorOverride.Value;
            }
            else
            {
                if (!IsEnabled) textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
                else textColor = isActivated ? (CustomHoverTextColor ?? _global.ButtonHoverColor) : (CustomDefaultTextColor ?? _global.Palette_BrightWhite);
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_isPressed) _squashAnimationTimer = Math.Min(_squashAnimationTimer + deltaTime, SQUASH_ANIMATION_DURATION);
            else _squashAnimationTimer = Math.Max(_squashAnimationTimer - deltaTime, 0);

            float xHoverOffset = 0f;
            float yHoverOffset = 0f;
            if (EnableHoverSway)
            {
                if (HoverAnimation == HoverAnimationType.Hop)
                {
                    yHoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
                }
                else // SlideAndHold
                {
                    float targetOffset = isActivated ? SLIDE_TARGET_OFFSET : 0f;
                    _slideOffset = MathHelper.Lerp(_slideOffset, targetOffset, SLIDE_SPEED * deltaTime);
                    xHoverOffset = _slideOffset;
                }
            }

            float totalXOffset = xHoverOffset + (horizontalOffset ?? 0f);
            float totalYOffset = yHoverOffset + (verticalOffset ?? 0f);

            Vector2 textSize = font.MeasureString(Text);

            Vector2 scale = Vector2.One;
            Vector2 shakeOffset = Vector2.Zero;
            if (_squashAnimationTimer > 0)
            {
                float progress = _squashAnimationTimer / SQUASH_ANIMATION_DURATION;
                float targetScaleY = 1.5f / textSize.Y;
                scale.Y = MathHelper.Lerp(1.0f, targetScaleY, progress);
                shakeOffset.X = MathF.Round((float)(_random.NextDouble() * 2 - 1) * SHAKE_MAGNITUDE);
            }

            Vector2 textOrigin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));
            Vector2 textPosition;

            if (AlignLeft)
            {
                textOrigin.X = 0;
                textPosition = new Vector2(Bounds.Left + totalXOffset + LEFT_ALIGN_PADDING, Bounds.Center.Y + totalYOffset);
            }
            else
            {
                textPosition = new Vector2(Bounds.Center.X + totalXOffset, Bounds.Center.Y + totalYOffset);
            }

            textPosition += TextRenderOffset + shakeOffset;

            spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor, 0f, textOrigin, scale, SpriteEffects.None, 0f);
        }
    }
}
#nullable restore