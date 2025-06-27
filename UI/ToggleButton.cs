using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.UI
{
    public class ToggleButton : Button
    {
        public bool IsSelected { get; set; }
        public Color? CustomToggledTextColor { get; set; }

        #nullable enable
        public ToggleButton(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, Color? customToggledTextColor = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor) {
            CustomToggledTextColor = customToggledTextColor;
        }
        #nullable restore

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, bool forceHover = false)
        {
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (IsSelected)
            {
                textColor = CustomToggledTextColor ?? Global.Instance.Palette_Yellow;
            }
            else if (!IsEnabled)
            {
                textColor = CustomDisabledTextColor ?? Global.Instance.ButtonDisableColor;
            }
            else if (isActivated)
            {
                textColor = CustomHoverTextColor ?? Global.Instance.ButtonHoverColor;
            }
            else
            {
                textColor = CustomDefaultTextColor ?? Global.Instance.Palette_BrightWhite;
            }

            // Animate if hovered or selected
            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated || IsSelected);

            Vector2 textSize = font.MeasureString(Text);
            Vector2 textPosition = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) / 2 + xOffset,
                Bounds.Y + (Bounds.Height - textSize.Y) / 2
            );

            spriteBatch.DrawString(font, Text, textPosition, textColor);
        }
    }
}
