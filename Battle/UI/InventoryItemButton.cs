using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Battle.UI
{
    public class InventoryItemButton : Button, IInventoryMenuItem
    {
        private readonly BitmapFont _itemFont;

        public InventoryItemButton(string itemName, BitmapFont font)
            : base(Rectangle.Empty, itemName.ToUpper())
        {
            _itemFont = font;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            var animatedBounds = new Rectangle(Bounds.X + (int)hopOffset, Bounds.Y, Bounds.Width, Bounds.Height);

            if (isActivated)
            {
                spriteBatch.DrawSnapped(pixel, animatedBounds, _global.Palette_DarkGray * 0.5f);
            }

            const int iconSize = 5;
            const int iconPadding = 2;
            var iconRect = new Rectangle(
                animatedBounds.X + iconPadding,
                animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                iconSize,
                iconSize
            );
            spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink);

            var textColor = isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
            if (!IsEnabled)
            {
                textColor = _global.ButtonDisableColor;
            }

            var textPosition = new Vector2(
                iconRect.Right + iconPadding + 1,
                animatedBounds.Y + (animatedBounds.Height - _itemFont.LineHeight) / 2
            );

            spriteBatch.DrawStringSnapped(_itemFont, this.Text, textPosition, textColor);
        }
    }
}