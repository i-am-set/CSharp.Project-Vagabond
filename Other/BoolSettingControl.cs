using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class BoolSettingControl : ISettingControl
    {
        private readonly Global _global;
        private readonly HapticsManager _hapticsManager;

        public string Label { get; }
        public bool IsDirty => _currentValue != _savedValue;
        public bool IsEnabled { get; set; } = true;
        public bool IsSelected { get; set; }
        public Rectangle Bounds { get; private set; }

        public ISelectable? NeighborUp { get; set; }
        public ISelectable? NeighborDown { get; set; }
        public ISelectable? NeighborLeft { get; set; }
        public ISelectable? NeighborRight { get; set; }

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

        private float _waveTimer = 0f;
        private const TextEffectType ActiveEffect = TextEffectType.LeftAlignedSmallWave;

        private const float VALUE_AREA_X_OFFSET = 155f;

        public BoolSettingControl(string label, Func<bool> getter, Action<bool> onApply)
        {
            _global = ServiceLocator.Get<Global>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            Label = label;
            _getter = getter;
            _savedValue = getter();
            _currentValue = _savedValue;
            _onApply = onApply;
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
            ToggleValue();
        }

        private void ToggleValue()
        {
            _currentValue = !_currentValue;
            _onApply?.Invoke(_currentValue);
        }

        public string GetCurrentValueAsString() => _currentValue ? "ON" : "OFF";
        public string GetSavedValueAsString() => _savedValue ? "ON" : "OFF";

        public bool HandleInput(InputManager input)
        {
            if (!IsEnabled) return false;
            if (input.NavigateLeft || input.NavigateRight || input.Confirm)
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                ToggleValue();
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

            // Approximate total bounds
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
                if (_isLeftArrowHovered || _isRightArrowHovered)
                {
                    _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                    ToggleValue();
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
        }

        public void RefreshValue()
        {
            _currentValue = _getter();
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

            Color labelColor = IsSelected && IsEnabled ? _global.ButtonHoverColor : (IsEnabled ? _global.GameTextColor : _global.ButtonDisableColor);

            if (IsSelected && IsEnabled)
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

            const float valueDisplayWidth = Global.VALUE_DISPLAY_WIDTH;

            float valueVisualOffset = (IsSelected && IsEnabled) ? 0f : 1f;
            Vector2 valueAreaPosition = new Vector2(position.X + VALUE_AREA_X_OFFSET + valueVisualOffset, position.Y);

            string leftArrowText = "<";
            string valueText = _currentValue ? "ON" : "OFF";
            string rightArrowText = ">";

            Color baseValueColor = IsEnabled ? (IsDirty ? _global.ConfirmSettingsColor : _global.GameTextColor) : _global.ButtonDisableColor;
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
            float textX = valueDrawPos.X + leftArrowSize.X + (spaceBetweenArrows - valueTextSize.X) * 0.5f;
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