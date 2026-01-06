using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Utils
{
    public class ChoiceGenerator
    {
        private static readonly Random _random = new Random();

        private readonly Dictionary<int, int> _rarityWeights = new Dictionary<int, int>
        {
            { 0, 60 },
            { 1, 30 },
            { 2, 9 },
            { 3, 1 }
        };

        private const float ITEM_BASE_WEIGHT = 100f;
        private const float FIRST_TAG_BONUS = 200f;
        private const float SUBSEQUENT_TAG_BONUS_FACTOR = 0.5f;
        private const float MAX_TAG_BONUS = 380f;

        public List<MoveData> GenerateSpellChoices(int count, HashSet<string>? excludeIds = null)
        {
            return GenerateChoices(
                count,
                BattleDataCache.Moves.Values.Where(m => m.MoveType == MoveType.Spell),
                item => item.Rarity,
                item => item.MoveID,
                item => item.MoveName,
                item => item.Tags,
                excludeIds
            );
        }

        public List<RelicData> GenerateAbilityChoices(int count, HashSet<string>? excludeIds = null)
        {
            return GenerateChoices(
                count,
                BattleDataCache.Relics.Values,
                item => item.Rarity,
                item => item.RelicID,
                item => item.RelicName,
                item => item.Tags,
                excludeIds
            );
        }

        private List<T> GenerateChoices<T>(
            int count,
            IEnumerable<T> allItems,
            Func<T, int> getRarity,
            Func<T, string> getId,
            Func<T, string> getName,
            Func<T, List<string>> getTags,
            HashSet<string>? excludeIds) where T : class
        {
            var log = new StringBuilder();
            log.AppendLine($"\n--- STARTING REWARD GENERATION ({typeof(T).Name}, {count} choices) ---");

            var chosenItems = new HashSet<T>();
            var playerTagCounts = CalculatePlayerTagCounts();

            log.AppendLine("Player Build Analysis (Tag Counts):");
            if (playerTagCounts.Any())
            {
                foreach (var tagCount in playerTagCounts.OrderBy(kvp => kvp.Key))
                {
                    log.AppendLine($"  - {tagCount.Key}: {tagCount.Value}");
                }
            }
            else
            {
                log.AppendLine("  - No relevant tags found in current build.");
            }

            var availableItemsQuery = allItems;

            if (excludeIds != null)
            {
                availableItemsQuery = availableItemsQuery.Where(item => !excludeIds.Contains(getId(item)));
            }

            var availableItems = availableItemsQuery.ToList();
            if (!availableItems.Any())
            {
                log.AppendLine("--- No available items found after filtering. Returning empty list. ---");
                Debug.WriteLine(log.ToString());
                return new List<T>();
            }

            var itemsByRarity = availableItems
                .GroupBy(getRarity)
                .ToDictionary(g => g.Key, g => g.ToList());

            var availableRarityWeights = _rarityWeights
                .Where(kvp => itemsByRarity.ContainsKey(kvp.Key) && itemsByRarity[kvp.Key].Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (!availableRarityWeights.Any())
            {
                log.AppendLine("--- No items with valid rarities found. Returning empty list. ---");
                Debug.WriteLine(log.ToString());
                return new List<T>();
            }

            while (chosenItems.Count < count && chosenItems.Count < availableItems.Count)
            {
                log.AppendLine($"\n--- Picking Choice {chosenItems.Count + 1} of {count} ---");

                int totalRarityWeight = availableRarityWeights.Values.Sum();
                int randomRarityRoll = _random.Next(0, totalRarityWeight);
                int chosenRarity = -1;

                log.AppendLine("Step 1: Selecting Rarity Tier");
                log.Append("  - Available Tiers: ");
                log.AppendLine(string.Join(", ", availableRarityWeights.Select(kvp => $"{((RarityLevel)kvp.Key)} ({kvp.Value})")));
                log.AppendLine($"  - Total Rarity Weight: {totalRarityWeight}");
                log.Append($"  - Rolled: {randomRarityRoll} -> ");

                int cumulativeWeight = 0;
                foreach (var (rarity, weight) in availableRarityWeights.OrderBy(kvp => kvp.Key))
                {
                    cumulativeWeight += weight;
                    if (randomRarityRoll < cumulativeWeight)
                    {
                        chosenRarity = rarity;
                        break;
                    }
                }
                log.AppendLine($"Chosen Tier: {(RarityLevel)chosenRarity}");


                if (chosenRarity == -1 || !itemsByRarity.TryGetValue(chosenRarity, out var potentialItems))
                {
                    log.AppendLine("  - ERROR: Failed to select a valid rarity tier. Skipping choice.");
                    continue;
                }

                log.AppendLine($"Step 2: Selecting Item from Tier '{(RarityLevel)chosenRarity}'");
                var availableInRarity = potentialItems.Except(chosenItems).ToList();
                if (!availableInRarity.Any())
                {
                    log.AppendLine("  - No new items available in this tier. Retrying...");
                    continue;
                }

                log.AppendLine($"  - Calculating weights for {availableInRarity.Count} available item(s)...");
                var weightedItemsInTier = new List<(T item, float weight)>();
                foreach (var item in availableInRarity)
                {
                    var bonusLog = new StringBuilder();
                    float tagBonus = CalculateTagBonus(getTags(item), playerTagCounts, bonusLog);
                    float finalWeight = ITEM_BASE_WEIGHT + tagBonus;
                    weightedItemsInTier.Add((item, finalWeight));

                    log.AppendLine($"    - Item: '{getName(item)}'");
                    log.AppendLine($"      - Base Weight: {ITEM_BASE_WEIGHT}");
                    if (bonusLog.Length > 0)
                    {
                        log.Append(bonusLog.ToString());
                    }
                    log.AppendLine($"      - Final Weight: {finalWeight}");
                }

                float totalItemWeight = weightedItemsInTier.Sum(p => p.weight);
                double randomItemRoll = _random.NextDouble() * totalItemWeight;
                log.AppendLine($"  - Total Item Weight in Tier: {totalItemWeight:F2}");
                log.Append($"  - Rolled: {randomItemRoll:F2} -> ");

                float cumulativeItemWeight = 0f;
                foreach (var (item, weight) in weightedItemsInTier)
                {
                    cumulativeItemWeight += weight;
                    if (randomItemRoll < cumulativeItemWeight)
                    {
                        chosenItems.Add(item);
                        log.AppendLine($"Chosen Item: '{getName(item)}'");
                        break;
                    }
                }
            }

            log.AppendLine("\n--- FINAL REWARD CHOICES ---");
            foreach (var item in chosenItems)
            {
                log.AppendLine($"  - {getName(item)}");
            }
            log.AppendLine("--- END REWARD GENERATION ---");
            Debug.WriteLine(log.ToString());

            return chosenItems.ToList();
        }

        private float CalculateTagBonus(List<string> itemTags, Dictionary<string, int> playerTagCounts, StringBuilder bonusLog)
        {
            if (itemTags == null || !itemTags.Any()) return 0f;

            float totalBonus = 0f;

            foreach (var tag in itemTags)
            {
                if (playerTagCounts.TryGetValue(tag, out int count))
                {
                    float tagBonus = 0;
                    float currentBonusIncrement = FIRST_TAG_BONUS;
                    for (int i = 0; i < count; i++)
                    {
                        tagBonus += currentBonusIncrement;
                        currentBonusIncrement *= SUBSEQUENT_TAG_BONUS_FACTOR;
                    }
                    totalBonus += tagBonus;
                    bonusLog.AppendLine($"      - Tag '{tag}' (Player has {count}): +{tagBonus:F2}");
                }
            }

            float clampedBonus = Math.Min(totalBonus, MAX_TAG_BONUS);
            if (clampedBonus < totalBonus)
            {
                bonusLog.AppendLine($"      - Total Bonus Clamped: {totalBonus:F2} -> {clampedBonus:F2}");
            }
            return clampedBonus;
        }

        private Dictionary<string, int> CalculatePlayerTagCounts()
        {
            var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var gameState = ServiceLocator.Get<GameState>();
            var componentStore = ServiceLocator.Get<ComponentStore>();

            if (gameState.PlayerState?.Spells != null)
            {
                foreach (var entry in gameState.PlayerState.Spells)
                {
                    if (entry != null && BattleDataCache.Moves.TryGetValue(entry.MoveID, out var moveData) && moveData.Tags != null)
                    {
                        foreach (var tag in moveData.Tags)
                        {
                            tagCounts[tag] = tagCounts.GetValueOrDefault(tag, 0) + 1;
                        }
                    }
                }
            }

            var abilitiesComponent = componentStore.GetComponent<PassiveAbilitiesComponent>(gameState.PlayerEntityId);
            if (abilitiesComponent?.RelicIDs != null)
            {
                foreach (var relicId in abilitiesComponent.RelicIDs)
                {
                    if (BattleDataCache.Relics.TryGetValue(relicId, out var relicData) && relicData.Tags != null)
                    {
                        foreach (var tag in relicData.Tags)
                        {
                            tagCounts[tag] = tagCounts.GetValueOrDefault(tag, 0) + 1;
                        }
                    }
                }
            }

            return tagCounts;
        }

        private enum RarityLevel { Action = -1, Common, Uncommon, Rare, Epic, Mythic, Legendary }
    }
}