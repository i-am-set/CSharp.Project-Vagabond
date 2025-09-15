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
            Color tintColor = Color.White;
            BitmapFont font = this.Font ?? defaultFont;

            if (!IsEnabled)
            {
                tintColor = _global.ButtonDisableColor * 0.5f;
            }
            else if (_isPressed)
            {
                tintColor = Color.Gray;
            }
            else if (isActivated)
            {
                tintColor = _global.ButtonHoverColor;
            }

            spriteBatch.DrawSnapped(_backgroundTexture, this.Bounds, tintColor);

            // --- Text Color ---
            Color textColor;
            if (!IsEnabled) textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            else textColor = isActivated ? (CustomHoverTextColor ?? _global.ButtonHoverColor) : (CustomDefaultTextColor ?? _global.Palette_BrightWhite);

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            Vector2 textSize = font.MeasureString(Text);

            // --- Combined Layout Calculation ---
            float totalContentWidth = textSize.X;
            int iconWidth = 0;
            const int iconTextGap = 2;

            if (IconTexture != null && IconSourceRect.HasValue)
            {
                iconWidth = IconSourceRect.Value.Width;
                totalContentWidth += iconWidth + iconTextGap;
            }

            // Calculate the starting X for the whole content block to be centered
            float contentStartX = Bounds.X + (Bounds.Width - totalContentWidth) / 2f;

            // --- Icon Drawing ---
            if (IconTexture != null && IconSourceRect.HasValue)
            {
                var iconRect = new Rectangle(
                    (int)(contentStartX + hopOffset),
                    Bounds.Y + (Bounds.Height - IconSourceRect.Value.Height) / 2,
                    IconSourceRect.Value.Width,
                    IconSourceRect.Value.Height
                );
                spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect.Value, Color.White);
            }

            // --- Text Drawing ---
            // The text starts after the icon and the gap
            float textStartX = contentStartX + iconWidth + (IconTexture != null ? iconTextGap : 0);
            Vector2 textPosition = new Vector2(textStartX + hopOffset, Bounds.Center.Y) + TextRenderOffset;
            Vector2 textOrigin = new Vector2(0, MathF.Round(textSize.Y / 2f)); // Left-align origin for text

            spriteBatch.DrawStringSnapped(font, Text, textPosition, textColor, 0f, textOrigin, 1f, SpriteEffects.None, 0f);
        }
    }
}
#nullable restore