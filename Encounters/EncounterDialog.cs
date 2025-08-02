using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Encounters;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class EncounterDialog : Dialog
    {
        private enum EncounterDialogState { Displaying, AwaitingSkillCheck }

        // Dependencies
        private readonly EncounterManager _encounterManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly DiceRollingSystem _diceRollingSystem;

        // State
        private EncounterData _currentEncounter;
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Rectangle _panelBounds;
        private Rectangle _imagePlaceholderBounds;
        private List<string> _wrappedDescription = new List<string>();
        private EncounterDialogState _currentState = EncounterDialogState.Displaying;

        // Skill Check State
        private EncounterChoiceData _skillCheckChoice;
        private int _skillCheckDC;
        private int _skillCheckModifier;
        private string _skillCheckStatName;

        public EncounterDialog(GameScene parentScene) : base(parentScene)
        {
            _encounterManager = ServiceLocator.Get<EncounterManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
        }

        public void Show(EncounterData encounterData)
        {
            _currentGameScene?.ResetInputBlockTimer();
            _currentEncounter = encounterData;
            _currentState = EncounterDialogState.Displaying;
            _choiceButtons.Clear();
            _wrappedDescription.Clear();
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            IsActive = true;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _core.IsMouseVisible = true;

            var font = ServiceLocator.Get<BitmapFont>();

            int panelWidth = 600;
            int panelHeight = 400;
            _panelBounds = new Rectangle((Global.VIRTUAL_WIDTH - panelWidth) / 2, (Global.VIRTUAL_HEIGHT - panelHeight) / 2, panelWidth, panelHeight);
            float currentY = _panelBounds.Y + 20;
            currentY += 20;
            _imagePlaceholderBounds = new Rectangle(_panelBounds.Center.X - 100, (int)currentY, 200, 120);
            currentY = _imagePlaceholderBounds.Bottom + 15;

            if (_currentEncounter != null && !string.IsNullOrEmpty(_currentEncounter.DescriptionText))
            {
                _wrappedDescription = WrapText(font, _currentEncounter.DescriptionText, _panelBounds.Width - 40);
            }

            if (_currentEncounter?.Choices != null)
            {
                int buttonWidth = 400;
                int buttonHeight = 20;
                int buttonSpacing = 5;
                float totalButtonHeight = _currentEncounter.Choices.Count * (buttonHeight + buttonSpacing) - buttonSpacing;
                float buttonStartY = _panelBounds.Bottom - totalButtonHeight - 20;

                foreach (var choice in _currentEncounter.Choices)
                {
                    var (requirementsMet, requirementText) = CheckRequirements(choice);
                    string buttonText = string.IsNullOrEmpty(requirementText) ? choice.Text : $"{choice.Text} {requirementText}";

                    var button = new Button(new Rectangle(_panelBounds.Center.X - buttonWidth / 2, (int)buttonStartY, buttonWidth, buttonHeight), buttonText)
                    {
                        IsEnabled = requirementsMet
                    };

                    button.OnClick += () => HandleChoiceClick(choice);
                    _choiceButtons.Add(button);
                    buttonStartY += buttonHeight + buttonSpacing;
                }
            }
        }

        public override void Hide()
        {
            base.Hide();
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        private (bool met, string requirementText) CheckRequirements(EncounterChoiceData choice)
        {
            if (choice.Requirements == null || !choice.Requirements.Any())
            {
                return (true, string.Empty);
            }

            var playerStats = _gameState.PlayerStats;
            if (playerStats == null) return (false, "[No Stats]");

            foreach (var req in choice.Requirements)
            {
                switch (req.Type.ToLowerInvariant())
                {
                    case "resource":
                        string[] parts = req.Value.Split(':');
                        if (parts.Length != 2) return (false, "[Invalid Req]");
                        if (Enum.TryParse<StatType>(parts[0], true, out var statType))
                        {
                            if (int.TryParse(parts[1], out int requiredValue))
                            {
                                int playerValue = playerStats.GetMainStat(statType);
                                if (playerValue < requiredValue)
                                {
                                    return (false, $"[Requires {requiredValue} {statType}]");
                                }
                            }
                        }
                        break;

                    case "skillcheck":
                        return (true, $"[{req.Value.Replace(":", " ")} Check]");

                    case "minigame":
                        return (true, "[Mini-Game]");
                }
            }
            return (true, string.Empty);
        }

        private void HandleChoiceClick(EncounterChoiceData choice)
        {
            var skillCheckReq = choice.Requirements.FirstOrDefault(r => r.Type.ToLowerInvariant() == "skillcheck");
            var miniGameReq = choice.Requirements.FirstOrDefault(r => r.Type.ToLowerInvariant() == "minigame");

            if (skillCheckReq != null)
            {
                string[] parts = skillCheckReq.Value.Split(':');
                if (parts.Length == 2 && Enum.TryParse<StatType>(parts[0], true, out var statType) && int.TryParse(parts[1], out int dc))
                {
                    _skillCheckChoice = choice;
                    _skillCheckDC = dc;
                    _skillCheckStatName = statType.ToString();
                    _skillCheckModifier = _gameState.PlayerStats.GetStatModifier(statType);
                    _currentState = EncounterDialogState.AwaitingSkillCheck;

                    foreach (var btn in _choiceButtons) btn.IsEnabled = false;

                    _diceRollingSystem.Roll(new List<DiceGroup>
                    {
                        new DiceGroup
                        {
                            GroupId = "skill_check",
                            NumberOfDice = 1,
                            DieType = DieType.D6, // Placeholder, should be D20
                            Tint = Color.CornflowerBlue,
                            ResultProcessing = DiceResultProcessing.IndividualValues
                        }
                    });
                }
            }
            else if (miniGameReq != null)
            {
                _encounterManager.ProcessOutcomes(new List<EncounterOutcomeData> { new EncounterOutcomeData { Type = "LogMessage", Value = "A mini-game would start here." } });
                Hide();
            }
            else
            {
                _encounterManager.ProcessOutcomes(choice.Outcomes);
                Hide();
            }
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            if (_currentState != EncounterDialogState.AwaitingSkillCheck) return;

            if (result.ResultsByGroup.TryGetValue("skill_check", out var values) && values.Any())
            {
                int roll = values[0];
                int total = roll + _skillCheckModifier;
                bool success = total >= _skillCheckDC;

                string resultText = $"[palette_yellow]{_skillCheckStatName} Check: Rolled {roll} + {_skillCheckModifier} = {total} vs DC {_skillCheckDC}. {(success ? "[palette_lightgreen]Success!" : "[palette_red]Failure!")}";
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = resultText });

                if (success)
                {
                    _encounterManager.ProcessOutcomes(_skillCheckChoice.SuccessOutcomes);
                }
                else
                {
                    _encounterManager.ProcessOutcomes(_skillCheckChoice.FailureOutcomes);
                }

                System.Threading.Tasks.Task.Delay(1500).ContinueWith(t =>
                {
                    Hide();
                });
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive || _currentState != EncounterDialogState.Displaying) return;

            var currentMouseState = Mouse.GetState();
            foreach (var button in _choiceButtons)
            {
                button.Update(currentMouseState);
            }
            _previousMouseState = currentMouseState;
        }

        protected override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_currentEncounter == null) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            spriteBatch.Draw(pixel, _panelBounds, _global.Palette_DarkGray);
            DrawRectangleBorder(spriteBatch, pixel, _panelBounds, 1, _global.Palette_LightGray);
            Vector2 titleSize = font.MeasureString(_currentEncounter.Title);
            spriteBatch.DrawString(font, _currentEncounter.Title, new Vector2(_panelBounds.Center.X - titleSize.X / 2, _panelBounds.Y + 15), _global.Palette_BrightWhite);
            spriteBatch.Draw(pixel, _imagePlaceholderBounds, _global.Palette_Black);
            string placeholderText = $"Image: {_currentEncounter.Image}";
            Vector2 placeholderTextSize = font.MeasureString(placeholderText);
            spriteBatch.DrawString(font, placeholderText, new Vector2(_imagePlaceholderBounds.Center.X - placeholderTextSize.X / 2, _imagePlaceholderBounds.Center.Y - placeholderTextSize.Y / 2), _global.Palette_Gray);

            float currentY = _imagePlaceholderBounds.Bottom + 15;
            foreach (var line in _wrappedDescription)
            {
                spriteBatch.DrawString(font, line, new Vector2(_panelBounds.X + 20, currentY), _global.Palette_White);
                currentY += font.LineHeight;
            }

            foreach (var button in _choiceButtons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            if (_currentState == EncounterDialogState.AwaitingSkillCheck)
            {
                string waitingText = "Rolling...";
                Vector2 waitingTextSize = font.MeasureString(waitingText);
                spriteBatch.DrawString(font, waitingText, new Vector2(_panelBounds.Center.X - waitingTextSize.X / 2, _panelBounds.Bottom - 40), _global.Palette_Yellow);
            }

            spriteBatch.End();
        }

        private List<string> WrapText(BitmapFont font, string text, float maxLineWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            var words = text.Split(' ');
            var currentLine = new StringBuilder();
            foreach (var word in words)
            {
                var testLine = new StringBuilder(currentLine.ToString());
                if (testLine.Length > 0) testLine.Append(" ");
                testLine.Append(word);
                if (font.MeasureString(testLine).Width > maxLineWidth)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear().Append(word);
                }
                else
                {
                    if (currentLine.Length > 0) currentLine.Append(" ");
                    currentLine.Append(word);
                }
            }
            lines.Add(currentLine.ToString());
            return lines;
        }
    }
}