using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.UI
{
    public class ToggleButton : Button
    {
        public bool IsSelected { get; set; }

        public ToggleButton(Rectangle bounds, string text) : base(bounds, text) { }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, bool forceHover = false)
        {
            Color textColor;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (IsSelected)
            {
                // A distinct color for the selected/toggled button
                textColor = Global.Instance.Palette_Yellow;
            }
            else if (!IsEnabled)
            {
                textColor = Global.Instance.Palette_Gray;
            }
            else if (isActivated)
            {
                textColor = Global.Instance.OptionHoverColor;
            }
            else
            {
                textColor = CustomTextColor ?? Global.Instance.Palette_BrightWhite;
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
