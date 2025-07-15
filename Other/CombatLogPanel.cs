using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel responsible for displaying combat log messages within a defined boundary.
    /// </summary>
    public class CombatLogPanel
    {
        private readonly Global _global;
        private readonly Rectangle _bounds;

        private readonly List<ColoredLine> _unwrappedMessages = new List<ColoredLine>();
        private List<ColoredLine> _wrappedMessages = new List<ColoredLine>();
        private bool _isDirty = true;

        private const int MAX_LOG_LINES = 100;
        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;

        public CombatLogPanel(Rectangle bounds)
        {
            _bounds = bounds;
            _global = ServiceLocator.Get<Global>();
            EventBus.Subscribe<GameEvents.CombatLogMessagePublished>(HandleMessageLogged);
        }

        private void HandleMessageLogged(GameEvents.CombatLogMessagePublished e)
        {
            // Prepend the carat and color tags to the original message string.
            string prefixedMessage = $"[dim]> [/]{e.Message}";
            var coloredLine = ParseColoredText(prefixedMessage, _global.OutputTextColor);
            _unwrappedMessages.Add(coloredLine);

            while (_unwrappedMessages.Count > MAX_LOG_LINES)
            {
                _unwrappedMessages.RemoveAt(0);
            }
            _isDirty = true;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (font == null) return;

            if (_isDirty)
            {
                ReWrapMessages(font);
            }

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Draw the border first (a slightly larger rectangle behind the background)
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(pixel, borderRect, _global.Palette_White);

            // Draw the background for the log panel
            spriteBatch.Draw(pixel, _bounds, _global.TerminalBg);

            // --- Draw Text ---
            int lineHeight = Global.TERMINAL_LINE_SPACING;
            int maxVisibleLines = (_bounds.Height - (PADDING * 2)) / lineHeight;

            float startY = _bounds.Bottom - PADDING - lineHeight;
            int linesToDraw = System.Math.Min(maxVisibleLines, _wrappedMessages.Count);

            for (int i = 0; i < linesToDraw; i++)
            {
                int messageIndex = _wrappedMessages.Count - 1 - i;
                if (messageIndex < 0) break;

                var line = _wrappedMessages[messageIndex];
                float currentX = _bounds.Left + PADDING;
                float currentY = startY - (i * lineHeight);

                // Ensure text isn't drawn outside the panel's vertical bounds
                if (currentY < _bounds.Top) break;

                foreach (var segment in line.Segments)
                {
                    spriteBatch.DrawString(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                    currentX += font.MeasureString(segment.Text).Width;
                }
            }
        }

        private void ReWrapMessages(BitmapFont font)
        {
            _wrappedMessages.Clear();
            float wrapWidth = _bounds.Width - (PADDING * 2);
            foreach (var line in _unwrappedMessages)
            {
                _wrappedMessages.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _isDirty = false;
        }

        #region Text Parsing and Wrapping (Adapted from TerminalRenderer)

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
                            currentColor = baseColor ?? _global.InputTextColor;
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
                default:
                    try
                    {
                        var colorProperty = typeof(Color).GetProperty(colorName,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);

                        if (colorProperty != null && colorProperty.PropertyType == typeof(Color))
                        {
                            return (Color)colorProperty.GetValue(null);
                        }
                    }
                    catch { /* Fallback */ }
                    return _global.GameTextColor;
            }
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, float maxWidthInPixels, BitmapFont font)
        {
            var wrappedLines = new List<ColoredLine>();
            if (!line.Segments.Any())
            {
                wrappedLines.Add(line);
                return wrappedLines;
            }

            var currentLine = new ColoredLine { LineNumber = line.LineNumber };
            var currentLineText = new StringBuilder();
            bool isFirstLineOfMessage = true; // Flag to track the first line of a given message.

            Action finishCurrentLine = () =>
            {
                if (currentLine.Segments.Any())
                {
                    wrappedLines.Add(currentLine);
                }
                currentLine = new ColoredLine { LineNumber = 0 };
                currentLineText.Clear();

                // If we just finished the first line, subsequent lines are not the first.
                if (isFirstLineOfMessage)
                {
                    isFirstLineOfMessage = false;
                }

                // Add indentation to the start of the new line if it's a wrapped line.
                if (!isFirstLineOfMessage)
                {
                    string indent = " ";
                    // Use a neutral color for the indentation space.
                    currentLine.Segments.Add(new ColoredText(indent, _global.OutputTextColor));
                    currentLineText.Append(indent);
                }
            };

            foreach (var segment in line.Segments)
            {
                string processedText = segment.Text.Replace("\r", "");
                var tokens = Regex.Split(processedText, @"(\s+|\n)");

                foreach (string token in tokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;

                    if (token == "\n")
                    {
                        finishCurrentLine();
                        continue;
                    }

                    float potentialWidth = font.MeasureString(currentLineText.ToString() + token).Width;
                    bool isWhitespace = string.IsNullOrWhiteSpace(token);

                    if (currentLineText.Length > 0 && !isWhitespace && potentialWidth > maxWidthInPixels)
                    {
                        finishCurrentLine();
                    }

                    // Add the token to the now-current line.
                    // Merge with the last segment if colors match.
                    if (currentLine.Segments.Any() && currentLine.Segments.Last().Color == segment.Color)
                    {
                        currentLine.Segments.Last().Text += token;
                    }
                    else // Otherwise, create a new segment.
                    {
                        currentLine.Segments.Add(new ColoredText(token, segment.Color));
                    }
                    // Append to our text tracker for measurement.
                    currentLineText.Append(token);
                }
            }

            // Add the last line if it has any content.
            if (currentLine.Segments.Any())
            {
                wrappedLines.Add(currentLine);
            }

            // Ensure we always return at least one line, even if it's empty.
            if (!wrappedLines.Any())
            {
                wrappedLines.Add(new ColoredLine { LineNumber = line.LineNumber });
            }

            return wrappedLines;
        }

        #endregion
    }
}