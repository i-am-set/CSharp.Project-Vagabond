using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    /// <summary>
    /// A UI panel responsible for displaying messages from the CombatLog within a defined boundary.
    /// </summary>
    public class CombatLogPanel
    {
        private readonly List<ColoredLine> _wrappedMessages = new List<ColoredLine>();
        private readonly Rectangle _bounds;
        private const int MAX_LOG_LINES = 100;
        private const int PADDING = 5;
        private const int BORDER_THICKNESS = 2;

        public CombatLogPanel(Rectangle bounds)
        {
            _bounds = bounds;
            CombatLog.OnMessageLogged += HandleMessageLogged;
        }

        private void HandleMessageLogged(string message)
        {
            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            var coloredLine = ParseColoredText(message, Global.Instance.OutputTextColor);
            // Use the bounds width minus padding on both sides for wrapping calculations.
            var wrappedLines = WrapColoredText(coloredLine, _bounds.Width - (PADDING * 2));

            _wrappedMessages.AddRange(wrappedLines);

            // Trim the log if it gets too long
            while (_wrappedMessages.Count > MAX_LOG_LINES)
            {
                _wrappedMessages.RemoveAt(0);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var font = Global.Instance.DefaultFont;
            if (font == null) return;

            // Draw the border first (a slightly larger rectangle behind the background)
            var borderRect = new Rectangle(
                _bounds.X - BORDER_THICKNESS,
                _bounds.Y - BORDER_THICKNESS,
                _bounds.Width + (BORDER_THICKNESS * 2),
                _bounds.Height + (BORDER_THICKNESS * 2)
            );
            spriteBatch.Draw(Core.Pixel, borderRect, Global.Instance.Palette_White);

            // Draw the background for the log panel
            spriteBatch.Draw(Core.Pixel, _bounds, Global.Instance.TerminalBg);

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

        #region Text Parsing and Wrapping (Adapted from TerminalRenderer)

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? Global.Instance.InputTextColor;
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
                            currentColor = baseColor ?? Global.Instance.InputTextColor;
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
                case "dim": return Global.Instance.TerminalDarkGray;
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
                    return Global.Instance.GameTextColor;
            }
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, float maxWidthInPixels)
        {
            var wrappedLines = new List<ColoredLine>();
            var font = Global.Instance.DefaultFont;

            var currentLine = new ColoredLine { LineNumber = line.LineNumber };
            float currentLineWidth = 0f;

            Action finishLine = () =>
            {
                if (currentLine.Segments.Any())
                {
                    wrappedLines.Add(currentLine);
                }
                currentLine = new ColoredLine { LineNumber = 0 };
                currentLineWidth = 0f;
            };

            void AddTokenToLine(string token, Color color)
            {
                float tokenWidth = font.MeasureString(token).Width;
                bool isWhitespace = string.IsNullOrWhiteSpace(token);

                if (!isWhitespace && currentLineWidth > 0 && currentLineWidth + tokenWidth > maxWidthInPixels)
                {
                    finishLine();
                }

                if (!isWhitespace && tokenWidth > maxWidthInPixels)
                {
                    if (currentLineWidth > 0) finishLine();

                    string remainingToken = token;
                    while (remainingToken.Length > 0)
                    {
                        int charsThatFit = 0;
                        for (int i = 1; i <= remainingToken.Length; i++)
                        {
                            if (font.MeasureString(remainingToken.Substring(0, i)).Width > maxWidthInPixels)
                            {
                                break;
                            }
                            charsThatFit = i;
                        }
                        if (charsThatFit == 0 && remainingToken.Length > 0) charsThatFit = 1;

                        string part = remainingToken.Substring(0, charsThatFit);
                        remainingToken = remainingToken.Substring(charsThatFit);

                        var newLine = new ColoredLine { LineNumber = 0 };
                        newLine.Segments.Add(new ColoredText(part, color));
                        wrappedLines.Add(newLine);
                    }
                    currentLineWidth = 0;
                    return;
                }

                if (currentLine.Segments.Count > 0 && currentLine.Segments.Last().Color == color)
                {
                    currentLine.Segments.Last().Text += token;
                }
                else
                {
                    currentLine.Segments.Add(new ColoredText(token, color));
                }
                currentLineWidth += tokenWidth;
            }

            foreach (var segment in line.Segments)
            {
                string text = segment.Text;
                Color color = segment.Color;

                var tokens = Regex.Split(text, @"(\s+)");

                foreach (var token in tokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;

                    if (token.Contains('\n'))
                    {
                        string[] subTokens = token.Split('\n');
                        for (int i = 0; i < subTokens.Length; i++)
                        {
                            string subToken = subTokens[i].Replace("\r", "");
                            if (!string.IsNullOrEmpty(subToken))
                            {
                                AddTokenToLine(subToken, color);
                            }

                            if (i < subTokens.Length - 1)
                            {
                                finishLine();
                            }
                        }
                        continue;
                    }

                    AddTokenToLine(token, color);
                }
            }

            if (currentLine.Segments.Any())
            {
                wrappedLines.Add(currentLine);
            }

            if (!wrappedLines.Any())
            {
                wrappedLines.Add(new ColoredLine { LineNumber = line.LineNumber });
            }
            else
            {
                wrappedLines[0].LineNumber = line.LineNumber;
            }

            return wrappedLines;
        }

        #endregion
    }
}