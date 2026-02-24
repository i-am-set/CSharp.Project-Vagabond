#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class TextOverImageButton : Button
    {
        private readonly Texture2D? _backgroundTexture;
        public Texture2D? IconTexture { get; set; }
        public Rectangle? IconSourceRect { get; set; }

        public bool TintBackgroundOnHover { get; set; } = true;
        public bool DrawBorderOnHover { get; set; } = false;
        public Color? HoverBorderColor { get; set; }

        public bool TintIconOnHover { get; set; } = true;
        public bool IconColorMatchesText { get; set; } = false;
        public float ContentXOffset { get; set; } = 0f;
        public Vector2 IconRenderOffset { get; set; } = Vector2.Zero;

        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f;

        public TextOverImageButton(Rectangle bounds, string text, Texture2D? backgroundTexture, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0, bool enableHoverSway = true, BitmapFont? font = null, Texture2D? iconTexture = null, Rectangle? iconSourceRect = null, bool startVisible = true)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, alignLeft, overflowScrollSpeed, enableHoverSway, font)
        {
            _backgroundTexture = backgroundTexture;
            IconTexture = iconTexture;
            IconSourceRect = iconSourceRect;
            _animState = startVisible ? AnimationState.Idle : AnimationState.Hidden;
        }

        public void TriggerAppearAnimation()
        {
            if (_animState == AnimationState.Hidden)
            {
                _animState = AnimationState.Appearing;
                _appearTimer = 0f;
            }
        }

        public void HideForAnimation()
        {
            _animState = AnimationState.Hidden;
        }

        public override void ResetAnimationState()
        {
            base.ResetAnimationState();
            _animState = AnimationState.Idle;
            _appearTimer = 0f;
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            base.Update(currentMouseState, worldTransform);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_animState == AnimationState.Hidden) return;

            bool isActivated = IsEnabled && (IsHovered || IsSelected || forceHover);
            BitmapFont font = this.Font ?? defaultFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += deltaTime;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);
                verticalScale = Easing.EaseOutBack(progress);
                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }
            if (verticalScale < 0.01f) return;

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            float yOffset = 0f;
            if (EnableHoverSway)
            {
                yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated, HoverLiftOffset, HoverLiftDuration);
            }
            else
            {
                _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated, HoverLiftOffset, HoverLiftDuration);
            }

            float totalX = Bounds.Center.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Center.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;
            Vector2 centerPos = new Vector2(totalX, totalY);

            int width = Bounds.Width;
            int height = (int)(Bounds.Height * verticalScale);

            Color backgroundTintColor;
            Color textColor;
            Color iconColor;

            if (tintColorOverride.HasValue)
            {
                backgroundTintColor = tintColorOverride.Value;
                textColor = tintColorOverride.Value;
                iconColor = tintColorOverride.Value;
            }
            else
            {
                if (!IsEnabled)
                {
                    backgroundTintColor = _global.ButtonDisableColor * 0.5f;
                    textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
                    iconColor = _global.ButtonDisableColor;
                }
                else if (_isPressed)
                {
                    backgroundTintColor = Color.Gray;
                    textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                    iconColor = TintIconOnHover ? (CustomHoverTextColor ?? _global.ButtonHoverColor) : Color.White;
                }
                else if (isActivated)
                {
                    backgroundTintColor = TintBackgroundOnHover ? _global.ButtonHoverColor : Color.White;
                    textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                    iconColor = TintIconOnHover ? (CustomHoverTextColor ?? _global.ButtonHoverColor) : Color.White;
                }
                else
                {
                    backgroundTintColor = Color.White;
                    textColor = CustomDefaultTextColor ?? _global.Palette_Sun;
                    iconColor = Color.White;
                }
            }

            if (IconColorMatchesText)
            {
                iconColor = textColor;
            }

            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                backgroundTintColor = Color.Lerp(backgroundTintColor, flashTint.Value, flashAmount);
                textColor = Color.Lerp(textColor, flashTint.Value, flashAmount);
                iconColor = Color.Lerp(iconColor, flashTint.Value, flashAmount);
            }

            if (_backgroundTexture != null)
            {
                Rectangle source = new Rectangle(0, 0, _backgroundTexture.Width, _backgroundTexture.Height);
                Vector2 texOrigin = new Vector2(_backgroundTexture.Width / 2f, _backgroundTexture.Height / 2f);
                Vector2 texScale = new Vector2(
                    (float)width / _backgroundTexture.Width,
                    (float)height / _backgroundTexture.Height
                );

                spriteBatch.DrawSnapped(_backgroundTexture, centerPos, source, backgroundTintColor, _currentHoverRotation, texOrigin, texScale, SpriteEffects.None, 0f);
            }

            if (isActivated && DrawBorderOnHover)
            {
                Color borderColor = HoverBorderColor ?? _global.Palette_Rust;
                var animatedBounds = new Rectangle(
                    (int)(centerPos.X - width / 2f),
                    (int)(centerPos.Y - height / 2f),
                    width,
                    height
                );

                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Top, animatedBounds.Width, 1), borderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Bottom - 1, animatedBounds.Width, 1), borderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Top, 1, animatedBounds.Height), borderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Right - 1, animatedBounds.Top, 1, animatedBounds.Height), borderColor);
            }

            if (verticalScale > 0.8f)
            {
                const int iconPaddingLeft = 5;
                const int iconTextGap = 2;

                bool hasIcon = IconTexture != null && IconSourceRect.HasValue;
                Vector2 textSize = font.MeasureString(Text);

                float contentWidth = 0f;
                float iconWidth = 0f;
                float textWidth = textSize.X;

                if (hasIcon)
                {
                    iconWidth = IconSourceRect!.Value.Width;
                    contentWidth = iconWidth + iconTextGap + textWidth;
                }
                else
                {
                    contentWidth = textWidth;
                }

                float startX = 0f;
                if (AlignLeft)
                {
                    startX = (-width / 2f) + iconPaddingLeft + ContentXOffset;
                }
                else
                {
                    startX = (-contentWidth / 2f) + ContentXOffset;
                }

                Vector2 iconOffset = Vector2.Zero;
                Vector2 textOffset = Vector2.Zero;

                if (hasIcon)
                {
                    float iconCenterX = startX + (iconWidth / 2f);
                    iconOffset = new Vector2(iconCenterX, 0) + IconRenderOffset;

                    float textCenterX = startX + iconWidth + iconTextGap + (textWidth / 2f);
                    textOffset = new Vector2(textCenterX, TextRenderOffset.Y);
                }
                else
                {
                    float textCenterX = startX + (textWidth / 2f);
                    textOffset = new Vector2(textCenterX, TextRenderOffset.Y);
                }

                Vector2 RotateOffset(Vector2 local)
                {
                    float cos = MathF.Cos(_currentHoverRotation);
                    float sin = MathF.Sin(_currentHoverRotation);
                    return new Vector2(
                        local.X * cos - local.Y * sin,
                        local.X * sin + local.Y * cos
                    );
                }

                if (hasIcon)
                {
                    Vector2 iconDrawPos = centerPos + RotateOffset(iconOffset);
                    Vector2 iconOrigin = new Vector2(IconSourceRect!.Value.Width / 2f, IconSourceRect.Value.Height / 2f);

                    spriteBatch.DrawSnapped(IconTexture, iconDrawPos, IconSourceRect.Value, iconColor, _currentHoverRotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                }

                Vector2 textDrawPos = centerPos + RotateOffset(textOffset);
                Vector2 textOrigin = textSize / 2f;

                if (EnableTextWave && isActivated)
                {
                    _waveTimer += deltaTime;
                    if (TextAnimator.IsOneShotEffect(WaveEffectType))
                    {
                        float duration = TextAnimator.GetSmallWaveDuration(Text.Length);
                        if (_waveTimer > duration + 0.1f) _waveTimer = 0f;
                    }

                    TextAnimator.DrawTextWithEffect(spriteBatch, font, Text, textDrawPos - textOrigin, textColor, WaveEffectType, _waveTimer, Vector2.One, null, _currentHoverRotation);
                }
                else
                {
                    _waveTimer = 0f;
                    spriteBatch.DrawStringSnapped(font, Text, textDrawPos, textColor, _currentHoverRotation, textOrigin, 1.0f, SpriteEffects.None, 0f);
                }

                if (!IsEnabled)
                {
                    Vector2 lineStartLocal = textOffset + new Vector2(-textSize.X / 2f - 2, 0);
                    Vector2 lineEndLocal = textOffset + new Vector2(textSize.X / 2f + 2, 0);

                    Vector2 p1 = centerPos + RotateOffset(lineStartLocal);
                    Vector2 p2 = centerPos + RotateOffset(lineEndLocal);

                    if (Math.Abs(_currentHoverRotation) < 0.01f)
                    {
                        int x1 = (int)MathF.Round(p1.X);
                        int x2 = (int)MathF.Round(p2.X);
                        int y = (int)MathF.Round(p1.Y);
                        int w = x2 - x1;
                        // Draw 1px rectangle instead of line for perfect snapping
                        spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), new Rectangle(x1, y, w, 1), _global.ButtonDisableColor);
                    }
                    else
                    {
                        spriteBatch.DrawLineSnapped(p1, p2, _global.ButtonDisableColor);
                    }
                }
            }
        }
    }
}
