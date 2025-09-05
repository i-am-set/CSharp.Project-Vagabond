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

        // --- UI State for Mouse Interaction ---
        private Rectangle _barAreaRect;
        private bool _isDragging;
        private int _hoveredSegmentIndex = -1;
        private readonly HoverAnimator _hoverAnimator = new HoverAnimator();
        public HoverAnimator HoverAnimator => _hoverAnimator;

        // --- Visual Tuning Constants ---
        private const int SEGMENT_WIDTH = 6;
        private const int SEGMENT_HEIGHT = 8;
        private const int SEGMENT_GAP = 2;

        public SegmentedBarSettingControl(string label, float min, float max, int segments, Func<float> getter, Action<float> setter)
        {
            _global = ServiceLocator.Get<Global>();
            Label = label;
            _minValue = min;
            _maxValue = max;
            _segmentCount = segments;
            _getter = getter;
            _setter = setter;

            _savedValue = getter();
            _currentValue = _savedValue;
            // An 11-segment bar represents 11 distinct values, so there are 10 "steps" between them.
            _step = (_maxValue - _minValue) / (_segmentCount - 1);
        }

        public string GetCurrentValueAsString() => _currentValue.ToString("F1");
        public string GetSavedValueAsString() => _savedValue.ToString("F1");

        private void SetValue(float newValue)
        {
            // Clamp the value to the min/max range
            newValue = Math.Clamp(newValue, _minValue, _maxValue);
            // Snap the value to the nearest valid step
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
                SetValue(_currentValue - _step);
            }
            else if (key == Keys.Right)
            {
                SetValue(_currentValue + _step);
            }
        }

        public void Update(Vector2 position, bool isSelected, MouseState currentMouseState, MouseState previousMouseState, Vector2 virtualMousePos, BitmapFont font)
        {
            if (!IsEnabled)
            {
                _isDragging = false;
                _hoveredSegmentIndex = -1;
                return;
            }

            // Calculate the bounds of the interactive elements for this frame
            CalculateBounds(position, font);

            _hoveredSegmentIndex = -1; // Reset hover index each frame
            if (_barAreaRect.Contains(virtualMousePos))
            {
                UpdateHoveredSegment(virtualMousePos);
            }

            bool leftClickPressed = currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickHeld = currentMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickReleased = currentMouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (UIInputManager.CanProcessMouseClick() && leftClickPressed)
            {
                if (_barAreaRect.Contains(virtualMousePos))
                {
                    _isDragging = true;
                    UpdateValueFromMousePosition(virtualMousePos); // Update on initial click
                    UIInputManager.ConsumeMouseClick();
                }
            }

            if (leftClickReleased)
            {
                _isDragging = false;
            }

            if (_isDragging && leftClickHeld)
            {
                UpdateValueFromMousePosition(virtualMousePos); // Update while dragging
            }
        }

        private void UpdateHoveredSegment(Vector2 virtualMousePos)
        {
            float relativeMouseX = virtualMousePos.X - _barAreaRect.X;
            int segmentUnitWidth = SEGMENT_WIDTH + SEGMENT_GAP;
            if (segmentUnitWidth <= 0) return;

            _hoveredSegmentIndex = (int)(relativeMouseX / segmentUnitWidth);
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
            _isDragging = false;
            _hoveredSegmentIndex = -1;
        }

        private void CalculateBounds(Vector2 position, BitmapFont font)
        {
            int totalBarWidth = (_segmentCount * SEGMENT_WIDTH) + ((_segmentCount - 1) * SEGMENT_GAP);
            const float valueAreaXOffset = 175f;

            // Center the bar within the standard value area used by other controls.
            float valueAreaX = position.X + valueAreaXOffset;
            float valueAreaWidth = Global.VALUE_DISPLAY_WIDTH;
            float barStartX = valueAreaX + (valueAreaWidth - totalBarWidth) / 2;

            _barAreaRect = new Rectangle((int)barStartX, (int)position.Y, totalBarWidth, font.LineHeight);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            // Recalculate bounds every frame to ensure correct positioning
            CalculateBounds(position, font);

            var pixel = ServiceLocator.Get<Texture2D>();
            float xOffset = HoverAnimator.UpdateAndGetOffset(gameTime, isSelected && IsEnabled);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.Palette_BrightWhite : _global.ButtonDisableColor);
            spriteBatch.DrawString(font, Label, animatedPosition, labelColor);

            // --- Segmented Bar Drawing Logic ---
            Vector2 barStartPosition = new Vector2(_barAreaRect.X + xOffset, position.Y + (font.LineHeight - SEGMENT_HEIGHT) / 2);

            float progress = (_currentValue - _minValue) / (_maxValue - _minValue);
            int filledSegments = (int)Math.Round(progress * (_segmentCount - 1)) + 1;

            Color emptyColor = IsEnabled ? _global.Palette_DarkGray : new Color(40, 40, 40);
            Color baseFillColor = IsEnabled ? (IsDirty ? _global.Palette_Teal : _global.Palette_BrightWhite) : _global.ButtonDisableColor;
            Color hoverColor = _global.ButtonHoverColor;

            // Draw the segments
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

            // --- Numeric Value ---
            string valueString = GetCurrentValueAsString();
            Vector2 valueSize = font.MeasureString(valueString);
            Vector2 valuePosition = new Vector2(_barAreaRect.Left - valueSize.X - 5 + xOffset, animatedPosition.Y);
            spriteBatch.DrawString(font, valueString, valuePosition, IsEnabled ? _global.Palette_DarkGray : _global.ButtonDisableColor);
        }
    }
}
