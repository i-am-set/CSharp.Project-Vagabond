using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Battle;
using ProjectVagabond.Items;

namespace ProjectVagabond.Systems
{
    public class LootManager
    {
        private List<BaseItem> _masterRelicPool;
        private Random _rng;

        public LootManager()
        {
            _rng = new Random();
            _masterRelicPool = new List<BaseItem>();
        }

        public void BuildLootTables()
        {
            _masterRelicPool.Clear();
            foreach (var r in BattleDataCache.Relics.Values)
                _masterRelicPool.Add(BaseItem.FromRelic(r));
        }

        public List<BaseItem> GenerateCombatLoot()
        {
            // Always generate 2 Relics
            List<BaseItem> loot = new List<BaseItem>();
            HashSet<string> currentBatchIds = new HashSet<string>();

            for (int i = 0; i < 2; i++)
            {
                var relic = GetRandomRelic(currentBatchIds);
                if (relic != null)
                {
                    loot.Add(relic);
                    currentBatchIds.Add(relic.ID);
                }
            }
            return loot;
        }

        private BaseItem GetRandomRelic(HashSet<string> currentBatchIds)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var candidates = _masterRelicPool
                .Where(i => !gameState.SeenItemIds.Contains(i.ID) && !currentBatchIds.Contains(i.ID))
                .ToList();

            if (candidates.Count == 0) return null;

            var selectedItem = candidates[_rng.Next(candidates.Count)];
            gameState.SeenItemIds.Add(selectedItem.ID);
            return selectedItem;
        }
    }
}