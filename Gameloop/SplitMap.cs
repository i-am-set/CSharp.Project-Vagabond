#nullable enable
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
        public int TargetFloorCount { get; }
        public int StartNodeId { get; }
        public float MapHeight { get; }

        public SplitMap(List<SplitMapNode> nodes, List<SplitMapPath> paths, int targetFloorCount, int startNodeId, float mapHeight)
        {
            Nodes = nodes.ToDictionary(n => n.Id, n => n);
            Paths = paths.ToDictionary(p => p.Id, p => p);
            TargetFloorCount = targetFloorCount;
            StartNodeId = startNodeId;
            MapHeight = mapHeight;
        }

        /// <summary>
        /// Removes all nodes on a given floor except for a specified one to keep,
        /// and cleans up all associated paths.
        /// </summary>
        /// <param name="floor">The floor index to prune.</param>
        /// <param name="keepNodeId">The ID of the single node to preserve on that floor.</param>
        public void PruneFloor(int floor, int keepNodeId)
        {
            var nodesToRemove = Nodes.Values.Where(n => n.Floor == floor && n.Id != keepNodeId).ToList();
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