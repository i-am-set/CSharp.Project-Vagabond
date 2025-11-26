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
        // Dictionaries now use Case-Insensitive Comparers
        public static Dictionary<string, MoveData> Moves { get; private set; }
        public static Dictionary<string, ConsumableItemData> Consumables { get; private set; }
        public static Dictionary<string, RelicData> Relics { get; private set; }
        public static Dictionary<string, WeaponData> Weapons { get; private set; }
        public static Dictionary<string, ArmorData> Armors { get; private set; } // <--- Added

        public static void LoadData(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // --- ELEMENTS ---
            string elementsPath = Path.Combine(content.RootDirectory, "Data", "Elements.json");
            if (!File.Exists(elementsPath)) throw new FileNotFoundException($"Could not find Elements.json at {elementsPath}");

            string elementsJson = File.ReadAllText(elementsPath);
            var elementList = JsonSerializer.Deserialize<List<ElementDefinition>>(elementsJson, jsonOptions);
            Elements = elementList.ToDictionary(e => e.ElementID, e => e);
            Debug.WriteLine($"[BattleDataCache] Successfully loaded {Elements.Count} element definitions.");

            // --- INTERACTION MATRIX ---
            LoadInteractionMatrix(content);

            // --- MOVES ---
            string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
            if (!File.Exists(movesPath)) throw new FileNotFoundException($"Could not find Moves.json at {movesPath}");

            string movesJson = File.ReadAllText(movesPath);
            var moveList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
            Moves = moveList.ToDictionary(m => m.MoveID, m => m, StringComparer.OrdinalIgnoreCase);
            ValidateWordLengths(Moves.Values.Select(m => m.MoveName), "Move", 11);
            Debug.WriteLine($"[BattleDataCache] Successfully loaded {Moves.Count} move definitions.");

            // --- CONSUMABLES ---
            string consumablesPath = Path.Combine(content.RootDirectory, "Data", "Items", "Consumables.json");
            if (!File.Exists(consumablesPath)) throw new FileNotFoundException($"Could not find Consumables.json at {consumablesPath}");

            string consumablesJson = File.ReadAllText(consumablesPath);
            var consumableList = JsonSerializer.Deserialize<List<ConsumableItemData>>(consumablesJson, jsonOptions);
            Consumables = consumableList.ToDictionary(c => c.ItemID, c => c, StringComparer.OrdinalIgnoreCase);
            ValidateWordLengths(Consumables.Values.Select(c => c.ItemName), "Consumable", 11);
            Debug.WriteLine($"[BattleDataCache] Successfully loaded {Consumables.Count} consumable item definitions.");

            // --- RELICS ---
            string relicsPath = Path.Combine(content.RootDirectory, "Data", "Items", "Relics.json");
            if (!File.Exists(relicsPath)) throw new FileNotFoundException($"Could not find Relics.json at {relicsPath}");

            string relicsJson = File.ReadAllText(relicsPath);
            var relicList = JsonSerializer.Deserialize<List<RelicData>>(relicsJson, jsonOptions);
            Relics = relicList.ToDictionary(a => a.RelicID, a => a, StringComparer.OrdinalIgnoreCase);
            ValidateWordLengths(Relics.Values.Select(a => a.RelicName), "Relic", 11);
            ValidateWordLengths(Relics.Values.Select(a => a.AbilityName), "Ability", 14);

            Debug.WriteLine($"[BattleDataCache] Successfully loaded {Relics.Count} relic definitions.");

            // --- WEAPONS ---
            string weaponsPath = Path.Combine(content.RootDirectory, "Data", "Items", "Weapons.json");
            if (!File.Exists(weaponsPath))
            {
                Debug.WriteLine($"[BattleDataCache] WARNING: Weapons.json not found at {weaponsPath}. Creating empty dictionary.");
                Weapons = new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                string weaponsJson = File.ReadAllText(weaponsPath);
                var weaponList = JsonSerializer.Deserialize<List<WeaponData>>(weaponsJson, jsonOptions);
                Weapons = weaponList.ToDictionary(w => w.WeaponID, w => w, StringComparer.OrdinalIgnoreCase);
                ValidateWordLengths(Weapons.Values.Select(w => w.WeaponName), "Weapon", 11);

                foreach (var weapon in Weapons.Values)
                {
                    if (!Moves.ContainsKey(weapon.MoveID))
                    {
                        Debug.WriteLine($"[BattleDataCache] [ERROR] Weapon '{weapon.WeaponName}' references missing MoveID '{weapon.MoveID}'.");
                    }
                }
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Weapons.Count} weapon definitions.");
            }

            // --- ARMOR --- (New Section)
            string armorPath = Path.Combine(content.RootDirectory, "Data", "Items", "Armor.json");
            if (!File.Exists(armorPath))
            {
                Debug.WriteLine($"[BattleDataCache] WARNING: Armor.json not found at {armorPath}. Creating empty dictionary.");
                Armors = new Dictionary<string, ArmorData>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                string armorJson = File.ReadAllText(armorPath);
                var armorList = JsonSerializer.Deserialize<List<ArmorData>>(armorJson, jsonOptions);
                Armors = armorList.ToDictionary(a => a.ArmorID, a => a, StringComparer.OrdinalIgnoreCase);
                ValidateWordLengths(Armors.Values.Select(a => a.ArmorName), "Armor", 11);

                // Validate that all armors have stat modifiers
                foreach (var armor in Armors.Values)
                {
                    if (armor.StatModifiers == null || armor.StatModifiers.Count == 0)
                    {
                        Debug.WriteLine($"[BattleDataCache] [ERROR] Armor '{armor.ArmorName}' (ID: {armor.ArmorID}) has no StatModifiers. All armor must modify stats.");
                    }
                }
                Debug.WriteLine($"[BattleDataCache] Successfully loaded {Armors.Count} armor definitions.");
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

            string matrixPath = Path.Combine(content.RootDirectory, "Data", "ElementalInteractionMatrix.csv");

            if (!File.Exists(matrixPath))
            {
                throw new FileNotFoundException($"[BattleDataCache] CSV file not found at '{matrixPath}'.");
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
    }
}