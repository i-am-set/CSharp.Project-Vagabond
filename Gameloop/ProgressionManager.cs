using ProjectVagabond.Battle;
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
        private readonly Dictionary<string, SplitData> _splits = new Dictionary<string, SplitData>();
        private static readonly Random _random = new Random();

        public SplitData? CurrentSplit { get; private set; }
        public SplitMap? CurrentSplitMap { get; private set; }

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

            CurrentSplit = _splits.Values.ElementAt(_random.Next(_splits.Count));
            CurrentSplitMap = SplitMapGenerator.Generate(CurrentSplit);
            Debug.WriteLine($"[ProgressionManager] Generated new split map: {CurrentSplit.Theme} with {CurrentSplitMap.TotalFloors} floors.");
        }

        public List<string>? GetRandomBattle() => GetRandomEncounter(CurrentSplit?.PossibleBattles);
        public List<string>? GetRandomMajorBattle() => GetRandomEncounter(CurrentSplit?.PossibleMajorBattles);
        public NarrativeEvent? GetRandomNarrative()
        {
            if (CurrentSplit?.PossibleNarratives == null || !CurrentSplit.PossibleNarratives.Any())
            {
                return null;
            }
            return CurrentSplit.PossibleNarratives[_random.Next(CurrentSplit.PossibleNarratives.Count)];
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