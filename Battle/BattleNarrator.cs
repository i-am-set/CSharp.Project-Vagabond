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

        // Tuning constants
        private const float TYPEWRITER_SPEED = 0.01f; // Seconds per character
        private const float AUTO_ADVANCE_SECONDS = 5.0f; // Seconds to wait for input

        /// <summary>
        /// If true, the narrator will automatically advance after a delay.
        /// If false, it waits indefinitely for user input.
        /// Defaults to false.
        /// </summary>
        public bool IsAutoProgressEnabled { get; set; } = false;

        public bool IsBusy => _messageQueue.Count > 0 || !string.IsNullOrEmpty(_currentSegment);

        /// <summary>
        /// Returns true if the narrator has finished typing the current segment and is waiting for the user to proceed.
        /// </summary>
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
            _currentSegment = "";
            _words.Clear();
            _displayLines.Clear();
            _isWaitingForInput = false;
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
            _displayLines.Clear();
            var currentLine = new StringBuilder();
            _displayLines.Add(currentLine);

            foreach (var word in _words)
            {
                var potentialText = (currentLine.Length > 0 ? currentLine.ToString() + " " : "") + word;
                if (_font.MeasureString(potentialText).Width > _wrapWidth)
                {
                    currentLine = new StringBuilder();
                    _displayLines.Add(currentLine);
                    if (_displayLines.Count > _maxVisibleLines)
                    {
                        _displayLines.RemoveAt(0);
                    }
                }

                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
            }

            _isWaitingForInput = true;
            _timeoutTimer = AUTO_ADVANCE_SECONDS;
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
                // Only tick the timer if auto-progress is enabled
                if (IsAutoProgressEnabled)
                {
                    _timeoutTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                }

                // Advance if input received OR (auto-progress is on AND timer expired)
                if (advance || (IsAutoProgressEnabled && _timeoutTimer <= 0))
                {
                    if (mouseJustReleased) UIInputManager.ConsumeMouseClick();
                    ProcessNextSegment();
                }
            }
            else // Typing out text
            {
                if (advance)
                {
                    // Finish the current segment instantly
                    FinishCurrentSegmentInstantly();
                    if (mouseJustReleased) UIInputManager.ConsumeMouseClick();
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

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsBusy) return;

            // Removed background and border drawing logic.
            // The border is now handled by the BattleBorderCombat sprite in the main UI layer.

            const int padding = 5;
            // Use the bounds directly for text positioning
            var panelBounds = new Rectangle(
                _bounds.X + padding,
                _bounds.Y + padding,
                _bounds.Width - padding * 2,
                _bounds.Height - padding * 2
            );

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
                const string arrow = "v";
                const string gap = " ";
                const string widestEllipsis = "...";

                Vector2 widestEllipsisSize = font.MeasureString(widestEllipsis);
                Vector2 arrowSize = font.MeasureString(arrow);
                Vector2 gapSize = font.MeasureString(gap);
                float totalIndicatorWidth = widestEllipsisSize.X + gapSize.X + arrowSize.X;

                float startX = panelBounds.Right - 3 - totalIndicatorWidth;
                float yPos = panelBounds.Bottom - 10;

                // Only draw the ellipsis countdown if auto-progress is enabled
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

                // Always draw the arrow to indicate input is required/accepted
                float yOffset = ((float)gameTime.TotalGameTime.TotalSeconds * 4 % 1.0f > 0.5f) ? -1f : 0f;
                var indicatorPosition = new Vector2(startX + widestEllipsisSize.X + gapSize.X, yPos + yOffset);
                spriteBatch.DrawStringSnapped(font, arrow, indicatorPosition, _global.Palette_Yellow);
            }
        }
    }
}
