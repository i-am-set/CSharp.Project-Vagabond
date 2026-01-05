using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            base.Update(currentMouseState, worldTransform);
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            var animatedBounds = new Rectangle(Bounds.X, Bounds.Y + (int)yOffset + (int)(verticalOffset ?? 0f), Bounds.Width, Bounds.Height);

            if (isActivated)
            {
                spriteBatch.DrawSnapped(pixel, animatedBounds, _global.Palette_DarkGray * 0.5f);
            }

            const int iconSize = 5;
            const int iconPadding = 2;

            // The base layout rectangle for the icon (5x5)
            var layoutIconRect = new Rectangle(
                animatedBounds.X + iconPadding,
                animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                iconSize,
                iconSize
            );

            // Determine the actual drawing rectangle for the sprite
            Rectangle drawRect = layoutIconRect;
            if (isActivated)
            {
                // Upscale to 32x32 centered on the original 5x5 position
                const int expandedSize = 32;
                drawRect = new Rectangle(
                    layoutIconRect.Center.X - expandedSize / 2,
                    layoutIconRect.Center.Y - expandedSize / 2,
                    expandedSize,
                    expandedSize
                );
            }

            var itemTexture = spriteManager.GetItemSprite(Item.ImagePath);

            if (itemTexture != null)
            {
                spriteBatch.DrawSnapped(itemTexture, drawRect, Color.White);
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, drawRect, _global.Palette_Pink);
            }

            // --- Item Name Drawing ---
            var nameColor = isActivated ? _global.ButtonHoverColor : _global.Palette_BlueWhite;
            if (!IsEnabled)
            {
                nameColor = _global.ButtonDisableColor;
            }

            // Use layoutIconRect for text positioning to keep text stable even when icon scales up
            var namePosition = new Vector2(
                layoutIconRect.Right + iconPadding + 1,
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