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
    public class EncounterScene : GameScene
    {
        // Dependencies
        private readonly EncounterManager _encounterManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly HapticsManager _hapticsManager;
        private readonly Global _global;

        // State
        private EncounterData _currentEncounter;
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Rectangle _panelBounds;
        private Rectangle _imagePlaceholderBounds;
        private List<string> _wrappedDescription = new List<string>();
        private enum ContentState { Displaying, AwaitingSkillCheck }
        private ContentState _contentState = ContentState.Displaying;

        // Skill Check State
        private EncounterChoiceData _skillCheckChoice;
        private int _skillCheckDC;
        private int _skillCheckModifier;
        private string _skillCheckStatName;

        public EncounterScene()
        {
            _encounterManager = ServiceLocator.Get<EncounterManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _diceRollingSystem = ServiceLocator.Get<DiceRollingSystem>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public void SetEncounter(EncounterData data)
        {
            _currentEncounter = data;
        }

        protected override Rectangle GetAnimatedBounds()
        {
            int panelWidth = 560;
            int panelHeight = 480;
            return new Rectangle((Global.VIRTUAL_WIDTH - panelWidth) / 2, (Global.VIRTUAL_HEIGHT - panelHeight) / 2, panelWidth, panelHeight);
        }

        public override void Enter()
        {
            base.Enter();
            _hapticsManager.QuickZoomInPulseSmall();

            _contentState = ContentState.Displaying;
            _choiceButtons.Clear();
            _wrappedDescription.Clear();
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            _core.IsMouseVisible = true;
            _previousKeyboardState = Keyboard.GetState();
            previousMouseState = Mouse.GetState();

            var font = ServiceLocator.Get<BitmapFont>();
            _panelBounds = GetAnimatedBounds();

            // Image is positioned first, near the top
            float currentY = _panelBounds.Y + 20;
            _imagePlaceholderBounds = new Rectangle(_panelBounds.Center.X - 256, (int)currentY, 512, 256);

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

        public override void Exit()
        {
            base.Exit();
            _diceRollingSystem.OnRollCompleted -= OnDiceRollCompleted;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // Updates the intro animator

            if (_introAnimator == null || !_introAnimator.IsComplete)
            {
                return; // Wait for intro animation to finish
            }

            if (_contentState == ContentState.Displaying)
            {
                var currentMouseState = Mouse.GetState();
                foreach (var button in _choiceButtons)
                {
                    button.Update(currentMouseState);
                }
                previousMouseState = currentMouseState;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_currentEncounter != null)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                spriteBatch.Draw(pixel, _panelBounds, _global.Palette_DarkGray);
                DrawRectangleBorder(spriteBatch, pixel, _panelBounds, 1, _global.Palette_LightGray);

                // Draw Image
                spriteBatch.Draw(pixel, _imagePlaceholderBounds, _global.Palette_Black);
                string placeholderText = $"Image: {_currentEncounter.Image}";
                Vector2 placeholderTextSize = font.MeasureString(placeholderText);
                spriteBatch.DrawString(font, placeholderText, new Vector2(_imagePlaceholderBounds.Center.X - placeholderTextSize.X / 2, _imagePlaceholderBounds.Center.Y - placeholderTextSize.Y / 2), _global.Palette_Gray);

                // Draw Text Content
                float currentY = _imagePlaceholderBounds.Bottom + 10;
                Vector2 titleSize = font.MeasureString(_currentEncounter.Title);
                Vector2 titlePosition = new Vector2(_panelBounds.Center.X - titleSize.X / 2, currentY);
                spriteBatch.DrawString(font, _currentEncounter.Title, titlePosition, _global.Palette_BrightWhite);

                currentY += titleSize.Y + 15;
                foreach (var line in _wrappedDescription)
                {
                    spriteBatch.DrawString(font, line, new Vector2(_panelBounds.X + 20, currentY), _global.Palette_White);
                    currentY += font.LineHeight;
                }

                foreach (var button in _choiceButtons)
                {
                    button.Draw(spriteBatch, font, gameTime);
                }

                if (_contentState == ContentState.AwaitingSkillCheck)
                {
                    string waitingText = "Rolling...";
                    Vector2 waitingTextSize = font.MeasureString(waitingText);
                    spriteBatch.DrawString(font, waitingText, new Vector2(_panelBounds.Center.X - waitingTextSize.X / 2, _panelBounds.Bottom - 40), _global.Palette_Yellow);
                }
            }
        }

        #region Encounter Logic (Adapted from EncounterDialog)

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
                    _contentState = ContentState.AwaitingSkillCheck;

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
                _sceneManager.TransitionToScene(GameSceneState.TerminalMap);
            }
            else
            {
                _encounterManager.ProcessOutcomes(choice.Outcomes);
                _sceneManager.TransitionToScene(GameSceneState.TerminalMap);
            }
        }

        private void OnDiceRollCompleted(DiceRollResult result)
        {
            if (_contentState != ContentState.AwaitingSkillCheck) return;

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
                    _sceneManager.TransitionToScene(GameSceneState.TerminalMap);
                });
            }
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

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }

        #endregion
    }
}