using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Battle.Abilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static Dictionary<string, RelicData> Relics { get; private set; }
        public static Dictionary<string, PartyMemberData> PartyMembers { get; private set; }

        // Weapons dictionary removed.

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
            string elementsJson = File.ReadAllText(elementsPath);
            var elementList = JsonSerializer.Deserialize<List<ElementDefinition>>(elementsJson, jsonOptions);
            Elements = elementList.ToDictionary(e => e.ElementID, e => e);

            // --- MOVES ---
            string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
            string movesJson = File.ReadAllText(movesPath);
            var moveList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
            Moves = moveList.ToDictionary(m => m.MoveID, m => m, StringComparer.OrdinalIgnoreCase);

            foreach (var move in Moves.Values)
            {
                if (move.Effects != null && move.Effects.Count > 0)
                {
                    move.Abilities = AbilityFactory.CreateAbilitiesFromData(move.Effects, new Dictionary<string, int>());
                }
            }

            // --- RELICS ---
            string relicsPath = Path.Combine(content.RootDirectory, "Data", "Items", "Relics.json");
            if (File.Exists(relicsPath))
            {
                string relicsJson = File.ReadAllText(relicsPath);
                var relicList = JsonSerializer.Deserialize<List<RelicData>>(relicsJson, jsonOptions);
                Relics = relicList.ToDictionary(a => a.RelicID, a => a, StringComparer.OrdinalIgnoreCase);
            }
            else Relics = new Dictionary<string, RelicData>();

            // --- PARTY MEMBERS ---
            string partyPath = Path.Combine(content.RootDirectory, "Data", "PartyMembers.json");
            if (File.Exists(partyPath))
            {
                string partyJson = File.ReadAllText(partyPath);
                var partyList = JsonSerializer.Deserialize<List<PartyMemberData>>(partyJson, jsonOptions);
                PartyMembers = partyList.ToDictionary(p => p.MemberID, p => p, StringComparer.OrdinalIgnoreCase);
            }
            else PartyMembers = new Dictionary<string, PartyMemberData>();
        }
    }
}