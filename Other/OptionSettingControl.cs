using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class OptionSettingControl<T> : ISettingControl
    {
        private readonly Global _global;

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
        private readonly HoverAnimator _hoverAnimator = new HoverAnimator();

        public Func<T, Color?> GetValueColor { get; set; }

        public OptionSettingControl(string label, List<KeyValuePair<string, T>> options, Func<T> getter, Action<T> setter)
        {
            _global = ServiceLocator.Get<Global>();
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

        private void CalculateBounds(Vector2 position, BitmapFont font)
        {
            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            string leftArrowText = "<";
            string rightArrowText = ">";
            Vector2 leftArrowSize = font.MeasureString(leftArrowText);
            Vector2 rightArrowSize = font.MeasureString(rightArrowText);
            int padding = 5;
            float arrowVisualHeight = font.LineHeight;

            _leftArrowRect = new Rectangle((int)(position.X + 340) - padding, (int)position.Y - padding, (int)leftArrowSize.X + (padding * 2), (int)arrowVisualHeight + (padding * 2));
            _rightArrowRect = new Rectangle((int)(position.X + 340 + valueDisplayWidth - rightArrowSize.X) - padding, (int)position.Y - padding, (int)rightArrowSize.X + (padding * 2), (int)arrowVisualHeight + (padding * 2));
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font)
        {
            CalculateBounds(position, font);

            _isLeftArrowHovered = _leftArrowRect.Contains(virtualMousePos);
            _isRightArrowHovered = _rightArrowRect.Contains(virtualMousePos);

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_isLeftArrowHovered) Decrement();
                else if (_isRightArrowHovered) Increment();
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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isSelected);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
            spriteBatch.DrawString(font, Label, animatedPosition, labelColor);

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            Vector2 valueAreaPosition = new Vector2(animatedPosition.X + 340, animatedPosition.Y);

            string leftArrowText = "<";
            string valueText = _options[_currentIndex].Key;
            string rightArrowText = ">";

            Color baseValueColor;
            if (IsDirty)
            {
                baseValueColor = _global.Palette_Teal;
            }
            else
            {
                var customColor = GetValueColor?.Invoke(_currentValue);
                baseValueColor = customColor ?? _global.Palette_BrightWhite;
            }

            Color leftArrowColor = _isLeftArrowHovered ? _global.ButtonHoverColor : baseValueColor;
            Color rightArrowColor = _isRightArrowHovered ? _global.ButtonHoverColor : baseValueColor;

            Vector2 leftArrowSize = font.MeasureString(leftArrowText);
            Vector2 valueTextSize = font.MeasureString(valueText);
            Vector2 rightArrowSize = font.MeasureString(rightArrowText);

            spriteBatch.DrawString(font, leftArrowText, valueAreaPosition, leftArrowColor);
            spriteBatch.DrawString(font, rightArrowText, new Vector2(valueAreaPosition.X + valueDisplayWidth - rightArrowSize.X, valueAreaPosition.Y), rightArrowColor);

            float spaceBetweenArrows = (valueAreaPosition.X + valueDisplayWidth - rightArrowSize.X) - (valueAreaPosition.X + leftArrowSize.X);
            float textX = valueAreaPosition.X + leftArrowSize.X + (spaceBetweenArrows - valueTextSize.X) * 0.5f;
            spriteBatch.DrawString(font, valueText, new Vector2(textX, valueAreaPosition.Y), baseValueColor);
        }
    }
}