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

        private readonly Texture2D? _defaultTexture;
        private readonly Texture2D? _hoverTexture;
        private readonly Texture2D? _clickedTexture;
        private readonly Texture2D? _disabledTexture;

        private bool _isHeldDown;
        private float _swayTimer = 0f;
        private bool _wasHoveredLastFrame = false;

        // Animation state for the squash effect
        private float _squashAnimationTimer = 0f;
        private const float SQUASH_ANIMATION_DURATION = 0.03f;

        private const float SWAY_SPEED = 3f;
        private const float SWAY_AMOUNT_X = 2f;

        public ImageButton(Rectangle bounds, Texture2D? defaultTexture = null, Texture2D? hoverTexture = null, Texture2D? clickedTexture = null, Texture2D? disabledTexture = null, bool enableHoverSway = true, bool zoomHapticOnClick = true, bool clickOnPress = false, BitmapFont? font = null)
            : base(bounds, "", enableHoverSway: enableHoverSway, clickOnPress: clickOnPress, font: font)
        {
            _defaultTexture = defaultTexture;
            _hoverTexture = hoverTexture;
            _clickedTexture = clickedTexture;
            _disabledTexture = disabledTexture;
            HoverBorderColor = _global.ButtonHoverColor;
        }

        public override void Update(MouseState currentMouseState)
        {
            // Let the base class handle hover, click, and previous state management.
            base.Update(currentMouseState);

            // Add the specific logic for ImageButton.
            if (!IsEnabled)
            {
                _isHeldDown = false;
            }
            else
            {
                // _isPressed is managed by the base class for click-on-release logic
                _isHeldDown = _isPressed;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            Texture2D? textureToDraw = _defaultTexture;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            if (!IsEnabled && _disabledTexture != null)
            {
                textureToDraw = _disabledTexture;
            }
            else if (_isHeldDown && _clickedTexture != null)
            {
                textureToDraw = _clickedTexture;
            }
            else if (isActivated && _hoverTexture != null)
            {
                textureToDraw = _hoverTexture;
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

            float swayOffsetX = 0f;
            if (isActivated && EnableHoverSway)
            {
                if (!_wasHoveredLastFrame)
                {
                    _swayTimer = 0f; // Reset timer on new hover.
                }
                _swayTimer += deltaTime;
                swayOffsetX = (float)Math.Sin(_swayTimer * SWAY_SPEED) * SWAY_AMOUNT_X;
            }
            else
            {
                _swayTimer = 0f; // Reset if not hovered.
            }
            _wasHoveredLastFrame = isActivated;

            Vector2 scale = Vector2.One;
            if (_squashAnimationTimer > 0 && textureToDraw != null)
            {
                float progress = _squashAnimationTimer / SQUASH_ANIMATION_DURATION;
                float targetScaleY = 1.0f / textureToDraw.Height; // Target a 1-pixel height
                scale.Y = MathHelper.Lerp(1.0f, targetScaleY, progress);
            }

            var position = new Vector2(Bounds.Center.X + swayOffsetX, Bounds.Center.Y);

            if (textureToDraw != null)
            {
                var origin = textureToDraw.Bounds.Center.ToVector2();
                spriteBatch.DrawSnapped(textureToDraw, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            if (isActivated && _hoverTexture == null)
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