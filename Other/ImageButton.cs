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

        // Shake animation state
        private float _shakeTimer = 0f;
        private const float SHAKE_DURATION = 0.3f;
        private const float SHAKE_MAGNITUDE = 4f;
        private const float SHAKE_FREQUENCY = 30f;

        // Unhover Fade Animation
        private float _unhoverTimer = 0f;
        private float _currentAlpha = 1.0f;
        private const float FADE_OUT_DELAY = 0.5f; // Delay before fading out
        private const float FADE_DURATION = 0.3f; // Speed of the fade
        private const float FADE_OUT_ALPHA = 0.2f; // Target alpha when faded out (20%)
        private const float FADE_IN_ALPHA = 1.0f; // Target alpha when hovered/active (100%)

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
            _unhoverTimer = 0f;
            _currentAlpha = 1.0f;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false, float? horizontalOffset = null, float? verticalOffset = null, Color? tintColorOverride = null)
        {
            if (_animState == AnimationState.Hidden) return;

            bool isActivated = IsEnabled && (IsHovered || forceHover);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Alpha Animation ---
            if (isActivated)
            {
                _unhoverTimer = 0f;
                _currentAlpha = FADE_IN_ALPHA; // Snap to full opacity
            }
            else
            {
                _unhoverTimer += dt;
                if (_unhoverTimer > FADE_OUT_DELAY)
                {
                    if (_currentAlpha > FADE_OUT_ALPHA)
                    {
                        _currentAlpha = Math.Max(FADE_OUT_ALPHA, _currentAlpha - dt / FADE_DURATION);
                    }
                }
            }

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

            var animatedBounds = new Rectangle(
                Bounds.X + (int)MathF.Round(totalHorizontalOffset),
                Bounds.Center.Y - animatedHeight / 2 + (int)(verticalOffset ?? 0f) + (int)hoverYOffset,
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
            Color baseColor;
            if (tintColorOverride.HasValue)
            {
                baseColor = tintColorOverride.Value;
            }
            else
            {
                if (isActivated)
                {
                    baseColor = _global.ButtonHoverColor;
                }
                else
                {
                    baseColor = Color.White;
                }
            }

            Color drawColor = baseColor * _currentAlpha;

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                spriteBatch.DrawSnapped(_spriteSheet, animatedBounds, sourceRectToDraw, drawColor);
            }
            else if (DebugColor.HasValue)
            {
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), animatedBounds, DebugColor.Value * _currentAlpha);
            }
        }
    }
}