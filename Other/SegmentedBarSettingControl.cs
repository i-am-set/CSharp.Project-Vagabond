using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class SegmentedBarSettingControl : ISettingControl
    {
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;
        private readonly Func<float> _getter;
        private readonly Action<float> _setter;

        private float _currentValue;
        private float _savedValue;
        private readonly float _minValue;
        private readonly float _maxValue;
        private readonly int _segmentCount;
        private readonly float _step;

        public string Label { get; }
        public bool IsDirty => Math.Abs(_currentValue - _savedValue) > 0.01f;
        public bool IsEnabled { get; set; } = true;

        private Rectangle _barAreaRect;
        private bool _isDragging;
        private int _hoveredSegmentIndex = -1;
        private readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        public HoverAnimator HoverAnimator => _hoverAnimator;

        private float _waveTimer = 0f;
        private const TextEffectType ActiveEffect = TextEffectType.LeftAlignedSmallWave;

        private const int SEGMENT_WIDTH = 6;
        private const int SEGMENT_HEIGHT = 7;
        private const int SEGMENT_GAP = 2;
        private const float VALUE_AREA_X_OFFSET = 155f;

        public SegmentedBarSettingControl(string label, float min, float max, int segments, Func<float> getter, Action<float> setter)
        {
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            Label = label;
            _minValue = min;
            _maxValue = max;
            _segmentCount = segments;
            _getter = getter;
            _setter = setter;

            _savedValue = getter();
            _currentValue = _savedValue;
            _step = (_maxValue - _minValue) / (_segmentCount - 1);
        }

        public string GetCurrentValueAsString() => _currentValue.ToString("F1");
        public string GetSavedValueAsString() => _savedValue.ToString("F1");

        private void SetValue(float newValue)
        {
            newValue = Math.Clamp(newValue, _minValue, _maxValue);
            float snappedValue = _minValue + (float)Math.Round((newValue - _minValue) / _step) * _step;

            if (Math.Abs(_currentValue - snappedValue) > 0.001f)
            {
                _currentValue = snappedValue;
                _setter?.Invoke(_currentValue);
            }
        }

        public void HandleInput(Keys key)
        {
            if (!IsEnabled) return;
            if (key == Keys.Left)
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                SetValue(_currentValue - _step);
            }
            else if (key == Keys.Right)
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                SetValue(_currentValue + _step);
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont valueFont)
        {
            if (!IsEnabled)
            {
                _isDragging = false;
                _hoveredSegmentIndex = -1;
                return;
            }

            CalculateBounds(position, valueFont);

            // Create 8x12 hitboxes for 6x7 visuals
            // Width: 6 -> 8 (+1 left, +1 right)
            // Height: 7 -> 12 (+2 top, +3 bottom)
            Rectangle hitRect = _barAreaRect;
            hitRect.X -= 1;
            hitRect.Width += 2;
            hitRect.Y -= 2;
            hitRect.Height += 5;

            _hoveredSegmentIndex = -1;
            if (hitRect.Contains(virtualMousePos))
            {
                UpdateHoveredSegment(virtualMousePos);
            }

            bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (UIInputManager.CanProcessMouseClick() && leftClickPressed)
            {
                if (hitRect.Contains(virtualMousePos))
                {
                    _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                    _isDragging = true;
                    UpdateValueFromMousePosition(virtualMousePos);
                    UIInputManager.ConsumeMouseClick();
                }
            }

            if (leftClickReleased)
            {
                _isDragging = false;
            }

            if (_isDragging && leftClickHeld)
            {
                UpdateValueFromMousePosition(virtualMousePos);
            }
        }

        private void UpdateHoveredSegment(Vector2 virtualMousePos)
        {
            float relativeMouseX = virtualMousePos.X - _barAreaRect.X;
            int segmentUnitWidth = SEGMENT_WIDTH + SEGMENT_GAP;
            if (segmentUnitWidth <= 0) return;

            // Offset by 1 to center the 8px hitbox on the 6px visual
            _hoveredSegmentIndex = (int)((relativeMouseX + 1) / segmentUnitWidth);
            _hoveredSegmentIndex = Math.Clamp(_hoveredSegmentIndex, 0, _segmentCount - 1);
        }

        private void UpdateValueFromMousePosition(Vector2 virtualMousePos)
        {
            UpdateHoveredSegment(virtualMousePos);
            if (_hoveredSegmentIndex != -1)
            {
                float newValue = _minValue + _hoveredSegmentIndex * _step;
                SetValue(newValue);
            }
        }

        public void Apply()
        {
            _savedValue = _currentValue;
        }

        public void Revert()
        {
            _currentValue = _savedValue;
            _setter?.Invoke(_currentValue);
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
        }

        public void ResetAnimationState()
        {
            HoverAnimator.Reset();
            _waveTimer = 0f;
            _isDragging = false;
            _hoveredSegmentIndex = -1;
        }

        private void CalculateBounds(Vector2 position, BitmapFont font)
        {
            int totalBarWidth = (_segmentCount * SEGMENT_WIDTH) + ((_segmentCount - 1) * SEGMENT_GAP);

            float valueAreaX = position.X + VALUE_AREA_X_OFFSET;
            float valueAreaWidth = Global.VALUE_DISPLAY_WIDTH;
            float barStartX = valueAreaX + (valueAreaWidth - totalBarWidth) / 2;

            _barAreaRect = new Rectangle((int)barStartX, (int)position.Y, totalBarWidth, SEGMENT_HEIGHT);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont labelFont, BitmapFont valueFont, Vector2 position, bool isSelected, GameTime gameTime)
        {
            CalculateBounds(position, labelFont);

            var pixel = ServiceLocator.Get<Texture2D>();
            // FIX: Pass Global tuning values
            float xOffset = HoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled, _global.UI_ButtonHoverLift, _global.UI_ButtonHoverDuration);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.GameTextColor : _global.ButtonDisableColor);

            if (isSelected && IsEnabled)
            {
                _waveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (TextAnimator.IsOneShotEffect(ActiveEffect))
                {
                    float duration = TextAnimator.GetSmallWaveDuration(Label.Length);
                    if (_waveTimer > duration + 0.1f) _waveTimer = 0f;
                }

                TextAnimator.DrawTextWithEffect(spriteBatch, labelFont, Label, animatedPosition, labelColor, ActiveEffect, _waveTimer, Vector2.One);
            }
            else
            {
                _waveTimer = 0f;
                spriteBatch.DrawStringSnapped(labelFont, Label, animatedPosition, labelColor);
            }

            // Shift values 1px right when NOT hovered, so they snap left to "correct" position when hovered
            float valueVisualOffset = (isSelected && IsEnabled) ? 0f : 1f;

            Vector2 barStartPosition = new Vector2(_barAreaRect.X + valueVisualOffset, position.Y + (labelFont.LineHeight - SEGMENT_HEIGHT) / 2);

            float progress = (_currentValue - _minValue) / (_maxValue - _minValue);
            int filledSegments = (int)Math.Round(progress * (_segmentCount - 1)) + 1;

            Color emptyColor = IsEnabled ? _global.DullTextColor : _global.Palette_DarkShadow;
            Color baseFillColor = IsEnabled ? (IsDirty ? _global.ConfirmSettingsColor : _global.GameTextColor) : _global.ButtonDisableColor;
            Color hoverColor = _global.ButtonHoverColor;

            for (int i = 0; i < _segmentCount; i++)
            {
                var segmentRect = new Rectangle(
                    (int)barStartPosition.X + i * (SEGMENT_WIDTH + SEGMENT_GAP),
                    (int)barStartPosition.Y,
                    SEGMENT_WIDTH,
                    SEGMENT_HEIGHT
                );

                Color baseColor = (i < filledSegments) ? baseFillColor : emptyColor;
                spriteBatch.Draw(pixel, segmentRect, baseColor);

                if (isSelected && IsEnabled && _hoveredSegmentIndex != -1)
                {
                    if (i <= _hoveredSegmentIndex)
                    {
                        spriteBatch.Draw(pixel, segmentRect, hoverColor);
                    }
                }
            }

            string valueString = GetCurrentValueAsString();
            Vector2 valueSize = labelFont.MeasureString(valueString);
            // Value text also shifts with the visual offset
            Vector2 valuePosition = new Vector2(_barAreaRect.Left - valueSize.X - 5 + valueVisualOffset, animatedPosition.Y);

            spriteBatch.DrawStringSnapped(labelFont, valueString, valuePosition, IsEnabled ? _global.DullTextColor : _global.ButtonDisableColor);

            if (!IsEnabled)
            {
                float startX = animatedPosition.X;
                float endX = _barAreaRect.Right; // Static end point
                float lineY = animatedPosition.Y + labelFont.LineHeight / 2f;
                spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), _global.ButtonDisableColor);
            }
        }
    }
}