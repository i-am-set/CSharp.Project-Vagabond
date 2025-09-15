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
    public class ImageButton : Button
    {
        private readonly Texture2D? _spriteSheet;
        private readonly Rectangle? _defaultSourceRect;
        private readonly Rectangle? _hoverSourceRect;
        private readonly Rectangle? _clickedSourceRect;
        private readonly Rectangle? _disabledSourceRect;

        private bool _isHeldDown;

        // Animation state
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f;

        // Sway animation state
        private float _swayTimer = 0f;
        private bool _wasHoveredLastFrame = false;
        private const float SWAY_SPEED = 2.5f;
        private const float SWAY_AMPLITUDE = 1.0f;

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = true, bool zoomHapticOnClick = true, bool clickOnPress = false, bool startVisible = true, BitmapFont? font = null, Color? debugColor = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, clickOnPress, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect ?? spriteSheet?.Bounds; // If no default is given, use the whole sheet.
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            DebugColor = debugColor;
            _animState = startVisible ? AnimationState.Idle : AnimationState.Hidden;
        }

        public void TriggerAppearAnimation()
        {
            if (_animState == AnimationState.Hidden)
            {
                _animState = AnimationState.Appearing;
                _appearTimer = 0f;
            }
        }

        public void HideForAnimation()
        {
            _animState = AnimationState.Hidden;
        }

        public override void Update(MouseState currentMouseState)
        {
            if (_animState != AnimationState.Idle)
            {
                IsHovered = false;
                _isPressed = false;
                return;
            }

            base.Update(currentMouseState);

            // Reset sway timer when hover begins
            if (IsHovered && !_wasHoveredLastFrame)
            {
                _swayTimer = 0f;
            }
            _wasHoveredLastFrame = IsHovered;

            if (!IsEnabled)
            {
                _isHeldDown = false;
            }
            else
            {
                _isHeldDown = _isPressed;
            }
        }

        public override void ResetAnimationState()
        {
            base.ResetAnimationState();
            _swayTimer = 0f;
            _wasHoveredLastFrame = false;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            if (_animState == AnimationState.Hidden) return;

            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // --- Sway Animation ---
            float swayOffset = 0f;
            if (isActivated)
            {
                _swayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                // Round the result of the sine wave to snap the sway to the virtual pixel grid.
                swayOffset = MathF.Round(MathF.Sin(_swayTimer * SWAY_SPEED) * SWAY_AMPLITUDE);
            }
            float totalHorizontalOffset = swayOffset;


            // --- Animation Scaling ---
            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);
                verticalScale = Easing.EaseOutCubic(progress);
                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (verticalScale < 0.01f) return;

            // --- Calculate Animated Bounds ---
            int animatedHeight = (int)(Bounds.Height * verticalScale);

            // If the button uses screen coordinates, we must scale the virtual hop offset
            // to match the screen's pixel grid.
            if (UseScreenCoordinates)
            {
                totalHorizontalOffset *= ServiceLocator.Get<Core>().FinalScale;
            }

            var animatedBounds = new Rectangle(
                Bounds.X + (int)MathF.Round(totalHorizontalOffset), // Apply the potentially scaled offset
                Bounds.Center.Y - animatedHeight / 2, // Expand from the center
                Bounds.Width,
                animatedHeight
            );

            Rectangle? sourceRectToDraw = _defaultSourceRect;

            if (!IsEnabled && _disabledSourceRect.HasValue)
            {
                sourceRectToDraw = _disabledSourceRect;
            }
            else if (_isHeldDown && _clickedSourceRect.HasValue)
            {
                sourceRectToDraw = _clickedSourceRect;
            }
            else if (isActivated && _hoverSourceRect.HasValue)
            {
                sourceRectToDraw = _hoverSourceRect;
            }

            // Always use Color.White to render the sprite without any tint or opacity modification.
            // The visual state is handled entirely by swapping the source rectangle.
            Color drawColor = Color.White;

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                spriteBatch.DrawSnapped(_spriteSheet, animatedBounds, sourceRectToDraw, drawColor);
            }
            else if (DebugColor.HasValue)
            {
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), animatedBounds, DebugColor.Value);
            }
        }
    }
}
#nullable restore