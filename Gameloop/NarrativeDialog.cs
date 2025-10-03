#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class NarrativeDialog : Dialog
    {
        private readonly StoryNarrator _narrator;
        private readonly List<Button> _choiceButtons = new();
        private Action? _onComplete;

        private enum DialogState { NarratingPrompt, AwaitingChoice, NarratingResult, Finished }
        private DialogState _state;

        public NarrativeDialog(GameScene currentGameScene) : base(currentGameScene)
        {
            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 80, Global.VIRTUAL_WIDTH, 80);
            _narrator = new StoryNarrator(narratorBounds);
            _narrator.OnFinished += OnNarrationFinished;
        }

        public void Show(NarrativeEvent narrativeEvent, Action onComplete)
        {
            IsActive = true;
            _onComplete = onComplete;
            _choiceButtons.Clear();

            _narrator.Show(narrativeEvent.Prompt);
            _state = DialogState.NarratingPrompt;

            // Create buttons but keep them disabled for now
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            float currentY = 40;
            foreach (var choice in narrativeEvent.Choices)
            {
                var button = new Button(Rectangle.Empty, choice.Text.ToUpper(), font: font) { AlignLeft = true, IsEnabled = false };
                var textSize = font.MeasureString(button.Text);
                button.Bounds = new Rectangle(40, (int)currentY, (int)textSize.Width + 10, (int)textSize.Height + 4);
                button.OnClick += () =>
                {
                    ServiceLocator.Get<GameState>().ApplyNarrativeOutcome(choice.Outcome);
                    _choiceButtons.Clear();
                    if (!string.IsNullOrEmpty(choice.ResultText))
                    {
                        _state = DialogState.NarratingResult;
                        _narrator.Show(choice.ResultText);
                    }
                    else
                    {
                        OnNarrationFinished(); // No result text, just finish
                    }
                };
                _choiceButtons.Add(button);
                currentY += textSize.Height + 8;
            }
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
            else if (_state == DialogState.NarratingResult)
            {
                _state = DialogState.Finished;
                _onComplete?.Invoke();
                Hide();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _narrator.Update(gameTime);

            if (_state == DialogState.AwaitingChoice)
            {
                var mouseState = Mouse.GetState();
                foreach (var button in _choiceButtons)
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
                    button.Draw(spriteBatch, secondaryFont, gameTime, transform);
                }
            }

            _narrator.Draw(spriteBatch, secondaryFont, gameTime);
        }
    }
}
#nullable restore