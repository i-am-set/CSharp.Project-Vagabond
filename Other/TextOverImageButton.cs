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

        public TextOverImageButton(Rectangle bounds, string text, Texture2D backgroundTexture, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, bool alignLeft = false, float overflowScrollSpeed = 0, bool enableHoverSway = true, bool clickOnPress = false, BitmapFont? font = null, Texture2D? iconTexture = null, Rectangle? iconSourceRect = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, alignLeft, overflowScrollSpeed, enableHoverSway, clickOnPress, font)
        {
            _backgroundTexture = backgroundTexture;
            IconTexture = iconTexture;
            IconSourceRect = iconSourceRect;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            BitmapFont font = this.Font ?? defaultFont;

            // 1. Calculate animation offset
            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            var animatedBounds = new Rectangle(Bounds.X + (int)hopOffset, Bounds.Y, Bounds.Width, Bounds.Height);

            // 2. Determine colors based on state
            Color backgroundTintColor;
            Color textColor;
            Color iconColor;

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

            // 3. Draw background with animation offset
            spriteBatch.DrawSnapped(_backgroundTexture, animatedBounds, backgroundTintColor);

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
#nullable restore