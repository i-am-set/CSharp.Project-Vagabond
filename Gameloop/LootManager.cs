using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.Utils; // For ServiceLocator

namespace ProjectVagabond.Systems
{
    public class LootManager
    {
        // Tunable: How many items drop by default
        public static int BaseCombatDropCount = 3;

        // Master list of all items categorized by Rarity (0-5)
        private Dictionary<int, List<BaseItem>> _lootTable;
        private Random _rng;

        // Rarity Weights (Sum should be 100)
        private readonly Dictionary<int, int> _rarityWeights = new Dictionary<int, int>
        {
            { 0, 50 }, // Common
            { 1, 30 }, // Uncommon
            { 2, 15 }, // Rare
            { 3, 4 },  // Epic
            { 4, 1 }   // Legendary
        };

        public LootManager()
        {
            _rng = new Random();
            _lootTable = new Dictionary<int, List<BaseItem>>();
            for (int i = 0; i <= 5; i++) _lootTable[i] = new List<BaseItem>();
        }

        /// <summary>
        /// Call this after BattleDataCache has loaded JSONs.
        /// </summary>
        public void BuildLootTables()
        {
            // Flatten all specific dictionaries into the master loot table
            foreach (var w in BattleDataCache.Weapons.Values)
                _lootTable[w.Rarity].Add(BaseItem.FromWeapon(w));

            foreach (var a in BattleDataCache.Armors.Values)
                _lootTable[a.Rarity].Add(BaseItem.FromArmor(a));

            foreach (var r in BattleDataCache.Relics.Values)
                _lootTable[r.Rarity].Add(BaseItem.FromRelic(r));
        }

        public List<BaseItem> GenerateCombatLoot()
        {
            return GenerateLoot(BaseCombatDropCount);
        }

        public List<BaseItem> GenerateLoot(int count)
        {
            List<BaseItem> loot = new List<BaseItem>();

            for (int i = 0; i < count; i++)
            {
                int rarity = RollRarity();

                // Fallback: If we rolled a rarity that has no items (e.g. no Legendaries defined yet),
                // step down until we find a populated pool.
                while (rarity >= 0 && _lootTable[rarity].Count == 0)
                {
                    rarity--;
                }

                if (rarity >= 0)
                {
                    var pool = _lootTable[rarity];
                    loot.Add(pool[_rng.Next(pool.Count)]);
                }
            }

            return loot;
        }

        private int RollRarity()
        {
            int roll = _rng.Next(1, 101);
            int cumulative = 0;

            foreach (var kvp in _rarityWeights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative) return kvp.Key;
            }
            return 0; // Default to common
        }
    }
}
