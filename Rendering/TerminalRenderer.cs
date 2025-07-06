using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace ProjectVagabond
{
    public class TerminalRenderer
    {
        private List<string> _inputHistory = new List<string>();
        private List<ColoredLine> _wrappedHistory = new List<ColoredLine>();
        private int _scrollOffset = 0;
        private int _nextLineNumber = 1;
        private Color _inputCaratColor;

        private float _caratBlinkTimer = 0f;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        private List<ColoredLine> _cachedPromptLines;
        private string _cachedWrappedStatusText;
        private int _cachedPendingActionCount = -1;
        private bool _cachedIsExecutingPath = false;
        private bool _cachedIsFreeMoveMode = false;
        private int _cachedCurrentPathIndex = -1;

        public int ScrollOffset => _scrollOffset;
        public List<ColoredLine> WrappedHistory => _wrappedHistory;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetScrollOffset(int index)
        {
            _scrollOffset = index;
        }

        public void ResetCaratBlink()
        {
            _caratBlinkTimer = 0f;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void AddToHistory(string message, Color? baseColor = null)
        {
            if (_nextLineNumber > 999)
            {
                int desiredLinesToKeep = GetMaxVisibleLines();
                int startIndex = Math.Max(0, _wrappedHistory.Count - desiredLinesToKeep);

                while (startIndex > 0 && _wrappedHistory[startIndex].LineNumber == 0)
                {
                    startIndex--;
                }

                var keptLines = _wrappedHistory.GetRange(startIndex, _wrappedHistory.Count - startIndex);

                _wrappedHistory.Clear();
                _scrollOffset = 0;

                var truncationMessage = ParseColoredText("--- HISTORY TRUNCATED ---", Global.Instance.Palette_Gray);
                truncationMessage.LineNumber = 1;
                _wrappedHistory.Add(truncationMessage);

                _wrappedHistory.AddRange(keptLines);

                int currentLineNum = 2;
                for (int i = 1; i < _wrappedHistory.Count; i++)
                {
                    if (_wrappedHistory[i].LineNumber > 0)
                    {
                        _wrappedHistory[i].LineNumber = currentLineNum++;
                    }
                }
                _nextLineNumber = currentLineNum;
            }

            _inputHistory.Add(message);

            var coloredLine = ParseColoredText(message, baseColor);
            coloredLine.LineNumber = _nextLineNumber++;

            var wrappedLines = WrapColoredText(coloredLine, GetTerminalContentWidthInPixels());
            foreach (var line in wrappedLines)
            {
                _wrappedHistory.Add(line);
            }

            while (_wrappedHistory.Count > Global.MAX_HISTORY_LINES)
            {
                _wrappedHistory.RemoveAt(0);
            }

            if (_inputHistory.Count > 50)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        public void AddOutputToHistory(string output)
        {
            AddToHistory(output, Global.Instance.OutputTextColor);
        }

        public void ClearHistory()
        {
            _inputHistory.Clear();
            _wrappedHistory.Clear();
            _scrollOffset = 0;
            _nextLineNumber = 1;
        }


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void DrawTerminal(GameTime gameTime)
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            BitmapFont _defaultFont = Global.Instance.DefaultFont;

            int terminalX = 375;
            int terminalY = 50;
            int terminalWidth = Global.DEFAULT_TERMINAL_WIDTH;
            int terminalHeight = Global.DEFAULT_TERMINAL_HEIGHT;

            Texture2D pixel = Core.Pixel;

            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), Global.Instance.TerminalBg);

            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), Global.Instance.Palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), Global.Instance.Palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), Global.Instance.Palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), Global.Instance.Palette_White); // Right

            _spriteBatch.DrawString(_defaultFont, "Terminal Output", new Vector2(terminalX, terminalY - 20), Global.Instance.GameTextColor);

            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 5, terminalWidth + 10, 2), Global.Instance.Palette_White);

            int inputLineY = GetInputLineY();
            int separatorY = GetSeparatorY();

            int maxVisibleLines = GetMaxVisibleLines();
            int totalLines = _wrappedHistory.Count;

            int lastHistoryIndexToDraw = totalLines - 1 - _scrollOffset;

            float lastScreenLineY = terminalY + (maxVisibleLines - 1) * Global.TERMINAL_LINE_SPACING;

            for (int i = 0; i < maxVisibleLines; i++)
            {
                int historyIndex = lastHistoryIndexToDraw - i;

                if (historyIndex < 0)
                    break;

                float y = lastScreenLineY - i * Global.TERMINAL_LINE_SPACING;

                if (y < terminalY)
                    continue;

                float x = terminalX;
                var line = _wrappedHistory[historyIndex];

                foreach (var segment in line.Segments)
                {
                    _spriteBatch.DrawString(_defaultFont, segment.Text, new Vector2(x, y), segment.Color);
                    x += _defaultFont.MeasureString(segment.Text).Width;
                }

                if (line.LineNumber > 0)
                {
                    string lineNumText = line.LineNumber.ToString();
                    float lineNumX = terminalX + 550;
                    _spriteBatch.DrawString(_defaultFont, lineNumText, new Vector2(lineNumX, y), Global.Instance.TerminalDarkGray);
                }
            }

            bool canScrollUp = _scrollOffset > 0;
            bool canScrollDown = _wrappedHistory.Count > maxVisibleLines;

            if (canScrollUp || canScrollDown)
            {
                string scrollIndicator = "";
                if (_scrollOffset > 0)
                {
                    _stringBuilder.Clear();
                    _stringBuilder.Append("^ Scrolled up ").Append(_scrollOffset).Append(" lines");
                    scrollIndicator = _stringBuilder.ToString();
                }

                int scrollY = terminalY - 35;
                _spriteBatch.DrawString(_defaultFont, scrollIndicator, new Vector2(terminalX, scrollY), Color.Gold);
            }

            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, separatorY, Global.DEFAULT_TERMINAL_WIDTH + 10, 2), Global.Instance.Palette_White);

            _caratBlinkTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            string caratUnderscore = "_";
            if (!Core.CurrentGameState.IsExecutingPath)
            {
                // Use a 1-second cycle (0.5s on, 0.5s off) for a standard blink rate.
                if (_caratBlinkTimer % 1.0f > 0.5f)
                {
                    caratUnderscore = "";
                }
            }

            _stringBuilder.Clear();
            _stringBuilder.Append("> ").Append(Core.CurrentInputHandler.CurrentInput).Append(caratUnderscore);
            string inputCarat = _stringBuilder.ToString();

            string wrappedInput = WrapText(inputCarat, GetTerminalContentWidthInPixels());

            _inputCaratColor = Core.CurrentGameState.IsExecutingPath ? Global.Instance.TerminalDarkGray : Global.Instance.InputCaratColor;

            _spriteBatch.DrawString(_defaultFont, wrappedInput, new Vector2(terminalX, inputLineY + 1), _inputCaratColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            if (Core.CurrentAutoCompleteManager.ShowingAutoCompleteSuggestions && Core.CurrentAutoCompleteManager.AutoCompleteSuggestions.Count > 0)
            {
                int suggestionY = inputLineY - 20;
                int visibleSuggestions = Math.Min(Core.CurrentAutoCompleteManager.AutoCompleteSuggestions.Count, 5);

                int maxSuggestionWidth = 0;
                for (int i = 0; i < visibleSuggestions; i++)
                {
                    string prefix = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                    string fullText = prefix + Core.CurrentAutoCompleteManager.AutoCompleteSuggestions[i];
                    int textWidth = (int)_defaultFont.MeasureString(fullText).Width;
                    maxSuggestionWidth = Math.Max(maxSuggestionWidth, textWidth);
                }

                int backgroundHeight = visibleSuggestions * Global.FONT_SIZE;
                int backgroundY = suggestionY - (visibleSuggestions - 1) * Global.FONT_SIZE;
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, backgroundHeight), Global.Instance.Palette_Black);

                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, 1), Global.Instance.Palette_LightGray); // Top
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY + backgroundHeight, maxSuggestionWidth + 4, 1), Global.Instance.Palette_LightGray); // Bottom
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, 1, backgroundHeight), Global.Instance.Palette_LightGray); // Left
                _spriteBatch.Draw(pixel, new Rectangle(terminalX + maxSuggestionWidth + 4, backgroundY, 1, backgroundHeight), Global.Instance.Palette_LightGray); // Right

                for (int i = 0; i < visibleSuggestions; i++)
                {
                    Color suggestionColor = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? Color.Khaki : Global.Instance.Palette_LightGray;
                    string prefix = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                    _spriteBatch.DrawString(_defaultFont, prefix + Core.CurrentAutoCompleteManager.AutoCompleteSuggestions[i],
                        new Vector2(terminalX + 2, suggestionY - i * Global.FONT_SIZE), suggestionColor);
                }
            }

            var gameState = Core.CurrentGameState;

            if (gameState.PendingActions.Count != _cachedPendingActionCount ||
                gameState.IsExecutingPath != _cachedIsExecutingPath ||
                gameState.IsFreeMoveMode != _cachedIsFreeMoveMode ||
                gameState.IsActionQueueDirty)
            {
                _cachedPendingActionCount = gameState.PendingActions.Count;
                _cachedIsExecutingPath = gameState.IsExecutingPath;
                _cachedIsFreeMoveMode = gameState.IsFreeMoveMode;

                _stringBuilder.Clear();
                _stringBuilder.Append("Actions Queued: ").Append(gameState.PendingActions.Count);
                if (gameState.IsExecutingPath)
                {
                    _stringBuilder.Append(" | Executing...");
                }
                _cachedWrappedStatusText = WrapText(_stringBuilder.ToString(), GetTerminalContentWidthInPixels());

                string promptText = GetPromptText();
                if (!string.IsNullOrEmpty(promptText))
                {
                    var coloredPrompt = ParseColoredText(promptText, Color.Khaki);
                    _cachedPromptLines = WrapColoredText(coloredPrompt, GetTerminalContentWidthInPixels());
                }
                else
                {
                    _cachedPromptLines = null;
                }

                gameState.IsActionQueueDirty = false;
            }

            int statusY = terminalY + terminalHeight + 15;
            _spriteBatch.DrawString(_defaultFont, _cachedWrappedStatusText, new Vector2(terminalX, statusY), Global.Instance.Palette_LightGray);

            int promptY = statusY + (_cachedWrappedStatusText.Split('\n').Length * Global.TERMINAL_LINE_SPACING) + 5;
            if (_cachedPromptLines != null)
            {
                for (int i = 0; i < _cachedPromptLines.Count; i++)
                {
                    float x = terminalX;
                    float y = promptY + i * Global.PROMPT_LINE_SPACING;

                    foreach (var segment in _cachedPromptLines[i].Segments)
                    {
                        _spriteBatch.DrawString(_defaultFont, segment.Text, new Vector2(x, y), segment.Color);
                        x += _defaultFont.MeasureString(segment.Text).Width;
                    }
                }
            }
        }

        private static string GetPromptText()
        {
            int moveCount = Core.CurrentGameState.PendingActions.Count(a => a is MoveAction);
            int restCount = Core.CurrentGameState.PendingActions.Count(a => a is RestAction);

            var promptBuilder = new StringBuilder();

            if (Core.CurrentGameState.IsFreeMoveMode && Core.CurrentGameState.PendingActions.Count <= 0)
            {
                promptBuilder.Append("[skyblue]Free moving... <[deepskyblue]Use ([royalblue]W[deepskyblue]/[royalblue]A[deepskyblue]/[royalblue]S[deepskyblue]/[royalblue]D[deepskyblue]) to queue moves>\n");
                promptBuilder.Append("[gold]Press[orange] ENTER[gold] to confirm,[orange] ESC[gold] to cancel\n");
                return promptBuilder.ToString();
            }
            else if (Core.CurrentGameState.PendingActions.Count > 0 && !Core.CurrentGameState.IsExecutingPath)
            {
                if (Core.CurrentGameState.IsFreeMoveMode)
                {
                    promptBuilder.Append("[skyblue]Free moving... <[deepskyblue]Use ([deepskyblue]W[deepskyblue]/[royalblue]A[deepskyblue]/[royalblue]S[deepskyblue]/[royalblue]D[deepskyblue]) to queue moves>\n");
                }
                else
                {
                    promptBuilder.Append("[khaki]Previewing action queue...\n");
                }
                promptBuilder.Append("[gold]Press[orange] ENTER[gold] to confirm,[orange] ESC[gold] to cancel\n");

                var details = new List<string>();
                if (moveCount > 0) details.Add($"[orange]{moveCount}[gold] move(s)");
                if (restCount > 0) details.Add($"[green]{restCount}[gold] rest(s)");

                promptBuilder.Append($"[gold]Pending[orange] {string.Join(", ", details)}\n");

                var simResult = Core.CurrentGameState.PendingQueueSimulationResult;
                int secondsPassed = simResult.secondsPassed;

                if (secondsPassed > 0)
                {
                    WorldClockManager worldClockManager = Core.CurrentWorldClockManager;
                    string finalETA = worldClockManager.GetCalculatedNewTime(worldClockManager.CurrentTime, secondsPassed);
                    finalETA = Global.Instance.Use24HourClock ? finalETA : worldClockManager.GetConverted24hToAmPm(finalETA);
                    string formattedDuration = worldClockManager.GetFormattedTimeFromSecondsShortHand(secondsPassed);
                    promptBuilder.Append($"[gold]Arrival Time:[orange] {finalETA} [Palette_Gray]({formattedDuration})\n");
                }

                return promptBuilder.ToString();
            }
            return "";
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

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
                            currentColor = Global.Instance.InputTextColor;
                        }
                        else if (colorTag == "/o")
                        {
                            currentColor = Global.Instance.OutputTextColor;
                        }
                        else
                        {
                            if (colorTag == "error") Core.ScreenShake(2, 0.25f);
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

                case "palette_black": return Global.Instance.Palette_Black;
                case "palette_darkgray": return Global.Instance.Palette_DarkGray;
                case "palette_gray": return Global.Instance.Palette_Gray;
                case "palette_lightgray": return Global.Instance.Palette_LightGray;
                case "palette_white": return Global.Instance.Palette_White;
                case "palette_teal": return Global.Instance.Palette_Teal;
                case "palette_lightblue": return Global.Instance.Palette_LightBlue;
                case "palette_darkblue": return Global.Instance.Palette_DarkBlue;
                case "palette_darkgreen": return Global.Instance.Palette_DarkGreen;
                case "palette_lightgreen": return Global.Instance.Palette_LightGreen;
                case "palette_lightyellow": return Global.Instance.Palette_LightYellow;
                case "palette_yellow": return Global.Instance.Palette_Yellow;
                case "palette_orange": return Global.Instance.Palette_Orange;
                case "palette_red": return Global.Instance.Palette_Red;
                case "palette_darkpurple": return Global.Instance.Palette_DarkPurple;
                case "palette_lightpurple": return Global.Instance.Palette_LightPurple;
                case "palette_pink": return Global.Instance.Palette_Pink;
                case "palette_brightwhite": return Global.Instance.Palette_BrightWhite;

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
            catch
            {
                // bruh
            }

            return Global.Instance.GameTextColor;
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

        private string WrapText(string text, float maxWidthInPixels)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var font = Global.Instance.DefaultFont;
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
                    float currentLineWidth = 0f;

                    foreach (string word in words)
                    {
                        float wordWidth = font.MeasureString(word).Width;
                        bool isSpace = string.IsNullOrWhiteSpace(word);

                        if (!isSpace && currentLine.Length > 0 && currentLineWidth + wordWidth > maxWidthInPixels)
                        {
                            finalLines.Add(currentLine.ToString());
                            currentLine.Clear();
                            currentLineWidth = 0;
                        }

                        if (!isSpace && wordWidth > maxWidthInPixels)
                        {
                            if (currentLine.Length > 0)
                            {
                                finalLines.Add(currentLine.ToString());
                                currentLine.Clear();
                            }

                            string remainingWord = word;
                            while (remainingWord.Length > 0)
                            {
                                int charsThatFit = 0;
                                for (int i = 1; i <= remainingWord.Length; i++)
                                {
                                    if (font.MeasureString(remainingWord.Substring(0, i)).Width > maxWidthInPixels)
                                    {
                                        break;
                                    }
                                    charsThatFit = i;
                                }
                                if (charsThatFit == 0 && remainingWord.Length > 0) charsThatFit = 1;

                                finalLines.Add(remainingWord.Substring(0, charsThatFit));
                                remainingWord = remainingWord.Substring(charsThatFit);
                            }
                            currentLineWidth = 0;
                        }
                        else
                        {
                            currentLine.Append(word);
                            currentLineWidth += wordWidth;
                        }
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
            int terminalWidth = Global.DEFAULT_TERMINAL_WIDTH;
            float charWidth = Global.Instance.DefaultFont.MeasureString("W").Width;
            return terminalWidth - (2 * charWidth);
        }

        private int GetTerminalY()
        {
            return 50;
        }

        private int GetTerminalHeight()
        {
            return Global.DEFAULT_TERMINAL_HEIGHT;
        }

        private int GetInputLineY()
        {
            return GetTerminalY() + GetTerminalHeight() - 10;
        }

        private int GetSeparatorY()
        {
            return GetInputLineY() - 5;
        }

        private int GetOutputAreaHeight()
        {
            return GetSeparatorY() - GetTerminalY() + 10;
        }

        public int GetMaxVisibleLines()
        {
            return GetOutputAreaHeight() / Global.TERMINAL_LINE_SPACING;
        }
    }
}