using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ProjectVagabond.UI
{
    public class BoolSettingControl : ISettingControl
    {
        public string Label { get; }
        public bool IsDirty => _currentValue != _savedValue;
        private bool _currentValue;
        private bool _savedValue;
        private readonly Action<bool> _onApply;

        public BoolSettingControl(string label, Func<bool> getter, Action<bool> onApply)
        {
            Label = label;
            _savedValue = getter();
            _currentValue = _savedValue;
            _onApply = onApply;
        }

        public void HandleInput(Keys key)
        {
            if (key == Keys.Left || key == Keys.Right)
            {
                _currentValue = !_currentValue;
            }
        }

        public void Apply()
        {
            if (IsDirty)
            {
                _onApply?.Invoke(_currentValue);
                _savedValue = _currentValue;
            }
        }

        public void Revert()
        {
            _currentValue = _savedValue;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Vector2 position, bool isSelected)
        {
            Color labelColor = isSelected ? Global.Instance.palette_Yellow : Global.Instance.palette_BrightWhite;
            spriteBatch.DrawString(font, Label, position, labelColor);

            string valueText = _currentValue ? "< ON >" : "< OFF >";
            Vector2 valuePosition = new Vector2(position.X + 300, position.Y);
            Color valueColor = IsDirty ? Global.Instance.palette_Teal : Global.Instance.palette_BrightWhite;
            spriteBatch.DrawString(font, valueText, valuePosition, valueColor);
        }
    }
}