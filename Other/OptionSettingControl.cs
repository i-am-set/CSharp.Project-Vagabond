using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class OptionSettingControl<T> : ISettingControl
    {
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;

        public string Label { get; }
        public bool IsDirty => !_savedValue.Equals(_currentValue);
        public bool IsEnabled { get; set; } = true;

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
        public HoverAnimator HoverAnimator => _hoverAnimator;

        public Func<T, Color?> GetValueColor { get; set; }
        public Func<T, bool> IsOptionNotRecommended { get; set; }
        public Func<string> ExtraInfoTextGetter { get; set; }

        public OptionSettingControl(string label, List<KeyValuePair<string, T>> options, Func<T> getter, Action<T> setter)
        {
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
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
            if (!IsEnabled) return;
            if (key == Keys.Left)
            {
                _hapticsManager.TriggerCompoundShake(0.75f);
                Decrement();
            }
            if (key == Keys.Right || key == Keys.Enter)
            {
                _hapticsManager.TriggerCompoundShake(0.75f);
                Increment();
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
                bool consumed = false;
                if (_isLeftArrowHovered)
                {
                    _hapticsManager.TriggerCompoundShake(0.75f);
                    Decrement();
                    consumed = true;
                }
                else if (_isRightArrowHovered)
                {
                    _hapticsManager.TriggerCompoundShake(0.75f);
                    Increment();
                    consumed = true;
                }

                if (consumed)
                {
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
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
            _currentIndex = _options.FindIndex(o => o.Value.Equals(_currentValue));
            if (_currentIndex == -1) _currentIndex = 0;
        }

        public void ResetAnimationState()
        {
            HoverAnimator.Reset();
            _isLeftArrowHovered = false;
            _isRightArrowHovered = false;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            float yOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled);
            Vector2 animatedPosition = new Vector2(position.X, position.Y + yOffset);

            // --- Label and Extra Info ---
            string labelText = Label;
            string extraInfoText = ExtraInfoTextGetter?.Invoke();
            Vector2 labelSize = font.MeasureString(labelText);
            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.Palette_BrightWhite : _global.ButtonDisableColor);
            spriteBatch.DrawStringSnapped(font, labelText, animatedPosition, labelColor);
            if (!string.IsNullOrEmpty(extraInfoText))
            {
                spriteBatch.DrawStringSnapped(font, extraInfoText, animatedPosition + new Vector2(labelSize.X + 2, 0), _global.Palette_DarkGray);
            }

            // --- Value and Arrows ---
            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            const float valueAreaXOffset = 175f;
            Vector2 valueAreaPosition = new Vector2(animatedPosition.X + valueAreaXOffset, animatedPosition.Y);

            string leftArrowText = "<";
            string valueText = _options[_currentIndex].Key;
            string rightArrowText = ">";

            Color baseValueColor;
            if (!IsEnabled)
            {
                baseValueColor = _global.ButtonDisableColor;
            }
            else
            {
                bool isNotRecommended = IsOptionNotRecommended?.Invoke(_currentValue) ?? false;
                if (isSelected && isNotRecommended)
                {
                    baseValueColor = _global.Palette_Orange;
                }
                else if (IsDirty)
                {
                    baseValueColor = _global.Palette_Teal;
                }
                else
                {
                    var customColor = GetValueColor?.Invoke(_currentValue);
                    baseValueColor = customColor ?? _global.Palette_BrightWhite;
                }
            }

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

            // --- Strikethrough ---
            if (!IsEnabled)
            {
                float startX = animatedPosition.X;
                float endX = position.X + valueAreaXOffset + valueDisplayWidth;
                float lineY = animatedPosition.Y + font.LineHeight / 2f;
                spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), _global.ButtonDisableColor);
            }
        }
    }
}
