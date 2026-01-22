#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        /// <summary>
        /// If true, the button will not respond to mouse input (hover/click) while IsSelected is true.
        /// Useful for radio-style buttons where clicking the active one should do nothing.
        /// </summary>
        public bool DisableInputWhenSelected { get; set; } = false;

        public ToggleButton(Rectangle bounds, string text, string? function = null, Color? customDefaultTextColor = null, Color? customHoverTextColor = null, Color? customDisabledTextColor = null, Color? customToggledTextColor = null, bool zoomHapticOnClick = true, BitmapFont? font = null)
            : base(bounds, text, function, customDefaultTextColor, customHoverTextColor, customDisabledTextColor, font: font)
        {
            CustomToggledTextColor = customToggledTextColor;
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            if (IsSelected && DisableInputWhenSelected)
            {
                // When selected and disabled, the button should be inert:
                // 1. No Hover state (IsHovered = false)
                // 2. No Click interaction
                // 3. No Cursor change (handled by IsHovered = false)

                IsHovered = false;
                _isPressed = false;

                // Important: Keep previous mouse state synced so we don't get weird edge cases 
                // when deselecting later.
                _previousMouseState = currentMouseState;
                return;
            }

            base.Update(currentMouseState, worldTransform);
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
                Color selectedColor = CustomToggledTextColor ?? _global.Palette_DarkSun;
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
                // Adjusted: Moved up 1 pixel (was +1, now +0) and right 2 pixels (was +5)
                float lineY = centerPos.Y;
                float padding = 2f;
                float startX = centerPos.X - (textSize.X / 2f) - padding + 2;
                float endX = centerPos.X + (textSize.X / 2f) + padding + 2;

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