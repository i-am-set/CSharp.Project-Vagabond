#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class ToggleButton : Button
    {
        public bool IsSelected { get; set; }
        public Color? CustomToggledTextColor { get; set; }

        public ToggleButton(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, Color? customToggledTextColor = null, bool zoomHapticOnClick = true, bool clickOnPress = false, BitmapFont? font = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, clickOnPress: clickOnPress, font: font)
        {
            CustomToggledTextColor = customToggledTextColor;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            // This method now just determines the color and then calls the base Draw method.
            // The base Draw method handles all animation and rendering logic.
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (IsSelected)
            {
                textColor = CustomToggledTextColor ?? _global.Palette_Yellow;
            }
            else if (!IsEnabled)
            {
                textColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
            }
            else if (isActivated)
            {
                textColor = CustomHoverTextColor ?? _global.ButtonHoverColor;
            }
            else
            {
                textColor = CustomDefaultTextColor ?? _global.Palette_BrightWhite;
            }

            // Temporarily set the custom color for the base Draw method to use
            var originalColor = this.CustomDefaultTextColor;
            this.CustomDefaultTextColor = textColor;

            base.Draw(spriteBatch, defaultFont, gameTime, transform, forceHover, horizontalOffset, verticalOffset, tintColorOverride);

            // Restore the original custom color
            this.CustomDefaultTextColor = originalColor;
        }
    }
}
#nullable restore