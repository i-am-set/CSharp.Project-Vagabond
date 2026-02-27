using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Progression
{
    public enum SplitNodeType
    {
        Origin,
        Battle,
        MajorBattle,
        Rest,
        Recruit
    }
    public enum BattleDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public class SplitMapNode
    {
        public int Id { get; }
        public int Floor { get; }
        public Vector2 Position { get; set; }
        public SplitNodeType NodeType { get; set; }
        public BattleDifficulty Difficulty { get; set; } = BattleDifficulty.Normal;
        public object? EventData { get; set; }
        public List<int> IncomingPathIds { get; } = new List<int>();
        public List<int> OutgoingPathIds { get; } = new List<int>();
        public bool IsReachable { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public bool IsAbandoned { get; set; } = false;
        public float VisualAlpha { get; set; } = 1.0f;
        public float AnimationOffset { get; }
        public Vector2 VisualOffset { get; set; } = Vector2.Zero;

        private static int _nextId = 0;
        private static readonly Random _random = new Random();

        public SplitMapNode(int floor, Vector2 position)
        {
            Id = _nextId++;
            Floor = floor;
            Position = position;
            NodeType = SplitNodeType.Battle;
            AnimationOffset = (float)_random.NextDouble() * 2f;
        }

        public Rectangle GetBounds()
        {
            // 16x16 hitbox centered on the node
            return new Rectangle((int)(Position.X - 8), (int)(Position.Y - 8), 16, 16);
        }

        public static void ResetIdCounter() => _nextId = 0;
    }
}