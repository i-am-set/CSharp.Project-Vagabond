using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using System;
using System.Collections;
using System.Collections.Generic;
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
        private readonly GraphicsDevice _graphicsDevice;
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

        public int ScrollOffset { get; set; } = 0;
        public int CombatScrollOffset { get; set; } = 0;

        private int _nextLineNumber = 1;
        private float _caratBlinkTimer = 0f;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        // Caching for prompt/status text
        private string _cachedStatusText;
        private string _cachedPromptText;
        private int _cachedPendingActionCount = -1;
        private bool _cachedIsExecutingPath = false;
        private bool _cachedIsFreeMoveMode = false;

        // Render Target Caching
        private RenderTarget2D _historyCacheTarget;
        private RenderTarget2D _combatHistoryCacheTarget;

        public List<ColoredLine> WrappedHistory => _wrappedHistory;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public TerminalRenderer()
        {
            // Acquire dependencies from the ServiceLocator
            _gameState = ServiceLocator.Get<GameState>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _global = ServiceLocator.Get<Global>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();

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
            var coloredLine = ParseColoredText(e.Message, _global.OutputTextColor);
            _unwrappedCombatHistory.Add(coloredLine);
            _combatHistoryDirty = true;

            while (_unwrappedCombatHistory.Count > Global.MAX_HISTORY_LINES)
            {
                _unwrappedCombatHistory.RemoveAt(0);
            }
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

        private void AddToHistory(string message, Color? baseColor = null)
        {
            if (_nextLineNumber > 999)
            {
                // Simple truncation logic for unwrapped history
                int desiredLinesToKeep = GetMaxVisibleLines();
                int startIndex = Math.Max(0, _unwrappedHistory.Count - desiredLinesToKeep);

                var keptLines = _unwrappedHistory.GetRange(startIndex, _unwrappedHistory.Count - startIndex);
                _unwrappedHistory.Clear();
                ScrollOffset = 0;

                var truncationMessage = ParseColoredText("--- HISTORY TRUNCATED ---", _global.Palette_Gray);
                truncationMessage.LineNumber = 1;
                _unwrappedHistory.Add(truncationMessage);
                _unwrappedHistory.AddRange(keptLines);

                int currentLineNum = 2;
                for (int i = 1; i < _unwrappedHistory.Count; i++)
                {
                    if (_unwrappedHistory[i].LineNumber > 0)
                    {
                        _unwrappedHistory[i].LineNumber = currentLineNum++;
                    }
                }
                _nextLineNumber = currentLineNum;
            }

            _inputHistory.Add(message);

            var coloredLine = ParseColoredText(message, baseColor ?? _global.OutputTextColor);
            coloredLine.LineNumber = _nextLineNumber++;
            _unwrappedHistory.Add(coloredLine);
            _historyDirty = true;

            while (_unwrappedHistory.Count > Global.MAX_HISTORY_LINES)
            {
                _unwrappedHistory.RemoveAt(0);
            }

            if (_inputHistory.Count > 50)
            {
                _inputHistory.RemoveAt(0);
            }
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
            _combatHistoryDirty = true;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void DrawTerminal(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Lazy-load dependencies to break initialization cycles
            _inputHandler ??= ServiceLocator.Get<InputHandler>();
            _autoCompleteManager ??= ServiceLocator.Get<AutoCompleteManager>();

            if (_historyDirty) ReWrapHistory(font);
            if (_combatHistoryDirty) ReWrapCombatHistory(font);

            RedrawHistoryCaches(spriteBatch, font);

            bool isInCombat = _gameState.IsInCombat;
            int terminalHeight = GetTerminalHeight();
            int terminalX = 375;
            int terminalY = Global.TERMINAL_Y;
            int terminalWidth = (!isInCombat ? Global.DEFAULT_TERMINAL_WIDTH : Global.DEFAULT_TERMINAL_WIDTH - 150);

            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            List<ColoredLine> activeHistory = isInCombat ? _wrappedCombatHistory : _wrappedHistory;
            int activeScrollOffset = isInCombat ? CombatScrollOffset : ScrollOffset;
            RenderTarget2D activeCache = isInCombat ? _combatHistoryCacheTarget : _historyCacheTarget;

            // Draw Frame
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), _global.TerminalBg);
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), _global.Palette_White); // Top
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), _global.Palette_White); // Bottom
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), _global.Palette_White); // Left
            spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), _global.Palette_White); // Right
            spriteBatch.DrawString(font, "Terminal Output", new Vector2(terminalX, terminalY - 20), _global.GameTextColor);
            spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 5, terminalWidth + 10, 2), _global.Palette_White);

            // Draw History from Cache
            if (activeCache != null && !activeCache.IsDisposed)
            {
                int outputAreaHeight = GetOutputAreaHeight();
                int totalHistoryPixelHeight = activeHistory.Count * Global.TERMINAL_LINE_SPACING;

                var sourceRect = new Rectangle
                {
                    X = 0,
                    Width = activeCache.Width,
                    Height = Math.Min(outputAreaHeight, activeCache.Height)
                };
                sourceRect.Y = Math.Max(0, totalHistoryPixelHeight - sourceRect.Height - (activeScrollOffset * Global.TERMINAL_LINE_SPACING));
                sourceRect.Y = Math.Min(sourceRect.Y, activeCache.Height - sourceRect.Height);

                var destRect = new Rectangle(terminalX, terminalY, (int)GetTerminalContentWidthInPixels(font), outputAreaHeight);

                spriteBatch.Draw(activeCache, destRect, sourceRect, Color.White);
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
                int secondsPassed = simResult.secondsPassed;

                if (secondsPassed > 0)
                {
                    string finalETA = _worldClockManager.GetCalculatedNewTime(_worldClockManager.CurrentTime, secondsPassed);
                    finalETA = _global.Use24HourClock ? finalETA : _worldClockManager.GetConverted24hToAmPm(finalETA);
                    string formattedDuration = _worldClockManager.GetFormattedTimeFromSecondsShortHand(secondsPassed);
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

        private void RedrawHistoryCaches(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!_historyDirty && !_combatHistoryDirty) return;

            int cacheWidth = (int)GetTerminalContentWidthInPixels(font);
            int cacheHeight = Global.MAX_HISTORY_LINES * Global.TERMINAL_LINE_SPACING;

            if (cacheWidth <= 0 || cacheHeight <= 0) return;

            if (_historyCacheTarget == null || _historyCacheTarget.Width != cacheWidth || _historyCacheTarget.Height != cacheHeight)
            {
                _historyCacheTarget?.Dispose();
                _historyCacheTarget = new RenderTarget2D(_graphicsDevice, cacheWidth, cacheHeight, false, _graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None);
                _historyDirty = true;
            }
            if (_combatHistoryCacheTarget == null || _combatHistoryCacheTarget.Width != cacheWidth || _combatHistoryCacheTarget.Height != cacheHeight)
            {
                _combatHistoryCacheTarget?.Dispose();
                _combatHistoryCacheTarget = new RenderTarget2D(_graphicsDevice, cacheWidth, cacheHeight, false, _graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None);
                _combatHistoryDirty = true;
            }

            spriteBatch.End();

            var originalRenderTargets = _graphicsDevice.GetRenderTargets();

            if (_historyDirty)
            {
                _graphicsDevice.SetRenderTarget(_historyCacheTarget);
                _graphicsDevice.Clear(_global.TerminalBg);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                for (int i = 0; i < _wrappedHistory.Count; i++)
                {
                    float x = 0;
                    float y = i * Global.TERMINAL_LINE_SPACING;
                    var line = _wrappedHistory[i];
                    foreach (var segment in line.Segments)
                    {
                        spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                        x += font.MeasureString(segment.Text).Width;
                    }
                }
                spriteBatch.End();
                _historyDirty = false;
            }

            if (_combatHistoryDirty)
            {
                _graphicsDevice.SetRenderTarget(_combatHistoryCacheTarget);
                _graphicsDevice.Clear(_global.TerminalBg);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                for (int i = 0; i < _wrappedCombatHistory.Count; i++)
                {
                    float x = 0;
                    float y = i * Global.TERMINAL_LINE_SPACING;
                    var line = _wrappedCombatHistory[i];
                    foreach (var segment in line.Segments)
                    {
                        spriteBatch.DrawString(font, segment.Text, new Vector2(x, y), segment.Color);
                        x += font.MeasureString(segment.Text).Width;
                    }
                }
                spriteBatch.End();
                _combatHistoryDirty = false;
            }

            _graphicsDevice.SetRenderTargets(originalRenderTargets);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        }

        private void ReWrapHistory(BitmapFont font)
        {
            _wrappedHistory.Clear();
            float wrapWidth = GetTerminalContentWidthInPixels(font);
            foreach (var line in _unwrappedHistory)
            {
                _wrappedHistory.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _historyDirty = true;
        }

        private void ReWrapCombatHistory(BitmapFont font)
        {
            _wrappedCombatHistory.Clear();
            float wrapWidth = GetTerminalContentWidthInPixels(font);
            foreach (var line in _unwrappedCombatHistory)
            {
                _wrappedCombatHistory.AddRange(WrapColoredText(line, wrapWidth, font));
            }
            _combatHistoryDirty = true;
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, float maxWidthInPixels, BitmapFont font)
        {
            var wrappedLines = new List<ColoredLine>();
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

        private float GetTerminalContentWidthInPixels(BitmapFont font)
        {
            int terminalWidth = _gameState.IsInCombat ? Global.DEFAULT_TERMINAL_WIDTH - 150 : Global.DEFAULT_TERMINAL_WIDTH;
            float charWidth = font.MeasureString("W").Width;
            return terminalWidth - (2 * charWidth);
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
            return (GetOutputAreaHeight() - 5) / Global.TERMINAL_LINE_SPACING;
        }
    }
}