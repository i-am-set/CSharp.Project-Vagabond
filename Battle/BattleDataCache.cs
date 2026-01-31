using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
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
        public static Dictionary<string, MoveData> Moves { get; private set; }
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

            // --- MOVES ---
            string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
            string movesJson = File.ReadAllText(movesPath);
            var moveList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
            Moves = moveList.ToDictionary(m => m.MoveID, m => m, StringComparer.OrdinalIgnoreCase);

            foreach (var move in Moves.Values)
            {
                if (move.Effects != null && move.Effects.Count > 0)
                {
                    // Pass the move object itself so the factory can set metadata flags
                    move.Abilities = AbilityFactory.CreateAbilitiesFromData(move, move.Effects, new Dictionary<string, int>());
                }
            }

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
