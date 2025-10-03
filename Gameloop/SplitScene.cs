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

namespace ProjectVagabond.Scenes
{
    public class SplitScene : GameScene
    {
        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly StoryNarrator _narrator;

        private enum SplitState { Advancing, Narrating, AwaitingChoice, AwaitingEvent }
        private SplitState _currentState = SplitState.Advancing;

        private readonly List<Button> _choiceButtons = new List<Button>();
        public static bool PlayerWonLastBattle { get; set; } = true;
        public static bool WasMajorBattle { get; set; } = false;
        private bool _isShowingResultNarration = false;

        public SplitScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();

            var narratorBounds = new Rectangle(0, 105, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - 105);
            _narrator = new StoryNarrator(narratorBounds);
            _narrator.OnFinished += OnNarrationFinished;
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
            if (_progressionManager.CurrentStepIndex == -1)
            {
                _progressionManager.StartNewSplit();
                _currentState = SplitState.Advancing;
            }
            else
            {
                if (WasMajorBattle && PlayerWonLastBattle)
                {
                    WasMajorBattle = false;
                    TriggerReward();
                }
                else
                {
                    if (_progressionManager.AdvanceStep())
                    {
                        _currentState = SplitState.Advancing;
                    }
                    else
                    {
                        _sceneManager.ChangeScene(GameSceneState.MainMenu);
                    }
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _narrator.Update(gameTime);

            if (_narrator.IsBusy)
            {
                return;
            }

            switch (_currentState)
            {
                case SplitState.Advancing:
                    ProcessCurrentStep();
                    break;
                case SplitState.AwaitingChoice:
                    var mouseState = Mouse.GetState();
                    // Iterate over a copy of the list to prevent modification during enumeration.
                    foreach (var button in _choiceButtons.ToList())
                    {
                        button.Update(mouseState);
                    }
                    break;
                case SplitState.AwaitingEvent:
                    if (!_sceneManager.IsModalActive)
                    {
                        if (_progressionManager.AdvanceStep())
                        {
                            _currentState = SplitState.Advancing;
                        }
                        else
                        {
                            _sceneManager.ChangeScene(GameSceneState.MainMenu);
                        }
                    }
                    break;
            }
        }

        private void ProcessCurrentStep()
        {
            string? stepType = _progressionManager.GetCurrentStepType();

            if (stepType == null)
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu);
                return;
            }

            _currentState = SplitState.Narrating;

            switch (stepType.ToLowerInvariant())
            {
                case "narrative":
                    var narrative = _progressionManager.GetRandomNarrative();
                    if (narrative != null)
                    {
                        _narrator.Show(narrative.Prompt);
                    }
                    else
                    {
                        _narrator.Show("An unexpected calm settles over the area.");
                    }
                    break;

                case "battle":
                    _narrator.Show("Danger lurks ahead...");
                    break;

                case "majorbattle":
                    _narrator.Show("A powerful foe blocks the path!");
                    break;

                case "reward":
                    _narrator.Show("A sense of accomplishment washes over you.");
                    break;

                case "event":
                    _narrator.Show("Something interesting happens.");
                    break;

                default:
                    _narrator.Show($"Unknown event: {stepType}");
                    break;
            }
        }

        private void OnNarrationFinished()
        {
            if (_isShowingResultNarration)
            {
                _isShowingResultNarration = false;
                AdvanceToNextStep();
                return;
            }

            // Clear the narrator's text and state before triggering the next event.
            _narrator.Clear();

            string? stepType = _progressionManager.GetCurrentStepType();
            if (stepType == null)
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu);
                return;
            }

            switch (stepType.ToLowerInvariant())
            {
                case "battle":
                    WasMajorBattle = false;
                    BattleSetup.EnemyArchetypes = _progressionManager.GetRandomBattle();
                    BattleSetup.ReturnSceneState = GameSceneState.Split;
                    _sceneManager.ChangeScene(GameSceneState.Battle);
                    break;

                case "majorbattle":
                    WasMajorBattle = true;
                    BattleSetup.EnemyArchetypes = _progressionManager.GetRandomMajorBattle();
                    BattleSetup.ReturnSceneState = GameSceneState.Split;
                    _sceneManager.ChangeScene(GameSceneState.Battle);
                    break;

                case "narrative":
                    var narrative = _progressionManager.GetRandomNarrative();
                    if (narrative != null)
                    {
                        CreateChoiceButtons(narrative);
                        _currentState = SplitState.AwaitingChoice;
                    }
                    else
                    {
                        AdvanceToNextStep();
                    }
                    break;

                case "reward":
                    TriggerReward();
                    break;

                default:
                    AdvanceToNextStep();
                    break;
            }
        }

        private void TriggerReward()
        {
            var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
            choiceMenu?.Show(ChoiceType.Spell, 3);
            _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
            _currentState = SplitState.AwaitingEvent;
        }

        private void CreateChoiceButtons(NarrativeEvent narrative)
        {
            _choiceButtons.Clear();
            var font = ServiceLocator.Get<Core>().SecondaryFont;
            float currentY = 40;
            foreach (var choice in narrative.Choices)
            {
                var button = new Button(Rectangle.Empty, choice.Text.ToUpper(), font: font) { AlignLeft = true };
                var textSize = font.MeasureString(button.Text);
                button.Bounds = new Rectangle(40, (int)currentY, (int)textSize.Width + 10, (int)textSize.Height + 4);
                button.OnClick += () =>
                {
                    _gameState.ApplyNarrativeOutcome(choice.Outcome);
                    _choiceButtons.Clear();

                    if (!string.IsNullOrEmpty(choice.ResultText))
                    {
                        _isShowingResultNarration = true;
                        _currentState = SplitState.Narrating;
                        _narrator.Show(choice.ResultText);
                    }
                    else
                    {
                        AdvanceToNextStep();
                    }
                };
                _choiceButtons.Add(button);
                currentY += textSize.Height + 8;
            }
        }

        private void AdvanceToNextStep()
        {
            if (_progressionManager.AdvanceStep())
            {
                _currentState = SplitState.Advancing;
            }
            else
            {
                _sceneManager.ChangeScene(GameSceneState.MainMenu);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            string themeText = _progressionManager.CurrentSplit?.Theme.ToUpper() ?? "THE VOID";
            var themeSize = font.MeasureString(themeText);
            var themePos = new Vector2((Global.VIRTUAL_WIDTH - themeSize.Width) / 2, 20);
            spriteBatch.DrawStringSnapped(font, themeText, themePos, Color.White);

            if (_currentState == SplitState.AwaitingChoice)
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
