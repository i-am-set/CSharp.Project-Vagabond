using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using ProjectVagabond.Utils;

namespace ProjectVagabond.Systems
{
    public class LootManager
    {
        // Master list of all items available in the game
        private List<BaseItem> _masterItemPool;
        private Random _rng;

        public LootManager()
        {
            _rng = new Random();
            _masterItemPool = new List<BaseItem>();
        }

        /// <summary>
        /// Call this after BattleDataCache has loaded JSONs.
        /// Flattens all items into a single unweighted pool.
        /// </summary>
        public void BuildLootTables()
        {
            _masterItemPool.Clear();

            foreach (var w in BattleDataCache.Weapons.Values)
                _masterItemPool.Add(BaseItem.FromWeapon(w));

            foreach (var r in BattleDataCache.Relics.Values)
                _masterItemPool.Add(BaseItem.FromRelic(r));
        }

        /// <summary>
        /// Generates exactly 2 items: 1 Weapon, 1 Relic.
        /// Uses a "Deck of Cards" system: Items seen in this run are removed from the pool.
        /// </summary>
        public List<BaseItem> GenerateCombatLoot()
        {
            List<BaseItem> loot = new List<BaseItem>();

            // We track picked IDs locally for this specific drop to ensure we don't pick the same item twice 
            // in one batch (though the global SeenItemIds handles cross-run uniqueness).
            HashSet<string> currentBatchIds = new HashSet<string>();

            // 1. Generate Weapon
            var weapon = GetRandomItemByType(ItemType.Weapon, currentBatchIds);
            if (weapon != null)
            {
                loot.Add(weapon);
                currentBatchIds.Add(weapon.ID);
            }

            // 2. Generate Relic
            var relic = GetRandomItemByType(ItemType.Relic, currentBatchIds);
            if (relic != null)
            {
                loot.Add(relic);
                currentBatchIds.Add(relic.ID);
            }

            return loot;
        }

        private BaseItem GetRandomItemByType(ItemType type, HashSet<string> currentBatchIds)
        {
            var gameState = ServiceLocator.Get<GameState>();

            // Filter the master pool:
            // 1. Must match requested type
            // 2. Must NOT have been seen in this run (GameState.SeenItemIds)
            // 3. Must NOT have been picked in this specific batch (currentBatchIds)
            var candidates = _masterItemPool
                .Where(i => i.Type == type &&
                            !gameState.SeenItemIds.Contains(i.ID) &&
                            !currentBatchIds.Contains(i.ID))
                .ToList();

            if (candidates.Count == 0)
            {
                // Fallback: If we've seen literally every item of this type, 
                // we might return null (no drop) or allow duplicates.
                // For a "sprint" roguelike, running out of items is unlikely unless the pool is tiny.
                // We return null to indicate "pool exhausted".
                return null;
            }

            // Pick a random item from the remaining candidates (Unweighted)
            var selectedItem = candidates[_rng.Next(candidates.Count)];

            // Mark as seen globally so it doesn't appear again this run
            gameState.SeenItemIds.Add(selectedItem.ID);

            return selectedItem;
        }
    }
}