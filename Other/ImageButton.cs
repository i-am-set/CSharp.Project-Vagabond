#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class ImageButton : Button
    {
        public Color HoverBorderColor { get; set; }
        public int BorderThickness { get; set; } = 1;
        public float CornerLengthRatio { get; set; } = 0.25f;
        public int MinCornerArmLength { get; set; } = 3;
        public int MaxCornerArmLength { get; set; } = 20;

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

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = true, bool zoomHapticOnClick = true, bool clickOnPress = false, bool startVisible = true, BitmapFont? font = null, Color? debugColor = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, clickOnPress, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect;
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            HoverBorderColor = _global.ButtonHoverColor;
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
            if (!IsEnabled)
            {
                _isHeldDown = false;
            }
            else
            {
                _isHeldDown = _isPressed;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            if (_animState == AnimationState.Hidden) return;

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
            var animatedBounds = new Rectangle(
                Bounds.X,
                Bounds.Center.Y - animatedHeight / 2, // Expand from the center
                Bounds.Width,
                animatedHeight
            );

            Rectangle? sourceRectToDraw = _defaultSourceRect;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

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

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                spriteBatch.DrawSnapped(_spriteSheet, animatedBounds, sourceRectToDraw, Color.White);
            }
            else if (DebugColor.HasValue)
            {
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), animatedBounds, DebugColor.Value);
            }

            if (isActivated && !_hoverSourceRect.HasValue)
            {
                DrawCornerBrackets(spriteBatch, ServiceLocator.Get<Texture2D>(), animatedBounds, BorderThickness, HoverBorderColor);
            }
        }

        private void DrawCornerBrackets(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            int shorterSide = Math.Min(rect.Width, rect.Height);
            int armLength = (int)(shorterSide * CornerLengthRatio);
            armLength = Math.Clamp(armLength, MinCornerArmLength, MaxCornerArmLength);

            if (armLength * 2 > rect.Width) armLength = rect.Width / 2;
            if (armLength * 2 > rect.Height) armLength = rect.Height / 2;

            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, armLength), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, armLength), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - armLength, thickness, armLength), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - armLength, thickness, armLength), color);
        }
    }
}
#nullable restore