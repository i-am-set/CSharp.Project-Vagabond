#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

            int animatedHeight = (int)(Bounds.Height * verticalScale);
            var animatedBounds = new Rectangle(
                Bounds.X + (int)MathF.Round(horizontalOffset ?? 0f) + (int)MathF.Round(shakeOffset.X),
                Bounds.Center.Y - animatedHeight / 2 + (int)yOffset + (int)(verticalOffset ?? 0f) + (int)MathF.Round(shakeOffset.Y),
                Bounds.Width,
                animatedHeight
            );

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
                    textColor = CustomDefaultTextColor ?? _global.Palette_BrightWhite;
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
                spriteBatch.DrawSnapped(_backgroundTexture, animatedBounds, backgroundTintColor);
            }

            // 4b. Draw Border if enabled and activated
            // Drawn AFTER background to ensure it's visible on top
            if (isActivated && DrawBorderOnHover)
            {
                Color borderColor = HoverBorderColor ?? _global.Palette_Red;

                // Draw strictly within the bounds (0 to Width-1)
                // Top
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Top, animatedBounds.Width, 1), borderColor);
                // Bottom
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Bottom - 1, animatedBounds.Width, 1), borderColor);
                // Left
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Left, animatedBounds.Top, 1, animatedBounds.Height), borderColor);
                // Right
                spriteBatch.DrawSnapped(pixel, new Rectangle(animatedBounds.Right - 1, animatedBounds.Top, 1, animatedBounds.Height), borderColor);
            }

            // Only draw contents if mostly visible
            if (verticalScale > 0.8f)
            {
                // --- NEW LAYOUT LOGIC ---
                const int iconPaddingLeft = 5;
                const int iconTextGap = 2;

                // 5. Draw Icon (if it exists)
                Rectangle iconRect = Rectangle.Empty;
                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    iconRect = new Rectangle(
                        animatedBounds.X + iconPaddingLeft,
                        animatedBounds.Y + (animatedBounds.Height - IconSourceRect.Value.Height) / 2,
                        IconSourceRect.Value.Width,
                        IconSourceRect.Value.Height
                    );
                    spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, iconColor);
                }

                // 6. Draw Text
                Vector2 textSize = font.MeasureString(Text);
                float textStartX;

                if (iconRect != Rectangle.Empty)
                {
                    // Calculate the space available for the text (to the right of the icon)
                    float textSpaceStartX = iconRect.Right + iconTextGap;
                    float textSpaceEndX = animatedBounds.Right - iconPaddingLeft; // Mirror the left padding for symmetry
                    float textSpaceWidth = textSpaceEndX - textSpaceStartX;

                    // Center the text within that available space
                    textStartX = textSpaceStartX + (textSpaceWidth - textSize.X) / 2f;
                }
                else
                {
                    // If there's no icon, center the text within the entire button
                    textStartX = animatedBounds.X + (animatedBounds.Width - textSize.X) / 2f;
                }

                Vector2 textPosition = new Vector2(textStartX, animatedBounds.Center.Y - (textSize.Y / 2f)) + TextRenderOffset;

                // --- Wave Animation State Management ---
                // Trigger animation only on the rising edge of hover (One-Shot)
                if (isActivated && !_wasHoveredLastDraw)
                {
                    _isWaveAnimating = true;
                    _waveTimer = 0f;
                }

                // Stop animation immediately if no longer hovered
                if (!isActivated)
                {
                    _isWaveAnimating = false;
                    _waveTimer = 0f;
                }

                _wasHoveredLastDraw = isActivated;

                if (_isWaveAnimating)
                {
                    _waveTimer += deltaTime;
                    float estimatedDuration = (Text.Length * WaveFrequency + MathHelper.Pi) / WaveSpeed + 0.5f;
                    if (_waveTimer > estimatedDuration)
                    {
                        _isWaveAnimating = false;
                    }
                }

                // --- Wave Animation Logic ---
                if (EnableTextWave && _isWaveAnimating)
                {
                    float startX = textPosition.X;
                    float baseY = textPosition.Y;

                    for (int i = 0; i < Text.Length; i++)
                    {
                        char c = Text[i];
                        string charStr = c.ToString();

                        // --- SENTINEL MEASUREMENT TRICK ---
                        // Measure the string up to this character, including a sentinel character '|'.
                        // Then subtract the width of the sentinel.
                        // This forces MeasureString to include trailing spaces and kerning correctly.
                        string sub = Text.Substring(0, i);
                        float charOffsetX = font.MeasureString(sub + "|").Width - font.MeasureString("|").Width;

                        // Calculate Wave Offset (Left to Right)
                        float waveArg = _waveTimer * WaveSpeed - i * WaveFrequency;

                        float yWaveOffset = 0f;
                        if (waveArg > 0 && waveArg < MathHelper.Pi)
                        {
                            float waveVal = MathF.Sin(waveArg);
                            yWaveOffset = -MathF.Round(waveVal * WaveAmplitude);
                        }

                        spriteBatch.DrawStringSnapped(font, charStr, new Vector2(startX + charOffsetX, baseY + yWaveOffset), textColor);
                    }
                }
                else
                {
                    spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor);
                }

                // --- Strikethrough Logic for Disabled State ---
                if (!IsEnabled)
                {
                    // Calculate line position based on the text position
                    // Center Y of text (textPosition.Y is top, so add half height)
                    float lineY = textPosition.Y + (textSize.Y / 2f);
                    float startX = textPosition.X - 2;
                    float endX = textPosition.X + textSize.X + 2;

                    // Use fully opaque color for the strikethrough
                    spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), _global.ButtonDisableColor);
                }
            }
        }
    }
}