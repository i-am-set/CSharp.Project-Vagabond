﻿#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class ImageButton : Button
    {
        private Texture2D? _spriteSheet;
        private Rectangle? _defaultSourceRect;
        private Rectangle? _hoverSourceRect;
        private readonly Rectangle? _clickedSourceRect;
        private readonly Rectangle? _disabledSourceRect;
        private bool _isHeldDown;

        // Animation state
        private enum AnimationState { Hidden, Idle, Appearing }
        private AnimationState _animState = AnimationState.Idle;
        private float _appearTimer = 0f;
        private const float APPEAR_DURATION = 0.25f;

        // Shake animation state
        private float _shakeTimer = 0f;
        private const float SHAKE_DURATION = 0.3f;
        private const float SHAKE_MAGNITUDE = 4f;
        private const float SHAKE_FREQUENCY = 30f;

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = false, bool zoomHapticOnClick = true, bool startVisible = true, BitmapFont? font = null, Color? debugColor = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect ?? spriteSheet?.Bounds; // If no default is given, use the whole sheet.
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            DebugColor = debugColor;
            _animState = startVisible ? AnimationState.Idle : AnimationState.Hidden;
        }

        public void SetSprites(Texture2D spriteSheet, Rectangle defaultRect, Rectangle hoverRect)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultRect;
            _hoverSourceRect = hoverRect;
        }

        public void TriggerAppearAnimation()
        {
            if (_animState == AnimationState.Hidden)
            {
                _animState = AnimationState.Appearing;
                _appearTimer = 0f;
            }
        }

        public void TriggerShake()
        {
            _shakeTimer = SHAKE_DURATION;
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

        public override void ResetAnimationState()
        {
            base.ResetAnimationState();
            _shakeTimer = 0f;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_animState == AnimationState.Hidden) return;

            bool isActivated = IsEnabled && (IsHovered || forceHover);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Hover Animation ---
            float hoverYOffset = 0f;
            if (EnableHoverSway)
            {
                hoverYOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }
            else
            {
                // Still update the animator to reset its state when not hovered, but don't use the offset.
                _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            }

            // --- Shake Animation ---
            float shakeOffset = 0f;
            if (_shakeTimer > 0)
            {
                _shakeTimer -= dt;
                float progress = 1f - (_shakeTimer / SHAKE_DURATION);
                float magnitude = SHAKE_MAGNITUDE * (1f - Easing.EaseOutQuad(progress));
                shakeOffset = MathF.Sin(_shakeTimer * SHAKE_FREQUENCY) * magnitude;
            }

            float totalHorizontalOffset = (horizontalOffset ?? 0f) + shakeOffset;


            // --- Animation Scaling ---
            float verticalScale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += dt;
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

            if (UseScreenCoordinates)
            {
                totalHorizontalOffset *= ServiceLocator.Get<Core>().FinalScale;
            }

            float tactileHoverOffset = isActivated ? -1f : 0f;
            var animatedBounds = new Rectangle(
                Bounds.X + (int)MathF.Round(totalHorizontalOffset),
                Bounds.Center.Y - animatedHeight / 2 + (int)(verticalOffset ?? 0f) + (int)hoverYOffset + (int)tactileHoverOffset,
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

            // --- Color Logic ---
            Color drawColor = tintColorOverride ?? Color.White;

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