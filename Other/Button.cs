using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public enum StrikethroughType
    {
        None,
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
        public bool EnableHoverRotation { get; set; } = true;
        public BitmapFont? Font { get; set; }
        public Vector2 TextRenderOffset { get; set; } = Vector2.Zero;
        public Color? DebugColor { get; set; }

        public HoverAnimationType HoverAnimation { get; set; } = HoverAnimationType.Scale;
        public bool EnableTextWave { get; set; } = true;
        public bool AlwaysAnimateText { get; set; } = false;
        public TextEffectType WaveEffectType { get; set; } = TextEffectType.SmallWave;
        protected float _waveTimer = 0f;

        public bool UseInputDebounce { get; set; } = true;
        public bool TriggerHapticOnHover { get; set; } = false;

        public Action? OnClick { get; set; }
        public Action? OnRightClick { get; set; }
        public Action? OnMiddleClick { get; set; }

        public bool HasRightClickHint { get; set; } = false;
        public bool HasMiddleClickHint { get; set; } = false;
        public bool HasLeftClickAction => OnClick != null;
        public bool HasRightClickAction => OnRightClick != null || HasRightClickHint;
        public bool HasMiddleClickAction => OnMiddleClick != null || HasMiddleClickHint;

        protected MouseState _previousMouseState;
        protected readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        protected bool _isPressed = false;

        private readonly Texture2D? _spriteSheet;
        private readonly Rectangle? _defaultSourceRect;
        private readonly Rectangle? _hoverSourceRect;
        private readonly Rectangle? _clickedSourceRect;
        private readonly Rectangle? _disabledSourceRect;

        private const int LEFT_ALIGN_PADDING = 4;
        private static readonly Random _random = new Random();
        private static readonly RasterizerState _clipRasterizerState = new RasterizerState { ScissorTestEnable = true };

        private float _slideOffset = 0f;
        private const float SLIDE_TARGET_OFFSET = -1f;
        private const float SLIDE_SPEED = 80f;

        protected float _currentScale = 1.0f;
        private float _targetScale = 1.0f;
        private const float SCALE_SPEED = 75f;
        private const float HOVER_SCALE = 1.1f;
        private const float PRESS_SCALE = 1.1f;

        protected float _shakeTimer = 0f;
        private const float SHAKE_DURATION = 0.3f;
        private const float SHAKE_MAGNITUDE = 2f;
        private const float SHAKE_FREQUENCY = 40f;

        protected float _flashTimer = 0f;
        protected float _flashDuration = 0f;
        protected Color _flashColor;

        protected float _currentHoverRotation = 0f;
        public float CurrentHoverRotation => _currentHoverRotation;

        private float _hoverRotationTimer = 0f;
        private const float HOVER_ROTATION_DURATION = 0.25f;
        private const float BASE_ROTATION_MAGNITUDE = 0.06f;
        private const float ROTATION_REFERENCE_WIDTH = 32f;
        private const float HOVER_ROTATION_SPEED = 4.0f;

        private DateTime _lastClickTime = DateTime.MinValue;
        private const double DEBOUNCE_DURATION = 0.1;

        // --- ENTRANCE ANIMATION STATE ---
        private bool _isEntering = false;
        private float _entranceTimer = 0f;
        private float _entranceDelay = 0f;
        private const float ENTRANCE_DURATION = 0.4f;

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

        public void PlayEntrance(float delay)
        {
            _isEntering = true;
            _entranceTimer = 0f;
            _entranceDelay = delay;
            _currentScale = 0f;
            _targetScale = 1.0f;
        }

        public void SetHiddenForEntrance()
        {
            _isEntering = true;
            _entranceTimer = 0f;
            _entranceDelay = 100f;
            _currentScale = 0f;
        }

        public virtual void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            // If entering, disable interaction.
            // The timer logic is now handled in UpdateFeedbackAnimations (called via Draw)
            // because that is where we have access to GameTime.
            if (_isEntering)
            {
                IsHovered = false;
                _isPressed = false;
                return;
            }

            if (!IsEnabled)
            {
                IsHovered = false;
                _isPressed = false;
                _previousMouseState = currentMouseState;
                _targetScale = 1.0f;
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

            bool wasHovered = IsHovered;

            UpdateHoverState(virtualMousePos);

            if (!wasHovered && IsHovered)
            {
                if (TriggerHapticOnHover) ServiceLocator.Get<HapticsManager>().TriggerUICompoundShake(_global.HoverHapticStrength);
                if (EnableHoverRotation) _hoverRotationTimer = HOVER_ROTATION_DURATION;
            }

            bool mouseReleasedThisFrame = currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool mouseIsDown = currentMouseState.LeftButton == ButtonState.Pressed;

            if (IsHovered && mouseReleasedThisFrame)
            {
                bool isDebounceClear = (DateTime.Now - _lastClickTime).TotalSeconds > DEBOUNCE_DURATION;
                if (!UseInputDebounce || (isDebounceClear && UIInputManager.CanProcessMouseClick()))
                {
                    if (UseInputDebounce) _lastClickTime = DateTime.Now;
                    TriggerClick();
                    if (UseInputDebounce) UIInputManager.ConsumeMouseClick();
                }
            }

            _isPressed = IsHovered && mouseIsDown;

            bool shouldScale = HoverAnimation == HoverAnimationType.Scale || HoverAnimation == HoverAnimationType.ScaleUp;
            if (shouldScale)
            {
                if (_isPressed) _targetScale = PRESS_SCALE;
                else if (IsHovered) _targetScale = HOVER_SCALE;
                else _targetScale = 1.0f;
            }
            else
            {
                _targetScale = 1.0f;
            }

            var cursorManager = ServiceLocator.Get<CursorManager>();
            if (IsHovered && (HasLeftClickAction || HasRightClickAction || HasMiddleClickAction))
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
            if (IsEnabled) OnClick?.Invoke();
        }

        public virtual void TriggerShake() => _shakeTimer = SHAKE_DURATION;

        public virtual void TriggerFlash(Color color, float duration = 0.4f)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTimer = duration;
        }

        public virtual void ResetAnimationState()
        {
            _hoverAnimator.Reset();
            _waveTimer = 0f;
            _isPressed = false;
            IsHovered = false;
            _slideOffset = 0f;
            _shakeTimer = 0f;
            _flashTimer = 0f;
            _hoverRotationTimer = 0f;
            _currentHoverRotation = 0f;
            _currentScale = 1.0f;
            _targetScale = 1.0f;
            _isEntering = false;
        }

        protected (Vector2 shakeOffset, Color? flashTint) UpdateFeedbackAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- ENTRANCE ANIMATION LOGIC (Moved here to access GameTime) ---
            if (_isEntering)
            {
                _entranceTimer += dt;

                if (_entranceTimer < _entranceDelay)
                {
                    _currentScale = 0f;
                }
                else
                {
                    float animTime = _entranceTimer - _entranceDelay;
                    float progress = Math.Clamp(animTime / ENTRANCE_DURATION, 0f, 1f);
                    _currentScale = Easing.EaseOutElastic(progress);

                    if (progress >= 1.0f)
                    {
                        _isEntering = false;
                        _currentScale = 1.0f;
                    }
                }
                // Return early to prevent other animations from interfering during entrance
                return (Vector2.Zero, null);
            }

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

            if (_hoverRotationTimer > 0)
            {
                _hoverRotationTimer -= dt;
                float progress = 1.0f - (_hoverRotationTimer / HOVER_ROTATION_DURATION);
                float decay = (1.0f - progress) * (1.0f - progress);
                float currentWidth = Bounds.Width > 0 ? Bounds.Width : ROTATION_REFERENCE_WIDTH;
                float widthScale = ROTATION_REFERENCE_WIDTH / Math.Max(ROTATION_REFERENCE_WIDTH, currentWidth);
                float effectiveMagnitude = BASE_ROTATION_MAGNITUDE * widthScale;
                _currentHoverRotation = MathF.Sin(progress * MathHelper.TwoPi * HOVER_ROTATION_SPEED) * effectiveMagnitude * decay;
                if (_hoverRotationTimer <= 0) _currentHoverRotation = 0f;
            }
            else
            {
                _currentHoverRotation = 0f;
            }

            float scaleDamping = 1.0f - MathF.Exp(-SCALE_SPEED * dt);
            _currentScale = MathHelper.Lerp(_currentScale, _targetScale, scaleDamping);

            return (shakeOffset, flashTint);
        }

        public virtual void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_spriteSheet != null)
                DrawSprite(spriteBatch, gameTime, transform, forceHover, horizontalOffset, verticalOffset, tintColorOverride);
            else
                DrawText(spriteBatch, defaultFont, gameTime, transform, forceHover, horizontalOffset, verticalOffset, tintColorOverride);
        }

        private void DrawSprite(SpriteBatch spriteBatch, GameTime gameTime, Matrix transform, bool forceHover, float? horizontalOffset, float? verticalOffset, Color? tintColorOverride)
        {
            Rectangle? sourceRectToDraw = _defaultSourceRect;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (!IsEnabled && _disabledSourceRect.HasValue) sourceRectToDraw = _disabledSourceRect;
            else if (_isPressed && _clickedSourceRect.HasValue) sourceRectToDraw = _clickedSourceRect;
            else if (isActivated && _hoverSourceRect.HasValue) sourceRectToDraw = _hoverSourceRect;

            UpdateFeedbackAnimations(gameTime);

            Vector2 scale = new Vector2(_currentScale);
            var position = new Vector2(Bounds.Center.X + (horizontalOffset ?? 0f), Bounds.Center.Y + (verticalOffset ?? 0f));

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                var origin = new Vector2(MathF.Round(sourceRectToDraw.Value.Width / 2f), MathF.Round(sourceRectToDraw.Value.Height / 2f));
                spriteBatch.DrawSnapped(_spriteSheet, position, sourceRectToDraw, tintColorOverride ?? Color.White, _currentHoverRotation, origin, scale, SpriteEffects.None, 0f);
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

            if (tintColorOverride.HasValue) textColor = tintColorOverride.Value;
            else if (!IsEnabled) textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            else textColor = isActivated ? (CustomHoverTextColor ?? _global.ButtonHoverColor) : (CustomDefaultTextColor ?? _global.GameTextColor);

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (EnableTextWave && (isActivated || AlwaysAnimateText))
            {
                _waveTimer += deltaTime;
                if (TextAnimator.IsOneShotEffect(WaveEffectType))
                {
                    if (WaveEffectType == TextEffectType.SmallWave || WaveEffectType == TextEffectType.LeftAlignedSmallWave)
                    {
                        float duration = TextAnimator.GetSmallWaveDuration(Text.Length);
                        if (_waveTimer > duration + 0.1f) _waveTimer = 0f;
                    }
                }
            }
            else
            {
                _waveTimer = 0f;
            }

            UpdateFeedbackAnimations(gameTime);

            float xHoverOffset = 0f;
            float yHoverOffset = 0f;
            if (EnableHoverSway)
            {
                if (HoverAnimation == HoverAnimationType.Hop)
                {
                    yHoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
                }
                else if (HoverAnimation == HoverAnimationType.SlideAndHold)
                {
                    float targetOffset = isActivated ? SLIDE_TARGET_OFFSET : 0f;
                    float slideDamping = 1.0f - MathF.Exp(-SLIDE_SPEED * deltaTime);
                    _slideOffset = MathHelper.Lerp(_slideOffset, targetOffset, slideDamping);
                    xHoverOffset = _slideOffset;
                }
            }

            float totalXOffset = xHoverOffset + (horizontalOffset ?? 0f);
            float totalYOffset = yHoverOffset + (verticalOffset ?? 0f);

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition;

            if (AlignLeft)
                textPosition = new Vector2(Bounds.Left + totalXOffset + LEFT_ALIGN_PADDING, Bounds.Center.Y + totalYOffset - (textSize.Y / 2f));
            else
                textPosition = new Vector2(Bounds.Center.X + totalXOffset - (textSize.X / 2f), Bounds.Center.Y + totalYOffset - (textSize.Y / 2f));

            textPosition += TextRenderOffset;
            Vector2 origin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));
            Vector2 drawPos = textPosition + origin;

            if (EnableTextWave && (isActivated || AlwaysAnimateText))
            {
                TextAnimator.DrawTextWithEffect(spriteBatch, font, Text, textPosition, textColor, WaveEffectType, _waveTimer, new Vector2(_currentScale), null, _currentHoverRotation);
            }
            else
            {
                spriteBatch.DrawStringSnapped(font, Text, drawPos, textColor, _currentHoverRotation, origin, _currentScale, SpriteEffects.None, 0f);
            }
        }
    }
}