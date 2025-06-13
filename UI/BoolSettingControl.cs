using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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

        private Rectangle _clickableArea;

        public BoolSettingControl(string label, Func<bool> getter, Action<bool> onApply)
        {
            Label = label;
            _savedValue = getter();
            _currentValue = _savedValue;
            _onApply = onApply;
        }

        private void ToggleValue()
        {
            _currentValue = !_currentValue;
            // CRITICAL FIX: Immediately invoke the action to update the temporary settings object.
            _onApply?.Invoke(_currentValue);
        }

        public void HandleInput(Keys key)
        {
            if (key == Keys.Left || key == Keys.Right || key == Keys.Enter)
            {
                ToggleValue();
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState)
        {
            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
                if (_clickableArea.Contains(virtualMousePos))
                {
                    ToggleValue();
                }
            }
        }

        public void Apply()
        {
            // This method's main job now is to reset the "dirty" state.
            // The actual setting application happens immediately on change.
            _savedValue = _currentValue;
        }

        public void Revert()
        {
            _currentValue = _savedValue;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, bool isSelected)
        {
            var font = Global.Instance.DefaultFont;
            Color labelColor = isSelected ? Global.Instance.palette_Yellow : Global.Instance.palette_BrightWhite;
            spriteBatch.DrawString(font, Label, position, labelColor);

            string valueText = _currentValue ? "< ON  >" : "< OFF >";
            Vector2 valuePosition = new Vector2(position.X + 280, position.Y);
            Color valueColor = IsDirty ? Global.Instance.palette_Teal : Global.Instance.palette_BrightWhite;
            spriteBatch.DrawString(font, valueText, valuePosition, valueColor);

            // Define clickable area for the update loop
            Vector2 valueSize = font.MeasureString(valueText);
            _clickableArea = new Rectangle((int)valuePosition.X, (int)valuePosition.Y, (int)valueSize.X, (int)valueSize.Y);
        }
    }
}