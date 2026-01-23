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
using System.Linq;
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

            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime); // Updates _currentHoverRotation

            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y + yOffset;

            // Calculate Center for Rotation
            Vector2 centerPos = new Vector2(totalX + WIDTH / 2f, totalY + HEIGHT / 2f);

            // ROTATION HELPER
            Vector2 RotateOffset(Vector2 local)
            {
                float cos = MathF.Cos(_currentHoverRotation);
                float sin = MathF.Sin(_currentHoverRotation);
                return new Vector2(
                    local.X * cos - local.Y * sin,
                    local.X * sin + local.Y * cos
                );
            }

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
                // Source is full texture
                Rectangle source = new Rectangle(0, 0, bgTexture.Width, bgTexture.Height);
                Vector2 origin = new Vector2(bgTexture.Width / 2f, bgTexture.Height / 2f);

                // Need to scale if texture size != bounds size + expansion
                // Texture is 180x16. Bounds are 180x16. Expansion makes it 182x18.
                Vector2 scale = new Vector2((float)(WIDTH + 2) / bgTexture.Width, (float)(HEIGHT + 2) / bgTexture.Height);

                spriteBatch.DrawSnapped(bgTexture, centerPos, source, Color.White, _currentHoverRotation, origin, scale, SpriteEffects.None, 0f);
            }

            // 4. Draw Content

            // Offsets relative to Top-Left
            float relativeTitleX = TITLE_X + (TITLE_WIDTH / 2f); // Center of title area
            float relativeIconX = ICON_X + (ICON_WIDTH / 2f); // Center of icon area
            float relativeMainX = MAIN_X; // Left edge of main text area

            // --- Title Text (Centered in 53x16) ---
            if (!string.IsNullOrEmpty(TitleText))
            {
                // Use defaultFont for Title
                Vector2 titleSize = defaultFont.MeasureString(TitleText);

                // Calculate local offset from center
                float localX = relativeTitleX - (WIDTH / 2f);
                float localY = 0; // Centered Y

                // Adjust for text centering
                // DrawString draws from Top-Left. We want to rotate around text center.
                // Or rotate the anchor position?

                // Let's use the TextAnimator logic: Pass Top-Left relative to anchor, and rotation.
                // TextAnimator: drawPos - origin.

                // Position of Title Center
                Vector2 titleCenterPos = centerPos + RotateOffset(new Vector2(localX, localY));
                Vector2 titleOrigin = titleSize / 2f;

                Color titleColor;
                if (isActivated)
                {
                    titleColor = _global.Palette_Sun;
                    TextAnimator.DrawTextWithEffect(spriteBatch, defaultFont, TitleText, titleCenterPos - titleOrigin, titleColor, TextEffectType.Wave, _waveTimer, Vector2.One, null, _currentHoverRotation);
                }
                else
                {
                    // Use custom color if set (e.g. for submenu striping), otherwise default to Gray
                    titleColor = CustomTitleTextColor ?? _global.Palette_DarkShadow;
                    spriteBatch.DrawStringSnapped(defaultFont, TitleText, titleCenterPos, titleColor, _currentHoverRotation, titleOrigin, 1.0f, SpriteEffects.None, 0f);
                }
            }

            // --- Icon (16x16) ---
            if (IconTexture != null)
            {
                // Calculate Lift Offset (1px up when hovered)
                float spriteLiftY = isActivated ? -1f : 0f;

                // --- JUICY FLOAT ANIMATION ---
                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float phase = Bounds.Y * 0.05f;
                float floatOffset = MathF.Sin(time * FLOAT_SPEED + phase) * FLOAT_AMPLITUDE;
                float rotation = MathF.Sin(time * FLOAT_ROTATION_SPEED + phase) * FLOAT_ROTATION_AMOUNT;

                // Apply Base Hover Rotation
                rotation += _currentHoverRotation;

                // Local Offset from Center
                float localX = relativeIconX - (WIDTH / 2f);
                float localY = spriteLiftY + floatOffset;

                Vector2 iconPos = centerPos + RotateOffset(new Vector2(localX, localY));
                Vector2 iconOrigin = new Vector2(8, 8);

                Rectangle src = IconSourceRect ?? IconTexture.Bounds;

                spriteBatch.DrawSnapped(IconTexture, iconPos, src, Color.White, rotation, iconOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            // --- Main Text (Left Aligned in 109x16) ---
            string textToDraw = (isActivated && !string.IsNullOrEmpty(HoverMainText)) ? HoverMainText! : MainText;

            if (!string.IsNullOrEmpty(textToDraw))
            {
                Vector2 mainSize = font.MeasureString(textToDraw);

                // Local Offset from Center (Left Aligned)
                float localX = relativeMainX - (WIDTH / 2f) + (mainSize.X / 2f); // Center of text block
                float localY = 0;

                Vector2 mainPos = centerPos + RotateOffset(new Vector2(localX, localY));
                Vector2 mainOrigin = mainSize / 2f;

                // Use CustomDefaultTextColor if set, otherwise BlueWhite
                Color defaultColor = CustomDefaultTextColor ?? _global.Palette_Sun;
                Color mainColor = isActivated ? Color.White : defaultColor;
                if (!IsEnabled) mainColor = _global.ButtonDisableColor;

                spriteBatch.DrawStringSnapped(font, textToDraw, mainPos, mainColor, _currentHoverRotation, mainOrigin, 1.0f, SpriteEffects.None, 0f);
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
