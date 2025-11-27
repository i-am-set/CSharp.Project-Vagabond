#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class EquipButton : Button
    {
        public string TitleText { get; set; } = "";
        public string MainText { get; set; } = "";
        public string? HoverMainText { get; set; }
        public Texture2D? IconTexture { get; set; }
        public Texture2D? IconSilhouette { get; set; }
        public Rectangle? IconSourceRect { get; set; }
        public int Rarity { get; set; } = -1; // -1 means no rarity icon

        /// <summary>
        /// Optional custom color for the Title text. If null, falls back to Gray (idle) -> White (hover).
        /// </summary>
        public Color? CustomTitleTextColor { get; set; }

        // Layout Constants
        private const int WIDTH = 180;
        private const int HEIGHT = 16;

        private const int TITLE_WIDTH = 53;
        private const int ICON_WIDTH = 16;
        private const int MAIN_WIDTH = 109;
        private const int GAP = 1;

        // Calculated X Offsets relative to button X
        private const int TITLE_X = 0;
        private const int ICON_X = TITLE_X + TITLE_WIDTH + GAP;
        private const int MAIN_X = ICON_X + ICON_WIDTH + GAP + 5;

        public EquipButton(Rectangle bounds, string mainText, string? hoverMainText = null)
            : base(bounds, "") // Pass empty string to base, we handle text rendering manually
        {
            // Enforce dimensions
            Bounds = new Rectangle(bounds.X, bounds.Y, WIDTH, HEIGHT);
            MainText = mainText;
            HoverMainText = hoverMainText;
            EnableHoverSway = false; // Disable the vertical lift on hover
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            // 1. Calculate State
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            BitmapFont font = this.Font ?? defaultFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // 2. Calculate Animation Offsets
            float yOffset = 0f;
            if (EnableHoverSway)
            {
                yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);

            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;

            // 3. Draw Background
            Texture2D? bgTexture = null;

            // Use hover sprite for both pressed and hovered states
            if (_isPressed || isActivated)
            {
                bgTexture = spriteManager.InventoryEquipHoverSprite;
            }

            if (bgTexture != null)
            {
                spriteBatch.DrawSnapped(bgTexture, new Vector2(totalX, totalY), Color.White);
            }

            // 4. Draw Content

            // --- Title Text (Centered in 53x16) ---
            if (!string.IsNullOrEmpty(TitleText))
            {
                // Use defaultFont for Title
                Vector2 titleSize = defaultFont.MeasureString(TitleText);
                Vector2 titlePos = new Vector2(
                    totalX + TITLE_X + (TITLE_WIDTH - titleSize.X) / 2f,
                    totalY + (HEIGHT - titleSize.Y) / 2f
                );

                // Round to pixel
                titlePos = new Vector2(MathF.Round(titlePos.X), MathF.Round(titlePos.Y));

                Color titleColor;
                if (isActivated)
                {
                    titleColor = _global.Palette_BrightWhite;
                }
                else
                {
                    // Use custom color if set (e.g. for submenu striping), otherwise default to Gray
                    titleColor = CustomTitleTextColor ?? _global.Palette_Gray;
                }

                spriteBatch.DrawStringSnapped(defaultFont, TitleText, titlePos, titleColor);
            }

            // --- Icon (16x16) ---
            if (IconTexture != null)
            {
                Rectangle destRect = new Rectangle(
                    (int)(totalX + ICON_X),
                    (int)(totalY),
                    ICON_WIDTH,
                    HEIGHT
                );

                Rectangle src = IconSourceRect ?? IconTexture.Bounds;

                // Draw Silhouette Outline if available
                if (IconSilhouette != null)
                {
                    // Use global outline colors
                    Color mainOutlineColor = isActivated ? _global.ItemOutlineColor_Hover : _global.ItemOutlineColor_Idle;
                    Color cornerOutlineColor = isActivated ? _global.ItemOutlineColor_Hover_Corner : _global.ItemOutlineColor_Idle_Corner;

                    // 1. Draw Diagonals (Corners) FIRST (Behind)
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X - 1, destRect.Y - 1), src, cornerOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X + 1, destRect.Y - 1), src, cornerOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X - 1, destRect.Y + 1), src, cornerOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X + 1, destRect.Y + 1), src, cornerOutlineColor);

                    // 2. Draw Cardinals (Main) SECOND (On Top)
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X - 1, destRect.Y), src, mainOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X + 1, destRect.Y), src, mainOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X, destRect.Y - 1), src, mainOutlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X, destRect.Y + 1), src, mainOutlineColor);
                }

                spriteBatch.DrawSnapped(IconTexture, destRect, src, Color.White);

                // Draw Rarity Icon
                if (Rarity >= 0 && spriteManager.RarityIconsSpriteSheet != null)
                {
                    var rarityRect = spriteManager.GetRarityIconSourceRect(Rarity, gameTime);
                    // Position at top-right of the icon area.
                    // destRect is 16x16.
                    // Rarity icon is 8x8.
                    // We want the rarity icon's top-right to align with destRect's top-right.
                    // Rarity Pos = (destRect.Right - 8, destRect.Top).
                    Vector2 rarityPos = new Vector2(destRect.Right - 8, destRect.Top);
                    spriteBatch.DrawSnapped(spriteManager.RarityIconsSpriteSheet, rarityPos, rarityRect, Color.White);
                }
            }

            // --- Main Text (Left Aligned in 109x16) ---
            string textToDraw = (isActivated && !string.IsNullOrEmpty(HoverMainText)) ? HoverMainText! : MainText;

            if (!string.IsNullOrEmpty(textToDraw))
            {
                Vector2 mainSize = font.MeasureString(textToDraw);

                // Left aligned within the 109px area, but vertically centered
                Vector2 mainPos = new Vector2(
                    totalX + MAIN_X,
                    totalY + (HEIGHT - mainSize.Y) / 2f
                );

                // Round to pixel
                mainPos = new Vector2(MathF.Round(mainPos.X), MathF.Round(mainPos.Y));

                // Use CustomDefaultTextColor if set, otherwise BrightWhite
                Color defaultColor = CustomDefaultTextColor ?? _global.Palette_BrightWhite;

                // Change: Use Color.White when hovered instead of ButtonHoverColor (Red)
                Color mainColor = isActivated ? Color.White : defaultColor;

                if (!IsEnabled) mainColor = _global.ButtonDisableColor;

                spriteBatch.DrawStringSnapped(font, textToDraw, mainPos, mainColor);
            }

            // 5. Debug Overlay (F1)
            if (_global.ShowSplitMapGrid)
            {
                var debugRect = new Rectangle((int)totalX, (int)totalY, WIDTH, HEIGHT);
                spriteBatch.DrawSnapped(pixel, debugRect, Color.HotPink * 0.5f);
            }
        }
    }
}
