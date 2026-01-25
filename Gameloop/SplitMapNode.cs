#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;

using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Progression
{
    public enum SplitNodeType
    {
        Origin,
        Battle,
        Narrative,
        MajorBattle,
        Recruit,
        Rest,
        Shop
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
        public Vector2 Position { get; set; } // Changed to get; set; to allow post-generation centering
        public SplitNodeType NodeType { get; set; }
        public BattleDifficulty Difficulty { get; set; } = BattleDifficulty.Normal;
        public object? EventData { get; set; } // List<string> for battles, NarrativeEvent for narrative
        public List<int> IncomingPathIds { get; } = new List<int>();
        public List<int> OutgoingPathIds { get; } = new List<int>();
        public bool IsReachable { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public float AnimationOffset { get; }
        public Vector2 VisualOffset { get; set; } = Vector2.Zero;

        private static int _nextId = 0;
        private const int NODE_SIZE = 16;
        private static readonly Random _random = new Random();

        public SplitMapNode(int floor, Vector2 position)
        {
            Id = _nextId++;
            Floor = floor;
            Position = position;
            NodeType = SplitNodeType.Battle; // Default
            AnimationOffset = (float)_random.NextDouble() * 2f; // Random offset up to 2 seconds for staggered animations
        }

        public Rectangle GetBounds()
        {
            return new Rectangle((int)(Position.X - NODE_SIZE / 2), (int)(Position.Y - NODE_SIZE / 2), NODE_SIZE, NODE_SIZE);
        }

        public static void ResetIdCounter() => _nextId = 0;
    }
}