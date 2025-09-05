using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class BoolSettingControl : ISettingControl
    {
        private readonly Global _global;

        public string Label { get; }
        public bool IsDirty => _currentValue != _savedValue;
        public bool IsEnabled { get; set; } = true;

        private bool _currentValue;
        private bool _savedValue;
        private readonly Func<bool> _getter;
        private readonly Action<bool> _onApply;

        private Rectangle _leftArrowRect;
        private Rectangle _rightArrowRect;
        private bool _isLeftArrowHovered;
        private bool _isRightArrowHovered;
        private readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        public HoverAnimator HoverAnimator => _hoverAnimator;

        public BoolSettingControl(string label, Func<bool> getter, Action<bool> onApply)
        {
            _global = ServiceLocator.Get<Global>();
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

        public string GetCurrentValueAsString() => _currentValue ? "ON" : "OFF";
        public string GetSavedValueAsString() => _savedValue ? "ON" : "OFF";

        public void HandleInput(Keys key)
        {
            if (!IsEnabled) return;
            if (key == Keys.Left || key == Keys.Right)
            {
                ToggleValue();
            }
        }

        private void CalculateBounds(Vector2 position, BitmapFont font)
        {
            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            const float valueAreaXOffset = 175f;
            string leftArrowText = "<";
            string rightArrowText = ">";
            Vector2 leftArrowSize = font.MeasureString(leftArrowText);
            Vector2 rightArrowSize = font.MeasureString(rightArrowText);
            int padding = 2; // Reduced padding for tighter hitboxes
            float arrowVisualHeight = font.LineHeight;

            _leftArrowRect = new Rectangle(
                (int)(position.X + valueAreaXOffset - padding),
                (int)(position.Y - padding),
                (int)leftArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2));

            _rightArrowRect = new Rectangle(
                (int)(position.X + valueAreaXOffset + valueDisplayWidth - rightArrowSize.X - padding),
                (int)position.Y - padding,
                (int)rightArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2));
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font)
        {
            if (!IsEnabled)
            {
                _isLeftArrowHovered = false;
                _isRightArrowHovered = false;
                return;
            }

            CalculateBounds(position, font);

            _isLeftArrowHovered = _leftArrowRect.Contains(virtualMousePos);
            _isRightArrowHovered = _rightArrowRect.Contains(virtualMousePos);

            if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_isLeftArrowHovered || _isRightArrowHovered)
                {
                    ToggleValue();
                    UIInputManager.ConsumeMouseClick();
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

        public void ResetAnimationState()
        {
            HoverAnimator.Reset();
            _isLeftArrowHovered = false;
            _isRightArrowHovered = false;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.Palette_BrightWhite : _global.ButtonDisableColor);
            spriteBatch.DrawStringSnapped(font, Label, animatedPosition, labelColor);

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            const float valueAreaXOffset = 175f;
            Vector2 valueAreaPosition = new Vector2(animatedPosition.X + valueAreaXOffset, animatedPosition.Y);

            string leftArrowText = "<";
            string valueText = _currentValue ? "ON" : "OFF";
            string rightArrowText = ">";

            Color baseValueColor = IsEnabled ? (IsDirty ? _global.Palette_Teal : _global.Palette_BrightWhite) : _global.ButtonDisableColor;
            Color leftArrowColor = IsEnabled ? (_isLeftArrowHovered ? _global.ButtonHoverColor : baseValueColor) : _global.ButtonDisableColor;
            Color rightArrowColor = IsEnabled ? (_isRightArrowHovered ? _global.ButtonHoverColor : baseValueColor) : _global.ButtonDisableColor;

            Vector2 leftArrowSize = font.MeasureString(leftArrowText);
            Vector2 valueTextSize = font.MeasureString(valueText);
            Vector2 rightArrowSize = font.MeasureString(rightArrowText);

            spriteBatch.DrawStringSnapped(font, leftArrowText, valueAreaPosition, leftArrowColor);
            spriteBatch.DrawStringSnapped(font, rightArrowText, new Vector2(valueAreaPosition.X + valueDisplayWidth - rightArrowSize.X, valueAreaPosition.Y), rightArrowColor);

            float spaceBetweenArrows = (valueAreaPosition.X + valueDisplayWidth - rightArrowSize.X) - (valueAreaPosition.X + leftArrowSize.X);
            float textX = valueAreaPosition.X + leftArrowSize.X + (spaceBetweenArrows - valueTextSize.X) * 0.5f;
            spriteBatch.DrawStringSnapped(font, valueText, new Vector2(textX, valueAreaPosition.Y), baseValueColor);
        }
    }
}