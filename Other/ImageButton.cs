#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

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

        private float _shakeTimer = 0f;
        private const float SHAKE_DURATION = 0.3f;
        private const float SHAKE_MAGNITUDE = 4f;
        private const float SHAKE_FREQUENCY = 30f;

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = false, bool zoomHapticOnClick = true, bool startVisible = true, BitmapFont? font = null, Color? debugColor = null, Rectangle? selectedSourceRect = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect ?? spriteSheet?.Bounds;
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            _selectedSourceRect = selectedSourceRect;
            DebugColor = debugColor;

            if (!startVisible)
            {
                SetHiddenForEntrance();
            }
        }

        public void SetSprites(Texture2D spriteSheet, Rectangle defaultRect, Rectangle hoverRect)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultRect;
            _hoverSourceRect = hoverRect;
        }

        public void TriggerAppearAnimation()
        {
            PlayEntrance(0f);
        }

        public new void TriggerShake()
        {
            _shakeTimer = SHAKE_DURATION;
        }

        public void HideForAnimation()
        {
            SetHiddenForEntrance();
        }

        public override void Update(MouseState currentMouseState, Matrix? worldTransform = null)
        {
            if (Plink.IsActive && Plink.Scale < 0.9f)
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
            bool isActivated = IsEnabled && (IsHovered || IsSelected || forceHover);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            var (feedbackShake, flashTint) = UpdateFeedbackAnimations(gameTime);
            float scale = _currentScale;

            if (scale < 0.01f) return;

            float hoverYOffset = 0f;
            if (EnableHoverSway)
            {
                hoverYOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated, HoverLiftOffset, HoverLiftDuration);

                if (UseScreenCoordinates)
                {
                    hoverYOffset *= ServiceLocator.Get<Core>().FinalScale;
                }
            }
            else
            {
                _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated, HoverLiftOffset, HoverLiftDuration);
            }

            float shakeOffset = feedbackShake.X;
            if (_shakeTimer > 0)
            {
                _shakeTimer -= dt;
                float progress = 1f - (_shakeTimer / SHAKE_DURATION);
                float magnitude = SHAKE_MAGNITUDE * (1f - Easing.EaseOutQuad(progress));
                shakeOffset += MathF.Sin(_shakeTimer * SHAKE_FREQUENCY) * magnitude;
            }

            float totalHorizontalOffset = (horizontalOffset ?? 0f) + shakeOffset;

            Vector2 drawPosition = new Vector2(
                Bounds.Center.X + totalHorizontalOffset,
                Bounds.Center.Y + (verticalOffset ?? 0f) + hoverYOffset + feedbackShake.Y
            );

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

            Color drawColor = tintColorOverride ?? Color.White;
            if (!IsEnabled && !_disabledSourceRect.HasValue)
            {
                drawColor = _global.ButtonDisableColor;
            }

            if (flashTint.HasValue)
            {
                float flashAmount = flashTint.Value.A / 255f;
                drawColor = Color.Lerp(drawColor, flashTint.Value, flashAmount);
            }

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                Vector2 origin = new Vector2(sourceRectToDraw.Value.Width / 2f, sourceRectToDraw.Value.Height / 2f);
                spriteBatch.DrawSnapped(_spriteSheet, drawPosition, sourceRectToDraw, drawColor, _currentHoverRotation, origin, scale, SpriteEffects.None, 0f);
            }
            else if (DebugColor.HasValue)
            {
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), Bounds, DebugColor.Value);
            }
        }
    }
}
