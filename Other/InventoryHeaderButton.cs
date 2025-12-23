#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;

namespace ProjectVagabond.UI
{
    public class InventoryHeaderButton : ImageButton
    {
        public int MenuIndex { get; }
        public string ButtonName { get; }

        public InventoryHeaderButton(Rectangle bounds, Texture2D spriteSheet, Rectangle defaultSourceRect, Rectangle hoverSourceRect, Rectangle selectedSourceRect, int menuIndex, string name)
            : base(bounds, spriteSheet, defaultSourceRect, hoverSourceRect, selectedSourceRect: selectedSourceRect, function: name)
        {
            MenuIndex = menuIndex;
            ButtonName = name;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_spriteSheet == null) return;

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Select the correct source rectangle based on the button's state
            Rectangle? sourceRectToDraw = _defaultSourceRect;
            Color drawColor = tintColorOverride ?? Color.White;

            if (!IsEnabled)
            {
                // If disabled, use the default (unselected) rect
                sourceRectToDraw = _defaultSourceRect;
                drawColor = Color.White * 0.2f;
            }
            else if (IsSelected && _selectedSourceRect.HasValue)
            {
                sourceRectToDraw = _selectedSourceRect;
            }
            else if (_isPressed && _clickedSourceRect.HasValue)
            {
                sourceRectToDraw = _clickedSourceRect;
            }
            else if (isActivated && _hoverSourceRect.HasValue)
            {
                sourceRectToDraw = _hoverSourceRect;
            }

            if (!sourceRectToDraw.HasValue) return;

            // Calculate animation offsets from the base class logic
            var (shakeOffset, flashTint) = UpdateFeedbackAnimations(gameTime);
            // We still need to update the animator to keep its state correct, but we won't use the returned yOffset.
            _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                drawColor = Color.Lerp(drawColor, flashTint.Value, flashAmount);
            }

            // The 'yOffset' from the hover animator is no longer added to the Y position.
            var spriteSize = sourceRectToDraw.Value.Size;
            var destinationRect = new Rectangle(
                (int)(Bounds.Center.X - spriteSize.X / 2f + shakeOffset.X + (horizontalOffset ?? 0f)),
                (int)(Bounds.Center.Y - spriteSize.Y / 2f + shakeOffset.Y + (verticalOffset ?? 0f)),
                spriteSize.X,
                spriteSize.Y
            );

            // Draw the sprite using the correctly sized and positioned destination rectangle
            spriteBatch.DrawSnapped(_spriteSheet, destinationRect, sourceRectToDraw, drawColor);
        }
    }
}