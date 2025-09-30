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

namespace ProjectVagabond.Scenes
{
    public class NarrativeChoiceScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly ProgressionManager _progressionManager;
        private readonly Global _global;

        private string _prompt = "";
        private readonly List<Button> _buttons = new List<Button>();
        private List<ChoiceOutcome> _outcomes = new List<ChoiceOutcome>();
        private int _selectedButtonIndex = -1;

        public NarrativeChoiceScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public void Show(string prompt, List<NarrativeChoice> choices)
        {
            _prompt = prompt.ToUpper();
            _buttons.Clear();
            _outcomes.Clear();

            const int panelWidth = 280;
            const int buttonHeight = 13;
            const int buttonGap = 2;
            int totalButtonHeight = choices.Count * (buttonHeight + buttonGap) - buttonGap;

            var font = ServiceLocator.Get<Core>().SecondaryFont;
            var wrappedPrompt = WrapText(_prompt, panelWidth - 20, font);

            int panelHeight = 20 + (wrappedPrompt.Count * font.LineHeight) + 10 + totalButtonHeight + 10;
            var panelBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - panelWidth) / 2,
                (Global.VIRTUAL_HEIGHT - panelHeight) / 2,
                panelWidth,
                panelHeight
            );

            float currentY = panelBounds.Bottom - 10 - totalButtonHeight;

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var button = new Button(
                    new Rectangle(panelBounds.X + 10, (int)currentY, panelBounds.Width - 20, buttonHeight),
                    choice.Text.ToUpper(),
                    font: font
                );
                int choiceIndex = i; // Capture index for the lambda
                button.OnClick += () => OnChoiceSelected(choiceIndex);
                _buttons.Add(button);
                _outcomes.Add(choice.Outcome);
                currentY += buttonHeight + buttonGap;
            }
        }

        private void OnChoiceSelected(int index)
        {
            if (index >= 0 && index < _outcomes.Count)
            {
                _progressionManager.OnNarrativeChoiceMade(_outcomes[index]);
            }
            _sceneManager.HideModal();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (IsInputBlocked) return;

            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedButtonIndex = i;
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont defaultFont, GameTime gameTime, Matrix transform)
        {
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            const int panelWidth = 280;
            int totalButtonHeight = _buttons.Count * (13 + 2) - 2;
            var wrappedPrompt = WrapText(_prompt, panelWidth - 20, font);
            int panelHeight = 20 + (wrappedPrompt.Count * font.LineHeight) + 10 + totalButtonHeight + 10;
            var panelBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - panelWidth) / 2,
                (Global.VIRTUAL_HEIGHT - panelHeight) / 2,
                panelWidth,
                panelHeight
            );

            // Draw panel
            spriteBatch.DrawSnapped(pixel, panelBounds, _global.TerminalBg);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Top), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Bottom), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Left, panelBounds.Top), new Vector2(panelBounds.Left, panelBounds.Bottom), _global.Palette_White);
            spriteBatch.DrawLineSnapped(new Vector2(panelBounds.Right, panelBounds.Top), new Vector2(panelBounds.Right, panelBounds.Bottom), _global.Palette_White);


            // Draw prompt
            float currentY = panelBounds.Y + 10;
            foreach (var line in wrappedPrompt)
            {
                var promptSize = font.MeasureString(line);
                spriteBatch.DrawStringSnapped(font, line, new Vector2(panelBounds.Center.X - promptSize.Width / 2f, currentY), _global.Palette_BrightWhite);
                currentY += font.LineHeight;
            }

            // Draw buttons
            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime, transform);
            }
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var screenBounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(ServiceLocator.Get<Texture2D>(), screenBounds, Color.Black * 0.7f);
            spriteBatch.End();
        }

        private List<string> WrapText(string text, float maxLineWidth, BitmapFont font)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0 ? currentLine.ToString() + " " + word : word;
                if (font.MeasureString(testLine).Width > maxLineWidth)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }
    }
}