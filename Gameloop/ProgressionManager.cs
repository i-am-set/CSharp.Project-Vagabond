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
        private static readonly Random _random = new Random();

        public SplitData? CurrentSplit { get; private set; }
        public SplitMap? CurrentSplitMap { get; private set; }
        public int CurrentSplitCap { get; set; } = 5;

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

            ValidateSplits();
        }

        private void ValidateSplits()
        {
            foreach (var split in _splits.Values)
            {

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

            CurrentSplitMap?.Dispose();

            CurrentSplit = _splits.Values.ElementAt(_random.Next(_splits.Count));
            CategorizeBattles(CurrentSplit);

            CurrentSplitMap = SplitMapGenerator.GenerateInitial(CurrentSplit);

            if (CurrentSplitMap != null)
            {
                Debug.WriteLine($"[ProgressionManager] Generated initial split map: {CurrentSplit.Theme} with target {CurrentSplitMap.TargetColumnCount} columns.");
            }
            else
            {
                Debug.WriteLine("[ProgressionManager] [CRITICAL] Map generation returned null. This should not happen.");
            }
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

            var dataManager = ServiceLocator.Get<DataManager>();
            var encountersWithPower = new List<(List<string> Encounter, int PowerScore)>();

            foreach (var encounter in splitData.PossibleBattles)
            {
                int powerScore = 0;
                foreach (var archetypeId in encounter)
                {
                    var enemyData = dataManager.GetEnemyData(archetypeId);
                    if (enemyData != null)
                    {
                        powerScore += enemyData.MinHP + (enemyData.MinStrength + enemyData.MinIntelligence + enemyData.MinTenacity + enemyData.MinAgility) * 10;
                    }
                }
                encountersWithPower.Add((encounter, powerScore));
            }

            var sortedEncounters = encountersWithPower.OrderBy(e => e.PowerScore).ToList();

            int totalCount = sortedEncounters.Count;
            if (totalCount == 0) return;

            int easyCount = totalCount / 3;
            int normalCount = totalCount / 3;

            _categorizedBattles[BattleDifficulty.Easy].AddRange(sortedEncounters.Take(easyCount).Select(e => e.Encounter));
            _categorizedBattles[BattleDifficulty.Normal].AddRange(sortedEncounters.Skip(easyCount).Take(normalCount).Select(e => e.Encounter));
            _categorizedBattles[BattleDifficulty.Hard].AddRange(sortedEncounters.Skip(easyCount + normalCount).Select(e => e.Encounter));

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