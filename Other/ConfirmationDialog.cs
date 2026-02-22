using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class ConfirmationDialog : Dialog
    {
        private class RichTextToken
        {
            public string Text;
            public Color Color;
        }

        private string _prompt;
        private List<List<RichTextToken>> _wrappedPromptLines;
        private List<string> _details;
        private List<Button> _buttons;
        private readonly NavigationGroup _navigationGroup;
        private readonly InputManager _inputManager;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;
        private bool _isHorizontalLayout;

        public ConfirmationDialog(GameScene currentGameScene) : base(currentGameScene)
        {
            _buttons = new List<Button>();
            _details = new List<string>();
            _wrappedPromptLines = new List<List<RichTextToken>>();
            _navigationGroup = new NavigationGroup(wrapNavigation: true);
            _inputManager = ServiceLocator.Get<InputManager>();
        }

        public void Show(string prompt, List<Tuple<string, Action>> buttonActions, List<string> details = null)
        {
            _currentGameScene?.ResetInputBlockTimer();

            _prompt = prompt;
            _details = details ?? new List<string>();
            _buttons.Clear();
            _navigationGroup.Clear();
            _isHorizontalLayout = false;

            IsActive = true;
            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            var defaultFont = ServiceLocator.Get<BitmapFont>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            float dialogWidth = 280;
            float currentHeight = 20;

            _wrappedPromptLines = ParseAndWrapPrompt(_prompt.ToUpper(), dialogWidth - 40, secondaryFont);

            currentHeight += _wrappedPromptLines.Count * secondaryFont.LineHeight;
            currentHeight += 10;

            if (_details.Any())
            {
                currentHeight += 10;
                foreach (var detail in _details)
                {
                    var wrappedDetail = WrapText(defaultFont, detail, dialogWidth - 60);
                    currentHeight += wrappedDetail.Count * (defaultFont.LineHeight + Global.APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING);
                }
                currentHeight += 15;
            }

            _isHorizontalLayout = buttonActions.Count == 2;

            // --- Pre-calculate button geometries to determine layout height ---
            var buttonGeometries = new List<(string text, Color? color, Action action, int width, int height)>();
            int maxButtonHeight = 0;
            int totalHorizontalButtonWidth = 0;
            int interButtonGap = 12;
            int verticalButtonGap = 8;

            foreach (var (taggedText, action) in buttonActions)
            {
                var (text, color) = ParseButtonTextAndColor(taggedText);
                Vector2 size = defaultFont.MeasureString(text);

                int bW = (int)size.X + 15;
                int bH = (int)size.Y + 7; // Base height calculation

                buttonGeometries.Add((text, color, action, bW, bH));

                if (bH > maxButtonHeight) maxButtonHeight = bH;
                totalHorizontalButtonWidth += bW;
            }

            if (_isHorizontalLayout) totalHorizontalButtonWidth += interButtonGap;

            float buttonAreaHeight;
            if (_isHorizontalLayout)
            {
                buttonAreaHeight = maxButtonHeight;
            }
            else
            {
                buttonAreaHeight = buttonGeometries.Sum(b => b.height) + ((buttonGeometries.Count - 1) * verticalButtonGap);
            }

            currentHeight += buttonAreaHeight;
            currentHeight += 20; // Bottom padding

            _dialogBounds = new Rectangle((Global.VIRTUAL_WIDTH - (int)dialogWidth) / 2, (Global.VIRTUAL_HEIGHT - (int)currentHeight) / 2, (int)dialogWidth, (int)currentHeight);

            float buttonAreaTopY = _dialogBounds.Bottom - buttonAreaHeight - 10;

            if (_isHorizontalLayout)
            {
                float startX = _dialogBounds.Center.X - totalHorizontalButtonWidth / 2f;
                float currentX = startX;
                float buttonY = buttonAreaTopY;

                foreach (var b in buttonGeometries)
                {
                    float yOffset = (maxButtonHeight - b.height) / 2f;

                    var button = new Button(new Rectangle((int)currentX, (int)(buttonY + yOffset) - 4, b.width, b.height + 5), b.text) { CustomDefaultTextColor = b.color };

                    button.OnClick += b.action;
                    _buttons.Add(button);
                    _navigationGroup.Add(button);
                    currentX += b.width + interButtonGap;
                }
            }
            else
            {
                float currentButtonY = buttonAreaTopY;
                foreach (var b in buttonGeometries)
                {
                    var button = new Button(new Rectangle(_dialogBounds.Center.X - b.width / 2, (int)currentButtonY - 4, b.width, b.height + 5), b.text) { CustomDefaultTextColor = b.color };

                    button.OnClick += b.action;
                    _buttons.Add(button);
                    _navigationGroup.Add(button);
                    currentButtonY += b.height + verticalButtonGap;
                }
            }

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            var currentMouseState = _inputManager.GetEffectiveMouseState();

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
            }

            if (_currentInputDelay <= 0 && _buttons.Any())
            {
                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    if (_navigationGroup.CurrentSelection == null)
                    {
                        _navigationGroup.SelectFirst();
                    }
                    _navigationGroup.UpdateInput(_inputManager);
                }
            }

            _previousMouseState = currentMouseState;
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            spriteBatch.Draw(pixel, _dialogBounds, _global.GameBg);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, _global.DullTextColor);

            float currentY = _dialogBounds.Y + 20;

            foreach (var line in _wrappedPromptLines)
            {
                float lineWidth = line.Sum(t => secondaryFont.MeasureString(t.Text).Width);
                float x = _dialogBounds.Center.X - lineWidth / 2f;

                foreach (var token in line)
                {
                    spriteBatch.DrawString(secondaryFont, token.Text, new Vector2(x, currentY), token.Color);
                    x += secondaryFont.MeasureString(token.Text).Width;
                }
                currentY += secondaryFont.LineHeight;
            }

            if (_details.Any())
            {
                currentY += 10;
                var dividerRect = new Rectangle(_dialogBounds.X + 20, (int)currentY, _dialogBounds.Width - 40, 1);
                spriteBatch.Draw(pixel, dividerRect, _global.Palette_DarkShadow);
                currentY += 10;

                foreach (var detail in _details)
                {
                    var wrappedDetail = WrapText(font, detail, _dialogBounds.Width - 60);
                    foreach (var line in wrappedDetail)
                    {
                        spriteBatch.DrawString(font, line, new Vector2(_dialogBounds.X + 30, currentY), _global.Palette_Sun);
                        currentY += font.LineHeight + Global.APPLY_OPTION_DIFFERENCE_TEXT_LINE_SPACING;
                    }
                }
            }

            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime, transform);

                if (button.IsHovered || button.IsSelected)
                {
                    DrawRectangleBorder(spriteBatch, pixel, button.Bounds, 1, _global.ButtonHoverColor);
                }
            }
        }

        private List<List<RichTextToken>> ParseAndWrapPrompt(string text, float maxWidth, BitmapFont font)
        {
            var rawTokens = new List<RichTextToken>();
            var currentColor = _global.Palette_Sun;

            int pos = 0;
            while (pos < text.Length)
            {
                int open = text.IndexOf('[', pos);
                if (open == -1)
                {
                    rawTokens.Add(new RichTextToken { Text = text.Substring(pos), Color = currentColor });
                    break;
                }

                if (open > pos)
                {
                    rawTokens.Add(new RichTextToken { Text = text.Substring(pos, open - pos), Color = currentColor });
                }

                int close = text.IndexOf(']', open);
                if (close == -1)
                {
                    rawTokens.Add(new RichTextToken { Text = text.Substring(open), Color = currentColor });
                    break;
                }

                string tag = text.Substring(open + 1, close - open - 1);
                if (tag == "/")
                {
                    currentColor = _global.Palette_Sun;
                }
                else
                {
                    currentColor = _global.GetNarrationColor(tag);
                }

                pos = close + 1;
            }

            var lines = new List<List<RichTextToken>>();
            var currentLine = new List<RichTextToken>();
            float currentLineWidth = 0f;

            foreach (var token in rawTokens)
            {
                string[] paragraphs = token.Text.Split('\n');

                for (int i = 0; i < paragraphs.Length; i++)
                {
                    if (i > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<RichTextToken>();
                        currentLineWidth = 0f;
                    }

                    string paragraph = paragraphs[i];
                    if (string.IsNullOrEmpty(paragraph)) continue;

                    string[] words = paragraph.Split(' ');

                    for (int j = 0; j < words.Length; j++)
                    {
                        string word = words[j];
                        bool needsSpace = (j > 0) || (currentLine.Count > 0);

                        string textWithSpace = needsSpace ? " " + word : word;
                        float widthWithSpace = font.MeasureString(textWithSpace).Width;

                        if (currentLineWidth + widthWithSpace <= maxWidth)
                        {
                            currentLine.Add(new RichTextToken { Text = textWithSpace, Color = token.Color });
                            currentLineWidth += widthWithSpace;
                        }
                        else
                        {
                            if (currentLine.Count > 0)
                            {
                                lines.Add(currentLine);
                                currentLine = new List<RichTextToken>();
                                currentLineWidth = 0f;
                            }

                            string textNoSpace = word;
                            float widthNoSpace = font.MeasureString(textNoSpace).Width;

                            currentLine.Add(new RichTextToken { Text = textNoSpace, Color = token.Color });
                            currentLineWidth += widthNoSpace;
                        }
                    }
                }
            }

            if (currentLine.Count > 0) lines.Add(currentLine);
            return lines;
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
                    case "red": color = _global.Palette_Rust; break;
                    case "green": color = _global.Palette_Leaf; break;
                    case "yellow": color = _global.Palette_DarkSun; break;
                    case "cemphasis": color = _global.ColorNarration_Emphasis; break;
                    case "chighlight": color = _global.ColorNarration_Highlight; break;
                    case "cdull": color = _global.ColorNarration_Dull; break;
                    default: return (taggedText, null);
                }
                return (text, color);
            }
            return (taggedText, null);
        }

        private List<string> WrapText(BitmapFont font, string text, float maxLineWidth)
        {
            var finalLines = new List<string>();
            if (string.IsNullOrEmpty(text)) return finalLines;

            var paragraphs = text.Split('\n');

            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(' ');
                var currentLine = "";
                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (font.MeasureString(testLine).Width > maxLineWidth)
                    {
                        finalLines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }
                finalLines.Add(currentLine);
            }
            return finalLines;
        }
    }
}