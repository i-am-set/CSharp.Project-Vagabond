using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class OptionSettingControl<T> : ISettingControl
    {
        public string Label { get; }
        public bool IsDirty => !_savedValue.Equals(_currentValue);

        private T _currentValue;
        private T _savedValue;
        private readonly Action<T> _setter;
        private readonly List<KeyValuePair<string, T>> _options;
        private int _currentIndex;

        private Rectangle _leftArrowRect;
        private Rectangle _rightArrowRect;

        public OptionSettingControl(string label, List<KeyValuePair<string, T>> options, Func<T> getter, Action<T> setter)
        {
            Label = label;
            _options = options;
            _setter = setter;
            _savedValue = getter();
            _currentValue = _savedValue;
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        private void Increment()
        {
            _currentIndex = (_currentIndex + 1) % _options.Count;
            _currentValue = _options[_currentIndex].Value;
            // CRITICAL FIX: Immediately invoke the action to update the temporary settings object.
            _setter?.Invoke(_currentValue);
        }

        private void Decrement()
        {
            _currentIndex = (_currentIndex - 1 + _options.Count) % _options.Count;
            _currentValue = _options[_currentIndex].Value;
            // CRITICAL FIX: Immediately invoke the action to update the temporary settings object.
            _setter?.Invoke(_currentValue);
        }

        public void HandleInput(Keys key)
        {
            if (key == Keys.Left) Decrement();
            if (key == Keys.Right || key == Keys.Enter) Increment();
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState)
        {
            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
                if (_leftArrowRect.Contains(virtualMousePos))
                {
                    Decrement();
                }
                else if (_rightArrowRect.Contains(virtualMousePos))
                {
                    Increment();
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
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, bool isSelected)
        {
            var font = Global.Instance.DefaultFont;
            Color labelColor = isSelected ? Global.Instance.OptionHoverColor : Global.Instance.Palette_BrightWhite;
            spriteBatch.DrawString(font, Label, position, labelColor);

            string valueText = $"< {_options[_currentIndex].Key} >";
            Vector2 valuePosition = new Vector2(position.X + 280, position.Y);
            Color valueColor = IsDirty ? Global.Instance.Palette_Teal : Global.Instance.Palette_BrightWhite;
            spriteBatch.DrawString(font, valueText, valuePosition, valueColor);

            // Define clickable areas for the update loop
            Vector2 valueSize = font.MeasureString(valueText);
            int padding = 10; // This is the extra clickable space around the arrow text. Adjust as needed.
            int arrowWidth = 15; // The original visual width of the arrow text like "< "

            // Left Arrow Click Box
            _leftArrowRect = new Rectangle(
                (int)valuePosition.X - padding, 
                (int)valuePosition.Y - padding, 
                arrowWidth + (padding * 2), 
                (int)valueSize.Y + (padding * 2)
            );

            // Right Arrow Click Box
            _rightArrowRect = new Rectangle(
                (int)(valuePosition.X + valueSize.X - arrowWidth) - padding, 
                (int)valuePosition.Y - padding, 
                arrowWidth + (padding * 2), 
                (int)valueSize.Y + (padding * 2)
            );
        }
    }
}