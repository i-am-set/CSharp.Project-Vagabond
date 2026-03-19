using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class HeaderSettingControl : ISettingControl
    {
        private readonly Global _global;
        private readonly Core _core;

        public string Label { get; }
        public bool IsDirty => false;
        public bool IsEnabled { get; set; } = false;
        public bool IsSelected { get; set; }
        public Rectangle Bounds { get; private set; }

        public ISelectable? NeighborUp { get; set; }
        public ISelectable? NeighborDown { get; set; }
        public ISelectable? NeighborLeft { get; set; }
        public ISelectable? NeighborRight { get; set; }

        public HoverAnimator HoverAnimator { get; } = new HoverAnimator();

        public HeaderSettingControl(string label)
        {
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            Label = label;
        }

        public void OnSelect() { }
        public void OnDeselect() { }
        public void OnSubmit() { }
        public bool HandleInput(InputManager input) => false;

        public string GetCurrentValueAsString() => "";
        public string GetSavedValueAsString() => "";

        public void Update(Vector2 position, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont labelFont, BitmapFont valueFont)
        {
            Bounds = new Rectangle((int)position.X, (int)position.Y, Global.VIRTUAL_WIDTH, labelFont.LineHeight);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, GameTime gameTime)
        {
            var font = _core.TertiaryFont;
            Vector2 size = font.MeasureString(Label);

            float x = (Global.VIRTUAL_WIDTH / 2f) - (size.X / 2f);
            float yOffset = (labelFont.LineHeight - font.LineHeight) / 2f;

            Vector2 drawPos = new Vector2(MathF.Round(x), MathF.Round(position.Y + yOffset));

            spriteBatch.DrawStringSnapped(font, Label, drawPos, _global.Palette_DarkestPale);
        }

        public void Apply() { }
        public void Revert() { }
        public void RefreshValue() { }
        public void ResetAnimationState() { }
    }
}