using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public enum PathfindingMode
    {
        Time, // Prioritizes the path that takes the least amount of in-game time
        Moves // Prioritizes the path with the fewest number of tiles
    }

    public static class Pathfinder
    {
        private static readonly Vector2[] _neighborOffsets = new Vector2[]
        {
            new Vector2(0, -1), // Up
            new Vector2(0, 1),  // Down
            new Vector2(-1, 0), // Left
            new Vector2(1, 0),  // Right
            new Vector2(-1, -1),// Up-Left
            new Vector2(1, -1), // Up-Right
            new Vector2(-1, 1), // Down-Left
            new Vector2(1, 1)   // Down-Right
        };

        public static List<Vector2> FindPath(int pathfindingEntityId, Vector2 start, Vector2 end, GameState gameState, MovementMode mode, PathfindingMode pathfindingMode)
        {
            var openQueue = new PriorityQueue<Vector2, float>();
            openQueue.Enqueue(start, 0);

            var cameFrom = new Dictionary<Vector2, Vector2>();
            var gScores = new Dictionary<Vector2, float> { { start, 0f } };

            while (openQueue.TryDequeue(out var currentPos, out _))
            {
                if (currentPos == end)
                {
                    return RetracePath(cameFrom, currentPos);
                }

                float current_gScore = gScores[currentPos];

                for (int i = 0; i < _neighborOffsets.Length; i++)
                {
                    var neighborPos = currentPos + _neighborOffsets[i];

                    if (!gameState.IsPositionPassable(neighborPos, MapView.World, pathfindingEntityId, end, out var mapData))
                        continue;

                    bool isDiagonal = i >= 4;
                    if (isDiagonal)
                    {
                        var neighbor1 = currentPos + new Vector2(_neighborOffsets[i].X, 0);
                        var neighbor2 = currentPos + new Vector2(0, _neighborOffsets[i].Y);
                        if (!gameState.IsPositionPassable(neighbor1, MapView.World, pathfindingEntityId, end, out _) || !gameState.IsPositionPassable(neighbor2, MapView.World, pathfindingEntityId, end, out _))
                        {
                            continue;
                        }
                    }

                    float moveCost;
                    if (pathfindingMode == PathfindingMode.Moves)
                    {
                        moveCost = isDiagonal ? 1.414f : 1f;
                    }
                    else
                    {
                        Vector2 moveDir = _neighborOffsets[i];
                        // We use player stats here because pathfinder doesn't know which entity is pathing.
                        // The cost is relative, so this is acceptable for finding the "fastest" path.
                        // The actual time cost is calculated later when truncating the path.
                        moveCost = gameState.GetSecondsPassedDuringMovement(gameState.PlayerStats, mode, mapData, moveDir);
                    }

                    float tentative_gScore = current_gScore + moveCost;
                    float known_gScore = gScores.GetValueOrDefault(neighborPos, float.PositiveInfinity);

                    if (tentative_gScore < known_gScore)
                    {
                        cameFrom[neighborPos] = currentPos;
                        gScores[neighborPos] = tentative_gScore;
                        float fScore = tentative_gScore + GetDistance(neighborPos, end);
                        openQueue.Enqueue(neighborPos, fScore);
                    }
                }
            }

            return null;
        }

        private static List<Vector2> RetracePath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
        {
            var path = new List<Vector2> { current };
            while (cameFrom.TryGetValue(current, out current))
            {
                path.Add(current);
            }

            path.RemoveAt(path.Count - 1);
            path.Reverse();
            return path;
        }

        // Using Octile distance for an 8 directional grid
        private static float GetDistance(Vector2 a, Vector2 b)
        {
            float dx = Math.Abs(a.X - b.X);
            float dy = Math.Abs(a.Y - b.Y);
            const float D = 1f; // Cost of cardinal move
            const float D2 = 1.414f; // Cost of diagonal move
            return D * (dx + dy) + (D2 - 2 * D) * Math.Min(dx, dy);
        }
    }
}