using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI; // Added for TextAnimator and TextEffectType
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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
            public TextEffectType Effect;
            public int Length => Text.Length;

            public NarratorToken(string text, Color color, TokenType type, float width, TextEffectType effect = TextEffectType.None)
            {
                Text = text;
                Color = color;
                Type = type;
                Width = width;
                Effect = effect;
            }
        }

        // --- Dependencies & State ---
        private readonly Global _global;
        private readonly Rectangle _bounds;

        // Parsing & Display Data
        private List<NarratorToken> _allTokens = new List<NarratorToken>();
        private readonly List<List<NarratorToken>> _displayLines = new List<List<NarratorToken>>();

        // Typewriter State
        private int _visibleCharCount = 0;
        private int _totalCharCount = 0;
        private float _typewriterTimer;
        private bool _isFastForwarding;

        // Layout
        private float _wrapWidth;
        private int _maxVisibleLines;
        private BitmapFont? _font;

        // Input State
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        // --- Tuning ---
        private const float TYPEWRITER_SPEED = 0.01f; // Very fast for combat log
        public const float TYPEWRITER_FAST_MULTIPLIER = 4.0f;

        private const int LINE_SPACING = 3;
        private const int SPACE_WIDTH = 5;

        public bool IsBusy => _visibleCharCount < _totalCharCount;
        public bool IsWaitingForInput => !IsBusy && _totalCharCount > 0; // Waiting for user to dismiss/advance

        public BattleNarrator(Rectangle bounds)
        {
            _global = ServiceLocator.Get<Global>();
            _bounds = bounds;
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void ForceClear()
        {
            _allTokens.Clear();
            _displayLines.Clear();
            _visibleCharCount = 0;
            _totalCharCount = 0;
            _isFastForwarding = false;
        }

        public void UpdateLog(string fullLogText, BitmapFont font)
        {
            _font = font;
            const int padding = 5;
            _wrapWidth = _bounds.Width - (padding * 4);
            _maxVisibleLines = Math.Min(7, (_bounds.Height - (padding * 2)) / (_font.LineHeight + LINE_SPACING));

            // If the log was cleared or reset, reset our counters
            if (string.IsNullOrEmpty(fullLogText))
            {
                ForceClear();
                return;
            }

            // Re-parse the entire log. 
            // Optimization: In a very large log, we might want to only parse the new suffix, 
            // but for a combat round log (usually < 10 lines), re-parsing is negligible and safer.
            ParseMessage(fullLogText);
            LayoutTokens();

            // Calculate total characters in the new layout
            int newTotalChars = 0;
            foreach (var line in _displayLines)
            {
                foreach (var token in line)
                {
                    newTotalChars += token.Length;
                }
            }

            // If text shrank (new round), reset visible count
            if (newTotalChars < _totalCharCount)
            {
                _visibleCharCount = 0;
            }

            _totalCharCount = newTotalChars;
            _isFastForwarding = false; // Reset speed on new text
        }

        private void ParseMessage(string message)
        {
            _allTokens.Clear();
            var colorStack = new Stack<Color>();
            var effectStack = new Stack<TextEffectType>();

            colorStack.Push(_global.ColorNarration_Default);
            effectStack.Push(TextEffectType.None);

            StringBuilder currentWord = new StringBuilder();

            for (int i = 0; i < message.Length; i++)
            {
                char c = message[i];

                if (c == '[')
                {
                    if (currentWord.Length > 0) FlushWord(currentWord, colorStack.Peek(), effectStack.Peek());

                    int closingBracketIndex = message.IndexOf(']', i);
                    if (closingBracketIndex != -1)
                    {
                        string tagContent = message.Substring(i + 1, closingBracketIndex - i - 1);

                        if (tagContent == "/")
                        {
                            if (colorStack.Count > 1) colorStack.Pop();
                            if (effectStack.Count > 1) effectStack.Pop();
                        }
                        else if (Enum.TryParse<TextEffectType>(tagContent, true, out var effect))
                        {
                            effectStack.Push(effect);
                        }
                        else
                        {
                            colorStack.Push(_global.GetNarrationColor(tagContent));
                        }
                        i = closingBracketIndex;
                        continue;
                    }
                    else
                    {
                        currentWord.Append(c);
                    }
                }
                else if (c == ' ')
                {
                    if (currentWord.Length > 0) FlushWord(currentWord, colorStack.Peek(), effectStack.Peek());
                    _allTokens.Add(new NarratorToken(" ", Color.Transparent, TokenType.Space, SPACE_WIDTH));
                }
                else if (c == '\n')
                {
                    if (currentWord.Length > 0) FlushWord(currentWord, colorStack.Peek(), effectStack.Peek());
                    _allTokens.Add(new NarratorToken("\n", Color.Transparent, TokenType.Newline, 0));
                }
                else
                {
                    currentWord.Append(c);
                }
            }

            if (currentWord.Length > 0) FlushWord(currentWord, colorStack.Peek(), effectStack.Peek());
        }

        private void FlushWord(StringBuilder sb, Color color, TextEffectType effect)
        {
            string text = sb.ToString().ToUpper();
            float width = _font!.MeasureString(text).Width;
            _allTokens.Add(new NarratorToken(text, color, TokenType.Word, width, effect));
            sb.Clear();
        }

        private void LayoutTokens()
        {
            _displayLines.Clear();
            var currentLine = new List<NarratorToken>();
            float currentLineWidth = 0f;

            foreach (var token in _allTokens)
            {
                if (token.Type == TokenType.Newline)
                {
                    _displayLines.Add(currentLine);
                    currentLine = new List<NarratorToken>();
                    currentLineWidth = 0f;
                    continue;
                }

                if (currentLineWidth + token.Width > _wrapWidth && currentLine.Count > 0)
                {
                    if (token.Type == TokenType.Space) continue; // Eat trailing space
                    _displayLines.Add(currentLine);
                    currentLine = new List<NarratorToken>();
                    currentLineWidth = 0f;
                }

                currentLine.Add(token);
                currentLineWidth += token.Width;
            }

            if (currentLine.Count > 0) _displayLines.Add(currentLine);
        }

        public void Update(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            bool mouseJustReleased = UIInputManager.CanProcessMouseClick() &&
                                     mouseState.LeftButton == ButtonState.Released &&
                                     _previousMouseState.LeftButton == ButtonState.Pressed;

            bool keyJustPressed = (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                                  (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space));

            bool advance = mouseJustReleased || keyJustPressed;

            if (IsBusy)
            {
                // If user clicks while typing, speed up
                if (advance)
                {
                    _isFastForwarding = true;
                    if (mouseJustReleased) UIInputManager.ConsumeMouseClick();
                }

                float currentSpeed = TYPEWRITER_SPEED;
                if (_isFastForwarding) currentSpeed /= TYPEWRITER_FAST_MULTIPLIER;

                _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                while (_typewriterTimer >= currentSpeed && _visibleCharCount < _totalCharCount)
                {
                    _typewriterTimer -= currentSpeed;
                    _visibleCharCount++;
                }
            }
            else
            {
                // If finished typing, user input is handled by BattleUIManager to advance the turn
                _isFastForwarding = false;
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Vector2 offset)
        {
            if (_displayLines.Count == 0) return;

            const int padding = 5;
            var panelBounds = new Rectangle(
                (int)(_bounds.X + padding + offset.X),
                (int)(_bounds.Y + padding + offset.Y),
                _bounds.Width - padding * 2,
                _bounds.Height - padding * 2
            );

            // --- SCROLLING LOGIC ---
            // If we have more lines than fit, show the bottom-most lines
            int startLineIndex = 0;
            if (_displayLines.Count > _maxVisibleLines)
            {
                startLineIndex = _displayLines.Count - _maxVisibleLines;
            }

            float lineHeight = font.LineHeight + LINE_SPACING;
            float startY = panelBounds.Y; // Top align for log

            int charsProcessed = 0;

            for (int i = startLineIndex; i < _displayLines.Count; i++)
            {
                var line = _displayLines[i];
                float currentX = panelBounds.X;
                float currentY = startY + ((i - startLineIndex) * lineHeight);

                foreach (var token in line)
                {
                    // Check visibility based on typewriter
                    int charsInToken = token.Length;
                    int charsToDraw = Math.Min(charsInToken, _visibleCharCount - charsProcessed);

                    if (charsToDraw > 0)
                    {
                        string textToDraw = token.Text.Substring(0, charsToDraw);

                        if (token.Type == TokenType.Word)
                        {
                            if (token.Effect == TextEffectType.None)
                            {
                                spriteBatch.DrawStringSnapped(font, textToDraw, new Vector2(currentX, currentY), token.Color);
                            }
                            else
                            {
                                // Render character by character for effects
                                for (int c = 0; c < textToDraw.Length; c++)
                                {
                                    char charToDraw = textToDraw[c];
                                    string charStr = charToDraw.ToString();
                                    string sub = textToDraw.Substring(0, c);
                                    float charOffsetX = font.MeasureString(sub + "|").Width - font.MeasureString("|").Width;

                                    var (animOffset, scale, rotation, color) = TextAnimator.GetTextEffectTransform(
                                        token.Effect,
                                        (float)gameTime.TotalGameTime.TotalSeconds,
                                        charsProcessed + c,
                                        token.Color
                                    );

                                    Vector2 pos = new Vector2(currentX + charOffsetX, currentY) + animOffset;
                                    Vector2 origin = font.MeasureString(charStr) / 2f;

                                    spriteBatch.DrawString(font, charStr, pos + origin, color, rotation, origin, scale, SpriteEffects.None, 0f);
                                }
                            }
                        }
                    }

                    currentX += token.Width;
                    charsProcessed += charsInToken;
                }
            }

            // Draw "Next" Indicator if waiting
            if (IsWaitingForInput)
            {
                const string arrow = "v";
                var arrowSize = font.MeasureString(arrow);
                float yOffset = ((float)gameTime.TotalGameTime.TotalSeconds * 4 % 1.0f > 0.5f) ? -1f : 0f;
                var indicatorPosition = new Vector2(panelBounds.Right - arrowSize.Width, panelBounds.Bottom - arrowSize.Height + yOffset);
                spriteBatch.DrawStringSnapped(font, arrow, indicatorPosition, _global.Palette_DarkSun);
            }
        }
    }
}