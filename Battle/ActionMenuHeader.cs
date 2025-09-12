using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// A non-interactive header item for the action menu, used for grouping sorted moves.
    /// </summary>
    public class ActionMenuHeader : IActionMenuItem
    {
        public string Title { get; }

        public ActionMenuHeader(string title)
        {
            Title = title.ToUpper();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Rectangle bounds)
        {
            var global = ServiceLocator.Get<Global>();

            // Draw the text on top, aligned to the left with padding.
            var textSize = font.MeasureString(Title);
            const int leftPadding = 10; // Align with the text of the move buttons.
            var textPosition = new Vector2(
                bounds.X + leftPadding,
                bounds.Y + (bounds.Height - textSize.Height) / 2
            );
            spriteBatch.DrawStringSnapped(font, Title, textPosition, global.Palette_DarkGray);
        }
    }
}