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
        bool IsEnabled { get; set; }
        HoverAnimator HoverAnimator { get; }
        string GetCurrentValueAsString();
        string GetSavedValueAsString();

        // Updated to take two fonts
        void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, bool isSelected, GameTime gameTime);

        // Update usually only needs the value font for measuring hitboxes of arrows/values
        void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont valueFont);

        void HandleInput(Keys key);
        void Apply();
        void Revert();
        void RefreshValue();
        void ResetAnimationState();
    }
}