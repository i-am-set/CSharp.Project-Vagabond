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
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

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

        // New properties for customization
        public bool TintIconOnHover { get; set; } = true;
        public float ContentXOffset { get; set; } = 0f;

        // Animation State
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

            bool isActivated = IsEnabled && (IsHovered || forceHover);
            BitmapFont font = this.Font ?? defaultFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Animation Scaling ---
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

            // 1. Calculate animation offsets
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            float yOffset = 0f;
            if (EnableHoverSway)
            {
                yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }
            else
            {
                _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }

            // Calculate Center Position for Rotation
            float totalX = Bounds.Center.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Center.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;
            Vector2 centerPos = new Vector2(totalX, totalY);

            // Calculate Bounds Size (scaled vertically by appear anim)
            int width = Bounds.Width;
            int height = (int)(Bounds.Height * verticalScale);

            // 2. Determine colors based on state
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

            // 3. Apply flash tint if active
            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                backgroundTintColor = Color.Lerp(backgroundTintColor, flashTint.Value, flashAmount);
                textColor = Color.Lerp(textColor, flashTint.Value, flashAmount);
                iconColor = Color.Lerp(iconColor, flashTint.Value, flashAmount);
            }

            // 4. Draw background with animation offset (if texture exists)
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

            // 4b. Draw Border if enabled and activated
            if (isActivated && DrawBorderOnHover)
            {
                Color borderColor = HoverBorderColor ?? _global.Palette_Red;
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

            // Only draw contents if mostly visible
            if (verticalScale > 0.8f)
            {
                // --- UPDATED LAYOUT LOGIC: CENTERED GROUP ---
                const int iconPaddingLeft = 5; // Used only for AlignLeft mode
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

                // Calculate Start X relative to Center (0,0)
                float startX = 0f;
                if (AlignLeft)
                {
                    // Start from left edge + padding + custom offset
                    startX = (-width / 2f) + iconPaddingLeft + ContentXOffset;
                }
                else
                {
                    // Center the entire content block + custom offset
                    startX = (-contentWidth / 2f) + ContentXOffset;
                }

                Vector2 iconOffset = Vector2.Zero;
                Vector2 textOffset = Vector2.Zero;

                if (hasIcon)
                {
                    // Icon center relative to button center
                    float iconCenterX = startX + (iconWidth / 2f);
                    iconOffset = new Vector2(iconCenterX, 0);

                    // Text center relative to button center
                    float textCenterX = startX + iconWidth + iconTextGap + (textWidth / 2f);
                    textOffset = new Vector2(textCenterX, TextRenderOffset.Y);
                }
                else
                {
                    float textCenterX = startX + (textWidth / 2f);
                    textOffset = new Vector2(textCenterX, TextRenderOffset.Y);
                }

                // --- ROTATION TRANSFORM HELPER ---
                Vector2 RotateOffset(Vector2 local)
                {
                    float cos = MathF.Cos(_currentHoverRotation);
                    float sin = MathF.Sin(_currentHoverRotation);
                    return new Vector2(
                        local.X * cos - local.Y * sin,
                        local.X * sin + local.Y * cos
                    );
                }

                // 5. Draw Icon
                if (hasIcon)
                {
                    Vector2 iconDrawPos = centerPos + RotateOffset(iconOffset);
                    Vector2 iconOrigin = new Vector2(IconSourceRect!.Value.Width / 2f, IconSourceRect.Value.Height / 2f);

                    spriteBatch.DrawSnapped(IconTexture, iconDrawPos, IconSourceRect.Value, iconColor, _currentHoverRotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                }

                // 6. Draw Text
                Vector2 textDrawPos = centerPos + RotateOffset(textOffset);
                Vector2 textOrigin = textSize / 2f;

                // --- Wave Animation Logic ---
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

                // --- Strikethrough Logic ---
                if (!IsEnabled)
                {
                    // Rotate start/end points relative to text center
                    Vector2 lineStartLocal = textOffset + new Vector2(-textSize.X / 2f - 2, 0);
                    Vector2 lineEndLocal = textOffset + new Vector2(textSize.X / 2f + 2, 0);

                    Vector2 p1 = centerPos + RotateOffset(lineStartLocal);
                    Vector2 p2 = centerPos + RotateOffset(lineEndLocal);

                    spriteBatch.DrawLineSnapped(p1, p2, _global.ButtonDisableColor);
                }
            }
        }
    }
}
