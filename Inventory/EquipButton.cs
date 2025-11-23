#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

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

        /// <summary>
        /// If true, the TitleText is only drawn when the button is hovered.
        /// If false, the TitleText is always drawn (gray when idle, white when hovered).
        /// </summary>
        public bool ShowTitleOnHoverOnly { get; set; } = true;

        /// <summary>
        /// Optional custom color for the Title text. If null, falls back to CustomDefaultTextColor or default logic.
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

            if (_isPressed)
            {
                bgTexture = spriteManager.InventoryEquipSelectedSprite;
            }
            else if (isActivated)
            {
                bgTexture = spriteManager.InventoryEquipHoverSprite;
            }

            if (bgTexture != null)
            {
                spriteBatch.DrawSnapped(bgTexture, new Vector2(totalX, totalY), Color.White);
            }

            // 4. Draw Content

            // --- Title Text (Centered in 53x16) ---
            // Logic: Draw if Title exists AND (Button is hovered OR we are configured to always show title)
            if (!string.IsNullOrEmpty(TitleText) && (isActivated || !ShowTitleOnHoverOnly))
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
                if (ShowTitleOnHoverOnly)
                {
                    // Only visible on hover. Use CustomTitleTextColor if set, otherwise CustomDefaultTextColor, otherwise BrightWhite.
                    titleColor = CustomTitleTextColor ?? CustomDefaultTextColor ?? _global.Palette_BrightWhite;
                }
                else
                {
                    // Always visible, change color based on state
                    titleColor = isActivated ? _global.Palette_BrightWhite : _global.Palette_Gray;
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
                    Color outlineColor = _global.Palette_Black;
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X - 1, destRect.Y), src, outlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X + 1, destRect.Y), src, outlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X, destRect.Y - 1), src, outlineColor);
                    spriteBatch.DrawSnapped(IconSilhouette, new Vector2(destRect.X, destRect.Y + 1), src, outlineColor);
                }

                spriteBatch.DrawSnapped(IconTexture, destRect, src, Color.White);
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
                Color mainColor = isActivated ? _global.ButtonHoverColor : defaultColor;

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