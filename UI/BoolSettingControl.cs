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
        private readonly Func<bool> _getter;
        private readonly Action<bool> _onApply;

        private Rectangle _leftArrowRect;
        private Rectangle _rightArrowRect;
        private bool _isLeftArrowHovered;
        private bool _isRightArrowHovered;

        public BoolSettingControl(string label, Func<bool> getter, Action<bool> onApply)
        {
            Label = label;
            _getter = getter;
            _savedValue = getter();
            _currentValue = _savedValue;
            _onApply = onApply;
        }

        private void ToggleValue()
        {
            _currentValue = !_currentValue;
            _onApply?.Invoke(_currentValue);
        }

        public void HandleInput(Keys key)
        {
            if (key == Keys.Left || key == Keys.Right)
            {
                ToggleValue();
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState)
        {
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            _isLeftArrowHovered = _leftArrowRect.Contains(virtualMousePos);
            _isRightArrowHovered = _rightArrowRect.Contains(virtualMousePos);

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_isLeftArrowHovered || _isRightArrowHovered)
                {
                    ToggleValue();
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
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, bool isSelected)
        {
            var font = Global.Instance.DefaultFont;
            Color labelColor = isSelected ? Global.Instance.OptionHoverColor : Global.Instance.Palette_BrightWhite;
            spriteBatch.DrawString(font, Label, position, labelColor);

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            Vector2 valueAreaPosition = new Vector2(position.X + 340, position.Y);

            string leftArrowText = "<";
            string valueText = _currentValue ? "ON" : "OFF";
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