using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
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
        public static Dictionary<string, MoveData> Moves { get; private set; }
        public static Dictionary<string, ConsumableItemData> Consumables { get; private set; }
        public static Dictionary<string, RelicData> Relics { get; private set; }
        public static Dictionary<string, WeaponData> Weapons { get; private set; }
        public static Dictionary<string, MiscItemData> MiscItems { get; private set; }
        public static Dictionary<string, PartyMemberData> PartyMembers { get; private set; }
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

            // --- MOVES ---
            string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
            if (!File.Exists(movesPath)) throw new FileNotFoundException($"Could not find Moves.json at {movesPath}");
            string movesJson = File.ReadAllText(movesPath);
            var moveList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
            Moves = moveList.ToDictionary(m => m.MoveID, m => m, StringComparer.OrdinalIgnoreCase);

            // Hydrate Moves with Abilities
            foreach (var move in Moves.Values)
            {
                if (move.Effects != null && move.Effects.Count > 0)
                {
                    // We pass an empty stat dictionary because moves don't have stat modifiers in the same way items do
                    move.Abilities = AbilityFactory.CreateAbilitiesFromData(move.Effects, new Dictionary<string, int>());
                }
            }

            // --- CONSUMABLES ---
            string consumablesPath = Path.Combine(content.RootDirectory, "Data", "Items", "Consumables.json");
            if (File.Exists(consumablesPath))
            {
                string consumablesJson = File.ReadAllText(consumablesPath);
                var consumableList = JsonSerializer.Deserialize<List<ConsumableItemData>>(consumablesJson, jsonOptions);
                Consumables = consumableList.ToDictionary(c => c.ItemID, c => c, StringComparer.OrdinalIgnoreCase);
            }
            else Consumables = new Dictionary<string, ConsumableItemData>();

            // --- RELICS ---
            string relicsPath = Path.Combine(content.RootDirectory, "Data", "Items", "Relics.json");
            if (File.Exists(relicsPath))
            {
                string relicsJson = File.ReadAllText(relicsPath);
                var relicList = JsonSerializer.Deserialize<List<RelicData>>(relicsJson, jsonOptions);
                Relics = relicList.ToDictionary(a => a.RelicID, a => a, StringComparer.OrdinalIgnoreCase);
            }
            else Relics = new Dictionary<string, RelicData>();

            // --- WEAPONS ---
            string weaponsPath = Path.Combine(content.RootDirectory, "Data", "Items", "Weapons.json");
            if (File.Exists(weaponsPath))
            {
                string weaponsJson = File.ReadAllText(weaponsPath);
                var weaponList = JsonSerializer.Deserialize<List<WeaponData>>(weaponsJson, jsonOptions);
                Weapons = weaponList.ToDictionary(w => w.WeaponID, w => w, StringComparer.OrdinalIgnoreCase);
            }
            else Weapons = new Dictionary<string, WeaponData>();

            // --- MISC ITEMS ---
            string miscPath = Path.Combine(content.RootDirectory, "Data", "Items", "Misc.json");
            if (File.Exists(miscPath))
            {
                string miscJson = File.ReadAllText(miscPath);
                var miscList = JsonSerializer.Deserialize<List<MiscItemData>>(miscJson, jsonOptions);
                MiscItems = miscList.ToDictionary(m => m.ItemID, m => m, StringComparer.OrdinalIgnoreCase);
            }
            else MiscItems = new Dictionary<string, MiscItemData>();

            // --- PARTY MEMBERS ---
            string partyPath = Path.Combine(content.RootDirectory, "Data", "PartyMembers.json");
            if (File.Exists(partyPath))
            {
                string partyJson = File.ReadAllText(partyPath);
                var partyList = JsonSerializer.Deserialize<List<PartyMemberData>>(partyJson, jsonOptions);
                PartyMembers = partyList.ToDictionary(p => p.MemberID, p => p, StringComparer.OrdinalIgnoreCase);
                Debug.WriteLine($"[BattleDataCache] Loaded {PartyMembers.Count} party member templates.");
            }
            else
            {
                Debug.WriteLine("[BattleDataCache] WARNING: PartyMembers.json not found.");
                PartyMembers = new Dictionary<string, PartyMemberData>();
            }
        }
    }
}