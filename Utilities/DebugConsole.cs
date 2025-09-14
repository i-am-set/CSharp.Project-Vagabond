using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A debug console overlay for entering commands.
    /// </summary>
    public class DebugConsole
    {
        public bool IsVisible { get; private set; }

        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly CommandProcessor _commandProcessor;

        private readonly List<ColoredLine> _history = new List<ColoredLine>();
        private readonly List<string> _commandHistory = new List<string>();
        private int _commandHistoryIndex = -1;
        private string _currentEditingCommand = "";

        private string _currentInput = "";
        private int _scrollOffset = 0;
        private float _caratBlinkTimer = 0f;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        public DebugConsole()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _commandProcessor = ServiceLocator.Get<CommandProcessor>();
            EventBus.Subscribe<GameEvents.TerminalMessagePublished>(OnDebugMessagePublished);
        }

        private void OnDebugMessagePublished(GameEvents.TerminalMessagePublished e)
        {
            if (IsVisible)
            {
                var coloredLine = ParseColoredText(e.Message, e.BaseColor);
                _history.Add(coloredLine);
                if (_history.Count > 100)
                {
                    _history.RemoveAt(0);
                }
                _scrollOffset = 0;
            }
        }

        public void ClearHistory()
        {
            _history.Clear();
            _scrollOffset = 0;
            _history.Add(ParseColoredText("--- CONSOLE CLEARED ---", _global.Palette_Gray));
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
        }

        public void Update(GameTime gameTime)
        {
            if (!IsVisible) return;

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (currentKeyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                Hide();
                return;
            }

            HandleTextInput(currentKeyboardState);
            HandleScrolling(currentMouseState);

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleScrolling(MouseState currentMouseState)
        {
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= Math.Sign(scrollDelta) * 3;
                _scrollOffset = Math.Clamp(_scrollOffset, 0, _history.Count - 1);
            }
        }

        private void HandleTextInput(KeyboardState currentKeyboardState)
        {
            foreach (Keys key in currentKeyboardState.GetPressedKeys())
            {
                if (!_previousKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Enter)
                    {
                        if (!string.IsNullOrWhiteSpace(_currentInput))
                        {
                            var commandLine = ParseColoredText($"> {_currentInput}", _global.Palette_BrightWhite);
                            _history.Add(commandLine);

                            // Add to command history if it's not a consecutive duplicate
                            if (!_commandHistory.Any() || _commandHistory.Last() != _currentInput)
                            {
                                _commandHistory.Add(_currentInput);
                            }

                            _commandProcessor.ProcessCommand(_currentInput);
                            _currentInput = "";
                            _scrollOffset = 0;
                            _commandHistoryIndex = -1;
                            _currentEditingCommand = "";
                        }
                    }
                    else if (key == Keys.Up)
                    {
                        NavigateCommandHistory(1);
                    }
                    else if (key == Keys.Down)
                    {
                        NavigateCommandHistory(-1);
                    }
                    else if (key == Keys.Back)
                    {
                        if (_currentInput.Length > 0)
                        {
                            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                        }
                    }
                    else if (key == Keys.PageUp)
                    {
                        _scrollOffset = Math.Min(_scrollOffset + 5, _history.Count - 1);
                    }
                    else if (key == Keys.PageDown)
                    {
                        _scrollOffset = Math.Max(0, _scrollOffset - 5);
                    }
                    else
                    {
                        char? character = GetCharFromKey(key, currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift));
                        if (character.HasValue)
                        {
                            _currentInput += character.Value;
                        }
                    }
                }
            }
        }

        private void NavigateCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            // If we are not currently browsing, save the current input
            if (_commandHistoryIndex == -1)
            {
                _currentEditingCommand = _currentInput;
            }

            // Adjust index
            _commandHistoryIndex += direction;

            // Clamp index
            _commandHistoryIndex = Math.Clamp(_commandHistoryIndex, -1, _commandHistory.Count - 1);

            if (_commandHistoryIndex == -1)
            {
                // Returned to the "present"
                _currentInput = _currentEditingCommand;
            }
            else
            {
                // Get command from history (it's stored in reverse order of access)
                _currentInput = _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex];
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsVisible) return;

            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var pixel = ServiceLocator.Get<Texture2D>();

            int panelWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
            int panelHeight = (int)(graphicsDevice.PresentationParameters.BackBufferHeight * 3f / 5f);
            var panelBounds = new Rectangle(0, 0, panelWidth, panelHeight);

            spriteBatch.Draw(pixel, panelBounds, _global.TerminalBg * 0.75f);

            float y = panelBounds.Bottom - (Global.TERMINAL_LINE_SPACING * 2) - 5;
            int maxVisibleLines = (panelHeight - (Global.TERMINAL_LINE_SPACING * 2) - 10) / Global.TERMINAL_LINE_SPACING;

            int startHistoryIndex = _history.Count - 1 - _scrollOffset;

            for (int i = 0; i < maxVisibleLines; i++)
            {
                int historyIndex = startHistoryIndex - i;
                if (historyIndex < 0) break;

                float lineY = y - i * Global.TERMINAL_LINE_SPACING;
                if (lineY < panelBounds.Y) break;

                float currentX = 5;
                var line = _history[historyIndex];
                foreach (var segment in line.Segments)
                {
                    // Use the standard DrawString here as we are in screen space, not virtual space.
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
                            currentColor = _global.Palette_BrightWhite;
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

            return _global.Palette_BrightWhite;
        }

        private char? GetCharFromKey(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return shift ? (char)key : char.ToLower((char)key);
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
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return (char)('0' + (key - Keys.NumPad0));

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