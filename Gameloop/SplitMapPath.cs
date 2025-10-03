#nullable enable
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond.Progression
{
    /// <summary>
    /// Represents a connection between two nodes on the split map.
    /// </summary>
    public class SplitMapPath
    {
        public int Id { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public List<Vector2> RenderPoints { get; set; } = new List<Vector2>();
        public List<Point> PixelPoints { get; set; } = new List<Point>();

        private static int _nextId = 0;

        public SplitMapPath(int fromNodeId, int toNodeId)
        {
            Id = _nextId++;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
        public static void ResetIdCounter() => _nextId = 0;
    }
}
#nullable restore