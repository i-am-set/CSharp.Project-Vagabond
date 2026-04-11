using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public enum StrikethroughType
    {
        None,
        Exhausted
    }

    public class Button : ISelectable
    {
        protected readonly Global _global;

        public Rectangle Bounds { get; set; }

        private string _text = "";
        public string Text
        {
            get => _text;
            set => _text = value?.ToUpperInvariant() ?? "";
        }

        public string Function { get; set; }
        public Color? CustomDefaultTextColor { get; set; }
        public Color? CustomHoverTextColor { get; set; }
        public Color? CustomDisabledTextColor { get; set; }
        public Color? CustomSelectedTextColor { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsHovered { get; set; }
        public bool IsSelected { get; set; }
        public bool IsPressed => _isPressed;
        public ISelectable? NeighborUp { get; set; }
        public ISelectable? NeighborDown { get; set; }
        public ISelectable? NeighborLeft { get; set; }
        public ISelectable? NeighborRight { get; set; }
        public bool UseScreenCoordinates { get; set; } = false;
        public bool AlignLeft { get; set; } = false;
        public float OverflowScrollSpeed { get; set; } = 0f;
        public StrikethroughType Strikethrough { get; set; } = StrikethroughType.None;
        public bool EnableHoverSway { get; set; } = true;
        public bool EnableHoverRotation { get; set; } = true;
        public BitmapFont? Font { get; set; }
        public Vector2 TextRenderOffset { get; set; } = Vector2.Zero;
        public Color? DebugColor { get; set; }

        public bool UseTextOutline { get; set; } = false;
        public Color? TextOutlineColor { get; set; }

        public bool DrawBorderOnHover { get; set; }
        public Color? HoverBorderColor { get; set; }

        // Vacuum seal border gap (default 2 pixels)
        public float VacuumGap { get; set; } = 2f;

        public HoverAnimationType HoverAnimation { get; set; } = HoverAnimationType.Hop;
        public bool EnableTextWave { get; set; } = true;
        public bool AlwaysAnimateText { get; set; } = false;
        public TextEffectType WaveEffectType { get; set; } = TextEffectType.SmallWave;
        protected float _waveTimer = 0f;

        public string HoverSoundCue { get; set; } = "ui_hover";
        public string PressSoundCue { get; set; } = "ui_click";
        public string ClickSoundCue { get; set; } = "ui_confirm";

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
        public HoverAnimator HoverAnimator => _hoverAnimator;

        // --- TUNABLE HOVER PROPERTIES ---
        public float HoverLiftOffset { get; set; }
        public float HoverLiftDuration { get; set; }

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

        // Persistent lists for contour rendering
        private readonly List<Vector2> _topContour = new List<Vector2>();
        private readonly List<Vector2> _bottomContour = new List<Vector2>();

        // --- ENTRANCE ANIMATION STATE ---
        public PlinkAnimator Plink { get; } = new PlinkAnimator();

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
            DrawBorderOnHover = true;

            HoverLiftOffset = _global.UI_ButtonHoverLift;
            HoverLiftDuration = _global.UI_ButtonHoverDuration;
        }

        public void OnSelect()
        {
            IsSelected = true;
            if (EnableHoverRotation) _hoverRotationTimer = HOVER_ROTATION_DURATION;
        }

        public void OnDeselect()
        {
            IsSelected = false;
        }

        public void OnSubmit()
        {
            TriggerClick();
        }

        public virtual bool HandleInput(InputManager input)
        {
            if (!IsEnabled) return false;
            if (input.Confirm)
            {
                TriggerClick();
                return true;
            }
            return false;
        }

        public void PlayEntrance(float delay)
        {
            Plink.Start(delay);
            _currentScale = 0f;
            _targetScale = 1.0f;
        }

        public void SetHiddenForEntrance()
        {
            Plink.Start(100f);
            _currentScale = 0f;
        }

        public virtual void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            if (Plink.IsActive)
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
                if (TriggerHapticOnHover) ServiceLocator.Get<HapticsManager>().TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                if (EnableHoverRotation) _hoverRotationTimer = HOVER_ROTATION_DURATION;

                if (!string.IsNullOrEmpty(HoverSoundCue))
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi(HoverSoundCue);
                }
            }

            bool mouseIsDown = currentMouseState.LeftButton == ButtonState.Pressed;
            bool mouseWasDown = _previousMouseState.LeftButton == ButtonState.Pressed;

            if (IsHovered && mouseIsDown && !mouseWasDown)
            {
                _isPressed = true;
                if (!string.IsNullOrEmpty(PressSoundCue))
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi(PressSoundCue);
                }
            }

            if (!mouseIsDown && _isPressed)
            {
                _isPressed = false;
                if (IsHovered)
                {
                    bool isDebounceClear = (DateTime.Now - _lastClickTime).TotalSeconds > DEBOUNCE_DURATION;
                    var inputManager = ServiceLocator.Get<InputManager>();
                    if (!UseInputDebounce || (isDebounceClear && inputManager.IsMouseClickAvailable()))
                    {
                        if (UseInputDebounce) _lastClickTime = DateTime.Now;
                        TriggerClick();
                        if (UseInputDebounce) inputManager.ConsumeMouseClick();
                    }
                }
            }

            bool shouldScale = HoverAnimation == HoverAnimationType.Scale || HoverAnimation == HoverAnimationType.ScaleUp;
            if (shouldScale)
            {
                if (_isPressed) _targetScale = PRESS_SCALE;
                else if (IsHovered || IsSelected) _targetScale = HOVER_SCALE;
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
            if (IsEnabled)
            {
                if (!string.IsNullOrEmpty(ClickSoundCue))
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi(ClickSoundCue);
                }
                OnClick?.Invoke();
            }
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
            IsSelected = false;
            _slideOffset = 0f;
            _shakeTimer = 0f;
            _flashTimer = 0f;
            _hoverRotationTimer = 0f;
            _currentHoverRotation = 0f;
            _currentScale = 1.0f;
            _targetScale = 1.0f;
        }

        protected (Vector2 shakeOffset, Color? flashTint) UpdateFeedbackAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (Plink.IsActive)
            {
                Plink.Update(gameTime, Bounds.Center.ToVector2());
                _currentScale = Plink.Scale;
                _currentHoverRotation = Plink.Rotation;
                return (Vector2.Zero, Plink.FlashTint);
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

            _currentHoverRotation = 0f;

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
            bool isActivated = IsEnabled && (IsHovered || IsSelected || forceHover);

            if (!IsEnabled && _disabledSourceRect.HasValue) sourceRectToDraw = _disabledSourceRect;
            else if (_isPressed && _clickedSourceRect.HasValue) sourceRectToDraw = _clickedSourceRect;
            else if (isActivated && _hoverSourceRect.HasValue) sourceRectToDraw = _hoverSourceRect;

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);
            if (_currentScale < 0.01f) return;

            Vector2 scale = new Vector2(_currentScale);
            var position = new Vector2(Bounds.Center.X + (horizontalOffset ?? 0f) + shakeOffset.X, Bounds.Center.Y + (verticalOffset ?? 0f) + shakeOffset.Y);

            Color finalColor = tintColorOverride ?? Color.White;
            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                finalColor = Color.Lerp(finalColor, flashTint.Value, flashAmount);
            }

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                var origin = new Vector2(MathF.Round(sourceRectToDraw.Value.Width / 2f), MathF.Round(sourceRectToDraw.Value.Height / 2f));
                spriteBatch.DrawSnapped(_spriteSheet, position, sourceRectToDraw, finalColor, _currentHoverRotation, origin, scale, SpriteEffects.None, 0f);
            }
            else if (DebugColor.HasValue)
            {
                var debugRect = new Rectangle((int)position.X - Bounds.Width / 2, (int)position.Y - Bounds.Height / 2, Bounds.Width, Bounds.Height);
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), debugRect, DebugColor.Value);
            }
        }

        private List<Vector2> SmoothContour(List<Vector2> points, int iterations = 2)
        {
            if (points.Count < 3) return points;
            List<Vector2> current = points;

            for (int iter = 0; iter < iterations; iter++)
            {
                List<Vector2> next = new List<Vector2>(current.Count * 2);
                next.Add(current[0]);

                for (int i = 0; i < current.Count - 1; i++)
                {
                    Vector2 p0 = current[i];
                    Vector2 p1 = current[i + 1];

                    next.Add(Vector2.Lerp(p0, p1, 0.25f));
                    next.Add(Vector2.Lerp(p0, p1, 0.75f));
                }

                next.Add(current[current.Count - 1]);
                current = next;
            }
            return current;
        }

        private void DrawText(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover, float? horizontalOffset, float? verticalOffset, Color? tintColorOverride)
        {
            BitmapFont font = this.Font ?? defaultFont;
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || IsSelected || forceHover);

            if (tintColorOverride.HasValue) textColor = tintColorOverride.Value;
            else if (!IsEnabled) textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            else if (_isPressed) textColor = CustomSelectedTextColor ?? _global.Palette_Fruit;
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

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);
            if (_currentScale < 0.01f) return;

            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                textColor = Color.Lerp(textColor, flashTint.Value, flashAmount);
            }

            float xHoverOffset = 0f;
            float yHoverOffset = 0f;
            if (EnableHoverSway)
            {
                if (HoverAnimation == HoverAnimationType.Hop)
                {
                    if (_isPressed)
                    {
                        yHoverOffset = 0f;
                    }
                    else
                    {
                        yHoverOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated, HoverLiftOffset, HoverLiftDuration);
                    }
                }
                else if (HoverAnimation == HoverAnimationType.SlideAndHold)
                {
                    float targetOffset = isActivated ? SLIDE_TARGET_OFFSET : 0f;
                    float slideDamping = 1.0f - MathF.Exp(-SLIDE_SPEED * deltaTime);
                    _slideOffset = MathHelper.Lerp(_slideOffset, targetOffset, slideDamping);
                    xHoverOffset = _slideOffset;
                }
            }

            float totalXOffset = xHoverOffset + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalYOffset = yHoverOffset + (verticalOffset ?? 0f) + shakeOffset.Y;

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition;

            if (AlignLeft)
                textPosition = new Vector2(Bounds.Left + totalXOffset + LEFT_ALIGN_PADDING, Bounds.Center.Y + totalYOffset - MathF.Round(textSize.Y / 2f));
            else
                textPosition = new Vector2(Bounds.Center.X + totalXOffset - MathF.Round(textSize.X / 2f), Bounds.Center.Y + totalYOffset - MathF.Round(textSize.Y / 2f));

            textPosition += TextRenderOffset;
            textPosition = new Vector2(MathF.Round(textPosition.X), MathF.Round(textPosition.Y));

            Vector2 origin = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));
            Vector2 drawPos = textPosition + origin;

            if (isActivated && DrawBorderOnHover)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                Color borderColor = HoverBorderColor ?? _global.Palette_Fruit;

                _topContour.Clear();
                _bottomContour.Clear();

                Vector2 layoutScale = new Vector2(_currentScale);
                Vector2 centeringOffset = (textSize * (Vector2.One - layoutScale)) / 2f;
                centeringOffset = new Vector2(MathF.Round(centeringOffset.X), MathF.Round(centeringOffset.Y));

                if (WaveEffectType == TextEffectType.LeftAlignedSmallWave || WaveEffectType == TextEffectType.RightAlignedSmallWave)
                {
                    centeringOffset = Vector2.Zero;
                }

                var glyphs = font.GetGlyphs(Text, textPosition);
                int charIndex = 0;

                float cosBase = MathF.Cos(_currentHoverRotation);
                float sinBase = MathF.Sin(_currentHoverRotation);
                Vector2 textCenterOffset = new Vector2(MathF.Round(textSize.X / 2f), MathF.Round(textSize.Y / 2f));
                Vector2 pivotPoint = textPosition + textCenterOffset;

                foreach (var glyph in glyphs)
                {
                    while (charIndex < Text.Length && Text[charIndex] == '\n') charIndex++;
                    if (charIndex >= Text.Length) break;

                    char c = Text[charIndex];
                    if (char.IsWhiteSpace(c))
                    {
                        charIndex++;
                        continue;
                    }

                    var character = glyph.Character;
                    if (character != null && character.TextureRegion != null)
                    {
                        var region = character.TextureRegion;
                        Vector2 charOrigin = new Vector2(MathF.Round(region.Width / 2f), MathF.Round(region.Height / 2f));

                        Vector2 animOffset = Vector2.Zero;
                        Vector2 effectScale = Vector2.One;
                        float animRotation = 0f;

                        if (EnableTextWave && (isActivated || AlwaysAnimateText))
                        {
                            var tempTransform = TextAnimator.GetTextEffectTransform(
                                WaveEffectType, _waveTimer, charIndex, textColor, Text.Length, null);
                            animOffset = tempTransform.Offset;
                            effectScale = tempTransform.Scale;
                            animRotation = tempTransform.Rotation;
                        }

                        Vector2 relativePos = glyph.Position - textPosition;
                        Vector2 unrotatedPos = textPosition + (relativePos * layoutScale) + centeringOffset;

                        Vector2 vecFromCenter = unrotatedPos - pivotPoint;
                        Vector2 rotatedVec = new Vector2(
                            vecFromCenter.X * cosBase - vecFromCenter.Y * sinBase,
                            vecFromCenter.X * sinBase + vecFromCenter.Y * cosBase
                        );
                        Vector2 rotatedPos = pivotPoint + rotatedVec;

                        Vector2 finalDrawPos = rotatedPos + charOrigin + animOffset;

                        Vector2 finalScale = layoutScale * effectScale;
                        float finalRotation = _currentHoverRotation + animRotation;

                        float charCos = MathF.Cos(finalRotation);
                        float charSin = MathF.Sin(finalRotation);

                        Vector2 Rotate(Vector2 p) => new Vector2(p.X * charCos - p.Y * charSin, p.X * charSin + p.Y * charCos);

                        Vector2 tlLocal = new Vector2(-charOrigin.X, -charOrigin.Y - VacuumGap) * finalScale;
                        Vector2 trLocal = new Vector2(region.Width - charOrigin.X, -charOrigin.Y - VacuumGap) * finalScale;
                        Vector2 blLocal = new Vector2(-charOrigin.X, region.Height - charOrigin.Y + VacuumGap) * finalScale;
                        Vector2 brLocal = new Vector2(region.Width - charOrigin.X, region.Height - charOrigin.Y + VacuumGap) * finalScale;

                        Vector2 rotatedTl = finalDrawPos + Rotate(tlLocal);
                        Vector2 rotatedTr = finalDrawPos + Rotate(trLocal);
                        Vector2 rotatedBl = finalDrawPos + Rotate(blLocal);
                        Vector2 rotatedBr = finalDrawPos + Rotate(brLocal);

                        _topContour.Add(rotatedTl);
                        _topContour.Add(rotatedTr);
                        _bottomContour.Add(rotatedBl);
                        _bottomContour.Add(rotatedBr);
                    }
                    charIndex++;
                }

                if (_topContour.Count > 0)
                {
                    var smoothedTop = SmoothContour(_topContour, 3);
                    var smoothedBottom = SmoothContour(_bottomContour, 3);

                    float bX = Bounds.X + totalXOffset;
                    float bR = Bounds.Right + totalXOffset;

                    smoothedTop.Insert(0, new Vector2(bX, smoothedTop[0].Y));
                    smoothedTop.Add(new Vector2(bR, smoothedTop[smoothedTop.Count - 1].Y));

                    smoothedBottom.Insert(0, new Vector2(bX, smoothedBottom[0].Y));
                    smoothedBottom.Add(new Vector2(bR, smoothedBottom[smoothedBottom.Count - 1].Y));

                    spriteBatch.DrawBresenhamLineSnapped(pixel, smoothedTop[0], smoothedBottom[0], borderColor);

                    spriteBatch.DrawBresenhamLineSnapped(pixel, smoothedTop[smoothedTop.Count - 1], smoothedBottom[smoothedBottom.Count - 1], borderColor);

                    for (int i = 0; i < smoothedTop.Count - 1; i++)
                    {
                        spriteBatch.DrawBresenhamLineSnapped(pixel, smoothedTop[i], smoothedTop[i + 1], borderColor);
                    }

                    for (int i = 0; i < smoothedBottom.Count - 1; i++)
                    {
                        spriteBatch.DrawBresenhamLineSnapped(pixel, smoothedBottom[i], smoothedBottom[i + 1], borderColor);
                    }
                }
                else
                {
                    int bX = (int)MathF.Round(Bounds.X + totalXOffset);
                    int bY = (int)MathF.Round(Bounds.Y + totalYOffset - 1);
                    int bW = Bounds.Width;
                    int bH = Bounds.Height + 2;
                    spriteBatch.Draw(pixel, new Rectangle(bX, bY, bW, 1), borderColor);
                    spriteBatch.Draw(pixel, new Rectangle(bX, bY + bH - 1, bW, 1), borderColor);
                    spriteBatch.Draw(pixel, new Rectangle(bX, bY, 1, bH), borderColor);
                    spriteBatch.Draw(pixel, new Rectangle(bX + bW - 1, bY, 1, bH), borderColor);
                }
            }

            Color outlineColor = TextOutlineColor ?? _global.Palette_Black;

            if (EnableTextWave && (isActivated || AlwaysAnimateText))
            {
                if (UseTextOutline)
                {
                    TextAnimator.DrawTextWithEffectOutlined(spriteBatch, font, Text, textPosition, textColor, outlineColor, WaveEffectType, _waveTimer, new Vector2(_currentScale), null, _currentHoverRotation);
                }
                else
                {
                    TextAnimator.DrawTextWithEffect(spriteBatch, font, Text, textPosition, textColor, WaveEffectType, _waveTimer, new Vector2(_currentScale), null, _currentHoverRotation);
                }
            }
            else
            {
                if (UseTextOutline)
                {
                    spriteBatch.DrawStringOutlinedSnapped(font, Text, drawPos, textColor, outlineColor, _currentHoverRotation, origin, _currentScale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawStringSnapped(font, Text, drawPos, textColor, _currentHoverRotation, origin, _currentScale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}