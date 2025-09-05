using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class SliderSettingControl : ISettingControl
    {
        private readonly Global _global;
        private readonly Slider _slider;
        private readonly Func<float> _getter;
        private readonly Action<float> _setter;

        private float _currentValue;
        private float _savedValue;

        public string Label => _slider.Label;
        public bool IsDirty => Math.Abs(_currentValue - _savedValue) > 0.01f;
        public bool IsEnabled { get; set; } = true;
        public HoverAnimator HoverAnimator { get; } = new HoverAnimator();

        public SliderSettingControl(string label, float min, float max, float step, Func<float> getter, Action<float> setter)
        {
            _global = ServiceLocator.Get<Global>();
            _getter = getter;
            _setter = setter;
            _savedValue = getter();
            _currentValue = _savedValue;

            // The slider's bounds will be set dynamically in the Update method.
            _slider = new Slider(Rectangle.Empty, label, min, max, _currentValue, step);
            _slider.OnValueChanged += (newValue) => {
                _currentValue = newValue;
                _setter?.Invoke(_currentValue);
            };
        }

        public string GetCurrentValueAsString() => _currentValue.ToString("F2");
        public string GetSavedValueAsString() => _savedValue.ToString("F2");

        public void HandleInput(Keys key)
        {
            if (!IsEnabled) return;
            if (key == Keys.Left)
            {
                _slider.SetValue(_slider.CurrentValue - _slider.Step);
            }
            else if (key == Keys.Right)
            {
                _slider.SetValue(_slider.CurrentValue + _slider.Step);
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font)
        {
            // Dynamically update the slider's position and bounds.
            var sliderBounds = new Rectangle((int)(position.X + 175f), (int)position.Y, (int)Global.VALUE_DISPLAY_WIDTH, 15);
            _slider.Bounds = sliderBounds;
            _slider.IsEnabled = this.IsEnabled;

            if (UIInputManager.CanProcessMouseClick())
            {
                _slider.Update(currentMouseState, previousMouseState);
            }
        }

        public void Apply()
        {
            _savedValue = _currentValue;
        }

        public void Revert()
        {
            _currentValue = _savedValue;
            _slider.SetValue(_savedValue);
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
            _slider.SetValue(_currentValue);
        }

        public void ResetAnimationState()
        {
            HoverAnimator.Reset();
            _slider.ResetAnimationState();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            float xOffset = HoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.Palette_BrightWhite : _global.ButtonDisableColor);
            spriteBatch.DrawStringSnapped(font, Label, animatedPosition, labelColor);

            // The slider draws itself, but we need to provide the correct value color.
            Color valueColor = IsEnabled ? (IsDirty ? _global.Palette_Teal : _global.Palette_BrightWhite) : _global.ButtonDisableColor;
            _slider.CustomValueColor = valueColor;

            _slider.Draw(spriteBatch, font);
        }
    }
}