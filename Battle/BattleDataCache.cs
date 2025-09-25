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
    /// <summary>
    /// A static class to load and store all battle-related data from JSON and CSV files at runtime.
    /// This creates a central, read-only repository for data that drives the battle system.
    /// </summary>
    public static class BattleDataCache
    {
        /// <summary>
        /// A dictionary mapping Element IDs to their definitions.
        /// </summary>
        public static Dictionary<int, ElementDefinition> Elements { get; private set; }

        /// <summary>
        /// A matrix storing all elemental interaction multipliers for fast lookups.
        /// Outer Key: AttackingElementID, Inner Key: DefendingElementID, Value: Multiplier.
        /// </summary>
        public static Dictionary<int, Dictionary<int, float>> InteractionMatrix { get; private set; }

        /// <summary>
        /// A dictionary mapping Move IDs to their definitions.
        /// </summary>
        public static Dictionary<string, MoveData> Moves { get; private set; }

        /// <summary>
        /// A dictionary mapping Item IDs to their consumable item definitions.
        /// </summary>
        public static Dictionary<string, ConsumableItemData> Consumables { get; private set; }

        /// <summary>
        /// A dictionary mapping Ability IDs to their definitions.
        /// </summary>
        public static Dictionary<string, AbilityData> Abilities { get; private set; }

        /// <summary>
        /// Loads all battle data from JSON files into memory.
        /// </summary>
        /// <param name="content">The game's ContentManager.</param>
        public static void LoadData(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip, // Allow comments in JSON files
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
                ValidateWordLengths(Moves.Values.Select(m => m.MoveName), "Move");
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
                ValidateWordLengths(Consumables.Values.Select(c => c.ItemName), "Consumable");
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Consumables.Count} consumable item definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Consumables.json: {ex.Message}");
                Consumables = new Dictionary<string, ConsumableItemData>();
            }

            try
            {
                string abilitiesPath = Path.Combine(content.RootDirectory, "Data", "Abilities.json");
                string abilitiesJson = File.ReadAllText(abilitiesPath);
                var abilityList = JsonSerializer.Deserialize<List<AbilityData>>(abilitiesJson, jsonOptions);
                Abilities = abilityList.ToDictionary(a => a.AbilityID, a => a);
                ValidateWordLengths(Abilities.Values.Select(a => a.AbilityName), "Ability");
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Abilities.Count} ability definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Abilities.json: {ex.Message}");
                Abilities = new Dictionary<string, AbilityData>();
            }
        }

        private static void ValidateWordLengths(IEnumerable<string> names, string dataType)
        {
            const int maxWordLength = 11;
            foreach (var name in names)
            {
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
                    Debug.WriteLine($"[BattleDataCache] WARNING: CSV file not found at standard runtime path '{matrixPath}'. This means its 'Copy to Output Directory' property is likely not set to 'Copy if newer'.");
                    string fallbackPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Content", "Data", "ElementalInteractionMatrix.csv"));
                    Debug.WriteLine($"[BattleDataCache] Attempting to load from source path as a fallback: {fallbackPath}");

                    if (File.Exists(fallbackPath))
                    {
                        matrixPath = fallbackPath;
                        Debug.WriteLine("[BattleDataCache] SUCCESS: Found CSV at source path. The data will be loaded, but the project's 'Copy to Output Directory' setting MUST be fixed for release builds.");
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleDataCache] FATAL: Could not find CSV at source path either. The file may be missing or the path is incorrect. Aborting matrix load.");
                        return;
                    }
                }

                var lines = File.ReadAllLines(matrixPath);

                if (lines.Length < 2) // Need at least a header and one data row
                {
                    Debug.WriteLine("[BattleDataCache] [ERROR] ElementalInteractionMatrix.csv is malformed. It must have at least 2 rows (header + data).");
                    return;
                }

                // Find the header row which contains "Attacking" and the defending IDs
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

                if (headerRowIndex == -1)
                {
                    Debug.WriteLine("[BattleDataCache] [ERROR] Could not find the header row (the one starting with 'Attacking').");
                    return;
                }

                var header = lines[headerRowIndex].Split(',');
                var defendingElementIds = new List<int>();
                // Defending IDs start from the 3rd column (index 2)
                for (int i = 2; i < header.Length; i++)
                {
                    if (int.TryParse(header[i].Trim(), out int id))
                    {
                        defendingElementIds.Add(id);
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleDataCache] [WARNING] Could not parse defending element ID from header: '{header[i]}'");
                    }
                }

                if (!defendingElementIds.Any())
                {
                    Debug.WriteLine("[BattleDataCache] [ERROR] No defending element IDs could be parsed from the header row.");
                    return;
                }

                // Data rows start from the line after the header
                for (int i = headerRowIndex + 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length < 3) continue; // Need at least Name, ID, and one value

                    // Attacking ID is in the 2nd column (index 1)
                    if (int.TryParse(values[1].Trim(), out int attackingId))
                    {
                        var rowMatrix = new Dictionary<int, float>();
                        // Multiplier values start from the 3rd column (index 2)
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
                                else
                                {
                                    Debug.WriteLine($"[BattleDataCache] [WARNING] Could not parse multiplier for AttackingID {attackingId} vs DefendingID {defendingId}. Value: '{values[j]}'");
                                }
                            }
                        }
                        InteractionMatrix[attackingId] = rowMatrix;
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleDataCache] [WARNING] Could not parse attacking element ID from row: '{lines[i]}'");
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