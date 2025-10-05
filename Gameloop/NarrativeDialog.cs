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
        private static readonly Random _random = new Random();

        public NarrativeDialog(GameScene currentGameScene) : base(currentGameScene)
        {
            var narratorBounds = new Rectangle(0, Global.VIRTUAL_HEIGHT - 80, Global.VIRTUAL_WIDTH, 80);
            _narrator = new StoryNarrator(narratorBounds);
            _narrator.OnFinished += OnNarrationFinished;
        }

        public override void Hide()
        {
            base.Hide();
            _narrator.Clear(); // Ensure the narrator's state is reset when the dialog is hidden.
        }

        public void Show(NarrativeEvent narrativeEvent, Action? onComplete)
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
                    _choiceButtons.Clear();
                    _narrator.Clear(); // Explicitly clear the narrator before showing the result.

                    // Perform weighted random selection
                    WeightedOutcome? selectedOutcome = null;
                    if (choice.Outcomes.Any())
                    {
                        int totalWeight = choice.Outcomes.Sum(o => o.Weight);
                        if (totalWeight > 0)
                        {
                            int roll = _random.Next(totalWeight);
                            foreach (var outcome in choice.Outcomes)
                            {
                                if (roll < outcome.Weight)
                                {
                                    selectedOutcome = outcome;
                                    break;
                                }
                                roll -= outcome.Weight;
                            }
                        }
                        else
                        {
                            // If all weights are 0, just pick the first one
                            selectedOutcome = choice.Outcomes.FirstOrDefault();
                        }
                    }

                    if (selectedOutcome != null)
                    {
                        ServiceLocator.Get<GameState>().ApplyNarrativeOutcome(selectedOutcome.Outcome);

                        if (!string.IsNullOrEmpty(selectedOutcome.ResultText))
                        {
                            _state = DialogState.NarratingResult;
                            _narrator.Show(selectedOutcome.ResultText);
                        }
                        else
                        {
                            // If there's no result text, the event is over.
                            _state = DialogState.Finished;
                            _onComplete?.Invoke();
                            Hide();
                        }
                    }
                    else
                    {
                        // No outcomes defined, just finish.
                        _state = DialogState.Finished;
                        _onComplete?.Invoke();
                        Hide();
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
                // Iterate over a copy of the list to prevent modification during enumeration.
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
                    button.Draw(spriteBatch, secondaryFont, gameTime, transform);
                }
            }

            _narrator.Draw(spriteBatch, secondaryFont, gameTime);
        }
    }
}
#nullable restore