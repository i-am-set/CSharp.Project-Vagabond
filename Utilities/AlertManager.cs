using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages the display of temporary, non-blocking alert messages at the top of the screen.
    /// </summary>
    public class AlertManager
    {
        private class AlertState
        {
            public string Text;
            public Vector2 CurrentPosition;
            public Vector2 TargetPosition;
            public float Timer;
            public const float HOLD_DURATION = 2.0f;
        }

        private readonly List<AlertState> _activeAlerts = new List<AlertState>();
        private readonly Global _global;
        private const int MAX_ALERTS = 3;

        public AlertManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Reset()
        {
            _activeAlerts.Clear();
        }

        public void StartAlert(string message)
        {
            if (_activeAlerts.Count >= MAX_ALERTS)
            {
                // Remove the oldest alert to make room for the new one.
                _activeAlerts.RemoveAt(0);
            }

            var alert = new AlertState
            {
                Text = message.ToUpper(),
                Timer = 0f,
            };

            _activeAlerts.Add(alert);
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            const int paddingY = 1;
            const int gap = 2;

            // Recalculate all target positions every frame for stacking.
            float yStackOffset = 0;
            for (int i = _activeAlerts.Count - 1; i >= 0; i--)
            {
                var alert = _activeAlerts[i];
                alert.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, yStackOffset);

                Vector2 textSize = font.MeasureString(alert.Text);
                int boxHeight = (int)textSize.Y + paddingY * 2;
                yStackOffset += boxHeight + gap;
            }

            for (int i = _activeAlerts.Count - 1; i >= 0; i--)
            {
                var alert = _activeAlerts[i];
                alert.Timer += deltaTime;

                if (alert.Timer >= AlertState.HOLD_DURATION)
                {
                    _activeAlerts.RemoveAt(i);
                    continue;
                }

                // Snap position directly to the target position.
                alert.CurrentPosition = alert.TargetPosition;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var font = ServiceLocator.Get<Core>().SecondaryFont;

            foreach (var alert in _activeAlerts)
            {
                Vector2 textSize = font.MeasureString(alert.Text);
                var textPosition = new Vector2(
                    alert.CurrentPosition.X - textSize.X / 2f,
                    alert.CurrentPosition.Y
                );

                spriteBatch.DrawStringOutlinedSnapped(font, alert.Text, textPosition, _global.AlertColor, _global.Palette_Black);
            }
        }
    }
}