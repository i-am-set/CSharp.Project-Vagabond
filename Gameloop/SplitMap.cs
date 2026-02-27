#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Progression
{
    /// <summary>
    /// A data structure representing a fully generated split map, including all nodes and their connections.
    /// </summary>
    public class SplitMap
    {
        public Dictionary<int, SplitMapNode> Nodes { get; }
        public Dictionary<int, SplitMapPath> Paths { get; }
        public RenderTarget2D BakedSceneryTexture { get; }
        public int TargetColumnCount { get; }
        public int StartNodeId { get; }
        public float MapWidth { get; }
        public SplitMap(List<SplitMapNode> nodes, List<SplitMapPath> paths, RenderTarget2D bakedScenery, int targetColumnCount, int startNodeId, float mapWidth)
        {
            Nodes = nodes.ToDictionary(n => n.Id, n => n);
            Paths = paths.ToDictionary(p => p.Id, p => p);
            BakedSceneryTexture = bakedScenery;
            TargetColumnCount = targetColumnCount;
            StartNodeId = startNodeId;
            MapWidth = mapWidth;
        }

        public void Dispose()
        {
            BakedSceneryTexture?.Dispose();
        }

        /// <summary>
        /// Marks all nodes on a given column (except the kept one) and their paths as abandoned.
        /// Recursively abandons downstream nodes that are no longer accessible.
        /// </summary>
        public void PruneColumn(int columnIndex, int keepNodeId)
        {
            // 1. Abandon unchosen nodes in the current column
            var nodesToAbandon = Nodes.Values.Where(n => n.Floor == columnIndex && n.Id != keepNodeId && !n.IsAbandoned).ToList();
            if (!nodesToAbandon.Any()) return;

            foreach (var node in nodesToAbandon)
            {
                node.IsAbandoned = true;

                foreach (var pathId in node.OutgoingPathIds)
                {
                    if (Paths.TryGetValue(pathId, out var path)) path.IsAbandoned = true;
                }

                foreach (var pathId in node.IncomingPathIds)
                {
                    if (Paths.TryGetValue(pathId, out var path)) path.IsAbandoned = true;
                }
            }

            // 2. Cascade abandonment forward to dead branches
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var node in Nodes.Values.Where(n => n.Floor > columnIndex && !n.IsAbandoned))
                {
                    // A node is completely cut off if ALL of its incoming paths are abandoned
                    bool allIncomingAbandoned = node.IncomingPathIds.Count > 0 &&
                        node.IncomingPathIds.All(pid => Paths.TryGetValue(pid, out var p) && p.IsAbandoned);

                    if (allIncomingAbandoned)
                    {
                        node.IsAbandoned = true;
                        changed = true; // Trigger another pass to catch nodes that relied on this one

                        foreach (var pathId in node.OutgoingPathIds)
                        {
                            if (Paths.TryGetValue(pathId, out var path)) path.IsAbandoned = true;
                        }
                    }
                }
            }
        }
    }
}