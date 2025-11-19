using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Battle
{
    public static class BattleDataCache
    {
        public static Dictionary<int, ElementDefinition> Elements { get; private set; }
        public static Dictionary<int, Dictionary<int, float>> InteractionMatrix { get; private set; }
        public static Dictionary<string, MoveData> Moves { get; private set; }
        public static Dictionary<string, ConsumableItemData> Consumables { get; private set; }
        public static Dictionary<string, RelicData> Relics { get; private set; }

        public static void LoadData(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
            };

            try
            {
                string elementsPath = Path.Combine(content.RootDirectory, "Data", "Elements.json");
                string elementsJson = File.ReadAllText(elementsPath);
                var elementList = JsonSerializer.Deserialize<List<ElementDefinition>>(elementsJson, jsonOptions);
                Elements = elementList.ToDictionary(e => e.ElementID, e => e);
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Elements.Count} element definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Elements.json: {ex.Message}");
                Elements = new Dictionary<int, ElementDefinition>();
            }

            LoadInteractionMatrix(content);

            try
            {
                string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
                string movesJson = File.ReadAllText(movesPath);
                var moveList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
                Moves = moveList.ToDictionary(m => m.MoveID, m => m);
                ValidateWordLengths(Moves.Values.Select(m => m.MoveName), "Move", 11);
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Moves.Count} move definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Moves.json: {ex.Message}");
                Moves = new Dictionary<string, MoveData>();
            }

            try
            {
                string consumablesPath = Path.Combine(content.RootDirectory, "Data", "items", "Consumables.json");
                string consumablesJson = File.ReadAllText(consumablesPath);
                var consumableList = JsonSerializer.Deserialize<List<ConsumableItemData>>(consumablesJson, jsonOptions);
                Consumables = consumableList.ToDictionary(c => c.ItemID, c => c);
                ValidateWordLengths(Consumables.Values.Select(c => c.ItemName), "Consumable", 11);
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Consumables.Count} consumable item definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Consumables.json: {ex.Message}");
                Consumables = new Dictionary<string, ConsumableItemData>();
            }

            try
            {
                string relicsPath = Path.Combine(content.RootDirectory, "Data", "Relics.json");
                string relicsJson = File.ReadAllText(relicsPath);
                var relicList = JsonSerializer.Deserialize<List<RelicData>>(relicsJson, jsonOptions);
                Relics = relicList.ToDictionary(a => a.RelicID, a => a);
                ValidateWordLengths(Relics.Values.Select(a => a.RelicName), "Relic", 11);
                ValidateWordLengths(Relics.Values.Select(a => a.AbilityName), "Ability", 14);
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Relics.Count} relic definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Relics.json: {ex.Message}");
                Relics = new Dictionary<string, RelicData>();
            }
        }

        private static void ValidateWordLengths(IEnumerable<string> names, string dataType, int maxWordLength)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;

                var words = name.Split(' ');
                foreach (var word in words)
                {
                    if (word.Length > maxWordLength)
                    {
                        Debug.WriteLine($"[BattleDataCache] [VALIDATION ERROR] {dataType} name '{name}' contains a word ('{word}') that is longer than the maximum of {maxWordLength} characters. This may cause UI overflow.");
                    }
                }
            }
        }

        private static void LoadInteractionMatrix(ContentManager content)
        {
            InteractionMatrix = new Dictionary<int, Dictionary<int, float>>();
            try
            {
                string matrixPath = Path.Combine(content.RootDirectory, "Data", "ElementalInteractionMatrix.csv");

                if (!File.Exists(matrixPath))
                {
                    Debug.WriteLine($"[BattleDataCache] WARNING: CSV file not found at standard runtime path '{matrixPath}'.");
                    return;
                }

                var lines = File.ReadAllLines(matrixPath);
                if (lines.Length < 2) return;

                int headerRowIndex = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length > 0 && parts[0].Trim().Equals("Attacking", StringComparison.OrdinalIgnoreCase))
                    {
                        headerRowIndex = i;
                        break;
                    }
                }

                if (headerRowIndex == -1) return;

                var header = lines[headerRowIndex].Split(',');
                var defendingElementIds = new List<int>();
                for (int i = 2; i < header.Length; i++)
                {
                    if (int.TryParse(header[i].Trim(), out int id))
                    {
                        defendingElementIds.Add(id);
                    }
                }

                for (int i = headerRowIndex + 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length < 3) continue;

                    if (int.TryParse(values[1].Trim(), out int attackingId))
                    {
                        var rowMatrix = new Dictionary<int, float>();
                        for (int j = 2; j < values.Length; j++)
                        {
                            int defendingIdIndex = j - 2;
                            if (defendingIdIndex < defendingElementIds.Count)
                            {
                                int defendingId = defendingElementIds[defendingIdIndex];
                                if (float.TryParse(values[j].Trim(), CultureInfo.InvariantCulture, out float multiplier))
                                {
                                    rowMatrix[defendingId] = multiplier;
                                }
                            }
                        }
                        InteractionMatrix[attackingId] = rowMatrix;
                    }
                }
                Debug.WriteLine($"[BattleDataCache] Successfully loaded elemental interaction matrix with {InteractionMatrix.Count} attacking types.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load ElementalInteractionMatrix.csv: {ex.Message}");
                InteractionMatrix = new Dictionary<int, Dictionary<int, float>>();
            }
        }
    }
}
