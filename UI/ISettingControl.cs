using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;

namespace ProjectVagabond.UI
{
    public interface ISettingControl
    {
        string Label { get; }
        bool IsDirty { get; }
        void Draw(SpriteBatch spriteBatch, BitmapFont font, Texture2D pixel, Vector2 position, bool isSelected);
        void HandleInput(Keys key);
        void Apply();
        void Revert();
    }
}