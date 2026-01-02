using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond
{
    public class TerminalRenderer
    {
        // Injected Dependencies
        private readonly GameState _gameState;
        private readonly HapticsManager _hapticsManager;
        private readonly Global _global;
        private AutoCompleteManager _autoCompleteManager; // Lazy loaded
                                                          // History State
        private readonly List<string> _inputHistory = new List<string>();
        private readonly List<ColoredLine> _unwrappedHistory = new List<ColoredLine>();
        private readonly List<ColoredLine> _unwrappedCombatHistory = new List<ColoredLine>();
        private List<ColoredLine> _wrappedHistory = new List<ColoredLine>();
        private List<ColoredLine> _wrappedCombatHistory = new List<ColoredLine>();
        private bool _historyDirty = true;
        private bool _combatHistoryDirty = true;

        public int ScrollOffset = 0;
        public int CombatScrollOffset = 0;

        private int _nextLineNumber = 1;
        private int _nextCombatLineNumber = 1;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        public List<ColoredLine> WrappedHistory => _wrappedHistory;
        private Rectangle _currentBounds;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public TerminalRenderer()
        {
            // Acquire dependencies from the ServiceLocator
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _global = ServiceLocator.Get<Global>();

            // Subscribe to events for decoupled communication
            EventBus.Subscribe<GameEvents.TerminalMessagePublished>(OnTerminalMessagePublished);
        }

        private void OnTerminalMessagePublished(GameEvents.TerminalMessagePublished e)
        {
            AddToHistory(e.Message, e.BaseColor);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// The core, shared logic for adding a line to a history buffer.
        /// </summary>
        private void AddLineToHistoryInternal(string message, Color? baseColor,
                                              List<ColoredLine> unwrappedHistory,
                                              ref bool historyDirty,
                                              ref int scrollOffset,
                                              ref int nextLineNumber)
        {
            if (nextLineNumber > 999)
            {
                // Simple truncation logic for the given history buffer
                int desiredLinesToKeep = GetMaxVisibleLines();
                int startIndex = Math.Max(0, unwrappedHistory.Count - desiredLinesToKeep);

                var keptLines = unwrappedHistory.GetRange(startIndex, unwrappedHistory.Count - startIndex);
                unwrappedHistory.Clear();
                scrollOffset = 0;

                var truncationMessage = ParseColoredText("--- HISTORY TRUNCATED ---", _global.Palette_Gray);
                truncationMessage.LineNumber = 1;
                unwrappedHistory.Add(truncationMessage);
                unwrappedHistory.AddRange(keptLines);

                int currentLineNum = 2;
                for (int i = 1; i < unwrappedHistory.Count; i++)
                {
                    if (unwrappedHistory[i].LineNumber > 0)
                    {
                        unwrappedHistory[i].LineNumber = currentLineNum++;
                    }
                }
                nextLineNumber = currentLineNum;
            }

            var coloredLine = ParseColoredText(message, baseColor ?? _global.OutputTextColor);
            coloredLine.LineNumber = nextLineNumber++;
            unwrappedHistory.Add(coloredLine);
            historyDirty = true;

            while (unwrappedHistory.Count > Global.MAX_HISTORY_LINES)
            {
                unwrappedHistory.RemoveAt(0);
            }
        }

        private void AddToHistory(string message, Color? baseColor = null)
        {
            _inputHistory.Add(message);
            if (_inputHistory.Count > 50)
            {
                _inputHistory.RemoveAt(0);
            }

            AddLineToHistoryInternal(message, baseColor, _unwrappedHistory, ref _historyDirty, ref ScrollOffset, ref _nextLineNumber);
        }

        private void AddToCombatHistory(string message, Color? baseColor = null)
        {
            AddLineToHistoryInternal(message, baseColor, _unwrappedCombatHistory, ref _combatHistoryDirty, ref CombatScrollOffset, ref _nextCombatLineNumber);
        }

        public void ClearHistory()
        {
            _inputHistory.Clear();
            _unwrappedHistory.Clear();
            _wrappedHistory.Clear();
            ScrollOffset = 0;
            _nextLineNumber = 1;
            _historyDirty = true;
        }

        private void ClearCombatHistory()
        {
            _unwrappedCombatHistory.Clear();
            _wrappedCombatHistory.Clear();
            CombatScrollOffset = 0;
            _nextCombatLineNumber = 1; // Reset the combat line counter
            _combatHistoryDirty = true;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void DrawTerminal(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Rectangle bounds)
        {
            _currentBounds = bounds;
            // Lazy-load dependencies to break initialization cycles
            _autoCompleteManager ??= ServiceLocator.Get<AutoCompleteManager>();

            // Re-wrap text only when the history has changed, using the provided font.
            if (_historyDirty)
            {
                ReWrapHistory(font);
            }
            if (_combatHistoryDirty)
            {
                ReWrapCombatHistory(font);
            }

            bool isInCombat = true;
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Determine which history and scroll offset to use
            List<ColoredLine> activeHistory = isInCombat ? _wrappedCombatHistory : _wrappedHistory;
            int activeScrollOffset = isInCombat ? CombatScrollOffset : ScrollOffset;

            // Draw Frame
            spriteBatch.DrawSnapped(pixel, new Rectangle(bounds.X - 5, bounds.Y - 5, bounds.Width + 10, bounds.Height + 10), _global.TerminalBg);
            spriteBatch.DrawSnapped(pixel, new Rectangle(bounds.X - 5, bounds.Y - 5, bounds.Width + 10, 2), _global.Palette_White); // Top
            spriteBatch.DrawSnapped(pixel, new Rectangle(bounds.X - 5, bounds.Y + bounds.Height + 3, bounds.Width + 10, 2), _global.Palette_White); // Bottom
            spriteBatch.DrawSnapped(pixel, new Rectangle(bounds.X - 5, bounds.Y - 5, 2, bounds.Height + 10), _global.Palette_White); // Left
            spriteBatch.DrawSnapped(pixel, new Rectangle(bounds.X + bounds.Width + 3, bounds.Y - 5, 2, bounds.Height + 10), _global.Palette_White); // Right

            // Draw Scroll Indicator
            if (activeScrollOffset > 0)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("^ Scrolled up ").Append(activeScrollOffset).Append(" lines");
                string scrollIndicator = _stringBuilder.ToString();
                int scrollY = bounds.Y - 15;
                spriteBatch.DrawStringSnapped(font, scrollIndicator, new Vector2(bounds.X, scrollY), Color.Gold);
            }

            // --- REVISED LAYOUT LOGIC ---
            int outputAreaBottom = bounds.Bottom;

            // Draw History
            int outputAreaHeight = outputAreaBottom - bounds.Y;
            int maxVisibleLines = outputAreaHeight / Global.TERMINAL_LINE_SPACING;
            int totalLines = activeHistory.Count;
            int lastHistoryIndexToDraw = totalLines - 1 - activeScrollOffset;
            float lastScreenLineY = (bounds.Y + outputAreaHeight) - Global.TERMINAL_LINE_SPACING + 1;

            for (int i = 0; i < maxVisibleLines; i++)
            {
                int historyIndex = lastHistoryIndexToDraw - i;
                if (historyIndex < 0) break;

                float y = lastScreenLineY - i * Global.TERMINAL_LINE_SPACING;
                if (y < bounds.Y) continue;

                float x = bounds.X;
                var line = activeHistory[historyIndex];

                foreach (var segment in line.Segments)
                {
                    spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(x, y), segment.Color);
                    x += font.MeasureString(segment.Text).Width;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? _global.InputTextColor;
            var currentText = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    // Check for escaped bracket [[
                    if (i + 1 < text.Length && text[i + 1] == '[')
                    {
                        currentText += '[';
                        i++; // Skip the second '['
                        continue;
                    }

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
                            if (colorTag == "error") _hapticsManager.TriggerCompoundShake(0.1f, 0.05f);
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

        private void ReWrapHistory(BitmapFont font)
        {
            _wrappedHistory.Clear();
            float wrapWidth = GetTerminalContentWidthInPixels();
            foreach (var line in _unwrappedHistory)
            {
                _wrappedHistory.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _historyDirty = false;
        }

        private void ReWrapCombatHistory(BitmapFont font)
        {
            _wrappedCombatHistory.Clear();
            float wrapWidth = GetTerminalContentWidthInPixels();
            foreach (var line in _unwrappedCombatHistory)
            {
                _wrappedCombatHistory.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _combatHistoryDirty = false;
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

            Action finishCurrentLine = () =>
            {
                if (currentLine.Segments.Any())
                {
                    wrappedLines.Add(currentLine);
                }
                currentLine = new ColoredLine { LineNumber = 0 };
                currentLineText.Clear();
            };

            foreach (var segment in line.Segments)
            {
                // Split text into tokens (words, spaces, newlines)
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
                    // First, merge with last segment if colors match.
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

        private string WrapText(string text, float maxWidthInPixels, BitmapFont font)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var finalLines = new List<string>();
            string[] existingLines = text.Split('\n');

            foreach (string line in existingLines)
            {
                if (font.MeasureString(line).Width <= maxWidthInPixels)
                {
                    finalLines.Add(line);
                }
                else
                {
                    var words = Regex.Split(line, @"(\s+)").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    var currentLine = new StringBuilder();

                    foreach (string word in words)
                    {
                        bool isSpace = string.IsNullOrWhiteSpace(word);
                        float potentialWidth = font.MeasureString(currentLine.ToString() + word).Width;

                        if (!isSpace && currentLine.Length > 0 && potentialWidth > maxWidthInPixels)
                        {
                            finalLines.Add(currentLine.ToString());
                            currentLine.Clear();
                        }

                        currentLine.Append(word);
                    }

                    if (currentLine.Length > 0)
                    {
                        finalLines.Add(currentLine.ToString());
                    }
                }
            }

            return string.Join("\n", finalLines);
        }

        private float GetTerminalContentWidthInPixels()
        {
            if (_currentBounds.Width <= 0) return 1;
            return _currentBounds.Width - 2.0f;
        }

        public int GetMaxVisibleLines()
        {
            if (_currentBounds.Height <= 0) return 0;

            int outputAreaBottom;
            int inputLineY = _currentBounds.Bottom - Global.TERMINAL_LINE_SPACING - 5;
            int separatorY = inputLineY - 5;
            outputAreaBottom = separatorY; // Simplified for now

            int outputAreaHeight = outputAreaBottom - _currentBounds.Y;
            return (outputAreaHeight - 5) / Global.TERMINAL_LINE_SPACING;
        }
    }
}