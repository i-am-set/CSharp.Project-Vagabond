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

namespace ProjectVagabond.Battle.UI
{
    public class BattleNarrator
    {
        private readonly Global _global;
        private readonly Rectangle _bounds;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        private string _currentSegment = "";
        private List<string> _words = new List<string>();
        private int _wordIndex;
        private int _charInWordIndex;

        private readonly List<StringBuilder> _displayLines = new List<StringBuilder>();
        private float _wrapWidth;
        private int _maxVisibleLines;
        private BitmapFont _font;

        private float _typewriterTimer;
        private float _timeoutTimer;
        private bool _isWaitingForInput;

        // Input state
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private Rectangle _ellipsisHoverBounds;

        // Tuning constants
        private const float TYPEWRITER_SPEED = 0.04f; // Seconds per character
        private const float AUTO_ADVANCE_SECONDS = 5.0f; // Seconds to wait for input

        public bool IsBusy => _messageQueue.Count > 0 || !string.IsNullOrEmpty(_currentSegment);

        public BattleNarrator(Rectangle bounds)
        {
            _global = ServiceLocator.Get<Global>();
            _bounds = bounds;
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Show(string message, BitmapFont font)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            _font = font;
            const int padding = 5;
            _wrapWidth = _bounds.Width - (padding * 4);
            _maxVisibleLines = Math.Min(7, (_bounds.Height - (padding * 2)) / _font.LineHeight);

            string upperMessage = message.ToUpper();

            _messageQueue.Clear();
            var segments = upperMessage.Split('|');
            foreach (var segment in segments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    _messageQueue.Enqueue(segment.Trim());
                }
            }

            if (_messageQueue.Count > 0)
            {
                ProcessNextSegment();
            }
        }

        private void ProcessNextSegment()
        {
            if (_messageQueue.Count > 0)
            {
                _currentSegment = _messageQueue.Dequeue();
                _words = _currentSegment.Split(' ').ToList();
                _displayLines.Clear();
                _displayLines.Add(new StringBuilder());
                _wordIndex = 0;
                _charInWordIndex = 0;
                _typewriterTimer = 0f;
                _timeoutTimer = AUTO_ADVANCE_SECONDS;
                _isWaitingForInput = false;
            }
            else
            {
                _currentSegment = "";
                _isWaitingForInput = false;
            }
        }

        private void FinishCurrentSegmentInstantly()
        {
            // Instantly builds the final wrapped text for the current segment
            while (_wordIndex < _words.Count)
            {
                var word = _words[_wordIndex];
                var currentLine = _displayLines.Last();

                // Check if the word needs to wrap
                var potentialText = (currentLine.Length > 0 ? currentLine.ToString() + " " : "") + word;
                if (_font.MeasureString(potentialText).Width > _wrapWidth)
                {
                    _displayLines.Add(new StringBuilder());
                    if (_displayLines.Count > _maxVisibleLines)
                    {
                        _displayLines.RemoveAt(0);
                    }
                    currentLine = _displayLines.Last();
                }

                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
                _wordIndex++;
            }

            _isWaitingForInput = true;
            _timeoutTimer = AUTO_ADVANCE_SECONDS;
        }

        public void Update(GameTime gameTime)
        {
            if (!IsBusy) return;

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            bool mouseJustClicked = UIInputManager.CanProcessMouseClick() &&
                                    mouseState.LeftButton == ButtonState.Pressed &&
                                    _previousMouseState.LeftButton == ButtonState.Released;

            bool keyJustPressed = (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                                  (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space));

            bool advance = mouseJustClicked || keyJustPressed;

            if (_isWaitingForInput)
            {
                _timeoutTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (advance || _timeoutTimer <= 0)
                {
                    if (mouseJustClicked) UIInputManager.ConsumeMouseClick();
                    ProcessNextSegment();
                }
            }
            else // Typing out text
            {
                if (advance)
                {
                    // Finish the current segment instantly
                    FinishCurrentSegmentInstantly();
                    if (mouseJustClicked) UIInputManager.ConsumeMouseClick();
                }
                else
                {
                    _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_typewriterTimer >= TYPEWRITER_SPEED)
                    {
                        _typewriterTimer = 0f;
                        if (_wordIndex < _words.Count)
                        {
                            var word = _words[_wordIndex];
                            if (_charInWordIndex == 0) // Start of a new word, check for wrapping
                            {
                                var currentLine = _displayLines.Last();
                                var potentialText = (currentLine.Length > 0 ? currentLine.ToString() + " " : "") + word;
                                if (_font.MeasureString(potentialText).Width > _wrapWidth)
                                {
                                    _displayLines.Add(new StringBuilder());
                                    if (_displayLines.Count > _maxVisibleLines)
                                    {
                                        _displayLines.RemoveAt(0);
                                    }
                                }
                            }

                            var lineToAppendTo = _displayLines.Last();
                            if (_charInWordIndex == 0 && lineToAppendTo.Length > 0)
                            {
                                lineToAppendTo.Append(" ");
                            }
                            lineToAppendTo.Append(word[_charInWordIndex]);
                            _charInWordIndex++;

                            if (_charInWordIndex >= word.Length)
                            {
                                _wordIndex++;
                                _charInWordIndex = 0;
                            }
                        }
                        else
                        {
                            _isWaitingForInput = true;
                            _timeoutTimer = AUTO_ADVANCE_SECONDS;
                        }
                    }
                }
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void CalculateIndicatorLayout()
        {
            const int padding = 5;
            var panelBounds = new Rectangle(
                _bounds.X + padding,
                _bounds.Y + padding,
                _bounds.Width - padding * 2,
                _bounds.Height - padding * 2
            );

            const string arrow = "v";
            const string gap = " ";
            const string widestEllipsis = "...";

            Vector2 widestEllipsisSize = _font.MeasureString(widestEllipsis);
            Vector2 arrowSize = _font.MeasureString(arrow);
            Vector2 gapSize = _font.MeasureString(gap);
            float totalIndicatorWidth = widestEllipsisSize.X + gapSize.X + arrowSize.X;

            float startX = panelBounds.Right - 2 - totalIndicatorWidth;
            float yPos = panelBounds.Bottom - 10;

            _ellipsisHoverBounds = new Rectangle((int)startX, (int)yPos, (int)widestEllipsisSize.X, _font.LineHeight);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsBusy) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int padding = 5;
            var panelBounds = new Rectangle(
                _bounds.X + padding,
                _bounds.Y + padding,
                _bounds.Width - padding * 2,
                _bounds.Height - padding * 2
            );

            // Draw panel background and border
            spriteBatch.DrawSnapped(pixel, panelBounds, _global.TerminalBg * 0.9f);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Bottom), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Left, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Right, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);

            // Draw text
            if (_displayLines.Any())
            {
                for (int i = 0; i < _displayLines.Count; i++)
                {
                    var textPosition = new Vector2(panelBounds.X + padding, panelBounds.Y + padding - 2 + (i * font.LineHeight));
                    spriteBatch.DrawStringSnapped(font, _displayLines[i].ToString(), textPosition, _global.Palette_BrightWhite);
                }
            }

            // Draw "next" indicator
            if (_isWaitingForInput)
            {
                string ellipsisToShow;
                if (_timeoutTimer > (AUTO_ADVANCE_SECONDS * 2 / 3f)) ellipsisToShow = "...";
                else if (_timeoutTimer > (AUTO_ADVANCE_SECONDS / 3f)) ellipsisToShow = "..";
                else ellipsisToShow = ".";

                const string arrow = "v";
                const string gap = " ";
                const string widestEllipsis = "...";

                Vector2 widestEllipsisSize = font.MeasureString(widestEllipsis);
                Vector2 arrowSize = font.MeasureString(arrow);
                Vector2 gapSize = font.MeasureString(gap);
                float totalIndicatorWidth = widestEllipsisSize.X + gapSize.X + arrowSize.X;

                float startX = panelBounds.Right - 3 - totalIndicatorWidth;
                float yPos = panelBounds.Bottom - 10;

                Vector2 currentEllipsisSize = font.MeasureString(ellipsisToShow);
                float ellipsisX = startX + (widestEllipsisSize.X - currentEllipsisSize.X);
                var ellipsisPosition = new Vector2(ellipsisX, yPos);
                spriteBatch.DrawStringSnapped(font, ellipsisToShow, ellipsisPosition, _global.Palette_Yellow);

                float yOffset = ((_timeoutTimer - MathF.Floor(_timeoutTimer)) > 0.5f) ? -1f : 0f;
                var indicatorPosition = new Vector2(startX + widestEllipsisSize.X + gapSize.X, yPos + yOffset);
                spriteBatch.DrawStringSnapped(font, arrow, indicatorPosition, _global.Palette_Yellow);
            }
        }
    }
}