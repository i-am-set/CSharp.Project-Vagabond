using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Manages the overall game loop, progressing the player through themed "Splits"
    /// consisting of battles, narrative choices, and rewards.
    /// </summary>
    public class ProgressionManager
    {
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly Dictionary<SplitTheme, SplitData> _splitDataCache = new();
        private static readonly Random _random = new Random();

        public int CurrentGameStage { get; private set; }
        private SplitData _currentSplit;
        private int _currentEventIndex;
        private bool _isWaitingForPlayerAction = true;

        public Action? OnNarrationCompleteAction { get; private set; }

        public ProgressionManager()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            EventBus.Subscribe<GameEvents.BattleWon>(OnBattleWon);
            EventBus.Subscribe<GameEvents.RewardChoiceCompleted>(OnRewardChoiceCompleted);
        }

        public void ClearPendingAction()
        {
            OnNarrationCompleteAction = null;
        }

        public void LoadAllSplits(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            };

            try
            {
                string splitsPath = Path.Combine(content.RootDirectory, "Data", "Splits");
                if (!Directory.Exists(splitsPath))
                {
                    Debug.WriteLine($"[ProgressionManager] [WARNING] 'Data/Splits' directory not found. Creating it.");
                    Directory.CreateDirectory(splitsPath);
                    return; // No files to load
                }

                foreach (var file in Directory.GetFiles(splitsPath, "*.json"))
                {
                    string json = File.ReadAllText(file);
                    var split = JsonSerializer.Deserialize<SplitData>(json, jsonOptions);
                    if (split != null)
                    {
                        _splitDataCache[split.Theme] = split;
                    }
                }
                Debug.WriteLine($"[ProgressionManager] Loaded {_splitDataCache.Count} split definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProgressionManager] [ERROR] Failed to load split data: {ex.Message}");
            }
        }

        public void StartNewGame()
        {
            CurrentGameStage = 0;
            _currentEventIndex = -1;

            if (_splitDataCache.TryGetValue(SplitTheme.Forest, out var firstSplit))
            {
                _currentSplit = firstSplit;
                _isWaitingForPlayerAction = false;
            }
            else
            {
                Debug.WriteLine("[ProgressionManager] [FATAL] Could not find starting split data for 'Forest'.");
            }
        }

        public void AdvanceToNextEvent()
        {
            if (_isWaitingForPlayerAction) return;

            ClearPendingAction();
            _currentEventIndex++;
            _isWaitingForPlayerAction = true;

            if (_currentSplit == null || _currentEventIndex >= _currentSplit.Structure.Count)
            {
                EventBus.Publish(new GameEvents.ProgressionNarrated { Message = "You have completed the current area." });
                return;
            }

            // Process any buffs that expire after one battle
            _gameState.PlayerState.DecrementBuffDurations();

            string eventType = _currentSplit.Structure[_currentEventIndex];

            switch (eventType)
            {
                case "Battle":
                    HandleRandomBattle(_currentSplit.PossibleBattles);
                    break;

                case "Narrative":
                    HandleRandomNarrative(_currentSplit.PossibleNarratives);
                    break;

                case "Reward":
                    HandleRewardEvent();
                    break;

                case "Event":
                    HandleRandomEvent();
                    break;

                case "MajorBattle":
                    HandleRandomBattle(_currentSplit.PossibleMajorBattles, isMajor: true);
                    break;
            }
        }

        private void HandleRandomBattle(List<List<string>> battlePool, bool isMajor = false)
        {
            if (battlePool == null || !battlePool.Any())
            {
                Debug.WriteLine($"[ProgressionManager] [WARNING] No battles defined in '{(isMajor ? "PossibleMajorBattles" : "PossibleBattles")}' for split {_currentSplit.Theme}. Skipping event.");
                _isWaitingForPlayerAction = false; // Allow next event to process
                return;
            }

            var enemyGroup = battlePool[_random.Next(battlePool.Count)];
            OnNarrationCompleteAction = () => {
                BattleSetup.EnemyArchetypes = enemyGroup;
                _sceneManager.ChangeScene(GameSceneState.Battle);
            };
            EventBus.Publish(new GameEvents.ProgressionNarrated { Message = "You are ambushed!" });
        }

        private void HandleRandomNarrative(List<NarrativeEventData> narrativePool)
        {
            if (narrativePool == null || !narrativePool.Any())
            {
                Debug.WriteLine($"[ProgressionManager] [WARNING] No narratives defined in 'PossibleNarratives' for split {_currentSplit.Theme}. Skipping event.");
                _isWaitingForPlayerAction = false; // Allow next event to process
                return;
            }
            var narrativeEvent = narrativePool[_random.Next(narrativePool.Count)];

            OnNarrationCompleteAction = () => {
                EventBus.Publish(new GameEvents.NarrativeChoiceRequested
                {
                    Prompt = narrativeEvent.Prompt,
                    Choices = narrativeEvent.Choices
                });
            };
            EventBus.Publish(new GameEvents.ProgressionNarrated { Message = narrativeEvent.Prompt });
        }

        private void HandleRewardEvent()
        {
            OnNarrationCompleteAction = () => {
                EventBus.Publish(new GameEvents.RewardChoiceRequested
                {
                    GameStage = this.CurrentGameStage,
                    RewardType = "Spell",
                    Count = 3
                });
            };
            // The _isWaitingForPlayerAction flag remains true until the RewardChoiceCompleted event is received.
            EventBus.Publish(new GameEvents.ProgressionNarrated { Message = "A moment of clarity reveals new paths to power." });
        }

        private void HandleRandomEvent()
        {
            int outcome = _random.Next(3); // 0, 1, or 2
            switch (outcome)
            {
                case 0: // Battle
                    HandleRandomBattle(_currentSplit.PossibleBattles);
                    break;
                case 1: // Narrative
                    HandleRandomNarrative(_currentSplit.PossibleNarratives);
                    break;
                case 2: // Nothing
                    EventBus.Publish(new GameEvents.ProgressionNarrated { Message = "You travel onward without incident." });
                    _isWaitingForPlayerAction = false; // Nothing happens, so we can advance immediately.
                    break;
            }
        }

        private void OnBattleWon(GameEvents.BattleWon e)
        {
            bool wasMajorMilestone = _currentSplit.Structure[_currentEventIndex] == "MajorBattle";

            if (wasMajorMilestone)
            {
                CurrentGameStage++;
                // TODO: Implement Major Milestone reward sequence (e.g., ability choice, item bundle)
                EventBus.Publish(new GameEvents.ProgressionNarrated { Message = $"[palette_yellow]MAJOR MILESTONE REACHED! Game Stage is now {CurrentGameStage}[/]", ClearPrevious = true });
            }
            _isWaitingForPlayerAction = false;
        }

        private void OnRewardChoiceCompleted(GameEvents.RewardChoiceCompleted e)
        {
            _isWaitingForPlayerAction = false;
        }

        public void OnNarrativeChoiceMade(ChoiceOutcome outcome)
        {
            if (outcome != null)
            {
                switch (outcome.OutcomeType)
                {
                    case "GiveItem":
                        _gameState.PlayerState.AddItem(outcome.Value);
                        EventBus.Publish(new GameEvents.ProgressionNarrated { Message = $"You obtained: {outcome.Value}" });
                        break;
                    case "AddBuff":
                        if (Enum.TryParse<StatusEffectType>(outcome.Value, true, out var effectType))
                        {
                            var buff = new TemporaryBuff { EffectType = effectType, DurationInBattles = outcome.Duration };
                            _gameState.PlayerState.TemporaryBuffs.Add(buff);
                            EventBus.Publish(new GameEvents.ProgressionNarrated { Message = $"You feel empowered for your next battle!" });
                        }
                        break;
                }
            }
            else
            {
                EventBus.Publish(new GameEvents.ProgressionNarrated { Message = "You continue on your path." });
            }
            _isWaitingForPlayerAction = false;
        }
    }
}