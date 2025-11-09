#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// Removes all nodes on a given column except for a specified one to keep,
        /// and cleans up all associated paths.
        /// </summary>
        /// <param name="columnIndex">The column index to prune.</param>
        /// <param name="keepNodeId">The ID of the single node to preserve on that column.</param>
        public void PruneColumn(int columnIndex, int keepNodeId)
        {
            var nodesToRemove = Nodes.Values.Where(n => n.Floor == columnIndex && n.Id != keepNodeId).ToList();
            if (!nodesToRemove.Any()) return;

            var pathsToRemove = new HashSet<int>();

            // Collect all paths connected to the nodes being removed
            foreach (var node in nodesToRemove)
            {
                foreach (var pathId in node.IncomingPathIds) pathsToRemove.Add(pathId);
                foreach (var pathId in node.OutgoingPathIds) pathsToRemove.Add(pathId);
            }

            // Remove references to these paths from their connected nodes and then remove the paths themselves
            foreach (var pathId in pathsToRemove)
            {
                if (Paths.TryGetValue(pathId, out var path))
                {
                    if (Nodes.TryGetValue(path.FromNodeId, out var fromNode))
                    {
                        fromNode.OutgoingPathIds.Remove(pathId);
                    }
                    if (Nodes.TryGetValue(path.ToNodeId, out var toNode))
                    {
                        toNode.IncomingPathIds.Remove(pathId);
                    }
                    Paths.Remove(pathId);
                }
            }

            // Finally, remove the nodes themselves
            foreach (var node in nodesToRemove)
            {
                Nodes.Remove(node.Id);
            }
        }
    }
}
#nullable restore
