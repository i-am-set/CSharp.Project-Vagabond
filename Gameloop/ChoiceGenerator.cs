using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A service responsible for generating curated lists of choices (spells, abilities, items)
    /// based on the current game progression and a weighted rarity system. This decouples the
    /// choice generation logic from the UI scenes that display them.
    /// </summary>
    public class ChoiceGenerator
    {
        private static readonly Random _random = new Random();

        // --- Tuning: Adjust these weights to change the feel of rarity distribution ---
        private readonly Dictionary<int, int> _rarityWeights = new Dictionary<int, int>
        {
            // Rarity Level -> Weight
            { 0, 60 }, // Common
            { 1, 30 }, // Uncommon
            { 2, 9 },  // Rare
            { 3, 1 }   // Epic
            // Mythic and Legendary are excluded from the default pool to make them special rewards.
        };

        /// <summary>
        /// Generates a list of spell choices for the player.
        /// </summary>
        /// <param name="gameStage">The current progression tier of the game. Spells with a LevelRequirement greater than this will be excluded.</param>
        /// <param name="count">The number of spell choices to generate.</param>
        /// <param name="excludeIds">An optional set of MoveIDs to exclude from the selection pool.</param>
        /// <returns>A list of MoveData objects representing the choices.</returns>
        public List<MoveData> GenerateSpellChoices(int gameStage, int count, HashSet<string>? excludeIds = null)
        {
            var chosenSpells = new HashSet<MoveData>();

            // 1. Filter the master spell list based on the current game stage and exclusions.
            var availableSpellsQuery = BattleDataCache.Moves.Values
                .Where(m => m.MoveType == MoveType.Spell && m.LevelRequirement <= gameStage);

            if (excludeIds != null)
            {
                availableSpellsQuery = availableSpellsQuery.Where(m => !excludeIds.Contains(m.MoveID));
            }

            var availableSpells = availableSpellsQuery.ToList();

            if (!availableSpells.Any())
            {
                // Fallback if no spells are available at the current stage
                return new List<MoveData>();
            }

            // 2. Group available spells by their rarity.
            var spellsByRarity = availableSpells
                .GroupBy(s => s.Rarity)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. Filter the rarity weights to only include rarities that are actually available.
            var availableRarityWeights = _rarityWeights
                .Where(kvp => spellsByRarity.ContainsKey(kvp.Key) && spellsByRarity[kvp.Key].Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (!availableRarityWeights.Any())
            {
                return new List<MoveData>(); // No valid rarities to choose from
            }

            // 4. Perform weighted random selection until we have enough unique spells.
            while (chosenSpells.Count < count && chosenSpells.Count < availableSpells.Count)
            {
                int totalWeight = availableRarityWeights.Values.Sum();
                int randomWeight = _random.Next(0, totalWeight);

                int chosenRarity = -1;
                foreach (var (rarity, weight) in availableRarityWeights.OrderBy(kvp => kvp.Key))
                {
                    if (randomWeight < weight)
                    {
                        chosenRarity = rarity;
                        break;
                    }
                    randomWeight -= weight;
                }

                if (chosenRarity != -1 && spellsByRarity.TryGetValue(chosenRarity, out var potentialSpells))
                {
                    // Select a random spell from the chosen rarity tier.
                    var availableInRarity = potentialSpells.Except(chosenSpells).ToList();
                    if (availableInRarity.Any())
                    {
                        var spell = availableInRarity[_random.Next(availableInRarity.Count)];
                        chosenSpells.Add(spell);
                    }
                }
            }

            // 5. Ensure variety by preventing all choices from being the same rarity if possible.
            if (chosenSpells.Count == count && chosenSpells.Select(s => s.Rarity).Distinct().Count() == 1)
            {
                // If we have other options, replace one of the choices.
                var otherSpells = availableSpells.Except(chosenSpells).ToList();
                if (otherSpells.Any())
                {
                    var spellToReplace = chosenSpells.First();
                    chosenSpells.Remove(spellToReplace);
                    chosenSpells.Add(otherSpells[_random.Next(otherSpells.Count)]);
                }
            }


            return chosenSpells.ToList();
        }

        /// <summary>
        /// Generates a list of ability choices for the player.
        /// </summary>
        /// <param name="gameStage">The current progression tier of the game. Abilities with a LevelRequirement greater than this will be excluded.</param>
        /// <param name="count">The number of ability choices to generate.</param>
        /// <param name="excludeIds">An optional set of AbilityIDs to exclude from the selection pool.</param>
        /// <returns>A list of AbilityData objects representing the choices.</returns>
        public List<AbilityData> GenerateAbilityChoices(int gameStage, int count, HashSet<string>? excludeIds = null)
        {
            var chosenAbilities = new HashSet<AbilityData>();

            var availableAbilitiesQuery = BattleDataCache.Abilities.Values
                .Where(a => a.LevelRequirement <= gameStage);

            if (excludeIds != null)
            {
                availableAbilitiesQuery = availableAbilitiesQuery.Where(a => !excludeIds.Contains(a.AbilityID));
            }

            var availableAbilities = availableAbilitiesQuery.ToList();

            if (!availableAbilities.Any())
            {
                return new List<AbilityData>();
            }

            var abilitiesByRarity = availableAbilities
                .GroupBy(a => a.Rarity)
                .ToDictionary(g => g.Key, g => g.ToList());

            var availableRarityWeights = _rarityWeights
                .Where(kvp => abilitiesByRarity.ContainsKey(kvp.Key) && abilitiesByRarity[kvp.Key].Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (!availableRarityWeights.Any())
            {
                return new List<AbilityData>();
            }

            while (chosenAbilities.Count < count && chosenAbilities.Count < availableAbilities.Count)
            {
                int totalWeight = availableRarityWeights.Values.Sum();
                int randomWeight = _random.Next(0, totalWeight);

                int chosenRarity = -1;
                foreach (var (rarity, weight) in availableRarityWeights.OrderBy(kvp => kvp.Key))
                {
                    if (randomWeight < weight)
                    {
                        chosenRarity = rarity;
                        break;
                    }
                    randomWeight -= weight;
                }

                if (chosenRarity != -1 && abilitiesByRarity.TryGetValue(chosenRarity, out var potentialAbilities))
                {
                    var availableInRarity = potentialAbilities.Except(chosenAbilities).ToList();
                    if (availableInRarity.Any())
                    {
                        var ability = availableInRarity[_random.Next(availableInRarity.Count)];
                        chosenAbilities.Add(ability);
                    }
                }
            }

            if (chosenAbilities.Count == count && chosenAbilities.Select(a => a.Rarity).Distinct().Count() == 1)
            {
                var otherAbilities = availableAbilities.Except(chosenAbilities).ToList();
                if (otherAbilities.Any())
                {
                    var abilityToReplace = chosenAbilities.First();
                    chosenAbilities.Remove(abilityToReplace);
                    chosenAbilities.Add(otherAbilities[_random.Next(otherAbilities.Count)]);
                }
            }

            return chosenAbilities.ToList();
        }
    }
}