#nullable enable
using Microsoft.Xna.Framework;
using System;
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
        public int DrawPatternOffset { get; }

        private static int _nextId = 0;
        private static readonly Random _random = new Random();

        public SplitMapPath(int fromNodeId, int toNodeId)
        {
            Id = _nextId++;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            DrawPatternOffset = _random.Next(1000); // Assign a large random offset for visual variety
        }
        public static void ResetIdCounter() => _nextId = 0;
    }
}
#nullable restore