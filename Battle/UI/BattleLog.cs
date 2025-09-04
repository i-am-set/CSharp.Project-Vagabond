using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class BattleLog
    {
        private readonly Global _global;
        private readonly List<ColoredLine> _unwrappedHistory = new List<ColoredLine>();
        private List<ColoredLine> _wrappedHistory = new List<ColoredLine>();
        private bool _historyDirty = true;
        private Rectangle _bounds;

        public BattleLog(Rectangle bounds)
        {
            _global = ServiceLocator.Get<Global>();
            _bounds = bounds;
        }

        public void AddMessage(string message)
        {
            var coloredLine = ParseColoredText(message, _global.OutputTextColor);
            _unwrappedHistory.Add(coloredLine);
            if (_unwrappedHistory.Count > 100) // Keep log from getting too large
            {
                _unwrappedHistory.RemoveAt(0);
            }
            _historyDirty = true;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (_historyDirty)
            {
                ReWrapHistory(font);
                _historyDirty = false;
            }

            int maxVisibleLines = _bounds.Height / Global.TERMINAL_LINE_SPACING;
            int totalLines = _wrappedHistory.Count;
            int lastHistoryIndexToDraw = totalLines - 1;
            float lastScreenLineY = (_bounds.Bottom) - Global.TERMINAL_LINE_SPACING;

            for (int i = 0; i < maxVisibleLines; i++)
            {
                int historyIndex = lastHistoryIndexToDraw - i;
                if (historyIndex < 0) break;

                float y = lastScreenLineY - i * Global.TERMINAL_LINE_SPACING;
                if (y < _bounds.Y) continue;

                float x = _bounds.X + 5;
                var line = _wrappedHistory[historyIndex];

                foreach (var segment in line.Segments)
                {
                    spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                    x += font.MeasureString(segment.Text).Width;
                }
            }
        }

        private void ReWrapHistory(BitmapFont font)
        {
            _wrappedHistory.Clear();
            float wrapWidth = _bounds.Width - 10;
            foreach (var line in _unwrappedHistory)
            {
                _wrappedHistory.AddRange(WrapColoredText(line, wrapWidth, font));
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

            var currentLine = new ColoredLine();
            var currentLineText = new StringBuilder();

            foreach (var segment in line.Segments)
            {
                var words = segment.Text.Split(' ');
                foreach (var word in words)
                {
                    if (string.IsNullOrEmpty(word)) continue;

                    string testWord = (currentLineText.Length > 0 ? " " : "") + word;
                    float potentialWidth = font.MeasureString(currentLineText.ToString() + testWord).Width;

                    if (potentialWidth > maxWidthInPixels)
                    {
                        wrappedLines.Add(currentLine);
                        currentLine = new ColoredLine();
                        currentLineText.Clear();
                        currentLine.Segments.Add(new ColoredText(word, segment.Color));
                        currentLineText.Append(word);
                    }
                    else
                    {
                        if (currentLine.Segments.Any() && currentLine.Segments.Last().Color == segment.Color)
                        {
                            currentLine.Segments.Last().Text += testWord;
                        }
                        else
                        {
                            currentLine.Segments.Add(new ColoredText(testWord, segment.Color));
                        }
                        currentLineText.Append(testWord);
                    }
                }
            }

            if (currentLine.Segments.Any())
            {
                wrappedLines.Add(currentLine);
            }

            return wrappedLines;
        }

        private ColoredLine ParseColoredText(string text, Color baseColor)
        {
            // This is a simplified parser for the battle log.
            // It doesn't need the full complexity of the terminal's parser.
            var line = new ColoredLine();
            line.Segments.Add(new ColoredText(text, baseColor));
            return line;
        }
    }
}
