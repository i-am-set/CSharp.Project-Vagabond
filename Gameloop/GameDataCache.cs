using Microsoft.Xna.Framework.Content;
using ProjectVagabond.Animations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Battle
{
    public static class GameDataCache
    {
        public static Dictionary<string, WizardCatData> WizardCats { get; private set; }
        public static Dictionary<string, MoveData> Moves { get; private set; }
        public static Dictionary<string, AnimationData> Animations { get; private set; }

        public static void LoadData(ContentManager content)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Converters = { new JsonStringEnumConverter() }
            };

            string partyPath = Path.Combine(content.RootDirectory, "Data", "WizardCats.json");
            if (File.Exists(partyPath))
            {
                string partyJson = File.ReadAllText(partyPath);
                var partyList = JsonSerializer.Deserialize<List<WizardCatData>>(partyJson, jsonOptions);
                WizardCats = partyList.ToDictionary(p => p.MemberID, p => p, StringComparer.OrdinalIgnoreCase);
            }
            else WizardCats = new Dictionary<string, WizardCatData>();

            string movesPath = Path.Combine(content.RootDirectory, "Data", "Moves.json");
            if (File.Exists(movesPath))
            {
                string movesJson = File.ReadAllText(movesPath);
                var movesList = JsonSerializer.Deserialize<List<MoveData>>(movesJson, jsonOptions);
                Moves = movesList.ToDictionary(m => m.ID, m => m, StringComparer.OrdinalIgnoreCase);
            }
            else Moves = new Dictionary<string, MoveData>();

            string animPath = Path.Combine(content.RootDirectory, "Data", "Animations.json");
            if (File.Exists(animPath))
            {
                string animJson = File.ReadAllText(animPath);
                var animList = JsonSerializer.Deserialize<List<AnimationData>>(animJson, jsonOptions);
                Animations = animList.ToDictionary(a => a.ID, a => a, StringComparer.OrdinalIgnoreCase);
            }
            else Animations = new Dictionary<string, AnimationData>();
        }
    }
}