#nullable enable
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
    public class SpellEquipButton : Button
    {
        public string SpellName { get; set; } = "EMPTY";
        public bool IsEquipped { get; set; } = false;
        // Layout Constants
        private const int WIDTH = 107;
        private const int HEIGHT = 8;

        public SpellEquipButton(Rectangle bounds) : base(bounds, "")
        {
            // Enforce width and height
            Bounds = new Rectangle(bounds.X, bounds.Y, WIDTH, HEIGHT);
            EnableHoverSway = false;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // 1. Calculate Animation Offsets
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);
            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y;

            // 2. Draw Border (No Background)
            if (isActivated)
            {
                Color borderColor = _global.Palette_Red;
                // Top
                spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX, (int)totalY, WIDTH, 1), borderColor);
                // Bottom (Extended down by 1 pixel)
                spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX, (int)totalY + HEIGHT, WIDTH, 1), borderColor);
                // Left (Extended height by 1)
                spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX, (int)totalY, 1, HEIGHT + 1), borderColor);
                // Right (Extended height by 1)
                spriteBatch.DrawSnapped(pixel, new Rectangle((int)totalX + WIDTH - 1, (int)totalY, 1, HEIGHT + 1), borderColor);
            }

            // 3. Draw Text
            // 5x5 Font, All Caps
            string textToDraw = SpellName.ToUpper();

            Color textColor;
            if (isActivated)
            {
                textColor = Color.White;
            }
            else if (IsEquipped)
            {
                // Equipped spells are lighter (LightGray)
                textColor = _global.Palette_LightGray;
            }
            else
            {
                // Empty slots are darker (Gray)
                textColor = _global.Palette_Gray;
            }

            if (!IsEnabled) textColor = _global.Palette_DarkGray;

            // Apply flash tint to text color if active
            if (flashTint.HasValue)
            {
                textColor = Color.Lerp(textColor, flashTint.Value, flashTint.Value.A / 255f);
            }

            // Center text both horizontally and vertically
            Vector2 textSize = secondaryFont.MeasureString(textToDraw);
            Vector2 textPos = new Vector2(
                totalX + (WIDTH - textSize.X) / 2f,
                totalY + (HEIGHT - textSize.Y) / 2f
            );

            spriteBatch.DrawStringSnapped(secondaryFont, textToDraw, textPos, textColor);
        }
    }
}