
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
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
        private readonly WorldClockManager _worldClockManager;
        private readonly HapticsManager _hapticsManager;
        private readonly Global _global;
        private AutoCompleteManager _autoCompleteManager; // Lazy loaded
        private InputHandler _inputHandler; // Lazy loaded

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
        private float _caratBlinkTimer = 0f;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        // Caching for prompt/status text
        private string _cachedStatusText;
        private string _cachedPromptText;
        private int _cachedPendingActionCount = -1;
        private bool _cachedIsExecutingPath = false;
        private bool _cachedIsFreeMoveMode = false;

        public List<ColoredLine> WrappedHistory => _wrappedHistory;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public TerminalRenderer()
        {
            // Acquire dependencies from the ServiceLocator
            _gameState = ServiceLocator.Get<GameState>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _global = ServiceLocator.Get<Global>();

            // Subscribe to events for decoupled communication
            EventBus.Subscribe<GameEvents.TerminalMessagePublished>(OnTerminalMessagePublished);
            EventBus.Subscribe<GameEvents.CombatLogMessagePublished>(OnCombatLogMessagePublished);
            EventBus.Subscribe<GameEvents.CombatStateChanged>(OnCombatStateChanged);
        }

        private void OnTerminalMessagePublished(GameEvents.TerminalMessagePublished e)
        {
            AddToHistory(e.Message, e.BaseColor);
        }

        private void OnCombatLogMessagePublished(GameEvents.CombatLogMessagePublished e)
        {
            AddToCombatHistory(e.Message, _global.OutputTextColor);
        }

        private void OnCombatStateChanged(GameEvents.CombatStateChanged e)
        {
            // Clear the combat log when combat starts to provide a clean slate.
            if (e.IsInCombat)
            {
                ClearCombatHistory();
            }
        }

        public void ResetCaratBlink()
        {
            _caratBlinkTimer = 0f;
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

        public void DrawTerminal(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Lazy-load dependencies to break initialization cycles
            _inputHandler ??= ServiceLocator.Get<InputHandler>();
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

            bool isInCombat = _gameState.IsInCombat;
            int terminalHeight = GetTerminalHeight();
            int terminalX = 375;
            int terminalY = Global.TERMINAL_Y;
            int terminalWidth = (!isInCombat ? Global.DEFAULT_TERMINAL_WIDTH : Global.DEFAULT_TERMINAL_WIDTH - Global.COMBAT_TERMINAL_BUFFER); // Adjusted width for combat

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            // Determine which history and scroll offset to use
            List<ColoredLine> activeHistory = isInCombat ? _wrappedCombatHistory : _wrappedHistory;
            int activeScrollOffset = isInCombat ? CombatScrollOffset : ScrollOffset;

            // Draw Frame
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), _global.TerminalBg);
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), _global.Palette_White); // Top
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), _global.Palette_White); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), _global.Palette_White); // Left
            spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), _global.Palette_White); // Right
            spriteBatch.DrawString(font, "Terminal Output", new Vector2(terminalX, terminalY - 20), _global.GameTextColor);
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 5, terminalWidth + 10, 2), _global.Palette_White);

            // Draw History
            int maxVisibleLines = GetMaxVisibleLines();
            int totalLines = activeHistory.Count;
            int lastHistoryIndexToDraw = totalLines - 1 - activeScrollOffset;
            float lastScreenLineY = terminalY + GetOutputAreaHeight() - Global.TERMINAL_LINE_SPACING;

            for (int i = 0; i < maxVisibleLines; i++)
            {
                int historyIndex = lastHistoryIndexToDraw - i;
                if (historyIndex < 0) break;

                float y = lastScreenLineY - i * Global.TERMINAL_LINE_SPACING;
                if (y < terminalY) continue;

                float x = terminalX;
                var line = activeHistory[historyIndex];

                foreach (var segment in line.Segments)
                {
                    spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                    x += font.MeasureString(segment.Text).Width;
                }
            }

            // Draw Scroll Indicator
            if (activeScrollOffset > 0)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("^ Scrolled up ").Append(activeScrollOffset).Append(" lines");
                string scrollIndicator = _stringBuilder.ToString();
                int scrollY = terminalY - 35;
                spriteBatch.DrawString(font, scrollIndicator, new Vector2(terminalX, scrollY), Color.Gold);
            }

            // Conditionally draw the input section
            if (!isInCombat)
            {
                float contentWidth = GetTerminalContentWidthInPixels(font);
                int inputLineY = GetInputLineY();
                int separatorY = GetSeparatorY();

                spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, separatorY, Global.DEFAULT_TERMINAL_WIDTH + 10, 2), _global.Palette_White);

                _caratBlinkTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                string caratUnderscore = "_";
                if (!_gameState.IsExecutingActions)
                {
                    if (_caratBlinkTimer % 1.0f > 0.5f)
                    {
                        caratUnderscore = "";
                    }
                }

                _stringBuilder.Clear();
                _stringBuilder.Append("> ").Append(_inputHandler.CurrentInput).Append(caratUnderscore);
                string inputCarat = _stringBuilder.ToString();
                string wrappedInput = WrapText(inputCarat, contentWidth, font);
                Color inputCaratColor = _gameState.IsExecutingActions ? _global.TerminalDarkGray : _global.InputCaratColor;
                spriteBatch.DrawString(font, wrappedInput, new Vector2(terminalX, inputLineY + 1), inputCaratColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                if (_autoCompleteManager.ShowingAutoCompleteSuggestions && _autoCompleteManager.AutoCompleteSuggestions.Count > 0)
                {
                    DrawAutoCompleteSuggestions(spriteBatch, font, terminalX, inputLineY);
                }

                // Update cached prompt/status text if state has changed
                if (_gameState.PendingActions.Count != _cachedPendingActionCount ||
                    _gameState.IsExecutingActions != _cachedIsExecutingPath ||
                    _gameState.IsFreeMoveMode != _cachedIsFreeMoveMode ||
                    _gameState.IsActionQueueDirty)
                {
                    UpdateCachedPromptAndStatus();
                    _gameState.IsActionQueueDirty = false;
                }

                // Draw Status Text
                int statusY = terminalY + terminalHeight + 15;
                string wrappedStatusText = WrapText(_cachedStatusText, contentWidth, font);
                spriteBatch.DrawString(font, wrappedStatusText, new Vector2(terminalX, statusY), _global.Palette_LightGray);

                // Draw Prompt Text
                int promptY = statusY + (wrappedStatusText.Split('\n').Length * Global.TERMINAL_LINE_SPACING) + 5;
                if (!string.IsNullOrEmpty(_cachedPromptText))
                {
                    var coloredPrompt = ParseColoredText(_cachedPromptText, Color.Khaki);
                    var promptLines = WrapColoredText(coloredPrompt, contentWidth, font);
                    for (int i = 0; i < promptLines.Count; i++)
                    {
                        float x = terminalX;
                        float y = promptY + i * Global.PROMPT_LINE_SPACING;
                        foreach (var segment in promptLines[i].Segments)
                        {
                            spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                            x += font.MeasureString(segment.Text).Width;
                        }
                    }
                }
            }
        }

        private void DrawAutoCompleteSuggestions(SpriteBatch spriteBatch, BitmapFont font, int terminalX, int inputLineY)
        {
            Texture2D pixel = ServiceLocator.Get<Texture2D>();
            int suggestionY = inputLineY - 20;
            int visibleSuggestions = Math.Min(_autoCompleteManager.AutoCompleteSuggestions.Count, 5);
            int maxSuggestionWidth = 0;
            for (int i = 0; i < visibleSuggestions; i++)
            {
                string prefix = (i == _autoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                string fullText = prefix + _autoCompleteManager.AutoCompleteSuggestions[i];
                int textWidth = (int)font.MeasureString(fullText).Width;
                maxSuggestionWidth = Math.Max(maxSuggestionWidth, textWidth);
            }
            int backgroundHeight = visibleSuggestions * Global.FONT_SIZE;
            int backgroundY = suggestionY - (visibleSuggestions - 1) * Global.FONT_SIZE;
            spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, backgroundHeight), _global.Palette_Black);
            spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, 1), _global.Palette_LightGray); // Top
            spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY + backgroundHeight, maxSuggestionWidth + 4, 1), _global.Palette_LightGray); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, 1, backgroundHeight), _global.Palette_LightGray); // Left
            spriteBatch.Draw(pixel, new Rectangle(terminalX + maxSuggestionWidth + 4, backgroundY, 1, backgroundHeight), _global.Palette_LightGray); // Right
            for (int i = 0; i < visibleSuggestions; i++)
            {
                Color suggestionColor = (i == _autoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? Color.Khaki : _global.Palette_LightGray;
                string prefix = (i == _autoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                spriteBatch.DrawString(font, prefix + _autoCompleteManager.AutoCompleteSuggestions[i],
                    new Vector2(terminalX + 2, suggestionY - i * Global.FONT_SIZE), suggestionColor);
            }
        }

        private void UpdateCachedPromptAndStatus()
        {
            _cachedPendingActionCount = _gameState.PendingActions.Count;
            _cachedIsExecutingPath = _gameState.IsExecutingActions;
            _cachedIsFreeMoveMode = _gameState.IsFreeMoveMode;

            _stringBuilder.Clear();
            _stringBuilder.Append("Actions Queued: ").Append(_gameState.PendingActions.Count);
            if (_gameState.IsExecutingActions)
            {
                _stringBuilder.Append(" | Executing...");
            }
            _cachedStatusText = _stringBuilder.ToString();
            _cachedPromptText = GetPromptText();
        }

        private string GetPromptText()
        {
            int moveCount = _gameState.PendingActions.Count(a => a is MoveAction);
            int restCount = _gameState.PendingActions.Count(a => a is RestAction);

            var promptBuilder = new StringBuilder();

            if (_gameState.IsFreeMoveMode && _gameState.PendingActions.Count <= 0)
            {
                promptBuilder.Append("[skyblue]Free moving... <[deepskyblue]Use ([royalblue]W[deepskyblue]/[royalblue]A[deepskyblue]/[royalblue]S[deepskyblue]/[royalblue]D[deepskyblue]) to queue moves>\n");
                promptBuilder.Append("[gold]Press[orange] ENTER[gold] to confirm,[orange] ESC[gold] to cancel\n");
                return promptBuilder.ToString();
            }
            else if (_gameState.PendingActions.Count > 0 && !_gameState.IsExecutingActions)
            {
                if (_gameState.IsFreeMoveMode)
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

                var simResult = _gameState.PendingQueueSimulationResult;
                float secondsPassed = simResult.secondsPassed;

                if (secondsPassed > 0.01f)
                {
                    string finalETA = _worldClockManager.GetCalculatedNewTime(_worldClockManager.CurrentTime, (int)secondsPassed);
                    finalETA = _global.Use24HourClock ? finalETA : _worldClockManager.GetConverted24hToAmPm(finalETA);
                    string formattedDuration = _worldClockManager.GetFormattedTimeFromSecondsShortHand(secondsPassed);
                    promptBuilder.Append($"[gold]Arrival Time:[orange] ~{finalETA} [Palette_Gray](about {formattedDuration})\n");
                }

                return promptBuilder.ToString();
            }
            return "";
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
                            if (colorTag == "error") _hapticsManager.TriggerShake(2, 0.25f);
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
            float wrapWidth = GetTerminalContentWidthInPixels(font);
            foreach (var line in _unwrappedHistory)
            {
                _wrappedHistory.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _historyDirty = false;
        }

        private void ReWrapCombatHistory(BitmapFont font)
        {
            _wrappedCombatHistory.Clear();
            float wrapWidth = GetTerminalContentWidthInPixels(font);
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

        private float GetTerminalContentWidthInPixels(BitmapFont font)
        {
            // When in combat, the terminal is narrower. This logic must match the width calculation in DrawTerminal.
            int terminalWidth = _gameState.IsInCombat ? (Global.DEFAULT_TERMINAL_WIDTH - Global.COMBAT_TERMINAL_BUFFER) : Global.DEFAULT_TERMINAL_WIDTH;

            // The text content area is `terminalWidth` pixels wide, based on the drawing logic.
            // The previous calculation using `font.MeasureString("W")` was fragile.
            // We subtract a small fixed padding to prevent text from touching the right border due to font rendering nuances.
            return terminalWidth - 2.0f;
        }

        private int GetTerminalHeight()
        {
            return _gameState.IsInCombat
                ? (Global.DEFAULT_TERMINAL_HEIGHT / 2) + 20
                : Global.DEFAULT_TERMINAL_HEIGHT;
        }

        private int GetInputLineY()
        {
            return Global.TERMINAL_Y + GetTerminalHeight() - 10;
        }

        private int GetSeparatorY()
        {
            return GetInputLineY() - 5;
        }

        private int GetOutputAreaHeight()
        {
            if (_gameState.IsInCombat)
            {
                return GetTerminalHeight();
            }
            return GetSeparatorY() - Global.TERMINAL_Y;
        }

        public int GetMaxVisibleLines()
        {
            // Subtract a small buffer to prevent the last line from being partially cut off.
            return (GetOutputAreaHeight() - 5) / Global.TERMINAL_LINE_SPACING;
        }
    }
}
