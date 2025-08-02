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
        private enum AnimationState { In, Idle, Out }
        private enum ContentState { Displaying, AwaitingSkillCheck }

        // Dependencies
        private readonly EncounterManager _encounterManager;
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly DiceRollingSystem _diceRollingSystem;
        private readonly HapticsManager _hapticsManager;

        // State
        private EncounterData _currentEncounter;
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Rectangle _panelBounds;
        private Rectangle _imagePlaceholderBounds;
        private List<string> _wrappedDescription = new List<string>();
        private ContentState _contentState = ContentState.Displaying;

        // Animation State
        private AnimationState _animationState;
        private float _animationTimer;
        private const float AnimationInDuration = 0.75f;
        private const float ImageFadeInDuration = AnimationInDuration * 0.4f; // Image appears quickly
        private float _dialogScaleY;
        private float _contentAlpha;
        private float _imageAlpha;
        private Vector2 _titleOffset;
        private readonly List<float> _buttonOffsets = new List<float>();
        private float _swayTimer = 0f;
        private const float IMAGE_SWAY_SPEED = 1.5f;
        private const float IMAGE_SWAY_AMOUNT_X = 2f;
        private const float IMAGE_SWAY_AMOUNT_Y = 1f;


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
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        public void Show(EncounterData encounterData)
        {
            _currentGameScene?.ResetInputBlockTimer();
            _hapticsManager.QuickZoomInPulseSmall();

            _currentEncounter = encounterData;
            _contentState = ContentState.Displaying;
            _animationState = AnimationState.In;
            _animationTimer = 0f;
            _swayTimer = 0f;
            _dialogScaleY = 0f;
            _contentAlpha = 0f;
            _imageAlpha = 0f;
            _titleOffset = new Vector2(0, -10f);
            _buttonOffsets.Clear();

            _choiceButtons.Clear();
            _wrappedDescription.Clear();
            _diceRollingSystem.OnRollCompleted += OnDiceRollCompleted;

            IsActive = true;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _core.IsMouseVisible = true;

            var font = ServiceLocator.Get<BitmapFont>();

            int panelWidth = 560;
            int panelHeight = 480;
            _panelBounds = new Rectangle((Global.VIRTUAL_WIDTH - panelWidth) / 2, (Global.VIRTUAL_HEIGHT - panelHeight) / 2, panelWidth, panelHeight);

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
                    _buttonOffsets.Add(30f); // Start offset below the panel for animation
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
                    Hide();
                });
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_contentState == ContentState.Displaying)
            {
                if (_animationState == AnimationState.In)
                {
                    UpdateAnimationIn();
                    _previousMouseState = Mouse.GetState();
                    _previousKeyboardState = Keyboard.GetState();
                    return;
                }
                else if (_animationState == AnimationState.Idle)
                {
                    _swayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                }

                var currentMouseState = Mouse.GetState();
                foreach (var button in _choiceButtons)
                {
                    button.Update(currentMouseState);
                }
                _previousMouseState = currentMouseState;
            }
        }

        private void UpdateAnimationIn()
        {
            float progress = Math.Clamp(_animationTimer / AnimationInDuration, 0f, 1f);

            // Phase 1: Dialog Pop-in (0% to 40% of duration)
            float popInDuration = AnimationInDuration * 0.4f;
            float popInProgress = Math.Clamp(_animationTimer / popInDuration, 0f, 1f);
            _dialogScaleY = Easing.EaseOutBack(popInProgress);

            // Phase 2: Image Flash-in (0% to 40%)
            float imageFadeProgress = Math.Clamp(_animationTimer / ImageFadeInDuration, 0f, 1f);
            _imageAlpha = Easing.EaseOutQuad(imageFadeProgress);

            // Phase 3: Content Fade-in (20% to 100%)
            float contentFadeStart = AnimationInDuration * 0.2f;
            if (_animationTimer > contentFadeStart)
            {
                float contentDuration = AnimationInDuration - contentFadeStart;
                float contentProgress = Math.Clamp((_animationTimer - contentFadeStart) / contentDuration, 0f, 1f);
                _contentAlpha = Easing.EaseInQuad(contentProgress);
                _titleOffset.Y = MathHelper.Lerp(-10f, 0f, contentProgress);
            }

            // Phase 4: Button slide-in (50% to 100%)
            float buttonSlideStart = AnimationInDuration * 0.5f;
            if (_animationTimer > buttonSlideStart)
            {
                float buttonMasterDuration = AnimationInDuration - buttonSlideStart;
                float staggerDelay = _buttonOffsets.Any() ? buttonMasterDuration * 0.3f / _buttonOffsets.Count : 0;

                for (int i = 0; i < _buttonOffsets.Count; i++)
                {
                    float buttonStartTime = buttonSlideStart + (i * staggerDelay);
                    if (_animationTimer > buttonStartTime)
                    {
                        float buttonDuration = buttonMasterDuration - (i * staggerDelay);
                        float buttonProgress = Math.Clamp((_animationTimer - buttonStartTime) / buttonDuration, 0f, 1f);
                        _buttonOffsets[i] = MathHelper.Lerp(30f, 0f, Easing.EaseOutCubic(buttonProgress));
                    }
                }
            }

            if (progress >= 1.0f)
            {
                _animationState = AnimationState.Idle;
            }
        }

        protected override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_currentEncounter == null) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            Rectangle finalPanelBounds = _panelBounds;
            if (_animationState == AnimationState.In)
            {
                finalPanelBounds.Height = (int)(_panelBounds.Height * _dialogScaleY);
                finalPanelBounds.Y = _panelBounds.Center.Y - finalPanelBounds.Height / 2;
            }

            spriteBatch.Draw(pixel, finalPanelBounds, _global.Palette_DarkGray);
            DrawRectangleBorder(spriteBatch, pixel, finalPanelBounds, 1, _global.Palette_LightGray);

            // Draw Image
            if (_imageAlpha > 0)
            {
                float swayOffsetX = 0f;
                float swayOffsetY = 0f;
                if (_animationState == AnimationState.Idle)
                {
                    swayOffsetX = (float)Math.Sin(_swayTimer * IMAGE_SWAY_SPEED) * IMAGE_SWAY_AMOUNT_X;
                    swayOffsetY = (float)Math.Sin(_swayTimer * IMAGE_SWAY_SPEED * 2) * IMAGE_SWAY_AMOUNT_Y;
                }
                var swayedImageBounds = new Rectangle(
                    _imagePlaceholderBounds.X + (int)swayOffsetX,
                    _imagePlaceholderBounds.Y + (int)swayOffsetY,
                    _imagePlaceholderBounds.Width,
                    _imagePlaceholderBounds.Height);

                spriteBatch.Draw(pixel, swayedImageBounds, _global.Palette_Black * _imageAlpha);
                string placeholderText = $"Image: {_currentEncounter.Image}";
                Vector2 placeholderTextSize = font.MeasureString(placeholderText);
                spriteBatch.DrawString(font, placeholderText, new Vector2(swayedImageBounds.Center.X - placeholderTextSize.X / 2, swayedImageBounds.Center.Y - placeholderTextSize.Y / 2), _global.Palette_Gray * _imageAlpha);
            }

            // Draw Text Content (Title, Description, Buttons)
            if (_contentAlpha > 0)
            {
                float currentY = _imagePlaceholderBounds.Bottom + 10;
                Vector2 titleSize = font.MeasureString(_currentEncounter.Title);
                Vector2 titlePosition = new Vector2(_panelBounds.Center.X - titleSize.X / 2, currentY) + _titleOffset;
                spriteBatch.DrawString(font, _currentEncounter.Title, titlePosition, _global.Palette_BrightWhite * _contentAlpha);

                currentY += titleSize.Y + 15;
                foreach (var line in _wrappedDescription)
                {
                    spriteBatch.DrawString(font, line, new Vector2(_panelBounds.X + 20, currentY), _global.Palette_White * _contentAlpha);
                    currentY += font.LineHeight;
                }

                for (int i = 0; i < _choiceButtons.Count; i++)
                {
                    var button = _choiceButtons[i];
                    var originalBounds = button.Bounds;
                    button.Bounds = new Rectangle(originalBounds.X, originalBounds.Y + (int)_buttonOffsets[i], originalBounds.Width, originalBounds.Height);

                    var originalDefaultColor = button.CustomDefaultTextColor;
                    var originalDisabledColor = button.CustomDisabledTextColor;
                    button.CustomDefaultTextColor = (originalDefaultColor ?? _global.Palette_BrightWhite) * _contentAlpha;
                    button.CustomDisabledTextColor = (originalDisabledColor ?? _global.ButtonDisableColor) * _contentAlpha;

                    button.Draw(spriteBatch, font, gameTime);

                    button.Bounds = originalBounds;
                    button.CustomDefaultTextColor = originalDefaultColor;
                    button.CustomDisabledTextColor = originalDisabledColor;
                }
            }

            if (_contentState == ContentState.AwaitingSkillCheck)
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