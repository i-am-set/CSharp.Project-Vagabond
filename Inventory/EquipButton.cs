#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // --- ANIMATION TUNING ---
        private const float FLOAT_SPEED = 2.5f;
        private const float FLOAT_AMPLITUDE = 0.5f;
        private const float FLOAT_ROTATION_SPEED = 2.0f;
        private const float FLOAT_ROTATION_AMOUNT = 0.05f;

        public EquipButton(Rectangle bounds, string mainText, string? hoverMainText = null)
            : base(bounds, "") // Pass empty string to base, we handle text rendering manually
        {
            // Enforce dimensions
            Bounds = new Rectangle(bounds.X, bounds.Y, WIDTH, HEIGHT);
            MainText = mainText;
            HoverMainText = hoverMainText;
            EnableHoverSway = false; // Disable the vertical lift on hover
            EnableTextWave = true;   // Enable wave timer updates
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            // 1. Calculate State
            bool isActivated = IsEnabled && (IsHovered || forceHover);
            BitmapFont font = this.Font ?? defaultFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Wave Timer manually since we override base.Draw
            if (isActivated)
            {
                _waveTimer += dt;
            }
            else
            {
                _waveTimer = 0f;
            }

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
                // Expand by 1 pixel in all directions when hovered
                var bgRect = new Rectangle(
                    (int)totalX - 1,
                    (int)totalY - 1,
                    WIDTH + 2,
                    HEIGHT + 2
                );
                spriteBatch.DrawSnapped(bgTexture, bgRect, Color.White);
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
                    titleColor = _global.Palette_Sun;
                    // Use TextAnimator for wave effect when hovered
                    // Use TextEffectType.Wave for continuous looping
                    TextAnimator.DrawTextWithEffect(spriteBatch, defaultFont, TitleText, titlePos, titleColor, TextEffectType.Wave, _waveTimer);
                }
                else
                {
                    // Use custom color if set (e.g. for submenu striping), otherwise default to Gray
                    titleColor = CustomTitleTextColor ?? _global.Palette_Gray;
                    spriteBatch.DrawStringSnapped(defaultFont, TitleText, titlePos, titleColor);
                }
            }

            // --- Icon (16x16) ---
            if (IconTexture != null)
            {
                // Calculate Lift Offset (1px up when hovered)
                float spriteLiftY = isActivated ? -1f : 0f;

                // --- JUICY FLOAT ANIMATION ---
                // Calculate a smooth sine wave bob.
                // We use Bounds.Y as a phase offset so items in a list don't bob in perfect unison.
                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float phase = Bounds.Y * 0.05f;
                float floatOffset = MathF.Sin(time * FLOAT_SPEED + phase) * FLOAT_AMPLITUDE;
                float rotation = MathF.Sin(time * FLOAT_ROTATION_SPEED + phase) * FLOAT_ROTATION_AMOUNT;

                // Combine offsets
                float finalIconY = totalY + spriteLiftY + floatOffset;

                // Center of the icon area
                Vector2 iconCenter = new Vector2(
                    totalX + ICON_X + ICON_WIDTH / 2f,
                    finalIconY + HEIGHT / 2f
                );

                // Origin for rotation (Center of 16x16 sprite)
                Vector2 iconOrigin = new Vector2(8, 8);

                Rectangle src = IconSourceRect ?? IconTexture.Bounds;

                // Draw Silhouette Outline if available
                if (IconSilhouette != null)
                {
                    // Use global outline colors
                    Color mainOutlineColor = isActivated ? _global.ItemOutlineColor_Hover : _global.ItemOutlineColor_Idle;
                    Color cornerOutlineColor = isActivated ? _global.ItemOutlineColor_Hover_Corner : _global.ItemOutlineColor_Idle_Corner;

                    // 1. Draw Diagonals (Corners) FIRST (Behind)
                    // Use Vector2 position + Rotation overload to ensure outline rotates with the sprite
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(-1, -1), src, cornerOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(1, -1), src, cornerOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(-1, 1), src, cornerOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(1, 1), src, cornerOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);

                    // 2. Draw Cardinals (Main) SECOND (On Top)
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(-1, 0), src, mainOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(1, 0), src, mainOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(0, -1), src, mainOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(IconSilhouette, iconCenter + new Vector2(0, 1), src, mainOutlineColor, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
                }

                spriteBatch.DrawSnapped(IconTexture, iconCenter, src, Color.White, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
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

                // Use CustomDefaultTextColor if set, otherwise BlueWhite
                Color defaultColor = CustomDefaultTextColor ?? _global.Palette_Sun;

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
