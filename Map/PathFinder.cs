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
        public static List<Vector2> FindPath(Vector2 start, Vector2 end, GameState gameState, bool isRunning, PathfindingMode mode)
        {
            var startNode = new PathfinderNode(start);
            var endNode = new PathfinderNode(end);

            var openList = new List<PathfinderNode> { startNode };
            var closedList = new HashSet<Vector2>();

            while (openList.Count > 0)
            {
                var currentNode = openList.OrderBy(n => n.TotalEstimatedCost).First();
                openList.Remove(currentNode);
                closedList.Add(currentNode.Position);

                if (currentNode.Position == endNode.Position)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (var neighborPos in GetNeighbors(currentNode.Position))
                {
                    if (!gameState.IsPositionPassable(neighborPos) || closedList.Contains(neighborPos))
                        continue;

                    // Add corner-cutting check for diagonal moves
                    Vector2 moveDir = neighborPos - currentNode.Position;
                    if (moveDir.X != 0 && moveDir.Y != 0) // It's a diagonal move
                    {
                        // Check adjacent cardinal tiles to prevent moving through corners of walls
                        if (!gameState.IsPositionPassable(new Vector2(currentNode.Position.X + moveDir.X, currentNode.Position.Y)) ||
                            !gameState.IsPositionPassable(new Vector2(currentNode.Position.X, currentNode.Position.Y + moveDir.Y)))
                        {
                            continue; // Can't cut the corner
                        }
                    }

                    float moveCost;
                    if (mode == PathfindingMode.Moves)
                    {
                        moveCost = 1;
                    }
                    else
                    {
                        var mapData = gameState.GetMapDataAt((int)neighborPos.X, (int)neighborPos.Y);
                        Vector2 moveDirection = neighborPos - currentNode.Position;
                        var actionType = isRunning ? ActionType.RunMove : ActionType.WalkMove;
                        moveCost = gameState.GetSecondsPassedDuringMovement(actionType, mapData.TerrainType, moveDirection);
                    }

                    float newGCost = currentNode.CostFromStartPoint + moveCost;

                    var neighborNode = openList.FirstOrDefault(n => n.Position == neighborPos);
                    if (neighborNode == null || newGCost < neighborNode.CostFromStartPoint)
                    {
                        if (neighborNode == null)
                        {
                            neighborNode = new PathfinderNode(neighborPos);
                            neighborNode.EstimatedCostToEndPoint = GetDistance(neighborPos, endNode.Position);
                            openList.Add(neighborNode);
                        }
                        neighborNode.CostFromStartPoint = newGCost;
                        neighborNode.Parent = currentNode;
                    }
                }
            }
            return null;
        }

        private static List<Vector2> RetracePath(PathfinderNode startNode, PathfinderNode endNode)
        {
            var path = new List<Vector2>();
            var currentNode = endNode;
            while (currentNode != startNode)
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

        // Using Manhattan distance as the heuristic
        private static float GetDistance(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }
}