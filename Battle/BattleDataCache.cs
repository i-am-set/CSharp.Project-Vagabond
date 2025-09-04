using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// A static class to load and store all battle-related data from JSON files at runtime.
    /// This creates a central, read-only repository for data that drives the battle system.
    /// </summary>
    public static class BattleDataCache
    {
        /// <summary>
        /// A dictionary mapping Element IDs to their definitions.
        /// </summary>
        public static Dictionary<int, ElementDefinition> Elements { get; private set; }

        /// <summary>
        /// A list of all elemental interaction rules.
        /// </summary>
        public static List<ElementalInteraction> Interactions { get; private set; }

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

            try
            {
                string interactionsPath = Path.Combine(content.RootDirectory, "Data", "ElementalInteractionMatrix.json");
                string interactionsJson = File.ReadAllText(interactionsPath);
                Interactions = JsonSerializer.Deserialize<List<ElementalInteraction>>(interactionsJson, jsonOptions);
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Interactions.Count} elemental interactions.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BattleDataCache] [ERROR] Failed to load ElementalInteractionMatrix.json: {ex.Message}");
                Interactions = new List<ElementalInteraction>();
            }

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
    }
}