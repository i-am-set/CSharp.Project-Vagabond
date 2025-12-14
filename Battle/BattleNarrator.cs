#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    public class BattleNarrator
    {
        // --- Internal Data Structures ---
        private enum TokenType { Word, Space, Newline }

        private class NarratorToken
        {
            public string Text;
            public Color Color;
            public TokenType Type;
            public float Width;
            public int Length => Text.Length;

            public NarratorToken(string text, Color color, TokenType type, float width)
            {
                Text = text;
                Color = color;
                Type = type;
                Width = width;
            }
        }

        // --- Dependencies & State ---
        private readonly Global _global;
        private readonly Rectangle _bounds;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        // Parsing & Display Data
        private List<NarratorToken> _allTokens = new List<NarratorToken>();
        private readonly List<List<NarratorToken>> _displayLines = new List<List<NarratorToken>>();

        // Typewriter State
        private int _currentTokenIndex;
        private int _currentCharIndex;
        private float _typewriterTimer;
        private float _timeoutTimer;
        private bool _isWaitingForInput;
        private bool _isFinishedTyping;

        // Layout
        private float _wrapWidth;
        private int _maxVisibleLines;
        private BitmapFont? _font;

        // Input State
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // --- Tuning ---
        private const float TYPEWRITER_SPEED = 0.01f;
        private const float AUTO_ADVANCE_SECONDS = 5.0f;
        private const int LINE_SPACING = 3;
        private const int SPACE_WIDTH = 5;

        public bool IsAutoProgressEnabled { get; set; } = false;
        public bool IsBusy => _messageQueue.Count > 0 || _allTokens.Count > 0;
        public bool IsWaitingForInput => _isWaitingForInput;

        public BattleNarrator(Rectangle bounds)
        {
            _global = ServiceLocator.Get<Global>();
            _bounds = bounds;
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void ForceClear()
        {
            _messageQueue.Clear();
            _allTokens.Clear();
            _displayLines.Clear();
            _isWaitingForInput = false;
            _isFinishedTyping = true;
        }

        public void Show(string message, BitmapFont font)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            _font = font;
            const int padding = 5;
            _wrapWidth = _bounds.Width - (padding * 4);
            _maxVisibleLines = Math.Min(7, (_bounds.Height - (padding * 2)) / (_font.LineHeight + LINE_SPACING));

            // Split by pipe for multiple segments, but don't parse yet
            var segments = message.Split('|');
            foreach (var segment in segments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    _messageQueue.Enqueue(segment.Trim());
                }
            }

            if (_messageQueue.Count > 0 && _allTokens.Count == 0)
            {
                ProcessNextSegment();
            }
        }

        private void ProcessNextSegment()
        {
            if (_messageQueue.Count > 0)
            {
                string rawMessage = _messageQueue.Dequeue();
                ParseMessage(rawMessage);

                // Reset Typewriter
                _currentTokenIndex = 0;
                _currentCharIndex = 0;
                _typewriterTimer = 0f;
                _timeoutTimer = AUTO_ADVANCE_SECONDS;
                _isWaitingForInput = false;
                _isFinishedTyping = false;

                // Prepare first line
                _displayLines.Clear();
                _displayLines.Add(new List<NarratorToken>());
            }
            else
            {
                _allTokens.Clear();
                _isWaitingForInput = false;
                _isFinishedTyping = true;
            }
        }

        /// <summary>
        /// High-performance, single-pass parser with Color Stack support.
        /// </summary>
        private void ParseMessage(string message)
        {
            _allTokens.Clear();
            var colorStack = new Stack<Color>();
            colorStack.Push(_global.Palette_BrightWhite); // Default color

            StringBuilder currentWord = new StringBuilder();

            for (int i = 0; i < message.Length; i++)
            {
                char c = message[i];

                if (c == '[')
                {
                    // 1. Flush existing word
                    if (currentWord.Length > 0)
                    {
                        FlushWord(currentWord, colorStack.Peek());
                    }

                    // 2. Parse Tag
                    int closingBracketIndex = message.IndexOf(']', i);
                    if (closingBracketIndex != -1)
                    {
                        string tagContent = message.Substring(i + 1, closingBracketIndex - i - 1);

                        if (tagContent == "/")
                        {
                            // Pop color, but never pop the base default color
                            if (colorStack.Count > 1) colorStack.Pop();
                        }
                        else
                        {
                            // Push new color
                            colorStack.Push(_global.GetNarrationColor(tagContent));
                        }

                        i = closingBracketIndex; // Advance index
                        continue;
                    }
                    else
                    {
                        // Malformed tag, treat as literal
                        currentWord.Append(c);
                    }
                }
                else if (c == ' ')
                {
                    // 1. Flush existing word
                    if (currentWord.Length > 0)
                    {
                        FlushWord(currentWord, colorStack.Peek());
                    }

                    // 2. Add Space Token
                    _allTokens.Add(new NarratorToken(" ", Color.Transparent, TokenType.Space, SPACE_WIDTH));
                }
                else if (c == '\n')
                {
                    // 1. Flush existing word
                    if (currentWord.Length > 0)
                    {
                        FlushWord(currentWord, colorStack.Peek());
                    }

                    // 2. Add Newline Token
                    _allTokens.Add(new NarratorToken("\n", Color.Transparent, TokenType.Newline, 0));
                }
                else
                {
                    // Accumulate character
                    currentWord.Append(c);
                }
            }

            // Flush remaining word
            if (currentWord.Length > 0)
            {
                FlushWord(currentWord, colorStack.Peek());
            }
        }

        private void FlushWord(StringBuilder sb, Color color)
        {
            string text = sb.ToString().ToUpper(); // Enforce uppercase style
            float width = _font!.MeasureString(text).Width;
            _allTokens.Add(new NarratorToken(text, color, TokenType.Word, width));
            sb.Clear();
        }

        private void FinishCurrentSegmentInstantly()
        {
            // Re-calculate all lines instantly
            _displayLines.Clear();
            var currentLine = new List<NarratorToken>();
            _displayLines.Add(currentLine);
            float currentLineWidth = 0f;

            foreach (var token in _allTokens)
            {
                ProcessTokenLayout(token, ref currentLine, ref currentLineWidth);
            }

            _isFinishedTyping = true;
            _isWaitingForInput = true;
            _timeoutTimer = AUTO_ADVANCE_SECONDS;
        }

        private void ProcessTokenLayout(NarratorToken token, ref List<NarratorToken> currentLine, ref float currentLineWidth)
        {
            if (token.Type == TokenType.Newline)
            {
                currentLine = new List<NarratorToken>();
                _displayLines.Add(currentLine);
                currentLineWidth = 0f;
                CheckMaxLines();
                return;
            }

            // Check wrapping
            // If it's a space, we generally allow it at the end of a line, but if it pushes us over, 
            // we might wrap. However, standard behavior is usually to wrap WORDS, not spaces.
            // For simplicity: If adding this token exceeds width, wrap.
            // Exception: If the line is empty, we must add it (to prevent infinite loops on huge words).

            if (currentLineWidth + token.Width > _wrapWidth && currentLine.Count > 0)
            {
                // If the token causing the wrap is a Space, just ignore it (eat trailing space)
                if (token.Type == TokenType.Space) return;

                currentLine = new List<NarratorToken>();
                _displayLines.Add(currentLine);
                currentLineWidth = 0f;
                CheckMaxLines();
            }

            // Add token
            currentLine.Add(token);
            currentLineWidth += token.Width;
        }

        private void CheckMaxLines()
        {
            if (_displayLines.Count > _maxVisibleLines)
            {
                _displayLines.RemoveAt(0);
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!IsBusy) return;

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            bool mouseJustReleased = UIInputManager.CanProcessMouseClick() &&
                                     mouseState.LeftButton == ButtonState.Released &&
                                     _previousMouseState.LeftButton == ButtonState.Pressed;

            bool keyJustPressed = (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                                  (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space));

            bool advance = mouseJustReleased || keyJustPressed;

            if (_isWaitingForInput)
            {
                if (IsAutoProgressEnabled)
                {
                    _timeoutTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                }

                if (advance || (IsAutoProgressEnabled && _timeoutTimer <= 0))
                {
                    if (mouseJustReleased) UIInputManager.ConsumeMouseClick();
                    ProcessNextSegment();
                }
            }
            else // Typing
            {
                if (advance)
                {
                    FinishCurrentSegmentInstantly();
                    if (mouseJustReleased) UIInputManager.ConsumeMouseClick();
                }
                else
                {
                    _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    // Process as many characters as needed based on elapsed time
                    while (_typewriterTimer >= TYPEWRITER_SPEED && !_isFinishedTyping)
                    {
                        _typewriterTimer -= TYPEWRITER_SPEED;
                        AdvanceTypewriter();
                    }
                }
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void AdvanceTypewriter()
        {
            if (_currentTokenIndex >= _allTokens.Count)
            {
                _isFinishedTyping = true;
                _isWaitingForInput = true;
                _timeoutTimer = AUTO_ADVANCE_SECONDS;
                return;
            }

            var token = _allTokens[_currentTokenIndex];

            // If we are starting a new token, we need to add it to the layout
            if (_currentCharIndex == 0)
            {
                // Get the current line and width state
                var currentLine = _displayLines.Last();
                float currentLineWidth = 0f;
                foreach (var t in currentLine) currentLineWidth += t.Width;

                // Determine if this token needs to wrap
                // We create a "Partial" token for the layout, but we check the FULL width for wrapping
                if (token.Type == TokenType.Newline)
                {
                    _displayLines.Add(new List<NarratorToken>());
                    CheckMaxLines();
                    _currentTokenIndex++;
                    return; // Done with this token
                }

                if (currentLineWidth + token.Width > _wrapWidth && currentLine.Count > 0)
                {
                    if (token.Type != TokenType.Space)
                    {
                        _displayLines.Add(new List<NarratorToken>());
                        CheckMaxLines();
                    }
                    else
                    {
                        // Skip space at end of line
                        _currentTokenIndex++;
                        return;
                    }
                }

                // Add a placeholder token to the display list that we will "fill up"
                // For spaces, we just add them fully immediately
                if (token.Type == TokenType.Space)
                {
                    _displayLines.Last().Add(token);
                    _currentTokenIndex++;
                    return;
                }
                else
                {
                    // Add an empty clone of the token to the display line
                    _displayLines.Last().Add(new NarratorToken("", token.Color, token.Type, 0f));
                }
            }

            // Append next character to the last token in the display list
            var displayLine = _displayLines.Last();
            var displayToken = displayLine.Last();

            displayToken.Text += token.Text[_currentCharIndex];
            // Update width for correct layout calculations next frame
            displayToken.Width = _font!.MeasureString(displayToken.Text).Width;

            _currentCharIndex++;

            if (_currentCharIndex >= token.Length)
            {
                _currentTokenIndex++;
                _currentCharIndex = 0;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_displayLines.Count == 0) return;

            const int padding = 5;
            var panelBounds = new Rectangle(
                _bounds.X + padding,
                _bounds.Y + padding,
                _bounds.Width - padding * 2,
                _bounds.Height - padding * 2
            );

            // Draw text
            for (int i = 0; i < _displayLines.Count; i++)
            {
                var line = _displayLines[i];
                float currentX = panelBounds.X + padding;
                float currentY = panelBounds.Y + padding - 2 + (i * (font.LineHeight + LINE_SPACING));

                foreach (var token in line)
                {
                    if (token.Type == TokenType.Word)
                    {
                        spriteBatch.DrawStringSnapped(font, token.Text, new Vector2(currentX, currentY), token.Color);
                    }
                    // Spaces just advance X, they don't draw
                    currentX += token.Width;
                }
            }

            // Draw "next" indicator
            if (_isWaitingForInput)
            {
                const string arrow = "v";
                const string gap = " ";
                const string widestEllipsis = "...";

                Vector2 widestEllipsisSize = font.MeasureString(widestEllipsis);
                Vector2 arrowSize = font.MeasureString(arrow);
                Vector2 gapSize = font.MeasureString(gap);
                float totalIndicatorWidth = widestEllipsisSize.X + gapSize.X + arrowSize.X;

                float startX = panelBounds.Right - 3 - totalIndicatorWidth;
                float yPos = panelBounds.Bottom - 10;

                if (IsAutoProgressEnabled)
                {
                    string ellipsisToShow;
                    if (_timeoutTimer > (AUTO_ADVANCE_SECONDS * 2 / 3f)) ellipsisToShow = "...";
                    else if (_timeoutTimer > (AUTO_ADVANCE_SECONDS / 3f)) ellipsisToShow = "..";
                    else ellipsisToShow = ".";

                    Vector2 currentEllipsisSize = font.MeasureString(ellipsisToShow);
                    float ellipsisX = startX + (widestEllipsisSize.X - currentEllipsisSize.X);
                    var ellipsisPosition = new Vector2(ellipsisX, yPos);
                    spriteBatch.DrawStringSnapped(font, ellipsisToShow, ellipsisPosition, _global.Palette_Yellow);
                }

                float yOffset = ((float)gameTime.TotalGameTime.TotalSeconds * 4 % 1.0f > 0.5f) ? -1f : 0f;
                var indicatorPosition = new Vector2(startX + widestEllipsisSize.X + gapSize.X, yPos + yOffset);
                spriteBatch.DrawStringSnapped(font, arrow, indicatorPosition, _global.Palette_Yellow);
            }
        }
    }
}