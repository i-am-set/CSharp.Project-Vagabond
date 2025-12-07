#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class BattleNarrator
    {
        private readonly Global _global;
        private readonly Rectangle _bounds;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        private string _currentSegment = "";
        private List<ColoredText> _words = new List<ColoredText>();
        private int _wordIndex;
        private int _charInWordIndex;

        private readonly List<List<ColoredText>> _displayLines = new List<List<ColoredText>>();
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
        private const int LINE_SPACING = 3; // Extra vertical pixels between lines

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
            // Adjust max lines calculation to account for extra line spacing
            _maxVisibleLines = Math.Min(7, (_bounds.Height - (padding * 2)) / (_font.LineHeight + LINE_SPACING));

            // Note: We do NOT uppercase here anymore, as it might mess with case-sensitive tags if any exist (though our tags are lowercase).
            // We will uppercase the text content during parsing.

            _messageQueue.Clear();
            var segments = message.Split('|');
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

                // Parse the segment into colored words
                _words = ParseMessageToWords(_currentSegment);

                _displayLines.Clear();
                _displayLines.Add(new List<ColoredText>());
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

        private List<ColoredText> ParseMessageToWords(string message)
        {
            var words = new List<ColoredText>();
            var parts = Regex.Split(message, @"(\[.*?\])");
            Color currentColor = _global.Palette_BrightWhite;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2);
                    if (tagContent == "/")
                    {
                        currentColor = _global.Palette_BrightWhite;
                    }
                    else
                    {
                        currentColor = _global.GetNarrationColor(tagContent);
                    }
                }
                else
                {
                    // Handle explicit newlines by replacing them with a special token or splitting
                    string processedPart = part.Replace("\n", " \n ");
                    var rawWords = processedPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var rawWord in rawWords)
                    {
                        words.Add(new ColoredText(rawWord.ToUpper(), currentColor));
                    }
                }
            }
            return words;
        }

        private void FinishCurrentSegmentInstantly()
        {
            _displayLines.Clear();
            var currentLine = new List<ColoredText>();
            _displayLines.Add(currentLine);

            float currentLineWidth = 0f;
            float spaceWidth = _font.MeasureString(" ").Width;

            foreach (var wordObj in _words)
            {
                string wordText = wordObj.Text;

                if (wordText == "\n")
                {
                    currentLine = new List<ColoredText>();
                    _displayLines.Add(currentLine);
                    currentLineWidth = 0f;
                    if (_displayLines.Count > _maxVisibleLines) _displayLines.RemoveAt(0);
                    continue;
                }

                float wordWidth = _font.MeasureString(wordText).Width;
                float potentialWidth = currentLineWidth + (currentLine.Count > 0 ? spaceWidth : 0) + wordWidth;

                if (potentialWidth > _wrapWidth)
                {
                    currentLine = new List<ColoredText>();
                    _displayLines.Add(currentLine);
                    currentLineWidth = 0f;
                    if (_displayLines.Count > _maxVisibleLines) _displayLines.RemoveAt(0);
                }

                if (currentLine.Count > 0)
                {
                    // Append space to the previous word if it exists, or add a space word?
                    // Simpler: Just add a space to the current word's text if it's not the start of line
                    // But we can't modify the source _words.
                    // We will add a separate space entry or handle it in draw.
                    // Let's append a space to the previous entry in the line for simplicity in this instant finish logic.
                    var last = currentLine.Last();
                    last.Text += " ";
                    currentLineWidth += spaceWidth;
                }

                currentLine.Add(new ColoredText(wordText, wordObj.Color));
                currentLineWidth += wordWidth;
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
            else // Typing out text
            {
                if (advance)
                {
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
                            var wordObj = _words[_wordIndex];
                            string wordText = wordObj.Text;

                            // Handle Newline Token
                            if (wordText == "\n")
                            {
                                _displayLines.Add(new List<ColoredText>());
                                if (_displayLines.Count > _maxVisibleLines)
                                {
                                    _displayLines.RemoveAt(0);
                                }
                                _wordIndex++;
                                _charInWordIndex = 0;
                                return;
                            }

                            // Start of a new word: Check wrapping
                            if (_charInWordIndex == 0)
                            {
                                var currentLine = _displayLines.Last();
                                float currentLineWidth = 0f;
                                foreach (var segment in currentLine) currentLineWidth += _font.MeasureString(segment.Text).Width;

                                float spaceWidth = _font.MeasureString(" ").Width;
                                float wordWidth = _font.MeasureString(wordText).Width;
                                float potentialWidth = currentLineWidth + (currentLine.Count > 0 ? spaceWidth : 0) + wordWidth;

                                if (potentialWidth > _wrapWidth)
                                {
                                    _displayLines.Add(new List<ColoredText>());
                                    if (_displayLines.Count > _maxVisibleLines)
                                    {
                                        _displayLines.RemoveAt(0);
                                    }
                                }
                                else if (currentLine.Count > 0)
                                {
                                    // Add space to the previous word in the line
                                    currentLine.Last().Text += " ";
                                }

                                // Add the new word container to the line
                                _displayLines.Last().Add(new ColoredText("", wordObj.Color));
                            }

                            // Append character
                            var lineToAppendTo = _displayLines.Last();
                            var wordToAppendTo = lineToAppendTo.Last();
                            wordToAppendTo.Text += wordText[_charInWordIndex];

                            _charInWordIndex++;

                            if (_charInWordIndex >= wordText.Length)
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
            if (_displayLines.Count == 0) return;

            const int padding = 5;
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
                    var line = _displayLines[i];
                    float currentX = panelBounds.X + padding;
                    float currentY = panelBounds.Y + padding - 2 + (i * (font.LineHeight + LINE_SPACING));

                    foreach (var segment in line)
                    {
                        spriteBatch.DrawStringSnapped(font, segment.Text, new Vector2(currentX, currentY), segment.Color);
                        currentX += font.MeasureString(segment.Text).Width;
                    }
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
