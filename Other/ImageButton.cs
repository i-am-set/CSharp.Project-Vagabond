#nullable enable
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
        protected Texture2D? _spriteSheet;
        protected Rectangle? _defaultSourceRect;
        protected Rectangle? _hoverSourceRect;
        protected readonly Rectangle? _clickedSourceRect;
        protected readonly Rectangle? _disabledSourceRect;
        protected readonly Rectangle? _selectedSourceRect;
        private bool _isHeldDown;
        public bool IsSelected { get; set; }

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

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = false, bool zoomHapticOnClick = true, bool startVisible = true, BitmapFont? font = null, Color? debugColor = null, Rectangle? selectedSourceRect = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect ?? spriteSheet?.Bounds; // If no default is given, use the whole sheet.
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            _selectedSourceRect = selectedSourceRect;
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

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            if (_animState != AnimationState.Idle)
            {
                IsHovered = false;
                _isPressed = false;
                return;
            }

            base.Update(currentMouseState, worldTransform);

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

            // --- Update Base Animations (Hover Rotation) ---
            UpdateFeedbackAnimations(gameTime);

            // --- Hover Animation ---
            float hoverYOffset = 0f;
            if (EnableHoverSway)
            {
                hoverYOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);

                // FIX: If using screen coordinates, scale the offset to match virtual pixels
                if (UseScreenCoordinates)
                {
                    hoverYOffset *= ServiceLocator.Get<Core>().FinalScale;
                }
            }
            else
            {
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
            float scale = 1.0f;
            if (_animState == AnimationState.Appearing)
            {
                _appearTimer += dt;
                float progress = Math.Clamp(_appearTimer / APPEAR_DURATION, 0f, 1f);
                scale = Easing.EaseOutCubic(progress);
                if (progress >= 1.0f)
                {
                    _animState = AnimationState.Idle;
                }
            }

            if (scale < 0.01f) return;

            // --- Calculate Draw Position (Center) ---
            Vector2 drawPosition = new Vector2(
                Bounds.Center.X + totalHorizontalOffset,
                Bounds.Center.Y + (verticalOffset ?? 0f) + hoverYOffset
            );

            // --- Select Source Rectangle ---
            Rectangle? sourceRectToDraw = _defaultSourceRect;
            if (!IsEnabled && _disabledSourceRect.HasValue)
            {
                sourceRectToDraw = _disabledSourceRect;
            }
            else if (IsSelected && _selectedSourceRect.HasValue)
            {
                sourceRectToDraw = _selectedSourceRect;
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
            if (!IsEnabled && !_disabledSourceRect.HasValue)
            {
                drawColor = _global.ButtonDisableColor; // Use global disable color
            }

            // --- Draw ---
            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                // Use Vector2 position + Origin + Scale + Rotation for proper center-based drawing
                Vector2 origin = new Vector2(sourceRectToDraw.Value.Width / 2f, sourceRectToDraw.Value.Height / 2f);

                // Use base class rotation
                spriteBatch.DrawSnapped(_spriteSheet, drawPosition, sourceRectToDraw, drawColor, _currentHoverRotation, origin, scale, SpriteEffects.None, 0f);
            }
            else if (DebugColor.HasValue)
            {
                // Debug draw should still respect the logical bounds for hit testing visualization
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), Bounds, DebugColor.Value);
            }
        }
    }
}