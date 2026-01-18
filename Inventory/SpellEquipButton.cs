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
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class SpellEquipButton : Button
    {
        public string SpellName { get; set; } = "EMPTY";
        public bool HasSpell { get; set; } = false;
        // Layout Constants
        private const int WIDTH = 64;
        private const int HEIGHT = 8;

        public SpellEquipButton(Rectangle bounds) : base(bounds, "")
        {
            // Enforce width and height
            Bounds = new Rectangle(bounds.X, bounds.Y, WIDTH, HEIGHT);
            EnableHoverSway = false;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // 1. Calculate Animation Offsets
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime); // Updates _currentHoverRotation
            float totalX = Bounds.X + (horizontalOffset ?? 0f) + shakeOffset.X;
            float totalY = Bounds.Y + (verticalOffset ?? 0f) + shakeOffset.Y;

            // Center of Button
            Vector2 centerPos = new Vector2(totalX + WIDTH / 2f, totalY + HEIGHT / 2f);

            // 2. Determine Frame
            int frameIndex = 0; // Empty
            if (isActivated)
            {
                frameIndex = 2; // Hover
            }
            else if (HasSpell)
            {
                frameIndex = 1; // Filled
            }

            var sourceRect = spriteManager.InventorySpellSlotButtonSourceRects[frameIndex];
            var texture = spriteManager.InventorySpellSlotButtonSpriteSheet;

            // 3. Draw Sprite
            if (texture != null)
            {
                Vector2 origin = new Vector2(WIDTH / 2f, HEIGHT / 2f);
                spriteBatch.DrawSnapped(texture, centerPos, sourceRect, Color.White, _currentHoverRotation, origin, 1.0f, SpriteEffects.None, 0f);
            }

            // 4. Draw Text (Only if filled or hovered)
            if (HasSpell)
            {
                string textToDraw = SpellName.ToUpper();
                Color textColor = _global.Palette_Sun;

                if (!IsEnabled) textColor = _global.Palette_DarkShadow;
                else if (isActivated) textColor = _global.ButtonHoverColor;

                // Apply flash tint to text color if active
                if (flashTint.HasValue)
                {
                    textColor = Color.Lerp(textColor, flashTint.Value, flashTint.Value.A / 255f);
                }

                Vector2 textSize = tertiaryFont.MeasureString(textToDraw);
                Vector2 textOrigin = textSize / 2f;

                if (isActivated)
                {
                    // No outline when hovered
                    spriteBatch.DrawStringSnapped(tertiaryFont, textToDraw, centerPos, textColor, _currentHoverRotation, textOrigin, 1.0f, SpriteEffects.None, 0f);
                }
                else
                {
                    // Outline when normal
                    spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, textToDraw, centerPos, textColor, _global.Palette_DarkShadow, _currentHoverRotation, textOrigin, 1.0f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}