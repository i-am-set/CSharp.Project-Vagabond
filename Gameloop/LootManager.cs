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

        /// <summary>
        /// Generates exactly 3 items: 1 Weapon, 1 Armor, 1 Relic.
        /// </summary>
        public List<BaseItem> GenerateCombatLoot()
        {
            List<BaseItem> loot = new List<BaseItem>();
            HashSet<string> pickedIds = new HashSet<string>();

            // 1. Generate Weapon
            var weapon = GetRandomItemByType(ItemType.Weapon, pickedIds);
            if (weapon != null)
            {
                loot.Add(weapon);
                pickedIds.Add(weapon.ID);
            }

            // 2. Generate Armor
            var armor = GetRandomItemByType(ItemType.Armor, pickedIds);
            if (armor != null)
            {
                loot.Add(armor);
                pickedIds.Add(armor.ID);
            }

            // 3. Generate Relic
            var relic = GetRandomItemByType(ItemType.Relic, pickedIds);
            if (relic != null)
            {
                loot.Add(relic);
                pickedIds.Add(relic.ID);
            }

            return loot;
        }

        private BaseItem GetRandomItemByType(ItemType type, HashSet<string> excludeIds)
        {
            // 1. Roll for target rarity
            int targetRarity = RollRarity();

            // 2. Try to find an item of that type and rarity
            // If not found, step down in rarity until we find one (or hit Common)
            // If still not found, step up from target rarity.

            // Simplified Fallback: Check Target -> 0, then Target+1 -> 5

            // Downward search
            for (int r = targetRarity; r >= 0; r--)
            {
                var item = GetItemFromPool(r, type, excludeIds);
                if (item != null) return item;
            }

            // Upward search (if nothing found in lower tiers)
            for (int r = targetRarity + 1; r <= 5; r++)
            {
                var item = GetItemFromPool(r, type, excludeIds);
                if (item != null) return item;
            }

            return null;
        }

        private BaseItem GetItemFromPool(int rarity, ItemType type, HashSet<string> excludeIds)
        {
            if (!_lootTable.ContainsKey(rarity)) return null;

            var pool = _lootTable[rarity]
                .Where(i => i.Type == type && !excludeIds.Contains(i.ID))
                .ToList();

            if (pool.Count == 0) return null;

            return pool[_rng.Next(pool.Count)];
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