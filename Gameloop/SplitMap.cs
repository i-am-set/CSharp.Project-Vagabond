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
        public int TotalFloors { get; }
        public int StartNodeId { get; }
        public float MapHeight { get; }

        public SplitMap(List<SplitMapNode> nodes, List<SplitMapPath> paths, int totalFloors, int startNodeId, float mapHeight)
        {
            Nodes = nodes.ToDictionary(n => n.Id, n => n);
            Paths = paths.ToDictionary(p => p.Id, p => p);
            TotalFloors = totalFloors;
            StartNodeId = startNodeId;
            MapHeight = mapHeight;
        }
    }
}
#nullable restore