#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class ToggleButton : Button
    {
        public bool IsSelected { get; set; }
        public Color? CustomToggledTextColor { get; set; }

        public ToggleButton(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, Color? customToggledTextColor = null, bool zoomHapticOnClick = true, BitmapFont? font = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, font: font)
        {
            CustomToggledTextColor = customToggledTextColor;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            // Store original state to restore after drawing
            var originalDefaultColor = this.CustomDefaultTextColor;
            var originalHoverColor = this.CustomHoverTextColor;
            var originalSwayState = this.EnableHoverSway;

            // --- Color & Logic Overrides ---
            if (IsSelected)
            {
                // If selected, force BOTH default and hover colors to the toggled color (Yellow).
                // This prevents the base Button.Draw from switching to Red when hovered.
                Color selectedColor = CustomToggledTextColor ?? _global.Palette_Yellow;
                this.CustomDefaultTextColor = selectedColor;
                this.CustomHoverTextColor = selectedColor;

                // Disable the base class's hover sway so it doesn't lift when hovered while selected.
                this.EnableHoverSway = false;
            }

            // --- Selected Animation Logic (Bob) ---
            float yAnimOffset = 0f;
            if (IsSelected && IsEnabled)
            {
                // Move 1 pixel UP and back.
                float speed = 5f;
                float val = MathF.Round((MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * speed) + 1f) * 0.5f);
                yAnimOffset = -val;
            }

            // Combine incoming vertical offset with our custom animation offset
            float finalVerticalOffset = (verticalOffset ?? 0f) + yAnimOffset;

            // Call base draw with modified properties and offsets
            base.Draw(spriteBatch, defaultFont, gameTime, transform, forceHover, horizontalOffset, finalVerticalOffset, tintColorOverride);

            // --- Strikethrough Logic (If Disabled) ---
            if (!IsEnabled)
            {
                BitmapFont font = this.Font ?? defaultFont;
                Vector2 textSize = font.MeasureString(Text);

                // Calculate center position based on alignment logic in base Button
                // Assuming center alignment for ToggleButtons
                float totalXOffset = (horizontalOffset ?? 0f);
                float totalYOffset = finalVerticalOffset;

                Vector2 centerPos = new Vector2(Bounds.Center.X + totalXOffset, Bounds.Center.Y + totalYOffset);

                // Calculate line start/end
                float lineY = centerPos.Y; // Center of text vertically
                float startX = centerPos.X - (textSize.X / 2f);
                float endX = centerPos.X + (textSize.X / 2f);

                // Draw line
                Color lineColor = CustomDisabledTextColor ?? _global.ButtonDisableColor;
                spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), lineColor);
            }

            // --- Restore Original State ---
            this.CustomDefaultTextColor = originalDefaultColor;
            this.CustomHoverTextColor = originalHoverColor;
            this.EnableHoverSway = originalSwayState;
        }
    }
}