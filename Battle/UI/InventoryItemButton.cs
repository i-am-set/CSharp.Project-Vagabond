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
        public ConsumableItemData Item { get; }
        public int Quantity { get; }

        public InventoryItemButton(ConsumableItemData item, int quantity, BitmapFont font)
            : base(Rectangle.Empty, item.ItemName.ToUpper())
        {
            _itemFont = font;
            Item = item;
            Quantity = quantity;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? externalSwayOffset = null)
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

            // --- Item Name Drawing ---
            var nameColor = isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
            if (!IsEnabled)
            {
                nameColor = _global.ButtonDisableColor;
            }

            var namePosition = new Vector2(
                iconRect.Right + iconPadding + 1,
                animatedBounds.Y + (animatedBounds.Height - _itemFont.LineHeight) / 2
            );

            spriteBatch.DrawStringSnapped(_itemFont, this.Text, namePosition, nameColor);

            // --- Quantity Drawing ---
            var quantityColor = IsEnabled ? _global.Palette_Gray : _global.ButtonDisableColor;

            string quantityText = $"x{Quantity}";
            var quantitySize = _itemFont.MeasureString(quantityText);

            var quantityPosition = new Vector2(
                animatedBounds.Right - quantitySize.Width - (iconPadding * 2),
                animatedBounds.Y + (animatedBounds.Height - _itemFont.LineHeight) / 2
            );

            spriteBatch.DrawStringSnapped(_itemFont, quantityText, quantityPosition, quantityColor);
        }
    }
}