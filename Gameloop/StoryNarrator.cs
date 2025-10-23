#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class StoryNarrator
    {
        public event Action? OnFinished;

        private readonly Global _global;
        private readonly Rectangle _bounds;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        private string _currentMessage = "";
        private int _charIndex;

        private readonly List<StringBuilder> _displayLines = new List<StringBuilder>();
        private float _wrapWidth;
        private int _maxVisibleLines;
        private BitmapFont? _font;

        private float _typewriterTimer;
        private bool _isWaitingForInput;

        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;

        private const float TYPEWRITER_SPEED = 0.01f;

        public bool IsBusy => _messageQueue.Count > 0 || !string.IsNullOrEmpty(_currentMessage);

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
            _currentMessage = "";
            _charIndex = 0;
            _displayLines.Clear();
            _isWaitingForInput = false;
            _typewriterTimer = 0f;
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                OnFinished?.Invoke();
                return;
            }
            _messageQueue.Enqueue(message.ToUpper());

            if (!IsBusy)
            {
                ProcessNextMessage();
            }
        }

        private void ProcessNextMessage()
        {
            if (_messageQueue.Count > 0)
            {
                _currentMessage = _messageQueue.Dequeue();
                _displayLines.Clear();
                _displayLines.Add(new StringBuilder());
                _charIndex = 0;
                _typewriterTimer = 0f;
                _isWaitingForInput = false;
            }
            else
            {
                _currentMessage = "";
                _isWaitingForInput = false;
                OnFinished?.Invoke();
            }
        }

        private void CheckForBlankMessageAndAdvance()
        {
            // If the message we just finished was effectively blank (only whitespace),
            // don't wait for input, just process the next message immediately.
            if (!_displayLines.Any(sb => sb.ToString().Trim().Length > 0))
            {
                ProcessNextMessage();
            }
            else
            {
                // The message had content, so wait for user input.
                _isWaitingForInput = true;
            }
        }

        private void FinishCurrentMessageInstantly()
        {
            _displayLines.Clear();
            var words = _currentMessage.Split(' ');
            var currentLine = new StringBuilder();
            _displayLines.Add(currentLine);

            foreach (var word in words)
            {
                var potentialText = (currentLine.Length > 0 ? currentLine + " " : "") + word;
                if (_font!.MeasureString(potentialText).Width > _wrapWidth)
                {
                    currentLine = new StringBuilder();
                    _displayLines.Add(currentLine);
                }
                if (currentLine.Length > 0) currentLine.Append(" ");
                currentLine.Append(word);
            }

            _charIndex = _currentMessage.Length;
            CheckForBlankMessageAndAdvance();
        }

        public void Update(GameTime gameTime)
        {
            _font ??= ServiceLocator.Get<Core>().SecondaryFont;
            const int padding = 5;
            _wrapWidth = _bounds.Width - (padding * 4);
            _maxVisibleLines = (_bounds.Height - (padding * 2)) / _font.LineHeight;

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
            else if (!_isWaitingForInput)
            {
                _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_typewriterTimer >= TYPEWRITER_SPEED)
                {
                    _typewriterTimer = 0f;
                    if (_charIndex < _currentMessage.Length)
                    {
                        char nextChar = _currentMessage[_charIndex];
                        var currentLine = _displayLines.Last();

                        if (char.IsWhiteSpace(nextChar))
                        {
                            var nextWord = GetNextWord(_currentMessage, _charIndex);
                            var potentialText = currentLine + (currentLine.Length > 0 ? " " : "") + nextWord;
                            if (_font.MeasureString(potentialText).Width > _wrapWidth)
                            {
                                _displayLines.Add(new StringBuilder());
                                if (_displayLines.Count > _maxVisibleLines) _displayLines.RemoveAt(0);
                            }
                            else if (currentLine.Length > 0)
                            {
                                currentLine.Append(" ");
                            }
                        }
                        else
                        {
                            _displayLines.Last().Append(nextChar);
                        }
                        _charIndex++;
                    }
                    else
                    {
                        CheckForBlankMessageAndAdvance();
                    }
                }
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private string GetNextWord(string text, int startIndex)
        {
            int i = startIndex;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int wordStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            return text.Substring(wordStart, i - wordStart);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsBusy) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int padding = 5;
            var panelBounds = new Rectangle(
                _bounds.X + padding, _bounds.Y + padding,
                _bounds.Width - padding * 2, _bounds.Height - padding * 2
            );

            spriteBatch.DrawSnapped(pixel, panelBounds, _global.TerminalBg * 0.9f);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Bottom), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Left, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Right, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);

            for (int i = 0; i < _displayLines.Count; i++)
            {
                var textPosition = new Vector2(panelBounds.X + padding, panelBounds.Y + padding - 2 + (i * font.LineHeight));
                spriteBatch.DrawStringSnapped(font, _displayLines[i].ToString(), textPosition, _global.Palette_BrightWhite);
            }

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