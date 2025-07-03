using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.UI
{
    public class Slider
    {
        public Rectangle Bounds { get; }
        public string Label { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
        public float Step { get; }
        public float CurrentValue { get; private set; }

        private Rectangle _handleBounds;
        private bool _isDragging;
        private readonly int _handleWidth = 8;
        private readonly int _handleHeight = 15;

        public event Action<float> OnValueChanged;

        public Slider(Rectangle bounds, string label, float minValue, float maxValue, float initialValue, float step = 1f)
        {
            Bounds = bounds;
            Label = label;
            MinValue = minValue;
            MaxValue = maxValue;
            Step = step;
            SetValue(initialValue);
        }

        public void SetValue(float value)
        {
            float previousValue = CurrentValue;
            CurrentValue = Math.Clamp(value, MinValue, MaxValue);
            if (Step > 0)
            {
                CurrentValue = (float)Math.Round(CurrentValue / Step) * Step;
            }

            if (CurrentValue != previousValue)
            {
                OnValueChanged?.Invoke(CurrentValue);
            }
        }

        public void Update(MouseState currentMouseState, MouseState previousMouseState)
        {
            Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            bool isHoveringHandle = _handleBounds.Contains(virtualMousePos);
            bool isHoveringTrack = Bounds.Contains(virtualMousePos);

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                if (isHoveringHandle || isHoveringTrack)
                {
                    _isDragging = true;
                }
            }

            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                float mouseX = Math.Clamp(virtualMousePos.X, Bounds.X, Bounds.Right);
                float progress = (mouseX - Bounds.X) / Bounds.Width;
                float newValue = MinValue + (MaxValue - MinValue) * progress;
                SetValue(newValue);
            }

            UpdateHandlePosition();
        }

        private void UpdateHandlePosition()
        {
            if (MaxValue - MinValue == 0) return;
            float progress = (CurrentValue - MinValue) / (MaxValue - MinValue);
            int handleX = (int)(Bounds.X + progress * Bounds.Width - (_handleWidth / 2f));
            _handleBounds = new Rectangle(handleX, Bounds.Y + (Bounds.Height - _handleHeight) / 2, _handleWidth, _handleHeight);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            // Draw Label
            spriteBatch.DrawString(font, Label, new Vector2(Bounds.X, Bounds.Y - font.LineHeight - 2), Global.Instance.Palette_White);

            // Draw Value
            string valueString = CurrentValue.ToString("F0");
            Vector2 valueSize = font.MeasureString(valueString);
            spriteBatch.DrawString(font, valueString, new Vector2(Bounds.Right - valueSize.X, Bounds.Y - font.LineHeight - 2), Global.Instance.Palette_BrightWhite);

            // Draw the slider rail line
            var railRect = new Rectangle(Bounds.X, Bounds.Y + (Bounds.Height / 2) - 1, Bounds.Width, 2);
            spriteBatch.Draw(Core.Pixel, railRect, Global.Instance.Palette_Gray);

            // Draw Handle
            UpdateHandlePosition();
            spriteBatch.Draw(Core.Pixel, _handleBounds, Global.Instance.Palette_BrightWhite);
        }
    }
}