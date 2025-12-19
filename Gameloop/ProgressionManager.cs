#nullable enable
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Progression
{
    public class ProgressionManager
    {
        private readonly Dictionary<string, SplitData> _splits = new Dictionary<string, SplitData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NarrativeEvent> _narrativeEvents = new Dictionary<string, NarrativeEvent>();
        private static readonly Random _random = new Random();

        public SplitData? CurrentSplit { get; private set; }
        public SplitMap? CurrentSplitMap { get; private set; }

        private readonly Dictionary<BattleDifficulty, List<List<string>>> _categorizedBattles = new();

        public void LoadSplits()
        {
            var content = ServiceLocator.Get<Core>().Content;
            string splitsDirectory = Path.Combine(content.RootDirectory, "Data", "Splits");

            if (!Directory.Exists(splitsDirectory))
            {
                Debug.WriteLine($"[ProgressionManager] [ERROR] Splits directory not found at '{splitsDirectory}'.");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            };

            foreach (var file in Directory.GetFiles(splitsDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var splitData = JsonSerializer.Deserialize<SplitData>(json, jsonOptions);
                    if (splitData != null && !string.IsNullOrEmpty(splitData.Theme))
                    {
                        _splits[splitData.Theme] = splitData;
                        Debug.WriteLine($"[ProgressionManager] Loaded split: {splitData.Theme}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProgressionManager] [ERROR] Failed to load or parse split file '{Path.GetFileName(file)}': {ex.Message}");
                }
            }

            LoadNarrativeEvents();
            ValidateSplits();
        }

        private void LoadNarrativeEvents()
        {
            var content = ServiceLocator.Get<Core>().Content;
            string eventsPath = Path.Combine(content.RootDirectory, "Data", "Events.json");

            if (!File.Exists(eventsPath))
            {
                Debug.WriteLine($"[ProgressionManager] [WARNING] Events file not found at '{eventsPath}'. No narrative events will be loaded.");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            };

            try
            {
                string json = File.ReadAllText(eventsPath);
                var eventList = JsonSerializer.Deserialize<List<NarrativeEvent>>(json, jsonOptions);

                if (eventList != null)
                {
                    foreach (var narrativeEvent in eventList)
                    {
                        if (!string.IsNullOrEmpty(narrativeEvent.EventID))
                        {
                            _narrativeEvents[narrativeEvent.EventID] = narrativeEvent;
                        }
                    }
                    Debug.WriteLine($"[ProgressionManager] Loaded {_narrativeEvents.Count} narrative events from Events.json.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProgressionManager] [ERROR] Failed to load or parse Events.json: {ex.Message}");
            }
        }

        private void ValidateSplits()
        {
            foreach (var split in _splits.Values)
            {
                if (split.PossibleNarrativeEventIDs != null)
                {
                    int removedCount = split.PossibleNarrativeEventIDs.RemoveAll(id => !_narrativeEvents.ContainsKey(id));
                    if (removedCount > 0)
                    {
                        Debug.WriteLine($"[ProgressionManager] [WARNING] Removed {removedCount} invalid event IDs from split '{split.Theme}'.");
                    }
                }
            }
        }

        public void GenerateNewSplitMap()
        {
            if (!_splits.Any())
            {
                Debug.WriteLine("[ProgressionManager] [ERROR] No splits loaded. Cannot generate map.");
                CurrentSplit = null;
                CurrentSplitMap = null;
                return;
            }

            CurrentSplitMap?.Dispose(); // Dispose the old render target if it exists

            CurrentSplit = _splits.Values.ElementAt(_random.Next(_splits.Count));
            CategorizeBattles(CurrentSplit);
            CurrentSplitMap = SplitMapGenerator.GenerateInitial(CurrentSplit);
            Debug.WriteLine($"[ProgressionManager] Generated initial split map: {CurrentSplit.Theme} with target {CurrentSplitMap.TargetColumnCount} columns.");
        }

        public void ClearCurrentSplitMap()
        {
            CurrentSplitMap?.Dispose();
            CurrentSplitMap = null;
        }

        private void CategorizeBattles(SplitData splitData)
        {
            _categorizedBattles.Clear();
            _categorizedBattles[BattleDifficulty.Easy] = new List<List<string>>();
            _categorizedBattles[BattleDifficulty.Normal] = new List<List<string>>();
            _categorizedBattles[BattleDifficulty.Hard] = new List<List<string>>();

            if (splitData.PossibleBattles == null || !splitData.PossibleBattles.Any())
            {
                return;
            }

            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var encountersWithLevels = new List<(List<string> Encounter, int TotalLevel)>();

            foreach (var encounter in splitData.PossibleBattles)
            {
                int totalLevel = 0;
                foreach (var archetypeId in encounter)
                {
                    var template = archetypeManager.GetArchetypeTemplate(archetypeId);
                    if (template != null)
                    {
                        var statProfile = template.TemplateComponents.OfType<EnemyStatProfileComponent>().FirstOrDefault();
                        if (statProfile != null)
                        {
                            totalLevel += statProfile.Level;
                        }
                    }
                }
                encountersWithLevels.Add((encounter, totalLevel));
            }

            var sortedEncounters = encountersWithLevels.OrderBy(e => e.TotalLevel).ToList();

            int totalCount = sortedEncounters.Count;
            if (totalCount == 0) return;

            int easyCount = totalCount / 3;
            int normalCount = totalCount / 3;

            _categorizedBattles[BattleDifficulty.Easy].AddRange(sortedEncounters.Take(easyCount).Select(e => e.Encounter));
            _categorizedBattles[BattleDifficulty.Normal].AddRange(sortedEncounters.Skip(easyCount).Take(normalCount).Select(e => e.Encounter));
            _categorizedBattles[BattleDifficulty.Hard].AddRange(sortedEncounters.Skip(easyCount + normalCount).Select(e => e.Encounter));

            // Handle cases where a category might be empty due to small numbers (e.g., totalCount < 3)
            if (!_categorizedBattles[BattleDifficulty.Easy].Any() && _categorizedBattles[BattleDifficulty.Normal].Any())
            {
                _categorizedBattles[BattleDifficulty.Easy].Add(_categorizedBattles[BattleDifficulty.Normal].First());
                _categorizedBattles[BattleDifficulty.Normal].RemoveAt(0);
            }
            if (!_categorizedBattles[BattleDifficulty.Normal].Any() && _categorizedBattles[BattleDifficulty.Hard].Any())
            {
                _categorizedBattles[BattleDifficulty.Normal].Add(_categorizedBattles[BattleDifficulty.Hard].First());
                _categorizedBattles[BattleDifficulty.Hard].RemoveAt(0);
            }

            var fallbackEncounter = sortedEncounters.FirstOrDefault().Encounter;
            if (fallbackEncounter != null)
            {
                if (!_categorizedBattles[BattleDifficulty.Easy].Any()) _categorizedBattles[BattleDifficulty.Easy].Add(fallbackEncounter);
                if (!_categorizedBattles[BattleDifficulty.Normal].Any()) _categorizedBattles[BattleDifficulty.Normal].Add(fallbackEncounter);
                if (!_categorizedBattles[BattleDifficulty.Hard].Any()) _categorizedBattles[BattleDifficulty.Hard].Add(fallbackEncounter);
            }
        }

        public List<string>? GetRandomBattle(BattleDifficulty difficulty)
        {
            if (_categorizedBattles.TryGetValue(difficulty, out var encounterList) && encounterList.Any())
            {
                return encounterList[_random.Next(encounterList.Count)];
            }
            // Fallback if a category is empty for some reason
            Debug.WriteLine($"[ProgressionManager] [WARNING] No battles found for difficulty '{difficulty}'. Using first available battle as fallback.");
            return CurrentSplit?.PossibleBattles?.FirstOrDefault();
        }

        public List<string>? GetRandomBattleFromSplit(string theme)
        {
            if (_splits.TryGetValue(theme, out var splitData) && splitData.PossibleBattles != null && splitData.PossibleBattles.Any())
            {
                return splitData.PossibleBattles[_random.Next(splitData.PossibleBattles.Count)];
            }
            return null;
        }

        public List<string>? GetRandomMajorBattle() => GetRandomEncounter(CurrentSplit?.PossibleMajorBattles);
        public NarrativeEvent? GetRandomNarrative()
        {
            if (CurrentSplit?.PossibleNarrativeEventIDs == null || !CurrentSplit.PossibleNarrativeEventIDs.Any())
            {
                return null;
            }

            string randomEventId = CurrentSplit.PossibleNarrativeEventIDs[_random.Next(CurrentSplit.PossibleNarrativeEventIDs.Count)];
            return GetNarrativeEvent(randomEventId);
        }

        public NarrativeEvent? GetNarrativeEvent(string eventId)
        {
            if (_narrativeEvents.TryGetValue(eventId, out var narrativeEvent))
            {
                return narrativeEvent;
            }

            Debug.WriteLine($"[ProgressionManager] [WARNING] Narrative event with ID '{eventId}' was requested but not found in the cache.");
            return null;
        }

        private List<string>? GetRandomEncounter(List<List<string>>? encounterList)
        {
            if (encounterList == null || !encounterList.Any())
            {
                return null;
            }
            return encounterList[_random.Next(encounterList.Count)];
        }
    }
}