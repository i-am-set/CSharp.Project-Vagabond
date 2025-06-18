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
        private readonly Func<T> _getter;
        private readonly Action<T> _setter;
        private readonly List<KeyValuePair<string, T>> _options;
        private int _currentIndex;

        private Rectangle _leftArrowRect;
        private Rectangle _rightArrowRect;
        private bool _isLeftArrowHovered;
        private bool _isRightArrowHovered;

        public OptionSettingControl(string label, List<KeyValuePair<string, T>> options, Func<T> getter, Action<T> setter)
        {
            Label = label;
            _options = options;
            _getter = getter;
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
            _setter?.Invoke(_currentValue);
        }

        private void Decrement()
        {
            _currentIndex = (_currentIndex - 1 + _options.Count) % _options.Count;
            _currentValue = _options[_currentIndex].Value;
            _setter?.Invoke(_currentValue);
        }

        private string GetValueAsString(T value)
        {
            var option = _options.FirstOrDefault(o => o.Value.Equals(value));
            return option.Key ?? "N/A";
        }
        public string GetCurrentValueAsString() => GetValueAsString(_currentValue);
        public string GetSavedValueAsString() => GetValueAsString(_savedValue);

        public void HandleInput(Keys key)
        {
            if (key == Keys.Left) Decrement();
            if (key == Keys.Right || key == Keys.Enter) Increment();
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState)
        {
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            _isLeftArrowHovered = _leftArrowRect.Contains(virtualMousePos);
            _isRightArrowHovered = _rightArrowRect.Contains(virtualMousePos);

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_isLeftArrowHovered)
                {
                    Decrement();
                }
                else if (_isRightArrowHovered)
                {
                    Increment();
                }
            }
        }

        public void Apply()
        {
            _savedValue = _currentValue;
        }

        public void Revert()
        {
            _currentValue = _savedValue;
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, bool isSelected)
        {
            var font = Global.Instance.DefaultFont;
            Color labelColor = isSelected ? Global.Instance.OptionHoverColor : Global.Instance.Palette_BrightWhite;
            spriteBatch.DrawString(font, Label, position, labelColor);

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            Vector2 valueAreaPosition = new Vector2(position.X + 340, position.Y);

            string leftArrowText = "<";
            string valueText = _options[_currentIndex].Key;
            string rightArrowText = ">";

            Color baseValueColor = IsDirty ? Global.Instance.Palette_Teal : Global.Instance.Palette_BrightWhite;
            Color leftArrowColor = _isLeftArrowHovered ? Global.Instance.OptionHoverColor : baseValueColor;
            Color rightArrowColor = _isRightArrowHovered ? Global.Instance.OptionHoverColor : baseValueColor;

            Vector2 leftArrowSize = font.MeasureString(leftArrowText);
            Vector2 valueTextSize = font.MeasureString(valueText);
            Vector2 rightArrowSize = font.MeasureString(rightArrowText);

            // Left arrow
            Vector2 leftArrowPos = valueAreaPosition;
            spriteBatch.DrawString(font, leftArrowText, leftArrowPos, leftArrowColor);

            // Right arrow
            Vector2 rightArrowPos = new Vector2(valueAreaPosition.X + valueDisplayWidth - rightArrowSize.X, valueAreaPosition.Y);
            spriteBatch.DrawString(font, rightArrowText, rightArrowPos, rightArrowColor);

            // Value text
            float spaceBetweenArrows = rightArrowPos.X - (leftArrowPos.X + leftArrowSize.X);
            float textX = leftArrowPos.X + leftArrowSize.X + (spaceBetweenArrows - valueTextSize.X) * 0.5f;
            Vector2 textPos = new Vector2(textX, valueAreaPosition.Y);
            spriteBatch.DrawString(font, valueText, textPos, baseValueColor);

            int padding = 5;
            float arrowVisualHeight = font.LineHeight;

            // Left Arrow Click Box
            _leftArrowRect = new Rectangle(
                (int)leftArrowPos.X - padding,
                (int)leftArrowPos.Y - padding,
                (int)leftArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2)
            );

            // Right Arrow Click Box
            _rightArrowRect = new Rectangle(
                (int)rightArrowPos.X - padding,
                (int)rightArrowPos.Y - padding,
                (int)rightArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2)
            );
        }
    }
}