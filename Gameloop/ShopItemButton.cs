using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class ShopItemButton : Button
    {
        public ShopItem Item { get; }
        private readonly Texture2D _iconTexture;
        private readonly Texture2D _iconSilhouette;
        private readonly BitmapFont _priceFont;
        private readonly BitmapFont _nameFont;

        public ShopItemButton(Rectangle bounds, ShopItem item, Texture2D icon, Texture2D silhouette, BitmapFont priceFont, BitmapFont nameFont)
            : base(bounds, "")
        {
            Item = item;
            _iconTexture = icon;
            _iconSilhouette = silhouette;
            _priceFont = priceFont;
            _nameFont = nameFont;

            // Ensure button is 16x16
            Bounds = new Rectangle(bounds.X, bounds.Y, 16, 16);
            UseScreenCoordinates = true;
            EnableHoverSway = true;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Calculate Animation Offsets
            float yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;

            Vector2 centerPos = new Vector2(totalX + Bounds.Width / 2f, totalY + Bounds.Height / 2f);

            // --- Draw Name (Above) - Only if Hovered ---
            if (isActivated && !Item.IsSold)
            {
                string nameText = Item.DisplayName.ToUpper();
                // No truncation
                Vector2 nameSize = _nameFont.MeasureString(nameText);
                Vector2 namePos = new Vector2(
                    centerPos.X - nameSize.X / 2f,
                    totalY - nameSize.Y - 2
                );

                // Draw background for readability if needed, but for now just text
                spriteBatch.DrawStringSnapped(_nameFont, nameText, namePos, _global.Palette_BrightWhite);
            }

            // --- Draw Icon (Center) ---
            if (Item.IsSold)
            {
                // Draw Empty Slot / Sold State
                spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX, (int)totalY, 16, 16), _global.Palette_DarkestGray);
                string soldText = "SOLD";
                Vector2 soldSize = _nameFont.MeasureString(soldText);
                Vector2 soldPos = new Vector2(centerPos.X - soldSize.X / 2f, centerPos.Y - soldSize.Y / 2f);
                spriteBatch.DrawStringSnapped(_nameFont, soldText, soldPos, _global.Palette_Red);
            }
            else
            {
                // Draw Background if hovered
                if (isActivated)
                {
                    spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX - 1, (int)totalY - 1, 18, 18), _global.Palette_DarkGray);
                }

                // Draw Icon
                if (_iconTexture != null)
                {
                    // Draw Silhouette Outline if hovered
                    if (isActivated && _iconSilhouette != null)
                    {
                        Color outlineColor = _global.ItemOutlineColor_Hover;
                        spriteBatch.DrawSnapped(_iconSilhouette, new Vector2(totalX - 1, totalY), Color.White);
                        spriteBatch.DrawSnapped(_iconSilhouette, new Vector2(totalX + 1, totalY), Color.White);
                        spriteBatch.DrawSnapped(_iconSilhouette, new Vector2(totalX, totalY - 1), Color.White);
                        spriteBatch.DrawSnapped(_iconSilhouette, new Vector2(totalX, totalY + 1), Color.White);
                    }

                    spriteBatch.DrawSnapped(_iconTexture, new Vector2(totalX, totalY), Color.White);
                }
            }

            // --- Draw Price (Below) ---
            if (!Item.IsSold)
            {
                string priceText = $"{Item.Price}G";
                Vector2 priceSize = _priceFont.MeasureString(priceText);
                Vector2 pricePos = new Vector2(
                    centerPos.X - priceSize.X / 2f,
                    totalY + Bounds.Height + 2
                );
                spriteBatch.DrawStringSnapped(_priceFont, priceText, pricePos, _global.Palette_Yellow);
            }
        }
    }
}