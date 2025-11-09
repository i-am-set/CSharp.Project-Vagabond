#nullable enable
using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Progression
{
    /// <summary>
    /// A static class responsible for procedurally generating a vertical branching map for a split.
    /// </summary>
    public static class SplitMapGenerator
    {
        private static readonly Random _random = new Random();

        // --- Generation Tuning ---
        private const int MIN_NODES_PER_COLUMN = 2;
        private const int MAX_NODES_PER_COLUMN = 3;
        private const int COLUMN_WIDTH = 64; // 2 * GRID_SIZE
        private const int HORIZONTAL_PADDING = 64; // 2 * GRID_SIZE
        private const int VERTICAL_PADDING = 32; // 1 * GRID_SIZE
        private const float BATTLE_EVENT_WEIGHT = 0.7f; // 70% chance for a battle
        private const float NARRATIVE_EVENT_WEIGHT = 0.3f; // 30% chance for a narrative
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float SECONDARY_PATH_CHANCE = 0.4f; // Chance for a node to have a second outgoing path
        private const float NODE_VERTICAL_VARIANCE_FACTOR = 1.0f; // Node can move within 100% of its "lane"
        private const float NODE_HORIZONTAL_VARIANCE_PIXELS = 15f;
        private const float MAX_CONNECTION_DISTANCE = 120f; // Max distance for a path to be considered "close"
        private const float PATH_SPLIT_POINT_MIN = 0.2f;
        private const float PATH_SPLIT_POINT_MAX = 0.8f;
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;
        private const float REWARD_NODE_CHANCE = 0.05f;
        private const float VERTICAL_SPREAD = 160; // Approx 5 * GRID_SIZE


        public static SplitMap? GenerateInitial(SplitData splitData)
        {
            SplitMapNode.ResetIdCounter();
            SplitMapPath.ResetIdCounter();

            int totalColumns = _random.Next(splitData.SplitLengthMin, splitData.SplitLengthMax + 1);
            float mapWidth = (totalColumns - 1) * COLUMN_WIDTH + HORIZONTAL_PADDING * 2;

            var allNodesByColumn = new List<List<SplitMapNode>>();
            var allPaths = new List<SplitMapPath>();

            // --- Column 0: Start Node ---
            var startNodePosition = new Vector2(HORIZONTAL_PADDING, Global.VIRTUAL_HEIGHT / 2f);
            var startNodes = PlaceNodesForColumn(0, totalColumns, mapWidth, 0, startNodePosition);
            startNodes.First().NodeType = SplitNodeType.Origin;
            allNodesByColumn.Add(startNodes);

            Vector2 previousColumnAvgPos = startNodePosition;
            int previousColumnCount = 1;

            // --- Intermediate Columns ---
            for (int i = 1; i < totalColumns - 1; i++)
            {
                var newNodes = PlaceNodesForColumn(i, totalColumns, mapWidth, previousColumnCount, previousColumnAvgPos);
                allNodesByColumn.Add(newNodes);
                AssignEvents(newNodes, splitData, totalColumns);

                var newPaths = ConnectNodes(allNodesByColumn[i - 1], newNodes);
                allPaths.AddRange(newPaths);

                previousColumnCount = newNodes.Count;
                if (newNodes.Any())
                {
                    previousColumnAvgPos = new Vector2(
                        newNodes.Average(n => n.Position.X),
                        newNodes.Average(n => n.Position.Y)
                    );
                }
            }

            // --- Final Column: Boss Node ---
            var lastRegularColumn = allNodesByColumn.Last();
            if (lastRegularColumn.Any())
            {
                previousColumnAvgPos = new Vector2(
                    lastRegularColumn.Average(n => n.Position.X),
                    lastRegularColumn.Average(n => n.Position.Y)
                );
            }
            var endNodes = PlaceNodesForColumn(totalColumns - 1, totalColumns, mapWidth, 0, previousColumnAvgPos);
            var endNode = endNodes.First();
            endNode.NodeType = SplitNodeType.MajorBattle;
            if (splitData.PossibleMajorBattles.Any())
            {
                endNode.EventData = splitData.PossibleMajorBattles[_random.Next(splitData.PossibleMajorBattles.Count)];
            }
            allNodesByColumn.Add(endNodes);
            var finalPaths = ConnectNodes(lastRegularColumn, endNodes);
            allPaths.AddRange(finalPaths);

            // --- Final Assembly ---
            var allNodes = allNodesByColumn.SelectMany(c => c).ToList();
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;

            if (startNodeId == -1) return null;

            GeneratePathRenderPoints(allPaths, allNodes);

            return new SplitMap(allNodes, allPaths, totalColumns, startNodeId, mapWidth);
        }

        public static void GenerateNextFloor(SplitMap map, SplitData splitData, int parentNodeId)
        {
            if (map == null || !map.Nodes.TryGetValue(parentNodeId, out var parentNode)) return;

            int newColumnIndex = parentNode.Floor + 1;

            if (newColumnIndex >= map.TargetColumnCount) return; // Don't generate past the target

            // 1. Place new nodes relative to the parent
            int previousColumnNodeCount = map.Nodes.Values.Count(n => n.Floor == parentNode.Floor);
            var newNodes = PlaceNodesForColumn(newColumnIndex, map.TargetColumnCount, map.MapWidth, previousColumnNodeCount, parentNode.Position);

            // Add new nodes to the map
            foreach (var node in newNodes)
            {
                map.Nodes.Add(node.Id, node);
            }

            // 2. Connect parent node to all new child nodes
            var newPaths = new List<SplitMapPath>();
            foreach (var childNode in newNodes)
            {
                var path = new SplitMapPath(parentNode.Id, childNode.Id);
                newPaths.Add(path);
                map.Paths.Add(path.Id, path);
                parentNode.OutgoingPathIds.Add(path.Id);
                childNode.IncomingPathIds.Add(path.Id);
            }

            // 3. Assign events to new nodes
            AssignEvents(newNodes, splitData, map.TargetColumnCount);

            // 4. Generate render points for new paths
            var allNodesForRender = map.Nodes.Values.ToList();
            GeneratePathRenderPoints(newPaths, allNodesForRender);
        }

        private static List<SplitMapNode> PlaceNodesForColumn(int columnIndex, int totalColumns, float mapWidth, int previousColumnNodeCount, Vector2 parentPosition)
        {
            var newNodes = new List<SplitMapNode>();
            const int nodeRadius = 8;
            int numNodes;

            if (columnIndex == 0 || columnIndex == totalColumns - 1)
            {
                numNodes = 1;
            }
            else
            {
                do
                {
                    numNodes = _random.Next(MIN_NODES_PER_COLUMN, MAX_NODES_PER_COLUMN + 1); // Generates 2 or 3 nodes.
                } while (numNodes == previousColumnNodeCount);
            }

            float x = HORIZONTAL_PADDING + (columnIndex * COLUMN_WIDTH);
            float laneHeight = (numNodes > 0) ? VERTICAL_SPREAD / numNodes : 0;
            float laneStartY = parentPosition.Y - VERTICAL_SPREAD / 2f;

            for (int i = 0; i < numNodes; i++)
            {
                float finalY;
                float finalX = x + ((float)_random.NextDouble() * 2f - 1f) * NODE_HORIZONTAL_VARIANCE_PIXELS;

                if (numNodes == 1)
                {
                    finalY = parentPosition.Y;
                }
                else
                {
                    float laneCenterY = laneStartY + (i * laneHeight) + (laneHeight / 2f);
                    float maxOffset = (laneHeight / 2f - nodeRadius) * NODE_VERTICAL_VARIANCE_FACTOR;
                    float randomYOffset = ((float)_random.NextDouble() * 2f - 1f) * maxOffset;
                    finalY = laneCenterY + randomYOffset;
                }

                // Snap the final calculated position to the grid.
                const float gridSize = Global.SPLIT_MAP_GRID_SIZE;
                float snappedX = MathF.Round(finalX / gridSize) * gridSize;
                float snappedY = MathF.Round(finalY / gridSize) * gridSize;

                newNodes.Add(new SplitMapNode(columnIndex, new Vector2(snappedX, snappedY)));
            }
            return newNodes;
        }

        private static List<SplitMapPath> ConnectNodes(List<SplitMapNode> previousColumn, List<SplitMapNode> nextColumn)
        {
            var paths = new List<SplitMapPath>();
            var sortedPrev = previousColumn.OrderBy(n => n.Position.Y).ToList();
            var sortedNext = nextColumn.OrderBy(n => n.Position.Y).ToList();

            // Pass 1: Ensure every node in the previous column connects to at least one in the next.
            foreach (var prevNode in sortedPrev)
            {
                var weights = CalculateWeights(prevNode, sortedNext, sortedPrev, sortedNext);
                if (!weights.Any()) continue;

                var targetNode = WeightedRandomSelect(weights);

                var path = new SplitMapPath(prevNode.Id, targetNode.Id);
                paths.Add(path);
                prevNode.OutgoingPathIds.Add(path.Id);
                targetNode.IncomingPathIds.Add(path.Id);
            }

            // Pass 2: Ensure every node in the next column is reachable.
            foreach (var nextNode in sortedNext)
            {
                if (!nextNode.IncomingPathIds.Any())
                {
                    // This node is unconnected. Find the best node from the previous column to connect to it.
                    var weights = CalculateWeights(nextNode, sortedPrev, sortedNext, sortedPrev); // Note the reversed sorted lists
                    if (!weights.Any()) continue;

                    var sourceNode = WeightedRandomSelect(weights);
                    var path = new SplitMapPath(sourceNode.Id, nextNode.Id);
                    paths.Add(path);
                    sourceNode.OutgoingPathIds.Add(path.Id);
                    nextNode.IncomingPathIds.Add(path.Id);
                }
            }

            // Pass 3: Add optional secondary paths for more branching.
            foreach (var prevNode in sortedPrev)
            {
                if (_random.NextDouble() < SECONDARY_PATH_CHANCE)
                {
                    var connectedNextNodeIds = prevNode.OutgoingPathIds.Select(pId => paths.First(p => p.Id == pId).ToNodeId).ToHashSet();
                    var availableTargets = sortedNext.Where(n => !connectedNextNodeIds.Contains(n.Id)).ToList();

                    if (availableTargets.Any())
                    {
                        var weights = CalculateWeights(prevNode, availableTargets, sortedPrev, sortedNext);
                        if (weights.Any())
                        {
                            var targetNode = WeightedRandomSelect(weights);
                            var path = new SplitMapPath(prevNode.Id, targetNode.Id);
                            paths.Add(path);
                            prevNode.OutgoingPathIds.Add(path.Id);
                            targetNode.IncomingPathIds.Add(path.Id);
                        }
                    }
                }
            }
            return paths;
        }

        private static Dictionary<SplitMapNode, float> CalculateWeights(SplitMapNode sourceNode, List<SplitMapNode> potentialTargets, List<SplitMapNode> sortedSourceColumn, List<SplitMapNode> sortedTargetColumn)
        {
            var weights = new Dictionary<SplitMapNode, float>();
            if (!potentialTargets.Any()) return weights;

            int sourceNodeIndex = sortedSourceColumn.IndexOf(sourceNode);
            if (sourceNodeIndex == -1) return weights; // Should not happen

            foreach (var targetNode in potentialTargets)
            {
                int targetNodeIndex = sortedTargetColumn.IndexOf(targetNode);
                if (targetNodeIndex == -1) continue;

                float distance = Vector2.Distance(sourceNode.Position, targetNode.Position);
                // Make distance less of a factor, and order more important
                float distanceWeight = 1.0f / (1.0f + (distance * 0.01f));

                // Heavily penalize connections that cross over in terms of vertical order
                float orderDifference = Math.Abs(sourceNodeIndex - targetNodeIndex);
                float orderPenaltyFactor = 3.0f; // Increased penalty
                float orderWeight = 1.0f / (1.0f + (float)Math.Pow(orderPenaltyFactor * orderDifference, 2));

                weights[targetNode] = distanceWeight * orderWeight;
            }
            return weights;
        }

        private static SplitMapNode WeightedRandomSelect(Dictionary<SplitMapNode, float> weights)
        {
            float totalWeight = weights.Values.Sum();
            double randomValue = _random.NextDouble() * totalWeight;

            foreach (var (node, weight) in weights)
            {
                if (randomValue < weight)
                {
                    return node;
                }
                randomValue -= weight;
            }
            return weights.Keys.Last(); // Fallback
        }

        private static void GeneratePathRenderPoints(List<SplitMapPath> paths, List<SplitMapNode> allNodes)
        {
            var pathsByStartNode = paths.ToLookup(p => p.FromNodeId);

            foreach (var group in pathsByStartNode)
            {
                var fromNode = allNodes.FirstOrDefault(n => n.Id == group.Key);
                if (fromNode == null) continue;

                var outgoingPaths = group.ToList();

                if (outgoingPaths.Count == 1)
                {
                    var path = outgoingPaths.First();
                    var toNode = allNodes.FirstOrDefault(n => n.Id == path.ToNodeId);
                    if (toNode != null)
                    {
                        path.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, toNode.Position, new List<int> { fromNode.Id, toNode.Id }, allNodes);
                    }
                }
                else if (outgoingPaths.Count > 1)
                {
                    // 1. Identify the main path (closest vertically)
                    SplitMapPath? mainPath = outgoingPaths
                        .Select(p => new { Path = p, ToNode = allNodes.FirstOrDefault(n => n.Id == p.ToNodeId) })
                        .Where(x => x.ToNode != null)
                        .OrderBy(x => Math.Abs(x.ToNode.Position.Y - fromNode.Position.Y))
                        .FirstOrDefault()?.Path;

                    if (mainPath == null) // Fallback: treat as single paths
                    {
                        foreach (var path in outgoingPaths)
                        {
                            var toNode = allNodes.FirstOrDefault(n => n.Id == path.ToNodeId);
                            if (toNode != null)
                            {
                                path.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, toNode.Position, new List<int> { fromNode.Id, toNode.Id }, allNodes);
                            }
                        }
                        continue; // Skip to next group
                    }

                    // 2. Generate the main path's points first
                    var mainPathToNode = allNodes.First(n => n.Id == mainPath.ToNodeId);
                    mainPath.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, mainPathToNode.Position, new List<int> { fromNode.Id, mainPathToNode.Id }, allNodes);

                    // 3. Generate branch paths splitting from the main path
                    var branchPaths = outgoingPaths.Where(p => p != mainPath).ToList();
                    foreach (var branchPath in branchPaths)
                    {
                        var branchToNode = allNodes.FirstOrDefault(n => n.Id == branchPath.ToNodeId);
                        if (branchToNode == null) continue;

                        // a. Choose a random split point on the main path
                        if (mainPath.RenderPoints.Count < 5) // Not enough points to split from, just generate a direct path
                        {
                            branchPath.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, branchToNode.Position, new List<int> { fromNode.Id, branchToNode.Id }, allNodes);
                            continue;
                        }

                        // b. Calculate a random split index
                        int minIndex = (int)(mainPath.RenderPoints.Count * PATH_SPLIT_POINT_MIN);
                        int maxIndex = (int)(mainPath.RenderPoints.Count * PATH_SPLIT_POINT_MAX);
                        if (minIndex >= maxIndex) // Ensure there's a valid range
                        {
                            minIndex = 1;
                            maxIndex = mainPath.RenderPoints.Count - 2;
                        }
                        int splitIndex = _random.Next(minIndex, maxIndex);
                        Vector2 splitPoint = mainPath.RenderPoints[splitIndex];

                        // c. Get the trunk portion
                        var trunkPoints = mainPath.RenderPoints.Take(splitIndex + 1).ToList();

                        // d. Generate the branch portion
                        var branchIgnoreIds = new List<int> { fromNode.Id, branchToNode.Id };
                        var branchPoints = GenerateWigglyPathPoints(splitPoint, branchToNode.Position, branchIgnoreIds, allNodes);

                        // e. Combine them
                        branchPath.RenderPoints = trunkPoints.Concat(branchPoints.Skip(1)).ToList();
                    }
                }
            }

            // Pre-calculate the rasterized pixel points for each path for efficient animation.
            foreach (var path in paths)
            {
                path.PixelPoints.Clear();
                if (path.RenderPoints.Count < 2) continue;
                for (int i = 0; i < path.RenderPoints.Count - 1; i++)
                {
                    var segmentPoints = SpriteBatchExtensions.GetBresenhamLinePoints(path.RenderPoints[i], path.RenderPoints[i + 1]);
                    if (i == 0)
                    {
                        path.PixelPoints.AddRange(segmentPoints);
                    }
                    else if (segmentPoints.Count > 1)
                    {
                        // Skip the first point of subsequent segments to avoid duplicates.
                        path.PixelPoints.AddRange(segmentPoints.Skip(1));
                    }
                }
            }
        }


        private static List<Vector2> GenerateWigglyPathPoints(Vector2 start, Vector2 end, List<int> ignoreNodeIds, List<SplitMapNode> allNodes)
        {
            var points = new List<Vector2> { start };
            var mainVector = end - start;
            var totalDistance = mainVector.Length();

            if (totalDistance < PATH_SEGMENT_LENGTH)
            {
                points.Add(end);
                return points;
            }

            var direction = Vector2.Normalize(mainVector);
            var perpendicular = new Vector2(-direction.Y, direction.X);
            int numSegments = (int)(totalDistance / PATH_SEGMENT_LENGTH);

            for (int i = 1; i < numSegments; i++)
            {
                float progress = (float)i / numSegments;
                var pointOnLine = start + direction * progress * totalDistance;

                float randomOffset = ((float)_random.NextDouble() * 2f - 1f) * PATH_MAX_OFFSET;
                float taper = MathF.Sin(progress * MathF.PI); // Tapering factor (0 at start/end, 1 in middle)

                Vector2 totalRepulsion = Vector2.Zero;
                foreach (var otherNode in allNodes)
                {
                    if (ignoreNodeIds.Contains(otherNode.Id))
                    {
                        continue;
                    }

                    float distanceToNode = Vector2.Distance(pointOnLine, otherNode.Position);

                    if (distanceToNode < NODE_REPULSION_RADIUS)
                    {
                        Vector2 repulsionVector = pointOnLine - otherNode.Position;
                        if (repulsionVector.LengthSquared() > 0)
                        {
                            repulsionVector.Normalize();
                            float falloff = 1.0f - (distanceToNode / NODE_REPULSION_RADIUS);
                            float strength = NODE_REPULSION_STRENGTH * Easing.EaseOutQuad(falloff);
                            totalRepulsion += repulsionVector * strength;
                        }
                    }
                }

                var finalPoint = pointOnLine + perpendicular * randomOffset * taper + totalRepulsion;
                points.Add(finalPoint);
            }

            points.Add(end);
            return points;
        }

        private static void AssignEvents(List<SplitMapNode> nodesToAssign, SplitData splitData, int totalColumns)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            var baseChoices = new List<(SplitNodeType type, float weight)>();
            if (splitData.PossibleBattles != null && splitData.PossibleBattles.Any())
            {
                baseChoices.Add((SplitNodeType.Battle, BATTLE_EVENT_WEIGHT * (1.0f - REWARD_NODE_CHANCE)));
                baseChoices.Add((SplitNodeType.Reward, BATTLE_EVENT_WEIGHT * REWARD_NODE_CHANCE));
            }
            if (splitData.PossibleNarrativeEventIDs != null && splitData.PossibleNarrativeEventIDs.Any())
            {
                baseChoices.Add((SplitNodeType.Narrative, NARRATIVE_EVENT_WEIGHT));
            }

            if (!baseChoices.Any())
            {
                if (splitData.PossibleBattles != null && splitData.PossibleBattles.Any())
                {
                    baseChoices.Add((SplitNodeType.Battle, 1.0f));
                }
                else
                {
                    return;
                }
            }

            for (int i = 0; i < nodesToAssign.Count; i++)
            {
                var node = nodesToAssign[i];
                if (node.Floor == totalColumns - 1)
                {
                    node.NodeType = SplitNodeType.MajorBattle;
                    if (splitData.PossibleMajorBattles.Any())
                    {
                        node.EventData = splitData.PossibleMajorBattles[_random.Next(splitData.PossibleMajorBattles.Count)];
                    }
                    continue;
                }

                SplitNodeType? previousNodeType = (i > 0) ? nodesToAssign[i - 1].NodeType : (SplitNodeType?)null;
                var availableChoices = baseChoices;
                if (previousNodeType.HasValue)
                {
                    availableChoices = baseChoices.Where(c => c.type != previousNodeType.Value).ToList();
                    if (!availableChoices.Any())
                    {
                        availableChoices = baseChoices;
                    }
                }

                float totalWeight = availableChoices.Sum(c => c.weight);
                double randomRoll = _random.NextDouble() * totalWeight;
                SplitNodeType chosenType = availableChoices.Last().type;

                foreach (var choice in availableChoices)
                {
                    if (randomRoll < choice.weight)
                    {
                        chosenType = choice.type;
                        break;
                    }
                    randomRoll -= choice.weight;
                }

                node.NodeType = chosenType;
                switch (chosenType)
                {
                    case SplitNodeType.Battle:
                        node.Difficulty = (BattleDifficulty)_random.Next(3);
                        node.EventData = progressionManager.GetRandomBattle(node.Difficulty);
                        break;
                    case SplitNodeType.Narrative:
                        node.EventData = progressionManager.GetRandomNarrative()?.EventID;
                        break;
                    case SplitNodeType.Reward:
                        node.EventData = null;
                        break;
                }
            }
        }
    }
}
#nullable restore