#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.UI
{
    public class InventoryHeaderButton : ImageButton
    {
        public int MenuIndex { get; }
        public string ButtonName { get; }

        public InventoryHeaderButton(Rectangle bounds, Texture2D spriteSheet, Rectangle defaultSourceRect, Rectangle hoverSourceRect, Rectangle selectedSourceRect, int menuIndex, string name)
            : base(bounds, spriteSheet, defaultSourceRect, hoverSourceRect, selectedSourceRect: selectedSourceRect, function: name)
        {
            MenuIndex = menuIndex;
            ButtonName = name;
        }
    }
}
#nullable restore