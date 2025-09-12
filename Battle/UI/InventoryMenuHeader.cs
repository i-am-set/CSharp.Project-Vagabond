using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// A non-interactive header item for the item menu, used for grouping sorted items.
    /// </summary>
    public class InventoryMenuHeader : IInventoryMenuItem
    {
        public string Title { get; }

        public InventoryMenuHeader(string title)
        {
            Title = title.ToUpper();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Rectangle bounds)
        {
            var global = ServiceLocator.Get<Global>();

            var textSize = font.MeasureString(Title);
            const int leftPadding = 10;
            var textPosition = new Vector2(
                bounds.X + leftPadding,
                bounds.Y + (bounds.Height - textSize.Height) / 2
            );
            spriteBatch.DrawStringSnapped(font, Title, textPosition, global.Palette_DarkGray);
        }
    }
}