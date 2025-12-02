using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A debug console overlay for entering commands and viewing logs.
    /// Features: History, Auto-Complete, Line Selection, Copy-Paste.
    /// </summary>
    public class DebugConsole
    {
        public bool IsVisible { get; private set; }

        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly CommandProcessor _commandProcessor;
        private readonly AutoCompleteManager _autoCompleteManager;

        // History
        private readonly List<ColoredLine> _history = new List<ColoredLine>();
        private const int MAX_HISTORY_LINES = 500;

        // Command Input
        private readonly List<string> _commandHistory = new List<string>();
        private int _commandHistoryIndex = -1;
        private string _currentEditingCommand = "";
        private string _currentInput = "";
        private int _scrollOffset = 0;
        private float _caratBlinkTimer = 0f;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        // Input State
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private float _backspaceTimer = 0f;
        private const float BACKSPACE_INITIAL_DELAY = 0.4f;
        private const float BACKSPACE_REPEAT_DELAY = 0.05f;

        // Recursive Crash Protection
        private bool _isDrawing = false;

        // Selection & Clipboard
        private int _selectionStartLine = -1;
        private int _selectionEndLine = -1;
        private bool _isSelecting = false;
        private Button _copyButton;

        public DebugConsole()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _commandProcessor = ServiceLocator.Get<CommandProcessor>();
            _autoCompleteManager = new AutoCompleteManager();

            // Initialize Copy Button
            _copyButton = new Button(new Rectangle(0, 0, 60, 15), "COPY", font: ServiceLocator.Get<Core>().SecondaryFont)
            {
                UseScreenCoordinates = true,
                CustomDefaultTextColor = Color.LightGray,
                CustomHoverTextColor = Color.White,
                // FIX: Disable input debounce so it works even when the game loop is paused/stalled
                UseInputDebounce = false
            };
            _copyButton.OnClick += CopySelectionToClipboard;
        }

        public void ClearHistory()
        {
            _history.Clear();
            _scrollOffset = 0;
            ClearSelection();
            GameLogger.Log(LogSeverity.Info, "--- CONSOLE CLEARED ---");
        }

        public void Show()
        {
            IsVisible = true;
            _gameState.IsPausedByConsole = true;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public void Hide()
        {
            IsVisible = false;
            _gameState.IsPausedByConsole = false;
            _autoCompleteManager.HideSuggestions();
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectionStartLine = -1;
            _selectionEndLine = -1;
            _isSelecting = false;
        }

        public void Update(GameTime gameTime)
        {
            // Always consume logs
            ProcessLogQueue();

            if (!IsVisible) return;

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                Hide();
                return;
            }

            // Ctrl+C to copy
            bool ctrlDown = currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl);
            if (ctrlDown && KeyPressed(Keys.C, currentKeyboardState, _previousKeyboardState))
            {
                CopySelectionToClipboard();
            }

            HandleTextInput(currentKeyboardState, gameTime);
            HandleScrolling(currentMouseState);
            HandleSelection(currentMouseState);

            // Update Copy Button
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            int panelWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
            int panelHeight = (int)(graphicsDevice.PresentationParameters.BackBufferHeight * 3f / 5f);
            _copyButton.Bounds = new Rectangle(panelWidth - 65, panelHeight - 20, 60, 15);
            _copyButton.Update(currentMouseState);

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void ProcessLogQueue()
        {
            bool newLogsAdded = false;
            while (GameLogger.LogQueue.TryDequeue(out var log))
            {
                Color baseColor = _global.Palette_LightGray;
                switch (log.Severity)
                {
                    case LogSeverity.Warning: baseColor = _global.Palette_Yellow; break;
                    case LogSeverity.Error: baseColor = _global.Palette_Red; break;
                    case LogSeverity.Critical: baseColor = Color.Magenta; break;
                    case LogSeverity.Info: baseColor = _global.Palette_LightGray; break;
                }

                var coloredLine = ParseColoredText(log.Text, baseColor);
                _history.Add(coloredLine);
                newLogsAdded = true;
            }

            if (newLogsAdded)
            {
                if (_history.Count > MAX_HISTORY_LINES)
                {
                    int removeCount = _history.Count - MAX_HISTORY_LINES;
                    _history.RemoveRange(0, removeCount);
                    // Adjust selection indices if items were removed
                    if (_selectionStartLine != -1) _selectionStartLine = Math.Max(0, _selectionStartLine - removeCount);
                    if (_selectionEndLine != -1) _selectionEndLine = Math.Max(0, _selectionEndLine - removeCount);
                }
            }
        }

        private void HandleSelection(MouseState mouseState)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            int panelHeight = (int)(graphicsDevice.PresentationParameters.BackBufferHeight * 3f / 5f);

            // Only handle selection if mouse is within the log area (above the input line)
            float logAreaBottom = panelHeight - (Global.TERMINAL_LINE_SPACING * 2) - 5;

            if (mouseState.Y > logAreaBottom) return;

            // Calculate which line index is under the mouse
            // Logic mirrors Draw loop: y = logAreaBottom - i * Spacing
            // i = (logAreaBottom - mouseY) / Spacing
            // historyIndex = startHistoryIndex - i

            int startHistoryIndex = _history.Count - 1 - _scrollOffset;
            float relativeY = logAreaBottom - mouseState.Y;
            int lineOffset = (int)(relativeY / Global.TERMINAL_LINE_SPACING);
            int hoveredIndex = startHistoryIndex - lineOffset;

            // Clamp index
            if (hoveredIndex < 0) hoveredIndex = -1;
            if (hoveredIndex >= _history.Count) hoveredIndex = -1;

            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                if (!_isSelecting)
                {
                    // Start selection
                    _isSelecting = true;
                    _selectionStartLine = hoveredIndex;
                    _selectionEndLine = hoveredIndex;
                }
                else
                {
                    // Drag selection
                    if (hoveredIndex != -1)
                    {
                        _selectionEndLine = hoveredIndex;
                    }
                }
            }
            else
            {
                _isSelecting = false;
            }
        }

        private void CopySelectionToClipboard()
        {
            if (_selectionStartLine == -1 || _selectionEndLine == -1)
            {
                // If nothing selected, copy everything visible or last 50 lines
                _selectionStartLine = Math.Max(0, _history.Count - 50);
                _selectionEndLine = _history.Count - 1;
            }

            int start = Math.Min(_selectionStartLine, _selectionEndLine);
            int end = Math.Max(_selectionStartLine, _selectionEndLine);

            StringBuilder sb = new StringBuilder();
            for (int i = start; i <= end; i++)
            {
                if (i >= 0 && i < _history.Count)
                {
                    foreach (var segment in _history[i].Segments)
                    {
                        sb.Append(segment.Text);
                    }
                    sb.AppendLine();
                }
            }

            if (sb.Length > 0)
            {
                ClipboardHelper.SetText(sb.ToString());
                GameLogger.Log(LogSeverity.Info, "[SYSTEM] Selection copied to clipboard.");
                ClearSelection();
            }
        }

        private void HandleScrolling(MouseState currentMouseState)
        {
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= Math.Sign(scrollDelta) * 3;
                _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _history.Count - 1));
            }
        }

        private void HandleTextInput(KeyboardState currentKeyboardState, GameTime gameTime)
        {
            foreach (Keys key in currentKeyboardState.GetPressedKeys())
            {
                if (KeyPressed(key, currentKeyboardState, _previousKeyboardState))
                {
                    // Ignore keys if Ctrl is held (for copy/paste shortcuts)
                    bool ctrlDown = currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl);
                    if (!ctrlDown)
                    {
                        char? character = GetCharFromKey(key, currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift));
                        if (character.HasValue)
                        {
                            _currentInput += character.Value;
                            _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
                        }
                    }
                }
            }

            if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
            {
                if (!string.IsNullOrWhiteSpace(_currentInput))
                {
                    GameLogger.Log(LogSeverity.Info, $"> {_currentInput}");
                    if (!_commandHistory.Any() || _commandHistory.Last() != _currentInput) _commandHistory.Add(_currentInput);
                    _commandProcessor.ProcessCommand(_currentInput);
                    _currentInput = "";
                    _scrollOffset = 0;
                    _commandHistoryIndex = -1;
                    _currentEditingCommand = "";
                    _autoCompleteManager.HideSuggestions();
                }
            }
            else if (KeyPressed(Keys.Tab, currentKeyboardState, _previousKeyboardState))
            {
                if (_autoCompleteManager.ShowingAutoCompleteSuggestions && _autoCompleteManager.SelectedAutoCompleteSuggestionIndex != -1)
                {
                    _currentInput = _autoCompleteManager.AutoCompleteSuggestions[_autoCompleteManager.SelectedAutoCompleteSuggestionIndex];
                    _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
                }
            }
            else if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState))
            {
                if (_autoCompleteManager.ShowingAutoCompleteSuggestions) _autoCompleteManager.CycleSelection(-1);
                else NavigateCommandHistory(1);
            }
            else if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState))
            {
                if (_autoCompleteManager.ShowingAutoCompleteSuggestions) _autoCompleteManager.CycleSelection(1);
                else NavigateCommandHistory(-1);
            }
            else if (KeyPressed(Keys.PageUp, currentKeyboardState, _previousKeyboardState))
            {
                _scrollOffset = Math.Min(_scrollOffset + 5, Math.Max(0, _history.Count - 1));
            }
            else if (KeyPressed(Keys.PageDown, currentKeyboardState, _previousKeyboardState))
            {
                _scrollOffset = Math.Max(0, _scrollOffset - 5);
            }

            HandleBackspace(currentKeyboardState, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private void HandleBackspace(KeyboardState currentKeyboardState, float deltaTime)
        {
            if (currentKeyboardState.IsKeyDown(Keys.Back))
            {
                bool ctrlDown = currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl);
                if (ctrlDown && KeyPressed(Keys.Back, currentKeyboardState, _previousKeyboardState))
                {
                    DeleteWord();
                    _backspaceTimer = BACKSPACE_INITIAL_DELAY;
                }
                else if (!ctrlDown)
                {
                    _backspaceTimer -= deltaTime;
                    if (_backspaceTimer <= 0f)
                    {
                        DeleteChar();
                        _backspaceTimer = KeyPressed(Keys.Back, currentKeyboardState, _previousKeyboardState) ? BACKSPACE_INITIAL_DELAY : BACKSPACE_REPEAT_DELAY;
                    }
                }
            }
            else
            {
                _backspaceTimer = 0f;
            }
        }

        private void DeleteChar()
        {
            if (_currentInput.Length > 0)
            {
                _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
            }
        }

        private void DeleteWord()
        {
            if (string.IsNullOrEmpty(_currentInput)) return;
            int trimEndIndex = _currentInput.Length - 1;
            while (trimEndIndex >= 0 && char.IsWhiteSpace(_currentInput[trimEndIndex])) trimEndIndex--;
            if (trimEndIndex < 0) { _currentInput = ""; _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput); return; }
            int lastSpace = _currentInput.LastIndexOf(' ', trimEndIndex);
            if (lastSpace != -1) _currentInput = _currentInput.Substring(0, lastSpace + 1);
            else _currentInput = "";
            _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
        }

        private void NavigateCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;
            if (_commandHistoryIndex == -1) _currentEditingCommand = _currentInput;
            _commandHistoryIndex += direction;
            _commandHistoryIndex = Math.Clamp(_commandHistoryIndex, -1, _commandHistory.Count - 1);
            if (_commandHistoryIndex == -1) _currentInput = _currentEditingCommand;
            else _currentInput = _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex];
            _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsVisible) return;

            if (_isDrawing) return;
            _isDrawing = true;

            try
            {
                var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
                var pixel = ServiceLocator.Get<Texture2D>();

                int panelWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
                int panelHeight = (int)(graphicsDevice.PresentationParameters.BackBufferHeight * 3f / 5f);
                var panelBounds = new Rectangle(0, 0, panelWidth, panelHeight);

                spriteBatch.Draw(pixel, panelBounds, _global.TerminalBg * 0.95f);

                float y = panelBounds.Bottom - (Global.TERMINAL_LINE_SPACING * 2) - 5;
                int maxVisibleLines = (panelHeight - (Global.TERMINAL_LINE_SPACING * 2) - 10) / Global.TERMINAL_LINE_SPACING;

                int startHistoryIndex = _history.Count - 1 - _scrollOffset;

                // Determine selection range
                int selMin = Math.Min(_selectionStartLine, _selectionEndLine);
                int selMax = Math.Max(_selectionStartLine, _selectionEndLine);
                bool hasSelection = _selectionStartLine != -1 && _selectionEndLine != -1;

                for (int i = 0; i < maxVisibleLines; i++)
                {
                    int historyIndex = startHistoryIndex - i;
                    if (historyIndex < 0) break;

                    float lineY = y - i * Global.TERMINAL_LINE_SPACING;
                    if (lineY < panelBounds.Y) break;

                    // Draw Selection Background
                    if (hasSelection && historyIndex >= selMin && historyIndex <= selMax)
                    {
                        var selectionRect = new Rectangle(0, (int)lineY, panelWidth, Global.TERMINAL_LINE_SPACING);
                        spriteBatch.Draw(pixel, selectionRect, Color.Blue * 0.5f);
                    }

                    float currentX = 5;
                    var line = _history[historyIndex];
                    foreach (var segment in line.Segments)
                    {
                        spriteBatch.DrawString(font, segment.Text, new Vector2(currentX, lineY), segment.Color);
                        currentX += font.MeasureString(segment.Text).Width;
                    }
                }

                float inputLineY = panelBounds.Bottom - Global.TERMINAL_LINE_SPACING - 5;
                _caratBlinkTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                string carat = (_caratBlinkTimer % 1.0f > 0.5f) ? "_" : "";

                _stringBuilder.Clear();
                _stringBuilder.Append("> ").Append(_currentInput).Append(carat);
                spriteBatch.DrawString(font, _stringBuilder.ToString(), new Vector2(5, inputLineY), _global.Palette_Yellow);

                DrawAutoComplete(spriteBatch, font, inputLineY);

                // Draw Copy Button
                _copyButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
            catch (Exception ex)
            {
                GameLogger.Log(LogSeverity.Error, $"Console Draw Error: {ex.Message}");
            }
            finally
            {
                _isDrawing = false;
            }
        }

        private void DrawAutoComplete(SpriteBatch spriteBatch, BitmapFont font, float inputLineY)
        {
            if (!_autoCompleteManager.ShowingAutoCompleteSuggestions || !_autoCompleteManager.AutoCompleteSuggestions.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            int visibleSuggestions = Math.Min(_autoCompleteManager.AutoCompleteSuggestions.Count, 5);
            float maxSuggestionWidth = 0;

            for (int i = 0; i < visibleSuggestions; i++)
            {
                string suggestion = _autoCompleteManager.AutoCompleteSuggestions[i];
                maxSuggestionWidth = Math.Max(maxSuggestionWidth, font.MeasureString(suggestion).Width);
            }
            maxSuggestionWidth += 20;

            float boxHeight = visibleSuggestions * Global.TERMINAL_LINE_SPACING + 4;
            float boxY = inputLineY - boxHeight - 2;
            var boxBounds = new Rectangle(5, (int)boxY, (int)maxSuggestionWidth, (int)boxHeight);

            spriteBatch.Draw(pixel, boxBounds, _global.Palette_Black * 0.9f);
            // Draw borders... (omitted for brevity, same as before)

            for (int i = 0; i < visibleSuggestions; i++)
            {
                bool isSelected = i == _autoCompleteManager.SelectedAutoCompleteSuggestionIndex;
                Color color = isSelected ? _global.Palette_Yellow : _global.Palette_LightGray;
                string suggestion = _autoCompleteManager.AutoCompleteSuggestions[i];
                float suggestionY = boxBounds.Bottom - (i + 1) * Global.TERMINAL_LINE_SPACING - 2;

                if (isSelected) spriteBatch.Draw(pixel, new Rectangle(boxBounds.X + 1, (int)suggestionY, boxBounds.Width - 2, Global.TERMINAL_LINE_SPACING), _global.Palette_DarkGray);
                spriteBatch.DrawString(font, suggestion, new Vector2(boxBounds.X + 4, suggestionY), color);
            }
        }

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? _global.Palette_BrightWhite;
            var currentText = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    if (i + 1 < text.Length && text[i + 1] == '[') { currentText += '['; i++; continue; }
                    if (currentText.Length > 0) { line.Segments.Add(new ColoredText(currentText, currentColor)); currentText = ""; }
                    int closeIndex = text.IndexOf(']', i);
                    if (closeIndex != -1)
                    {
                        string colorTag = text.Substring(i + 1, closeIndex - i - 1);
                        i = closeIndex;
                        currentColor = colorTag == "/" ? (baseColor ?? _global.Palette_BrightWhite) : ParseColor(colorTag);
                    }
                    else currentText += text[i];
                }
                else currentText += text[i];
            }
            if (currentText.Length > 0) line.Segments.Add(new ColoredText(currentText, currentColor));
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
                case "palette_teal": return _global.Palette_Teal;
                case "palette_yellow": return _global.Palette_Yellow;
                case "palette_red": return _global.Palette_Red;
                case "gray": return Color.Gray;
                default: return _global.Palette_BrightWhite;
            }
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        private char? GetCharFromKey(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z) return shift ? (char)key : char.ToLower((char)key);
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    switch (key)
                    {
                        case Keys.D1: return '!';
                        case Keys.D2: return '@';
                        case Keys.D3: return '#';
                        case Keys.D4: return '$';
                        case Keys.D5: return '%';
                        case Keys.D6: return '^';
                        case Keys.D7: return '&';
                        case Keys.D8: return '*';
                        case Keys.D9: return '(';
                        case Keys.D0: return ')';
                    }
                }
                return (char)('0' + (key - Keys.D0));
            }
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
            switch (key)
            {
                case Keys.Space: return ' ';
                case Keys.OemTilde: return shift ? '~' : '`';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.OemMinus: return shift ? '_' : '-';
            }
            return null;
        }
    }
}
