using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Progression;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Scenes
{
    public class SplitScene : GameScene
    {
        private readonly ProgressionManager _progressionManager;
        private readonly SceneManager _sceneManager;
        private readonly StoryNarrator _narrator;

        private enum SplitState { Advancing, Narrating, AwaitingEvent }
        private SplitState _currentState = SplitState.Advancing;

        public SplitScene()
        {
            _progressionManager = ServiceLocator.Get<ProgressionManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();

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
            _progressionManager.StartNewSplit();
            _currentState = SplitState.Advancing;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _narrator.Update(gameTime);

            if (_narrator.IsBusy)
            {
                return;
            }

            if (_currentState == SplitState.Advancing)
            {
                ProcessCurrentStep();
            }
        }

        private void ProcessCurrentStep()
        {
            string? stepType = _progressionManager.GetCurrentStepType();

            if (stepType == null)
            {
                // Split is over, for now, go back to the main menu.
                _sceneManager.ChangeScene(GameSceneState.MainMenu);
                return;
            }

            _currentState = SplitState.AwaitingEvent;

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

                case "event": // Generic event placeholder
                    _narrator.Show("Something interesting happens.");
                    break;

                default:
                    _narrator.Show($"Unknown event: {stepType}");
                    break;
            }
        }

        private void OnNarrationFinished()
        {
            // For now, we just advance to the next step after any narration.
            // In Step 2, this is where we will trigger battles, choices, etc.
            if (_progressionManager.AdvanceStep())
            {
                _currentState = SplitState.Advancing;
            }
            else
            {
                // End of split
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

            _narrator.Draw(spriteBatch, secondaryFont, gameTime);
        }
    }
}