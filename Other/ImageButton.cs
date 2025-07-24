using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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

        private readonly Texture2D _defaultTexture;
        private readonly Texture2D _hoverTexture;
        private readonly Texture2D _clickedTexture;
        private readonly Texture2D _disabledTexture;

        private bool _isHeldDown;
        private float _swayTimer = 0f;
        private bool _wasHoveredLastFrame = false;

        private const float SWAY_SPEED = 4f;
        private const float SWAY_AMOUNT = 2f;

        public ImageButton(Rectangle bounds, Texture2D defaultTexture = null, Texture2D hoverTexture = null, Texture2D clickedTexture = null, Texture2D disabledTexture = null, bool enableHoverSway = true)
            : base(bounds, "", enableHoverSway: enableHoverSway)
        {
            _defaultTexture = defaultTexture;
            _hoverTexture = hoverTexture;
            _clickedTexture = clickedTexture;
            _disabledTexture = disabledTexture;
            HoverBorderColor = _global.ButtonHoverColor;
        }

        public override void Update(MouseState currentMouseState)
        {
            if (!IsEnabled)
            {
                IsHovered = false;
                _isHeldDown = false;
                return;
            }

            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
            IsHovered = Bounds.Contains(virtualMousePos);

            if (IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TriggerClick();
            }

            _isHeldDown = IsHovered && currentMouseState.LeftButton == ButtonState.Pressed;
            _previousMouseState = currentMouseState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, bool forceHover = false)
        {
            Texture2D textureToDraw = _defaultTexture;
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

            float swayOffset = 0f;
            if (isActivated && EnableHoverSway)
            {
                if (!_wasHoveredLastFrame)
                {
                    _swayTimer = 0f; // Reset timer on new hover.
                }
                _swayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                swayOffset = (float)Math.Sin(_swayTimer * SWAY_SPEED) * SWAY_AMOUNT;
            }
            else
            {
                _swayTimer = 0f; // Reset if not hovered.
            }
            _wasHoveredLastFrame = isActivated;

            var swayedBounds = new Rectangle(Bounds.X + (int)swayOffset, Bounds.Y, Bounds.Width, Bounds.Height);

            if (textureToDraw != null)
            {
                spriteBatch.Draw(textureToDraw, swayedBounds, Color.White);
            }

            if (isActivated && _hoverTexture == null)
            {
                DrawCornerBrackets(spriteBatch, ServiceLocator.Get<Texture2D>(), swayedBounds, BorderThickness, HoverBorderColor);
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