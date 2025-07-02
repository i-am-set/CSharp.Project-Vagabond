using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class ConfirmationDialog : Dialog
    {
        private string _prompt;
        private List<string> _details;
        private List<Button> _buttons;
        private int _selectedButtonIndex;

        private bool _keyboardNavigatedLastFrame;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private bool _isHorizontalLayout;

        public ConfirmationDialog(GameScene currentGameScene) : base(currentGameScene)
        {
            _buttons = new List<Button>();
            _details = new List<string>();
        }

        public void Show(string prompt, List<Tuple<string, Action>> buttonActions, List<string> details = null)
        {
            _currentGameScene?.ResetInputBlockTimer();

            _prompt = prompt;
            _details = details ?? new List<string>();
            _buttons.Clear();
            _isHorizontalLayout = false;

            IsActive = true;
            _selectedButtonIndex = 0;
            _keyboardNavigatedLastFrame = false;
            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            Core.Instance.IsMouseVisible = true;

            var font = Global.Instance.DefaultFont;
            float dialogWidth = 450;
            float currentHeight = 20;

            var wrappedPrompt = WrapText(font, _prompt, dialogWidth - 40);
            currentHeight += wrappedPrompt.Count * font.LineHeight;
            currentHeight += 10;

            if (_details.Any())
            {
                currentHeight += 10;
                foreach (var detail in _details)
                {
                    var wrappedDetail = WrapText(font, detail, dialogWidth - 60);
                    currentHeight += wrappedDetail.Count * (font.LineHeight + Global.APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING);
                }
                currentHeight += 15;
            }

            _isHorizontalLayout = buttonActions.Count == 2;

            float buttonAreaHeight;
            if (_isHorizontalLayout)
            {
                buttonAreaHeight = 25;
            }
            else
            {
                buttonAreaHeight = buttonActions.Count * 25;
            }
            currentHeight += buttonAreaHeight;
            currentHeight += 10;

            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - (int)dialogWidth) / 2,
                (Global.VIRTUAL_HEIGHT - (int)currentHeight) / 2,
                (int)dialogWidth,
                (int)currentHeight
            );

            int buttonHeight = 20;
            float buttonAreaTopY = _dialogBounds.Bottom - buttonAreaHeight - 10;

            if (_isHorizontalLayout)
            {
                int textHorizontalPadding = 32;
                int interButtonGap = 24;
                float buttonY = buttonAreaTopY + (buttonAreaHeight - buttonHeight) / 2f;

                var (taggedText1, action1) = buttonActions[0];
                var (taggedText2, action2) = buttonActions[1];

                var (text1, color1) = ParseButtonTextAndColor(taggedText1);
                var (text2, color2) = ParseButtonTextAndColor(taggedText2);

                float width1 = font.MeasureString(text1).Width + textHorizontalPadding;
                float width2 = font.MeasureString(text2).Width + textHorizontalPadding;

                float totalGroupWidth = width1 + interButtonGap + width2;
                float startX = _dialogBounds.Center.X - totalGroupWidth / 2;

                var button1 = new Button(new Rectangle((int)startX, (int)buttonY, (int)width1, buttonHeight), text1)
                {
                    CustomDefaultTextColor = color1
                };
                button1.OnClick += action1;
                _buttons.Add(button1);

                var button2 = new Button(new Rectangle((int)(startX + width1 + interButtonGap), (int)buttonY, (int)width2, buttonHeight), text2)
                {
                    CustomDefaultTextColor = color2
                };
                button2.OnClick += action2;
                _buttons.Add(button2);
            }
            else // Vertical layout
            {
                int buttonWidth = 180;
                float currentButtonY = buttonAreaTopY;
                foreach (var (taggedText, action) in buttonActions)
                {
                    var (text, color) = ParseButtonTextAndColor(taggedText);
                    float buttonY = currentButtonY + (25 - buttonHeight) / 2f;
                    var button = new Button(new Rectangle(_dialogBounds.Center.X - buttonWidth / 2, (int)buttonY, buttonWidth, buttonHeight), text)
                    {
                        CustomDefaultTextColor = color
                    };
                    button.OnClick += action;
                    _buttons.Add(button);
                    currentButtonY += 25;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (_keyboardNavigatedLastFrame)
            {
                _keyboardNavigatedLastFrame = false;
            }
            else if (currentMouseState.Position != _previousMouseState.Position)
            {
                Core.Instance.IsMouseVisible = true;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedButtonIndex = i;
                }
            }

            if (_currentInputDelay <= 0 && _buttons.Any())
            {
                bool upPressed = KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState);
                bool downPressed = KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState);
                bool leftPressed = KeyPressed(Keys.Left, currentKeyboardState, _previousKeyboardState);
                bool rightPressed = KeyPressed(Keys.Right, currentKeyboardState, _previousKeyboardState);

                if (upPressed || downPressed || leftPressed || rightPressed)
                {
                    Core.Instance.IsMouseVisible = false;
                    _keyboardNavigatedLastFrame = true;

                    if (_isHorizontalLayout)
                    {
                        if (leftPressed && _selectedButtonIndex > 0)
                        {
                            _selectedButtonIndex--;
                        }
                        else if (rightPressed && _selectedButtonIndex < _buttons.Count - 1)
                        {
                            _selectedButtonIndex++;
                        }
                    }
                    else
                    {
                        if (upPressed)
                        {
                            _selectedButtonIndex = (_selectedButtonIndex - 1 + _buttons.Count) % _buttons.Count;
                        }
                        else if (downPressed)
                        {
                            _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;
                        }
                    }

                    Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                    Mouse.SetPosition(screenPos.X, screenPos.Y);
                }

                if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                {
                    bool isButtonHighlighted = _buttons[_selectedButtonIndex].IsHovered || _keyboardNavigatedLastFrame;

                    if (isButtonHighlighted)
                    {
                        _buttons[_selectedButtonIndex].TriggerClick();
                    }
                    else
                    {
                        _selectedButtonIndex = 0;

                        Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        _keyboardNavigatedLastFrame = true;
                        Core.Instance.IsMouseVisible = false;
                    }
                }
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        protected override void DrawContent(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            var pixel = Core.Pixel;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            spriteBatch.Draw(pixel, _dialogBounds, Global.Instance.Palette_DarkGray);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, Global.Instance.Palette_LightGray);

            float currentY = _dialogBounds.Y + 20;

            var wrappedPrompt = WrapText(font, _prompt, _dialogBounds.Width - 40);
            foreach (var line in wrappedPrompt)
            {
                var promptSize = font.MeasureString(line);
                spriteBatch.DrawString(font, line, new Vector2(_dialogBounds.Center.X - promptSize.Width / 2, currentY), Global.Instance.Palette_BrightWhite);
                currentY += font.LineHeight;
            }

            if (_details.Any())
            {
                currentY += 10;
                var dividerRect = new Rectangle(_dialogBounds.X + 20, (int)currentY, _dialogBounds.Width - 40, 1);
                spriteBatch.Draw(pixel, dividerRect, Global.Instance.Palette_Gray);
                currentY += 10;

                foreach (var detail in _details)
                {
                    var wrappedDetail = WrapText(font, detail, _dialogBounds.Width - 60);
                    foreach (var line in wrappedDetail)
                    {
                        spriteBatch.DrawString(font, line, new Vector2(_dialogBounds.X + 30, currentY), Global.Instance.Palette_White);
                        currentY += font.LineHeight + Global.APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING;
                    }
                }
            }

            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            if (_buttons.Any())
            {
                var selectedButton = _buttons[_selectedButtonIndex];
                if (selectedButton.IsHovered || _keyboardNavigatedLastFrame)
                {
                    Vector2 textSize = font.MeasureString(selectedButton.Text);
                    int horizontalPadding = 8;
                    int verticalPadding = 4;
                    Rectangle highlightRect = new Rectangle(
                        (int)(selectedButton.Bounds.X + (selectedButton.Bounds.Width - textSize.X) * 0.5f - horizontalPadding),
                        (int)(selectedButton.Bounds.Y + (selectedButton.Bounds.Height - textSize.Y) * 0.5f - verticalPadding),
                        (int)(textSize.X + horizontalPadding * 2),
                        (int)(textSize.Y + verticalPadding * 2)
                    );
                    DrawRectangleBorder(spriteBatch, pixel, highlightRect, 1, Global.Instance.ButtonHoverColor);
                }
            }

            spriteBatch.End();
        }

        private (string text, Color? color) ParseButtonTextAndColor(string taggedText)
        {
            if (taggedText.StartsWith("[") && taggedText.Contains("]"))
            {
                int closingBracketIndex = taggedText.IndexOf(']');
                if (closingBracketIndex == -1) return (taggedText, null);

                string colorName = taggedText.Substring(1, closingBracketIndex - 1).ToLowerInvariant();
                string text = taggedText.Substring(closingBracketIndex + 1);

                Color color;
                switch (colorName)
                {
                    case "red": color = Global.Instance.Palette_Red; break;
                    case "green": color = Global.Instance.Palette_LightGreen; break;
                    case "yellow": color = Global.Instance.Palette_Yellow; break;
                    case "gray": color = Global.Instance.Palette_LightGray; break;
                    case "grey": color = Global.Instance.Palette_LightGray; break;
                    default:
                        return (taggedText, null);
                }
                return (text, color);
            }
            return (taggedText, null);
        }

        private List<string> WrapText(BitmapFont font, string text, float maxLineWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var words = text.Split(' ');
            var currentLine = "";
            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (font.MeasureString(testLine).Width > maxLineWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            lines.Add(currentLine);
            return lines;
        }
    }
}