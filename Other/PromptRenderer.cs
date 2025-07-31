using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class PromptRenderer
    {
        private readonly GameState _gameState;
        private readonly WorldClockManager _worldClockManager;
        private readonly Global _global;
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public PromptRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, Vector2 position)
        {
            string statusText = GetStatusText();
            List<string> promptLines = GetPromptLines();

            float y = position.Y - font.LineHeight;

            // Draw prompt lines from bottom up
            if (promptLines.Any())
            {
                for (int i = promptLines.Count - 1; i >= 0; i--)
                {
                    var lineText = promptLines[i];
                    var coloredLine = ParseColoredText(lineText, Color.Khaki);
                    float x = position.X;
                    foreach (var segment in coloredLine.Segments)
                    {
                        spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                        x += font.MeasureString(segment.Text).Width;
                    }
                    y -= Global.PROMPT_LINE_SPACING;
                }
            }

            // Draw status text above the prompt
            if (!string.IsNullOrEmpty(statusText))
            {
                y -= (Global.TERMINAL_LINE_SPACING - Global.PROMPT_LINE_SPACING); // Adjust spacing
                spriteBatch.DrawString(font, statusText, new Vector2(position.X, y), _global.Palette_LightGray);
            }
        }

        private string GetStatusText()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("Actions Queued: ").Append(_gameState.PendingActions.Count);
            if (_gameState.IsExecutingActions)
            {
                _stringBuilder.Append(" | Executing...");
            }
            return _stringBuilder.ToString();
        }

        private List<string> GetPromptLines()
        {
            var lines = new List<string>();
            int moveCount = _gameState.PendingActions.Count(a => a is MoveAction);
            int restCount = _gameState.PendingActions.Count(a => a is RestAction);

            if (_gameState.IsFreeMoveMode && _gameState.PendingActions.Count <= 0)
            {
                lines.Add("[skyblue]Free moving...");
                lines.Add("<[deepskyblue]Use ([royalblue]W[deepskyblue]/[royalblue]A[deepskyblue]/[royalblue]S[deepskyblue]/[royalblue]D[deepskyblue]) to queue moves>");
                lines.Add("[gold]Press[orange] ENTER[gold] to confirm");
                lines.Add("[gold]Press[orange] ESC[gold] to cancel");
            }
            else if (_gameState.PendingActions.Count > 0 && !_gameState.IsExecutingActions)
            {
                if (_gameState.IsFreeMoveMode)
                {
                    lines.Add("[skyblue]Free moving...");
                }
                else
                {
                    lines.Add("[khaki]Previewing action queue...");
                }
                lines.Add("[gold]Press[orange] ENTER[gold] to confirm");
                lines.Add("[gold]Press[orange] ESC[gold] to cancel");

                var details = new List<string>();
                if (moveCount > 0) details.Add($"[orange]{moveCount}[gold] move(s)");
                if (restCount > 0) details.Add($"[green]{restCount}[gold] rest(s)");

                lines.Add($"[gold]Pending[orange] {string.Join(", ", details)}");

                var simResult = _gameState.PendingQueueSimulationResult;
                float secondsPassed = simResult.secondsPassed;

                if (secondsPassed > 0.01f)
                {
                    string finalETA = _worldClockManager.GetCalculatedNewTime(_worldClockManager.CurrentTime, (int)secondsPassed);
                    finalETA = _global.Use24HourClock ? finalETA : _worldClockManager.GetConverted24hToAmPm(finalETA);
                    string formattedDuration = _worldClockManager.GetFormattedTimeFromSecondsShortHand(secondsPassed);
                    lines.Add($"[gold]Arrival Time:[orange] ~{finalETA}");
                    lines.Add($"[Palette_Gray](about {formattedDuration})");
                }
            }
            return lines;
        }

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? _global.InputTextColor;
            var currentText = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    if (currentText.Length > 0)
                    {
                        line.Segments.Add(new ColoredText(currentText, currentColor));
                        currentText = "";
                    }

                    int closeIndex = text.IndexOf(']', i);
                    if (closeIndex != -1)
                    {
                        string colorTag = text.Substring(i + 1, closeIndex - i - 1);
                        i = closeIndex;

                        if (colorTag == "/")
                        {
                            currentColor = _global.InputTextColor;
                        }
                        else if (colorTag == "/o")
                        {
                            currentColor = _global.OutputTextColor;
                        }
                        else
                        {
                            currentColor = ParseColor(colorTag);
                        }
                    }
                    else
                    {
                        currentText += text[i];
                    }
                }
                else
                {
                    currentText += text[i];
                }
            }

            if (currentText.Length > 0)
            {
                line.Segments.Add(new ColoredText(currentText, currentColor));
            }

            return line;
        }

        private Color ParseColor(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "error": return Color.Crimson;
                case "undo": return Color.DarkTurquoise;
                case "cancel": return Color.Orange;
                case "warning": return Color.Gold;
                case "debug": return Color.Chartreuse;
                case "rest": return Color.LightGreen;
                case "dim": return _global.TerminalDarkGray;

                case "palette_black": return _global.Palette_Black;
                case "palette_darkgray": return _global.Palette_DarkGray;
                case "palette_gray": return _global.Palette_Gray;
                case "palette_lightgray": return _global.Palette_LightGray;
                case "palette_white": return _global.Palette_White;
                case "palette_teal": return _global.Palette_Teal;
                case "palette_lightblue": return _global.Palette_LightBlue;
                case "palette_darkblue": return _global.Palette_DarkBlue;
                case "palette_darkgreen": return _global.Palette_DarkGreen;
                case "palette_lightgreen": return _global.Palette_LightGreen;
                case "palette_lightyellow": return _global.Palette_LightYellow;
                case "palette_yellow": return _global.Palette_Yellow;
                case "palette_orange": return _global.Palette_Orange;
                case "palette_red": return _global.Palette_Red;
                case "palette_darkpurple": return _global.Palette_DarkPurple;
                case "palette_lightpurple": return _global.Palette_LightPurple;
                case "palette_pink": return _global.Palette_Pink;
                case "palette_brightwhite": return _global.Palette_BrightWhite;

                case "khaki": return Color.Khaki;
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "cyan": return Color.Cyan;
                case "magenta": return Color.Magenta;
                case "white": return Color.White;
                case "orange": return Color.Orange;
                case "gray":
                case "grey": return Color.Gray;
            }

            try
            {
                var colorProperty = typeof(Color).GetProperty(colorName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);

                if (colorProperty != null && colorProperty.PropertyType == typeof(Color))
                {
                    return (Color)colorProperty.GetValue(null);
                }
            }
            catch { /* Fallback on failure */ }

            return _global.GameTextColor;
        }
    }
}