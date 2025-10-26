#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public float Length { get; private set; }

        private static int _nextId = 0;

        public SplitMapPath(int fromNodeId, int toNodeId)
        {
            Id = _nextId++;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
        public static void ResetIdCounter() => _nextId = 0;

        public void CalculateLength()
        {
            Length = 0f;
            if (RenderPoints.Count < 2) return;
            for (int i = 0; i < RenderPoints.Count - 1; i++)
            {
                Length += Vector2.Distance(RenderPoints[i], RenderPoints[i + 1]);
            }
        }
    }
}
#nullable restore