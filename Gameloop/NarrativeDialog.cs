#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;

using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.UI
{
    public class NarrativeDialog : Dialog
    {
        private readonly StoryNarrator _narrator;
        private readonly List<Button> _choiceButtons = new();
        // Store the original rich text for each button to draw manually
        private readonly Dictionary<Button, string> _buttonRichText = new Dictionary<Button, string>();

        private Action<NarrativeChoice>? _onChoiceSelected;

        private enum DialogState { NarratingPrompt, AwaitingChoice }
        private DialogState _state;
        private static readonly Random _random = new Random();
        private readonly Global _global;

        // Rich Text Parsing Structs
        private struct RichTextToken
        {
            public string Text;
            public Color Color;
        }

        public NarrativeDialog(GameScene currentGameScene) : base(currentGameScene)
        {
            _global = ServiceLocator.Get<Global>();
            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 50, Global.VIRTUAL_WIDTH, 50);
            _narrator = new StoryNarrator(narratorBounds);
            _narrator.OnFinished += OnNarrationFinished;
        }

        public override void Hide()
        {
            base.Hide();
            _narrator.Clear();
        }

        public void Show(NarrativeEvent narrativeEvent, Action<NarrativeChoice> onChoiceSelected)
        {
            IsActive = true;
            _onChoiceSelected = onChoiceSelected;
            _choiceButtons.Clear();
            _buttonRichText.Clear();

            _narrator.Show(narrativeEvent.Prompt);
            _state = DialogState.NarratingPrompt;

            var font = ServiceLocator.Get<Core>().SecondaryFont;
            float currentY = 40;

            foreach (var choice in narrativeEvent.Choices)
            {
                // 1. Parse text to get raw string for measurement (stripping tags)
                string rawText = StripTags(choice.Text.ToUpper());
                var textSize = font.MeasureString(rawText);

                // 2. Create button with EMPTY text so it handles input/bg but doesn't draw the string
                var button = new Button(Rectangle.Empty, "", font: font) { AlignLeft = true, IsEnabled = false };
                button.Bounds = new Rectangle(40, (int)currentY, (int)textSize.Width + 10, (int)textSize.Height + 4);

                button.OnClick += () =>
                {
                    _onChoiceSelected?.Invoke(choice);
                    Hide();
                };

                _choiceButtons.Add(button);
                _buttonRichText[button] = choice.Text.ToUpper(); // Store rich text for manual drawing

                currentY += textSize.Height + 8;
            }
        }

        private string StripTags(string text)
        {
            return Regex.Replace(text, @"\[.*?\]", "");
        }

        private void OnNarrationFinished()
        {
            if (_state == DialogState.NarratingPrompt)
            {
                _state = DialogState.AwaitingChoice;
                foreach (var button in _choiceButtons)
                {
                    button.IsEnabled = true;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _narrator.Update(gameTime);

            if (_state == DialogState.AwaitingChoice)
            {
                var mouseState = Mouse.GetState();
                foreach (var button in _choiceButtons.ToList())
                {
                    button.Update(mouseState);
                }
            }
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            if (_state == DialogState.AwaitingChoice)
            {
                foreach (var button in _choiceButtons)
                {
                    // 1. Draw the button (Background, Border, Hover State)
                    // Since text is empty, it just draws the box.
                    button.Draw(spriteBatch, secondaryFont, gameTime, transform);

                    // 2. Manually draw the Rich Text on top
                    if (_buttonRichText.TryGetValue(button, out string richText))
                    {
                        // Calculate position (Align Left + Padding)
                        // Button.cs uses LEFT_ALIGN_PADDING = 4
                        float x = button.Bounds.X + 4;
                        float y = button.Bounds.Center.Y - (secondaryFont.LineHeight / 2f);

                        DrawRichTextLine(spriteBatch, secondaryFont, richText, new Vector2(x, y), button.IsHovered);
                    }
                }
            }

            _narrator.Draw(spriteBatch, secondaryFont, gameTime);
        }

        private void DrawRichTextLine(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, bool isHovered)
        {
            var tokens = ParseRichText(text);
            float currentX = position.X;
            float currentY = position.Y;

            foreach (var token in tokens)
            {
                // If hovered, override default text to hover color (Red), 
                // BUT keep specific colored tags (like [cGreen]) as is.
                Color drawColor = token.Color;

                if (drawColor == _global.ColorNarration_Default && isHovered)
                {
                    drawColor = _global.ButtonHoverColor;
                }

                spriteBatch.DrawStringSnapped(font, token.Text, new Vector2(currentX, currentY), drawColor);
                currentX += font.MeasureString(token.Text).Width;
            }
        }

        private List<RichTextToken> ParseRichText(string text)
        {
            var tokens = new List<RichTextToken>();
            var colorStack = new Stack<Color>();

            colorStack.Push(_global.ColorNarration_Default);

            var parts = Regex.Split(text, @"(\[.*?\])");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string tagContent = part.Substring(1, part.Length - 2);
                    if (tagContent == "/" || tagContent.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        if (colorStack.Count > 1) colorStack.Pop();
                    }
                    else
                    {
                        colorStack.Push(_global.GetNarrationColor(tagContent));
                    }
                }
                else
                {
                    tokens.Add(new RichTextToken { Text = part, Color = colorStack.Peek() });
                }
            }
            return tokens;
        }
    }
}