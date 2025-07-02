using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.UI
{
    public class ImageButton : Button
    {
        public Color HoverBorderColor { get; set; } = Global.Instance.ButtonHoverColor;
        public int BorderThickness { get; set; } = 1;

        // --- Corner Bracket Customization ---
        /// <summary>
        /// The length of the corner arms as a percentage of the button's shortest side.
        /// </summary>
        public float CornerLengthRatio { get; set; } = 0.25f;
        /// <summary>
        /// The minimum pixel length for a corner arm.
        /// </summary>
        public int MinCornerArmLength { get; set; } = 3;
        /// <summary>
        /// The maximum pixel length for a corner arm.
        /// </summary>
        public int MaxCornerArmLength { get; set; } = 20;


        private readonly Texture2D _defaultTexture;
        private readonly Texture2D _hoverTexture;
        private readonly Texture2D _clickedTexture;
        private readonly Texture2D _disabledTexture;

        private bool _isHeldDown;

        /// <summary>
        /// Creates a button that can be visually represented by textures or a simple border on hover.
        /// </summary>
        /// <param name="bounds">The clickable area of the button.</param>
        /// <param name="defaultTexture">The texture to display by default.</param>
        /// <param name="hoverTexture">The texture to display when the mouse is over the button.</param>
        /// <param name="clickedTexture">The texture to display when the button is being clicked.</param>
        /// <param name="disabledTexture">The texture to display when the button is disabled.</param>
        public ImageButton(Rectangle bounds, Texture2D defaultTexture = null, Texture2D hoverTexture = null, Texture2D clickedTexture = null, Texture2D disabledTexture = null)
            : base(bounds, "")
        {
            _defaultTexture = defaultTexture;
            _hoverTexture = hoverTexture;
            _clickedTexture = clickedTexture;
            _disabledTexture = disabledTexture;
        }

        /// <summary>
        /// Overrides the base Update to handle the held-down state for visual feedback.
        /// </summary>
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

            // Handle the click event (fires on mouse release)
            if (IsHovered && currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TriggerClick();
            }

            // Track if the button is currently being held down for visual state
            _isHeldDown = IsHovered && currentMouseState.LeftButton == ButtonState.Pressed;

            _previousMouseState = currentMouseState;
        }

        /// <summary>
        /// Overrides the default Draw method to render textures or a hover border.
        /// </summary>
        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, bool forceHover = false)
        {
            Texture2D textureToDraw = null;
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Determine which texture to draw based on the button's state
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
            else if (_defaultTexture != null)
            {
                textureToDraw = _defaultTexture;
            }

            if (textureToDraw != null)
            {
                spriteBatch.Draw(textureToDraw, Bounds, Color.White);
            }
            // Fallback: If no appropriate texture is found, draw the corner brackets on hover.
            else if (isActivated)
            {
                DrawCornerBrackets(spriteBatch, Core.Pixel, Bounds, BorderThickness, HoverBorderColor);
            }
        }

        /// <summary>
        /// Draws four corner brackets inside the given rectangle.
        /// </summary>
        private void DrawCornerBrackets(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            // Calculate the length of the corner "arms" based on the shortest side of the rectangle.
            int shorterSide = Math.Min(rect.Width, rect.Height);
            int armLength = (int)(shorterSide * CornerLengthRatio);
            armLength = Math.Clamp(armLength, MinCornerArmLength, MaxCornerArmLength);

            // Ensure the arms don't overlap on very small buttons
            if (armLength * 2 > rect.Width) armLength = rect.Width / 2;
            if (armLength * 2 > rect.Height) armLength = rect.Height / 2;

            // Top-Left Corner
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, armLength), color);

            // Top-Right Corner
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Top, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, armLength), color);

            // Bottom-Left Corner
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - armLength, thickness, armLength), color);

            // Bottom-Right Corner
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - armLength, rect.Bottom - thickness, armLength, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - armLength, thickness, armLength), color);
        }
    }
}