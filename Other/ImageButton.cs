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

        // Animation state for the squash effect
        private float _squashAnimationTimer = 0f;
        private const float SQUASH_ANIMATION_DURATION = 0.03f;

        private const float SHAKE_AMOUNT = 1f;
        private static readonly Random _random = new Random();

        public ImageButton(Rectangle bounds, Texture2D? spriteSheet = null, Rectangle? defaultSourceRect = null, Rectangle? hoverSourceRect = null, Rectangle? clickedSourceRect = null, Rectangle? disabledSourceRect = null, string? function = null, bool enableHoverSway = true, bool zoomHapticOnClick = true, bool clickOnPress = false, BitmapFont? font = null, Color? debugColor = null)
            : base(bounds, "", function, null, null, null, false, 0.0f, enableHoverSway, clickOnPress, font)
        {
            _spriteSheet = spriteSheet;
            _defaultSourceRect = defaultSourceRect;
            _hoverSourceRect = hoverSourceRect;
            _clickedSourceRect = clickedSourceRect;
            _disabledSourceRect = disabledSourceRect;
            HoverBorderColor = _global.ButtonHoverColor;
            DebugColor = debugColor;
        }

        public override void Update(MouseState currentMouseState)
        {
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

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_isPressed && !ClickOnPress)
            {
                _squashAnimationTimer = Math.Min(_squashAnimationTimer + deltaTime, SQUASH_ANIMATION_DURATION);
            }
            else
            {
                _squashAnimationTimer = Math.Max(_squashAnimationTimer - deltaTime, 0);
            }

            float swayOffsetX = 0f; // Sway has been removed

            Vector2 scale = Vector2.One;
            Vector2 shakeOffset = Vector2.Zero;
            if (_squashAnimationTimer > 0)
            {
                if (_spriteSheet != null)
                {
                    float progress = _squashAnimationTimer / SQUASH_ANIMATION_DURATION;
                    float targetScaleY = 1.5f / Bounds.Height;
                    scale.Y = MathHelper.Lerp(1.0f, targetScaleY, progress);
                }
                shakeOffset.X = MathF.Round((float)(_random.NextDouble() * 2 - 1) * SHAKE_AMOUNT);
            }

            var position = new Vector2(Bounds.Center.X + swayOffsetX, Bounds.Center.Y) + shakeOffset;

            if (_spriteSheet != null && sourceRectToDraw.HasValue)
            {
                var origin = sourceRectToDraw.Value.Size.ToVector2() / 2f;
                spriteBatch.DrawSnapped(_spriteSheet, position, sourceRectToDraw, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }
            else if (DebugColor.HasValue)
            {
                var debugRect = new Rectangle((int)position.X - Bounds.Width / 2, (int)position.Y - Bounds.Height / 2, Bounds.Width, Bounds.Height);
                spriteBatch.DrawSnapped(ServiceLocator.Get<Texture2D>(), debugRect, DebugColor.Value);
            }

            if (isActivated && !_hoverSourceRect.HasValue)
            {
                var rectWithOffset = new Rectangle(Bounds.X + (int)swayOffsetX, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawCornerBrackets(spriteBatch, ServiceLocator.Get<Texture2D>(), rectWithOffset, BorderThickness, HoverBorderColor);
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