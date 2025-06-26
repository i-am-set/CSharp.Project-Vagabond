using Microsoft.Xna.Framework;
using ProjectVagabond;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public enum PathfindingMode
    {
        Time, // Prioritizes the path that takes the least amount of in-game time
        Moves // Prioritizes the path with the fewest number of tiles
    }

    public class PathfinderNode
    {
        public Vector2 Position { get; }
        public PathfinderNode Parent { get; set; }
        public float CostFromStartPoint { get; set; } // Cost from start (G)
        public float EstimatedCostToEndPoint { get; set; } // Heuristic cost to end (H)
        public float TotalEstimatedCost => CostFromStartPoint + EstimatedCostToEndPoint; // F = G + H

        public PathfinderNode(Vector2 position)
        {
            Position = position;
        }
    }

    public static class Pathfinder
    {
        public static List<Vector2> FindPath(Vector2 start, Vector2 end, GameState gameState, bool isRunning, PathfindingMode mode, MapView mapView)
        {
            var startNode = new PathfinderNode(start)
            {
                CostFromStartPoint = 0,
                EstimatedCostToEndPoint = GetDistance(start, end)
            };

            var openQueue = new PriorityQueue<PathfinderNode, float>();
            openQueue.Enqueue(startNode, startNode.TotalEstimatedCost);

            var gScores = new Dictionary<Vector2, float> { { start, 0f } };

            while (openQueue.Count > 0)
            {
                var currentNode = openQueue.Dequeue();

                if (currentNode.Position == end)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (var neighborPos in GetNeighbors(currentNode.Position))
                {
                    if (!gameState.IsPositionPassable(neighborPos, mapView))
                        continue;

                    Vector2 moveDir = neighborPos - currentNode.Position;
                    if (moveDir.X != 0 && moveDir.Y != 0)
                    {
                        if (!gameState.IsPositionPassable(new Vector2(currentNode.Position.X + moveDir.X, currentNode.Position.Y), mapView) ||
                            !gameState.IsPositionPassable(new Vector2(currentNode.Position.X, currentNode.Position.Y + moveDir.Y), mapView))
                        {
                            continue;
                        }
                    }

                    float moveCost;
                    if (mode == PathfindingMode.Moves)
                    {
                        moveCost = 1;
                    }
                    else
                    {
                        var actionType = isRunning ? ActionType.RunMove : ActionType.WalkMove;
                        string terrainType = (mapView == MapView.Local) ? "LOCAL" : gameState.GetMapDataAt((int)neighborPos.X, (int)neighborPos.Y).TerrainType;
                        moveCost = gameState.GetSecondsPassedDuringMovement(actionType, terrainType, moveDir, mapView == MapView.Local);
                    }

                    float tentative_gScore = currentNode.CostFromStartPoint + moveCost;

                    float known_gScore = gScores.GetValueOrDefault(neighborPos, float.PositiveInfinity);

                    if (tentative_gScore < known_gScore)
                    {
                        gScores[neighborPos] = tentative_gScore;

                        var neighborNode = new PathfinderNode(neighborPos)
                        {
                            Parent = currentNode,
                            CostFromStartPoint = tentative_gScore,
                            EstimatedCostToEndPoint = GetDistance(neighborPos, end)
                        };

                        openQueue.Enqueue(neighborNode, neighborNode.TotalEstimatedCost);
                    }
                }
            }

            return null;
        }

        private static List<Vector2> RetracePath(PathfinderNode startNode, PathfinderNode endNode)
        {
            var path = new List<Vector2>();
            var currentNode = endNode;
            while (currentNode != null && currentNode.Position != startNode.Position)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }
            path.Reverse();
            return path;
        }

        private static IEnumerable<Vector2> GetNeighbors(Vector2 pos)
        {
            // Cardinal
            yield return new Vector2(pos.X, pos.Y - 1); // Up
            yield return new Vector2(pos.X, pos.Y + 1); // Down
            yield return new Vector2(pos.X - 1, pos.Y); // Left
            yield return new Vector2(pos.X + 1, pos.Y); // Right
            // Diagonal
            yield return new Vector2(pos.X - 1, pos.Y - 1); // Up-Left
            yield return new Vector2(pos.X + 1, pos.Y - 1); // Up-Right
            yield return new Vector2(pos.X - 1, pos.Y + 1); // Down-Left
            yield return new Vector2(pos.X + 1, pos.Y + 1); // Down-Right
        }

        private static float GetDistance(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }
}