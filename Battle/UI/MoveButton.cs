using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    public class MoveButton : Button
    {
        public MoveData Move { get; }
        private readonly BitmapFont _moveFont;
        private readonly Texture2D _backgroundTexture;

        // Future-proofing for icons
        public Texture2D IconTexture { get; set; }
        public Rectangle? IconSourceRect { get; set; }

        // Animation State
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f; // Duration of the appear animation

        public MoveButton(MoveData move, BitmapFont font, Texture2D backgroundTexture, bool startVisible = true)
            : base(Rectangle.Empty, move.MoveName.ToUpper(), function: move.MoveID)
        {
            Move = move;
            _moveFont = font;
            _backgroundTexture = backgroundTexture;
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

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            if (_animState == AnimationState.Hidden) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

            // --- Animation Scaling ---
            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);

                // Apply overshoot bounce easing to the Y-scale
                verticalScale = Easing.EaseOutBack(progress);

                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (verticalScale < 0.01f) return;

            // --- Calculate Animated Bounds ---
            int animatedHeight = (int)(Bounds.Height * verticalScale);
            var animatedBounds = new Rectangle(
                Bounds.X + (int)hopOffset,
                Bounds.Center.Y - animatedHeight / 2, // Expand from the center
                Bounds.Width,
                animatedHeight
            );

            // Draw background texture
            Color tintColor = Color.White;
            if (!IsEnabled) tintColor = _global.ButtonDisableColor * 0.5f;
            else if (_isPressed) tintColor = Color.Gray;
            else if (isActivated) tintColor = _global.ButtonHoverColor;
            spriteBatch.DrawSnapped(_backgroundTexture, animatedBounds, tintColor);


            // Only draw contents if the button is mostly visible to avoid squashed text/icons
            if (verticalScale > 0.8f)
            {
                // --- Draw Icon/Placeholder ---
                const int iconSize = 5;
                const int iconPadding = 4;
                var iconRect = new Rectangle(
                    animatedBounds.X + iconPadding,
                    animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                    iconSize,
                    iconSize
                );
                spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink);

                // --- Draw Text ---
                var textColor = isActivated ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
                if (!IsEnabled)
                {
                    textColor = _global.ButtonDisableColor;
                }

                var textPosition = new Vector2(
                    iconRect.Right + iconPadding,
                    animatedBounds.Y + (animatedBounds.Height - _moveFont.LineHeight) / 2
                );
                spriteBatch.DrawStringSnapped(_moveFont, this.Text, textPosition, textColor);
            }
        }
    }
}