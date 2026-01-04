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
        private readonly HapticsManager _hapticsManager;
        private readonly Slider _slider;
        private readonly Func<float> _getter;
        private readonly Action<float> _setter;

        private float _currentValue;
        private float _savedValue;

        private string _realLabel;
        public string Label => _realLabel;

        public bool IsDirty => Math.Abs(_currentValue - _savedValue) > 0.01f;
        public bool IsEnabled { get; set; } = true;
        public HoverAnimator HoverAnimator { get; } = new HoverAnimator();

        // Animation State
        private float _waveTimer = 0f;

        // --- Visual Tuning Constants ---
        private const float VALUE_AREA_X_OFFSET = 155f; // Reduced from 175f to widen value area

        public SliderSettingControl(string label, float min, float max, float step, Func<float> getter, Action<float> setter)
        {
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _getter = getter;
            _setter = setter;
            _savedValue = getter();
            _currentValue = _savedValue;

            _realLabel = label;

            // Pass empty string for label to the internal Slider so it doesn't draw a static text underneath our animated one.
            _slider = new Slider(Rectangle.Empty, "", min, max, _currentValue, step);

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
                _hapticsManager.TriggerCompoundShake(0.5f);
                _slider.SetValue(_slider.CurrentValue - _slider.Step);
            }
            else if (key == Keys.Right)
            {
                _hapticsManager.TriggerCompoundShake(0.5f);
                _slider.SetValue(_slider.CurrentValue + _slider.Step);
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont valueFont)
        {
            // Dynamically update the slider's position and bounds.
            // Use 12px height to match the new standard.
            // Position is passed as the top of the slot + 3px (text center).
            // We want the slider to fill the slot, so we subtract 3 to get back to the top.
            var sliderBounds = new Rectangle((int)(position.X + VALUE_AREA_X_OFFSET), (int)position.Y - 3, (int)Global.VALUE_DISPLAY_WIDTH, 12);
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
            _waveTimer = 0f;
            _slider.ResetAnimationState();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, bool isSelected, GameTime gameTime)
        {
            float xOffset = HoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.Palette_BrightWhite : _global.ButtonDisableColor);

            // --- Label Drawing with Wave (Using labelFont) ---
            if (isSelected && IsEnabled)
            {
                _waveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float duration = TextUtils.GetSmallWaveDuration(Label.Length);
                if (_waveTimer > duration + 0.1f) _waveTimer = 0f;

                TextUtils.DrawTextWithEffect(spriteBatch, labelFont, Label, animatedPosition, labelColor, TextEffectType.LeftAlignedSmallWave, _waveTimer, Vector2.One);
            }
            else
            {
                _waveTimer = 0f;
                spriteBatch.DrawStringSnapped(labelFont, Label, animatedPosition, labelColor);
            }

            // The slider draws itself, but we need to provide the correct value color.
            Color valueColor = IsEnabled ? (IsDirty ? _global.Palette_Teal : _global.Palette_BrightWhite) : _global.ButtonDisableColor;
            _slider.CustomValueColor = valueColor;

            // Pass valueFont to the slider for drawing the numeric value
            _slider.Draw(spriteBatch, valueFont);
        }
    }
}