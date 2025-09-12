using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;

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

        public MoveButton(MoveData move, BitmapFont font, Texture2D backgroundTexture)
            : base(Rectangle.Empty, move.MoveName.ToUpper(), function: move.MoveID)
        {
            Move = move;
            _moveFont = font;
            _backgroundTexture = backgroundTexture;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform, bool forceHover = false)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            bool isActivated = IsEnabled && (IsHovered || forceHover);

            // Get the animation offset from the base Button's HoverAnimator
            float hopOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isActivated);
            var animatedBounds = new Rectangle(Bounds.X + (int)hopOffset, Bounds.Y, Bounds.Width, Bounds.Height);

            // Draw background texture
            Color tintColor = Color.White;
            if (!IsEnabled) tintColor = _global.ButtonDisableColor * 0.5f;
            else if (_isPressed) tintColor = Color.Gray;
            else if (isActivated) tintColor = _global.ButtonHoverColor;
            spriteBatch.DrawSnapped(_backgroundTexture, animatedBounds, tintColor);


            // --- Draw Icon/Placeholder ---
            const int iconSize = 5;
            const int iconPadding = 4;
            var iconRect = new Rectangle(
                animatedBounds.X + iconPadding,
                animatedBounds.Y + (animatedBounds.Height - iconSize) / 2,
                iconSize,
                iconSize
            );

            if (IconTexture != null)
            {
                spriteBatch.DrawSnapped(IconTexture, iconRect, IconSourceRect, Color.White);
            }
            else
            {
                // Draw the pink square placeholder
                spriteBatch.DrawSnapped(pixel, iconRect, _global.Palette_Pink);
            }

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