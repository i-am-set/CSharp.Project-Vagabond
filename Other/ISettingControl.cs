using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;

namespace ProjectVagabond.UI
{
    public interface ISettingControl
    {
        string Label { get; }
        bool IsDirty { get; }
        HoverAnimator HoverAnimator { get; }
        string GetCurrentValueAsString();
        string GetSavedValueAsString();
        void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime);
        void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font);
        void HandleInput(Keys key);
        void Apply();
        void Revert();
        void RefreshValue();
    }
}
