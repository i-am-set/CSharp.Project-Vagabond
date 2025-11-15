#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.UI
{
    public class InventoryHeaderButton : ImageButton
    {
        public int MenuIndex { get; }
        public string ButtonName { get; }

        public InventoryHeaderButton(Rectangle bounds, Texture2D spriteSheet, Rectangle defaultSourceRect, Rectangle hoverSourceRect, int menuIndex, string name)
            : base(bounds, spriteSheet, defaultSourceRect, hoverSourceRect, function: name)
        {
            MenuIndex = menuIndex;
            ButtonName = name;
        }
    }
}
#nullable restore