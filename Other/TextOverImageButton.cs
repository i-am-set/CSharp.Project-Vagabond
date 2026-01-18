#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

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

            // FIX: Respect EnableHoverSway property
            float yOffset = 0f;
            if (EnableHoverSway)
            {
                yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }
            else
            {
                // Update the animator state without using the offset, so it doesn't get stuck
                _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }

            // Calculate Center Position for Rotation
            float totalX = Bounds.Center.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Center.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;
            Vector2 centerPos = new Vector2(totalX, totalY);

            // Calculate Bounds Size (scaled vertically by appear anim)
            int width = Bounds.Width;
            int height = (int)(Bounds.Height * verticalScale);
            Vector2 origin = new Vector2(width / 2f, Bounds.Height / 2f); // Origin based on FULL height to scale properly

            // Scale vector for the draw call (Vertical scale only affects height)
            Vector2 drawScale = new Vector2(1.0f, verticalScale);

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
                    iconColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                }
                else if (isActivated)
                {
                    // Only tint background if allowed
                    backgroundTintColor = TintBackgroundOnHover ? _global.ButtonHoverColor : Color.White;
                    textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                    iconColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
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
            // Use DrawSnapped with rotation
            if (_backgroundTexture != null)
            {
                // We use the full texture bounds as source
                Rectangle source = new Rectangle(0, 0, _backgroundTexture.Width, _backgroundTexture.Height);
                // Adjust origin to texture center
                Vector2 texOrigin = new Vector2(_backgroundTexture.Width / 2f, _backgroundTexture.Height / 2f);

                // If the texture is meant to fill the bounds:
                Vector2 texScale = new Vector2(
                    (float)width / _backgroundTexture.Width,
                    (float)height / _backgroundTexture.Height // Use 'height' which already has verticalScale applied
                );

                spriteBatch.DrawSnapped(_backgroundTexture, centerPos, source, backgroundTintColor, _currentHoverRotation, texOrigin, texScale, SpriteEffects.None, 0f);
            }

            // 4b. Draw Border if enabled and activated
            if (isActivated && DrawBorderOnHover)
            {
                // Skipped rotation on 1px border lines to avoid aliasing artifacts
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
                // --- NEW LAYOUT LOGIC ---
                // We need to calculate positions relative to center to apply rotation.
                const int iconPaddingLeft = 5;
                const int iconTextGap = 2;

                // Relative offsets from Center
                float leftEdgeX = -width / 2f;
                float rightEdgeX = width / 2f;

                Vector2 iconOffset = Vector2.Zero;
                bool hasIcon = IconTexture != null && IconSourceRect.HasValue;

                if (hasIcon)
                {
                    // Icon is left aligned
                    float iconX = leftEdgeX + iconPaddingLeft + (IconSourceRect!.Value.Width / 2f);
                    iconOffset = new Vector2(iconX, 0); // Centered vertically relative to button
                }

                // Text Position Calculation
                Vector2 textSize = font.MeasureString(Text);
                float textCenterX;

                if (hasIcon)
                {
                    float iconRight = leftEdgeX + iconPaddingLeft + IconSourceRect!.Value.Width;
                    float textSpaceStartX = iconRight + iconTextGap;
                    float textSpaceEndX = rightEdgeX - iconPaddingLeft;
                    float textSpaceWidth = textSpaceEndX - textSpaceStartX;
                    textCenterX = textSpaceStartX + (textSpaceWidth / 2f);
                }
                else
                {
                    textCenterX = 0; // Absolute center
                }

                // Apply TextRenderOffset
                Vector2 textOffset = new Vector2(textCenterX, TextRenderOffset.Y);

                // --- ROTATION TRANSFORM HELPER ---
                // Rotates a local offset by _currentHoverRotation
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
                    // Rotate start/end points
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