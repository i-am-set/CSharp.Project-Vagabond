#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class StoryNarrator
    {
        public event Action? OnFinished;

        private readonly Global _global;
        private readonly Rectangle _bounds;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        // --- Token-Based Rendering ---
        private class NarratorToken
        {
            public string Text;
            public Color Color;
            public float Width;
            public bool IsNewline;
            public bool IsSmall; // New flag for font switching

            public NarratorToken(string text, Color color, float width, bool isNewline = false, bool isSmall = false)
            {
                Text = text;
                Color = color;
                Width = width;
                IsNewline = isNewline;
                IsSmall = isSmall;
            }
        }

        private List<List<NarratorToken>> _displayLines = new List<List<NarratorToken>>();

        // Typewriter State
        private int _currentLineIndex;
        private int _currentTokenIndex;
        private int _currentCharIndex;
        private float _typewriterTimer;
        private bool _isWaitingForInput;
        private bool _isFinishedTyping;

        private float _wrapWidth;
        private int _maxVisibleLines;
        private BitmapFont? _font;
        private BitmapFont? _tertiaryFont; // Added for small text

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        private const float TYPEWRITER_SPEED = 0.01f;
        private const int SPACE_WIDTH = 5; // Fixed width for spaces
        private const int LINE_SPACING = 4; // Increased line spacing

        public bool IsBusy => _messageQueue.Count > 0 || _displayLines.Count > 0;

        public StoryNarrator(Rectangle bounds)
        {
            _global = ServiceLocator.Get<Global>();
            _bounds = bounds;
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Clear()
        {
            _messageQueue.Clear();
            _displayLines.Clear();
            ResetTypewriter();
            _isWaitingForInput = false;
        }

        private void ResetTypewriter()
        {
            _currentLineIndex = 0;
            _currentTokenIndex = 0;
            _currentCharIndex = 0;
            _typewriterTimer = 0f;
            _isFinishedTyping = false;
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                OnFinished?.Invoke();
                return;
            }
            // Enqueue raw message. We parse it when we process it.
            _messageQueue.Enqueue(message);

            if (_displayLines.Count == 0 && !_isWaitingForInput)
            {
                ProcessNextMessage();
            }
        }

        private void ProcessNextMessage()
        {
            if (_messageQueue.Count > 0)
            {
                string rawMessage = _messageQueue.Dequeue();
                ParseAndLayoutMessage(rawMessage);
                ResetTypewriter();
                _isWaitingForInput = false;
            }
            else
            {
                _displayLines.Clear();
                _isWaitingForInput = false;
                OnFinished?.Invoke();
            }
        }

        private void ParseAndLayoutMessage(string message)
        {
            _font ??= ServiceLocator.Get<Core>().SecondaryFont;
            _tertiaryFont ??= ServiceLocator.Get<Core>().TertiaryFont;

            const int padding = 5;
            _wrapWidth = _bounds.Width - (padding * 4);
            _maxVisibleLines = (_bounds.Height - (padding * 2)) / (_font.LineHeight + LINE_SPACING);

            _displayLines.Clear();
            var currentLine = new List<NarratorToken>();
            float currentLineWidth = 0f;

            // 1. Parse into raw tokens (Text + Color + Font)
            var rawTokens = ParseRichText(message);

            // 2. Layout tokens into lines (Word Wrapping)
            foreach (var token in rawTokens)
            {
                if (token.IsNewline)
                {
                    _displayLines.Add(currentLine);
                    currentLine = new List<NarratorToken>();
                    currentLineWidth = 0f;
                    continue;
                }

                // Split token text by spaces to handle word wrapping
                // We keep the color and font of the original token
                string[] words = Regex.Split(token.Text, @"( )"); // Split but keep delimiters

                foreach (var word in words)
                {
                    if (string.IsNullOrEmpty(word)) continue;

                    // Use fixed width for spaces, otherwise measure font
                    float wordWidth;
                    if (word == " ")
                    {
                        wordWidth = SPACE_WIDTH;
                    }
                    else
                    {
                        // Measure using the correct font
                        var fontToUse = token.IsSmall ? _tertiaryFont : _font;
                        wordWidth = fontToUse.MeasureString(word).Width;
                    }

                    // Check if word fits
                    if (currentLineWidth + wordWidth > _wrapWidth && currentLine.Count > 0)
                    {
                        // If it's just a space causing the wrap, ignore it (eat trailing space)
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        _displayLines.Add(currentLine);
                        currentLine = new List<NarratorToken>();
                        currentLineWidth = 0f;
                    }

                    // Add word as a new token to the line
                    currentLine.Add(new NarratorToken(word, token.Color, wordWidth, false, token.IsSmall));
                    currentLineWidth += wordWidth;
                }
            }

            if (currentLine.Count > 0)
            {
                _displayLines.Add(currentLine);
            }
        }

        private List<NarratorToken> ParseRichText(string text)
        {
            var tokens = new List<NarratorToken>();
            var colorStack = new Stack<Color>();
            var fontStack = new Stack<bool>(); // true = small, false = normal

            colorStack.Push(_global.ColorNarration_Default);
            fontStack.Push(false); // Default to normal font

            // Regex to split by tags [tag] or newlines
            var parts = Regex.Split(text, @"(\[.*?\]|\n)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part == "\n")
                {
                    tokens.Add(new NarratorToken("", Color.Transparent, 0, true));
                }
                else if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2);

                    // Handle Font Tags
                    if (tagContent.Equals("small", StringComparison.OrdinalIgnoreCase))
                    {
                        fontStack.Push(true);
                    }
                    else if (tagContent == "/" || tagContent.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        // Pop color stack if > 1
                        if (colorStack.Count > 1) colorStack.Pop();

                        // Pop font stack if > 1
                        if (fontStack.Count > 1) fontStack.Pop();
                    }
                    else
                    {
                        // Assume color tag
                        colorStack.Push(ParseColor(tagContent));
                    }
                }
                else
                {
                    // Regular text
                    // Uppercase it here to match game style
                    string content = part.ToUpper();
                    tokens.Add(new NarratorToken(content, colorStack.Peek(), 0, false, fontStack.Peek())); // Width calculated later
                }
            }

            return tokens;
        }

        private Color ParseColor(string colorName)
        {
            // Use the centralized Global parser
            return _global.GetNarrationColor(colorName);
        }

        public void Update(GameTime gameTime)
        {
            if (!IsBusy) return;

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            bool advance = (UIInputManager.CanProcessMouseClick() && mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed) ||
                           (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                           (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space));

            if (advance)
            {
                UIInputManager.ConsumeMouseClick();
                if (_isWaitingForInput)
                {
                    ProcessNextMessage();
                }
                else
                {
                    FinishCurrentMessageInstantly();
                }
            }
            else if (!_isWaitingForInput && !_isFinishedTyping)
            {
                _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                while (_typewriterTimer >= TYPEWRITER_SPEED && !_isFinishedTyping)
                {
                    _typewriterTimer -= TYPEWRITER_SPEED;
                    AdvanceTypewriter();
                }
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void AdvanceTypewriter()
        {
            if (_displayLines.Count == 0)
            {
                _isFinishedTyping = true;
                _isWaitingForInput = true;
                return;
            }

            var currentLine = _displayLines[_currentLineIndex];
            var currentToken = currentLine[_currentTokenIndex];

            _currentCharIndex++;

            if (_currentCharIndex >= currentToken.Text.Length)
            {
                _currentCharIndex = 0;
                _currentTokenIndex++;

                if (_currentTokenIndex >= currentLine.Count)
                {
                    _currentTokenIndex = 0;
                    _currentLineIndex++;

                    if (_currentLineIndex >= _displayLines.Count)
                    {
                        _isFinishedTyping = true;
                        _isWaitingForInput = true;
                        // Clamp indices to end
                        _currentLineIndex = _displayLines.Count - 1;
                        _currentTokenIndex = _displayLines.Last().Count - 1;
                        _currentCharIndex = _displayLines.Last().Last().Text.Length;
                    }
                }
            }
        }

        private void FinishCurrentMessageInstantly()
        {
            _isFinishedTyping = true;
            _isWaitingForInput = true;
            // Set indices to the very end
            _currentLineIndex = _displayLines.Count - 1;
            if (_currentLineIndex >= 0)
            {
                var lastLine = _displayLines[_currentLineIndex];
                _currentTokenIndex = lastLine.Count - 1;
                if (_currentTokenIndex >= 0)
                {
                    _currentCharIndex = lastLine[_currentTokenIndex].Text.Length;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_displayLines.Count == 0) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int padding = 5;
            var panelBounds = new Rectangle(
                _bounds.X + padding, _bounds.Y + padding,
                _bounds.Width - padding * 2, _bounds.Height - padding * 2
            );

            // Draw Background - Fully Opaque
            spriteBatch.DrawSnapped(pixel, panelBounds, _global.TerminalBg);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Bottom), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Left, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Right, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);

            // Draw Text
            for (int i = 0; i < _displayLines.Count; i++)
            {
                // Only draw up to the current typing line
                if (i > _currentLineIndex && !_isFinishedTyping) break;

                var line = _displayLines[i];
                float currentX = panelBounds.X + padding;
                float currentY = panelBounds.Y + padding - 2 + (i * (font.LineHeight + LINE_SPACING));

                for (int j = 0; j < line.Count; j++)
                {
                    // If on current line, stop at current token
                    if (i == _currentLineIndex && j > _currentTokenIndex && !_isFinishedTyping) break;

                    var token = line[j];
                    string textToDraw = token.Text;

                    // If on current token, substring it
                    if (i == _currentLineIndex && j == _currentTokenIndex && !_isFinishedTyping)
                    {
                        textToDraw = token.Text.Substring(0, _currentCharIndex);
                    }

                    // Select font based on token property
                    var fontToUse = token.IsSmall ? _tertiaryFont : font;

                    // Adjust Y for small font to align baseline roughly
                    float yOffset = 0;
                    if (token.IsSmall)
                    {
                        yOffset = (font.LineHeight - fontToUse.LineHeight) / 2f + 1;
                    }

                    spriteBatch.DrawStringSnapped(fontToUse, textToDraw, new Vector2(currentX, currentY + yOffset), token.Color);
                    currentX += token.Width;
                }
            }

            // Draw "Next" Indicator
            if (_isWaitingForInput)
            {
                string arrow = "v";
                var arrowSize = font.MeasureString(arrow);
                float yOffset = ((float)gameTime.TotalGameTime.TotalSeconds * 4 % 1.0f > 0.5f) ? -1f : 0f;
                var indicatorPosition = new Vector2(panelBounds.Right - padding - arrowSize.Width, panelBounds.Bottom - padding - arrowSize.Height + yOffset);
                spriteBatch.DrawStringSnapped(font, arrow, indicatorPosition, _global.Palette_Yellow);
            }
        }
    }
}