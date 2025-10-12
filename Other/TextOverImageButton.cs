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

namespace ProjectVagabond.UI
{
    public class TextOverImageButton : Button
    {
        private readonly Texture2D _backgroundTexture;
        public Texture2D? IconTexture { get; set; }
        public Rectangle? IconSourceRect { get; set; }

        // Animation State
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f;

        public TextOverImageButton(Rectangle bounds, string text, Texture2D backgroundTexture, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0, bool enableHoverSway = true, bool clickOnPress = false, BitmapFont? font = null, Texture2D? iconTexture = null, Rectangle? iconSourceRect = null, bool startVisible = true)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, alignLeft, overflowScrollSpeed, enableHoverSway, clickOnPress, font)
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

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_animState == AnimationState.Hidden) return;

            bool isActivated = IsEnabled && (IsHovered || forceHover);
            BitmapFont font = this.Font ?? defaultFont;

            // --- Animation Scaling ---
            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);
                verticalScale = Easing.EaseOutBack(progress);
                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }
            if (verticalScale < 0.01f) return;

            // 1. Calculate animation offset
            float yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            int animatedHeight = (int)(Bounds.Height * verticalScale);
            var animatedBounds = new Rectangle(
                Bounds.X + (int)(horizontalOffset ?? 0f),
                Bounds.Center.Y - animatedHeight / 2 + (int)yOffset + (int)(verticalOffset ?? 0f),
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
                    iconColor = _global.ButtonHoverColor;
                }
                else if (isActivated)
                {
                    backgroundTintColor = _global.ButtonHoverColor;
                    textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
                    iconColor = _global.ButtonHoverColor;
                }
                else
                {
                    backgroundTintColor = Color.White;
                    textColor = CustomDefaultTextColor ?? _global.Palette_BrightWhite;
                    iconColor = Color.White;
                }
            }

            // 3. Draw background with animation offset
            spriteBatch.DrawSnapped(_backgroundTexture, animatedBounds, backgroundTintColor);

            // Only draw contents if mostly visible
            if (verticalScale > 0.8f)
            {
                // 4. Calculate content layout
                Vector2 textSize = font.MeasureString(Text);
                float totalContentWidth = textSize.X;
                int iconWidth = 0;
                const int iconTextGap = 2;

                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    iconWidth = IconSourceRect.Value.Width;
                    totalContentWidth += iconWidth + iconTextGap;
                }

                float contentStartX = animatedBounds.X + (animatedBounds.Width - totalContentWidth) / 2f;

                // 5. Draw Icon
                if (IconTexture != null && IconSourceRect.HasValue)
                {
                    var iconRect = new Rectangle(
                        (int)contentStartX,
                        animatedBounds.Y + (animatedBounds.Height - IconSourceRect.Value.Height) / 2,
                        IconSourceRect.Value.Width,
                        IconSourceRect.Value.Height
                    );
                    spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, iconColor);
                }

                // 6. Draw Text
                float textStartX = contentStartX + iconWidth + (IconTexture != null ? iconTextGap : 0);
                Vector2 textPosition = new Vector2(textStartX, animatedBounds.Center.Y) + TextRenderOffset;
                Vector2 textOrigin = new Vector2(0, MathF.Round(textSize.Y / 2f));

                spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor, 0f, textOrigin, 1f, SpriteEffects.None, 0f);
            }
        }
    }
}
#nullable restore