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
        public bool IsSelected { get; set; }
        public Rectangle Bounds { get; private set; }

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

        private float _waveTimer = 0f;
        private const TextEffectType ActiveEffect = TextEffectType.LeftAlignedSmallWave;

        private const float VALUE_AREA_X_OFFSET = 155f;

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

        public void OnSelect()
        {
            IsSelected = true;
        }

        public void OnDeselect()
        {
            IsSelected = false;
        }

        public void OnSubmit()
        {
            Increment();
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

        public bool HandleInput(InputManager input)
        {
            if (!IsEnabled) return false;
            if (input.NavigateLeft)
            {
                _hapticsManager.TriggerCompoundShake(0.5f);
                Decrement();
                return true;
            }
            if (input.NavigateRight || input.Confirm)
            {
                _hapticsManager.TriggerCompoundShake(0.5f);
                Increment();
                return true;
            }
            return false;
        }

        private void CalculateBounds(Vector2 position, BitmapFont labelFont, BitmapFont valueFont)
        {
            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;
            string leftArrowText = "<";
            string rightArrowText = ">";

            Vector2 leftArrowSize = valueFont.MeasureString(leftArrowText);
            Vector2 rightArrowSize = valueFont.MeasureString(rightArrowText);
            int padding = 2;
            float arrowVisualHeight = valueFont.LineHeight;

            _leftArrowRect = new Rectangle(
                (int)(position.X + VALUE_AREA_X_OFFSET - padding),
                (int)(position.Y - padding + 1),
                (int)leftArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2));

            _rightArrowRect = new Rectangle(
                (int)(position.X + VALUE_AREA_X_OFFSET + valueDisplayWidth - rightArrowSize.X - padding),
                (int)position.Y - padding + 1,
                (int)rightArrowSize.X + (padding * 2),
                (int)arrowVisualHeight + (padding * 2));

            float totalWidth = VALUE_AREA_X_OFFSET + valueDisplayWidth;
            float height = Math.Max(labelFont.LineHeight, valueFont.LineHeight);
            Bounds = new Rectangle((int)position.X, (int)position.Y - 2, (int)totalWidth, (int)height + 4);
        }

        public void Update(Vector2 position, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont labelFont, BitmapFont valueFont)
        {
            CalculateBounds(position, labelFont, valueFont);

            if (!IsEnabled)
            {
                _isLeftArrowHovered = false;
                _isRightArrowHovered = false;
                return;
            }

            _isLeftArrowHovered = _leftArrowRect.Contains(virtualMousePos);
            _isRightArrowHovered = _rightArrowRect.Contains(virtualMousePos);

            var inputManager = ServiceLocator.Get<InputManager>();
            if (inputManager.IsMouseClickAvailable() && currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                bool consumed = false;
                if (_isLeftArrowHovered)
                {
                    _hapticsManager.TriggerCompoundShake(0.5f);
                    Decrement();
                    consumed = true;
                }
                else if (_isRightArrowHovered)
                {
                    _hapticsManager.TriggerCompoundShake(0.5f);
                    Increment();
                    consumed = true;
                }

                if (consumed)
                {
                    inputManager.ConsumeMouseClick();
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
            _waveTimer = 0f;
            _isLeftArrowHovered = false;
            _isRightArrowHovered = false;
            IsSelected = false;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, GameTime gameTime)
        {
            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, IsSelected && IsEnabled, _global.UI_ButtonHoverLift, _global.UI_ButtonHoverDuration);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            string labelText = Label;
            string extraInfoText = ExtraInfoTextGetter?.Invoke();
            Vector2 labelSize = labelFont.MeasureString(labelText);
            Color labelColor = IsSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.GameTextColor : _global.ButtonDisableColor);

            if (IsSelected && IsEnabled)
            {
                _waveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (TextAnimator.IsOneShotEffect(ActiveEffect))
                {
                    float duration = TextAnimator.GetSmallWaveDuration(labelText.Length);
                    if (_waveTimer > duration + 0.1f) _waveTimer = 0f;
                }

                TextAnimator.DrawTextWithEffect(spriteBatch, labelFont, labelText, animatedPosition, labelColor, ActiveEffect, _waveTimer, Vector2.One);
            }
            else
            {
                _waveTimer = 0f;
                spriteBatch.DrawStringSnapped(labelFont, labelText, animatedPosition, labelColor);
            }

            if (!string.IsNullOrEmpty(extraInfoText))
            {
                spriteBatch.DrawStringSnapped(labelFont, extraInfoText, animatedPosition + new Vector2(labelSize.X + 2, 0), _global.DullTextColor);
            }

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;

            float valueVisualOffset = (IsSelected && IsEnabled) ? 0f : 1f;
            Vector2 valueAreaPosition = new Vector2(position.X + VALUE_AREA_X_OFFSET + valueVisualOffset, position.Y);

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
                if (IsSelected && isNotRecommended)
                {
                    baseValueColor = _global.Palette_Fruit;
                }
                else if (IsDirty)
                {
                    baseValueColor = _global.ConfirmSettingsColor;
                }
                else
                {
                    var customColor = GetValueColor?.Invoke(_currentValue);
                    baseValueColor = customColor ?? _global.GameTextColor;
                }
            }

            Color leftArrowColor = IsEnabled ? (_isLeftArrowHovered ? _global.ButtonHoverColor : baseValueColor) : _global.ButtonDisableColor;
            Color rightArrowColor = IsEnabled ? (_isRightArrowHovered ? _global.ButtonHoverColor : baseValueColor) : _global.ButtonDisableColor;

            Vector2 leftArrowSize = valueFont.MeasureString(leftArrowText);
            Vector2 valueTextSize = valueFont.MeasureString(valueText);
            Vector2 rightArrowSize = valueFont.MeasureString(rightArrowText);

            float valueYOffset = (labelFont.LineHeight - valueFont.LineHeight) / 2f + 1f;
            Vector2 valueDrawPos = valueAreaPosition + new Vector2(0, valueYOffset);

            spriteBatch.DrawStringSnapped(valueFont, leftArrowText, valueDrawPos, leftArrowColor);
            spriteBatch.DrawStringSnapped(valueFont, rightArrowText, new Vector2(valueDrawPos.X + valueDisplayWidth - rightArrowSize.X, valueDrawPos.Y), rightArrowColor);

            float spaceBetweenArrows = (valueDrawPos.X + valueDisplayWidth - rightArrowSize.X) - (valueDrawPos.X + leftArrowSize.X);

            float textX = (int)(valueDrawPos.X + leftArrowSize.X + (spaceBetweenArrows - valueTextSize.X) * 0.5f);

            spriteBatch.DrawStringSnapped(valueFont, valueText, new Vector2(textX, valueDrawPos.Y), baseValueColor);

            if (!IsEnabled)
            {
                float startX = animatedPosition.X;
                float endX = position.X + VALUE_AREA_X_OFFSET + valueDisplayWidth;
                float lineY = animatedPosition.Y + labelFont.LineHeight / 2f;
                spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), _global.ButtonDisableColor);
            }
        }
    }
}