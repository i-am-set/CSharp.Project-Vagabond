using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Battle;
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
        /// Loads all battle data from JSON files into memory.
        /// </summary>
        /// <param name="content">The game's ContentManager.</param>
        public static void LoadData(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
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
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Moves.Count} move definitions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load Moves.json: {ex.Message}");
                Moves = new Dictionary<string, MoveData>();
            }
        }

        private static void LoadInteractionMatrix(ContentManager content)
        {
            InteractionMatrix = new Dictionary<int, Dictionary<int, float>>();
            try
            {
                string matrixPath = Path.Combine(content.RootDirectory, "Data", "ElementalInteractionMatrix.csv");
                var lines = File.ReadAllLines(matrixPath);

                if (lines.Length < 3) // Need at least 2 header rows and 1 data row
                {
                    Debug.WriteLine("[BattleDataCache] [ERROR] ElementalInteractionMatrix.csv is malformed. It must have at least 3 rows.");
                    return;
                }

                // The second row contains the defending element IDs.
                var header = lines[1].Split(',');
                var defendingElementIds = new List<int>();
                for (int i = 2; i < header.Length; i++) // Skip the first two columns ("Attacking", "")
                {
                    if (int.TryParse(header[i], out int id))
                    {
                        defendingElementIds.Add(id);
                    }
                    else
                    {
                        Debug.WriteLine($"[BattleDataCache] [WARNING] Could not parse defending element ID '{header[i]}' in CSV header.");
                    }
                }

                // Parse each data row, starting from the third line
                for (int i = 2; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length < 3) continue; // Skip empty or malformed lines

                    // The attacking ID is in the second column
                    if (int.TryParse(values[1], out int attackingId))
                    {
                        var rowMatrix = new Dictionary<int, float>();
                        for (int j = 2; j < values.Length; j++) // Data starts from the third column
                        {
                            if (j - 2 < defendingElementIds.Count)
                            {
                                int defendingId = defendingElementIds[j - 2];
                                if (float.TryParse(values[j], CultureInfo.InvariantCulture, out float multiplier))
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
