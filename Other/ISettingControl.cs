using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;

namespace ProjectVagabond.UI
{
    public interface ISettingControl : ISelectable
    {
        string Label { get; }
        bool IsDirty { get; }
        HoverAnimator HoverAnimator { get; }
        string GetCurrentValueAsString();
        string GetSavedValueAsString();

        void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, GameTime gameTime);

        void Update(Vector2 position, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont labelFont, BitmapFont valueFont);

        void HandleInput(Keys key);
        void Apply();
        void Revert();
        void RefreshValue();
        void ResetAnimationState();
    }
}