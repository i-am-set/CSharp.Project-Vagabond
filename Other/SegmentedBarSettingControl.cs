﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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

        // --- UI State for Mouse Interaction ---
        private Rectangle _barAreaRect;
        private bool _isDragging;
        private readonly HoverAnimator _hoverAnimator = new HoverAnimator();

        // --- Visual Tuning Constants ---
        private const int SEGMENT_WIDTH = 8;
        private const int SEGMENT_HEIGHT = 10;
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
            // Calculate the bounds of the interactive elements for this frame
            CalculateBounds(position, font);

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

        /// <summary>
        /// Calculates the correct value based on which segment hitbox the mouse is over.
        /// </summary>
        private void UpdateValueFromMousePosition(Vector2 virtualMousePos)
        {
            // The Y position of the segments, centered within the row.
            int segmentY = _barAreaRect.Y + (_barAreaRect.Height - SEGMENT_HEIGHT) / 2;

            // Loop through each segment to find which one is being clicked/dragged over.
            for (int i = 0; i < _segmentCount; i++)
            {
                // Calculate the visual rectangle for the current segment.
                var segmentRect = new Rectangle(
                    _barAreaRect.X + i * (SEGMENT_WIDTH + SEGMENT_GAP),
                    segmentY,
                    SEGMENT_WIDTH,
                    SEGMENT_HEIGHT
                );

                // Create a slightly larger hitbox for easier clicking.
                var hitboxRect = segmentRect;
                hitboxRect.Inflate(1, 1);

                if (hitboxRect.Contains(virtualMousePos))
                {
                    // If the mouse is over this segment, calculate the corresponding value and set it.
                    float newValue = _minValue + i * _step;
                    SetValue(newValue);
                    break; // Found the correct segment, no need to check others.
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
            _setter?.Invoke(_currentValue);
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
        }

        private void CalculateBounds(Vector2 position, BitmapFont font)
        {
            int totalBarWidth = (_segmentCount * SEGMENT_WIDTH) + ((_segmentCount - 1) * SEGMENT_GAP);

            // Center the bar within the standard value area used by other controls.
            float valueAreaX = position.X + 340;
            float valueAreaWidth = Global.VALUE_DISPLAY_WIDTH;
            float barStartX = valueAreaX + (valueAreaWidth - totalBarWidth) / 2;

            _barAreaRect = new Rectangle((int)barStartX, (int)position.Y, totalBarWidth, font.LineHeight);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position, bool isSelected, GameTime gameTime)
        {
            // Recalculate bounds every frame to ensure correct positioning
            CalculateBounds(position, font);

            var pixel = ServiceLocator.Get<Texture2D>();
            float xOffset = _hoverAnimator.UpdateAndGetOffset(gameTime, isSelected);
            Vector2 animatedPosition = new Vector2(position.X + xOffset, position.Y);

            Color labelColor = isSelected ? _global.ButtonHoverColor : _global.Palette_BrightWhite;
            spriteBatch.DrawString(font, Label, animatedPosition, labelColor);

            // --- Segmented Bar Drawing Logic ---
            Vector2 barStartPosition = new Vector2(_barAreaRect.X + xOffset, position.Y + (font.LineHeight - SEGMENT_HEIGHT) / 2);

            float progress = (_currentValue - _minValue) / (_maxValue - _minValue);
            int filledSegments = (int)Math.Round(progress * (_segmentCount - 1)) + 1;

            Color emptyColor = _global.Palette_DarkGray;
            Color fillColor = IsDirty ? _global.Palette_Teal : _global.Palette_BrightWhite;
            if (isSelected)
            {
                fillColor = _global.ButtonHoverColor;
            }

            // Draw the segments
            for (int i = 0; i < _segmentCount; i++)
            {
                var segmentRect = new Rectangle(
                    (int)barStartPosition.X + i * (SEGMENT_WIDTH + SEGMENT_GAP),
                    (int)barStartPosition.Y,
                    SEGMENT_WIDTH,
                    SEGMENT_HEIGHT
                );
                Color segmentColor = (i < filledSegments) ? fillColor : emptyColor;
                spriteBatch.Draw(pixel, segmentRect, segmentColor);
            }

            // --- Numeric Value ---
            string valueString = GetCurrentValueAsString();
            // Position the numeric value to the far right, aligned with the resolution's extra text.
            Vector2 valuePosition = new Vector2(position.X + 460, position.Y);
            spriteBatch.DrawString(font, valueString, valuePosition, _global.Palette_DarkGray);
        }
    }
}
