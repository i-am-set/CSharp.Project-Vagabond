#nullable enable
using Microsoft.Xna.Framework;
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
        public const int MAP_WIDTH = 280;
        private const int FLOOR_HEIGHT = 80;
        private const int HORIZONTAL_PADDING = 8; // Minimal padding to keep nodes off the absolute edge
        private const int VERTICAL_PADDING = 50;
        private const float BATTLE_EVENT_WEIGHT = 0.7f; // 70% chance for a battle
        private const float NARRATIVE_EVENT_WEIGHT = 0.3f; // 30% chance for a narrative
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float SECONDARY_PATH_CHANCE = 0.4f; // Chance for a node to have a second outgoing path
        private const float NODE_HORIZONTAL_VARIANCE_FACTOR = 1.0f; // Node can move within 100% of its "lane"
        private const float NODE_VERTICAL_VARIANCE_PIXELS = 36f;
        private const float MAX_CONNECTION_DISTANCE = 120f; // Max distance for a path to be considered "close"
        private const float MERGE_CHANCE = 0.5f;
        private const float MERGE_DISTANCE_THRESHOLD = 100f;
        private const float PATH_SPLIT_POINT_MIN = 0.2f;
        private const float PATH_SPLIT_POINT_MAX = 0.8f;


        public static SplitMap? Generate(SplitData splitData)
        {
            SplitMapNode.ResetIdCounter();
            SplitMapPath.ResetIdCounter();

            int totalFloors = _random.Next(splitData.SplitLengthMin, splitData.SplitLengthMax + 1);
            float mapHeight = (totalFloors - 1) * FLOOR_HEIGHT + VERTICAL_PADDING * 2;

            var nodesByFloor = PlaceNodes(totalFloors, mapHeight);
            if (nodesByFloor == null) return null;

            var paths = ConnectNodes(nodesByFloor);
            AssignEvents(nodesByFloor, splitData, totalFloors);

            var allNodes = nodesByFloor.SelectMany(floor => floor).ToList();
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;

            if (startNodeId == -1) return null;

            // Generate render points and pixel points for each path
            GeneratePathRenderPoints(paths, allNodes);


            return new SplitMap(allNodes, paths, totalFloors, startNodeId, mapHeight);
        }

        private static List<SplitMapNode>[] PlaceNodes(int totalFloors, float mapHeight)
        {
            var nodesByFloor = new List<SplitMapNode>[totalFloors];
            int previousFloorNodeCount = 0;
            float mapHorizontalOffset = (Global.VIRTUAL_WIDTH - MAP_WIDTH) / 2f;
            const int nodeRadius = 8; // Half of the node's visual size (16x16)

            for (int floor = 0; floor < totalFloors; floor++)
            {
                nodesByFloor[floor] = new List<SplitMapNode>();
                int numNodes;

                if (floor == 0 || floor == totalFloors - 1)
                {
                    numNodes = 1; // Start and Boss floors always have one node.
                }
                else if (floor >= 2) // Floors past the second one (0-indexed) must have at least 2 nodes.
                {
                    do
                    {
                        numNodes = _random.Next(2, 5); // 2, 3, or 4 nodes.
                    } while (numNodes == previousFloorNodeCount && totalFloors > 3);
                }
                else // This only leaves floor 1 (the second floor).
                {
                    do
                    {
                        numNodes = _random.Next(1, 5); // 1, 2, 3, or 4 nodes.
                    } while (numNodes == previousFloorNodeCount && totalFloors > 3);
                }

                float y = mapHeight - VERTICAL_PADDING - (floor * FLOOR_HEIGHT);

                float availableWidth = MAP_WIDTH - (HORIZONTAL_PADDING * 2);
                float laneWidth = (numNodes > 0) ? availableWidth / numNodes : 0;

                for (int i = 0; i < numNodes; i++)
                {
                    float finalX;
                    float finalY = y + ((float)_random.NextDouble() * 2f - 1f) * NODE_VERTICAL_VARIANCE_PIXELS;

                    if (numNodes == 1)
                    {
                        finalX = (MAP_WIDTH / 2f) + mapHorizontalOffset;
                    }
                    else
                    {
                        // Calculate the center of the node's "lane"
                        float laneCenterX = HORIZONTAL_PADDING + (i * laneWidth) + (laneWidth / 2f);

                        // The max offset is half the lane width, minus the node radius to prevent overlap.
                        float maxOffset = (laneWidth / 2f - nodeRadius) * NODE_HORIZONTAL_VARIANCE_FACTOR;
                        float randomXOffset = ((float)_random.NextDouble() * 2f - 1f) * maxOffset;

                        finalX = laneCenterX + randomXOffset + mapHorizontalOffset;
                    }

                    nodesByFloor[floor].Add(new SplitMapNode(floor, new Vector2(finalX, finalY)));
                }
                previousFloorNodeCount = numNodes;
            }
            return nodesByFloor;
        }

        private static List<SplitMapPath> ConnectNodes(List<SplitMapNode>[] nodesByFloor)
        {
            var paths = new List<SplitMapPath>();

            for (int i = 0; i < nodesByFloor.Length - 1; i++)
            {
                var lowerFloor = nodesByFloor[i];
                var upperFloor = nodesByFloor[i + 1];

                // Pass 1: Ensure every lower node connects up to at least one upper node.
                foreach (var lowerNode in lowerFloor)
                {
                    var closeNodes = upperFloor.Where(n => Vector2.Distance(n.Position, lowerNode.Position) <= MAX_CONNECTION_DISTANCE).ToList();

                    SplitMapNode targetNode;
                    if (closeNodes.Any())
                    {
                        var weights = CalculateWeights(lowerNode, closeNodes);
                        targetNode = WeightedRandomSelect(weights);
                    }
                    else
                    {
                        // Fallback: if no nodes are "close", connect to the absolute closest one.
                        targetNode = upperFloor.OrderBy(n => Vector2.DistanceSquared(n.Position, lowerNode.Position)).First();
                    }

                    var path = new SplitMapPath(lowerNode.Id, targetNode.Id);
                    paths.Add(path);
                    lowerNode.OutgoingPathIds.Add(path.Id);
                    targetNode.IncomingPathIds.Add(path.Id);
                }

                // Pass 2: Ensure every upper node is reachable.
                foreach (var upperNode in upperFloor)
                {
                    if (!upperNode.IncomingPathIds.Any())
                    {
                        // Find the horizontally closest lower node and force a connection.
                        var closestLower = lowerFloor.OrderBy(n => Vector2.DistanceSquared(n.Position, upperNode.Position)).First();
                        var path = new SplitMapPath(closestLower.Id, upperNode.Id);
                        paths.Add(path);
                        closestLower.OutgoingPathIds.Add(path.Id);
                        upperNode.IncomingPathIds.Add(path.Id);
                    }
                }

                // Pass 3: Add optional secondary paths for more branching.
                foreach (var lowerNode in lowerFloor)
                {
                    if (_random.NextDouble() < SECONDARY_PATH_CHANCE)
                    {
                        var connectedUpperNodeIds = lowerNode.OutgoingPathIds.Select(pId => paths.First(p => p.Id == pId).ToNodeId).ToHashSet();
                        var availableTargets = upperFloor
                            .Where(n => !connectedUpperNodeIds.Contains(n.Id) && Vector2.Distance(n.Position, lowerNode.Position) <= MAX_CONNECTION_DISTANCE)
                            .ToList();

                        if (availableTargets.Any())
                        {
                            var weights = CalculateWeights(lowerNode, availableTargets);
                            if (weights.Any())
                            {
                                var targetNode = WeightedRandomSelect(weights);
                                var path = new SplitMapPath(lowerNode.Id, targetNode.Id);
                                paths.Add(path);
                                lowerNode.OutgoingPathIds.Add(path.Id);
                                targetNode.IncomingPathIds.Add(path.Id);
                            }
                        }
                    }
                }
            }
            return paths;
        }

        private static Dictionary<SplitMapNode, float> CalculateWeights(SplitMapNode lowerNode, List<SplitMapNode> potentialUpperNodes)
        {
            var weights = new Dictionary<SplitMapNode, float>();
            if (!potentialUpperNodes.Any()) return weights;

            foreach (var upperNode in potentialUpperNodes)
            {
                float horizontalDistance = Math.Abs(lowerNode.Position.X - upperNode.Position.X);
                // The weight decreases exponentially as horizontal distance increases.
                // The divisor (e.g., 20f) controls how strongly horizontal distance is penalized. A smaller number means a stronger penalty.
                float weight = 1.0f / (1.0f + (horizontalDistance * 0.1f));
                weights[upperNode] = weight;
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
            var processedPathIds = new HashSet<int>();

            foreach (var group in pathsByStartNode)
            {
                var fromNode = allNodes.First(n => n.Id == group.Key);
                var availablePaths = group.Where(p => !processedPathIds.Contains(p.Id)).ToList();

                while (availablePaths.Count >= 2)
                {
                    var path1 = availablePaths[0];
                    var toNode1 = allNodes.First(n => n.Id == path1.ToNodeId);

                    // Find a suitable partner for path1 to merge with
                    SplitMapPath? path2 = availablePaths
                        .Skip(1)
                        .Where(p2 => Vector2.Distance(toNode1.Position, allNodes.First(n => n.Id == p2.ToNodeId).Position) < MERGE_DISTANCE_THRESHOLD)
                        .FirstOrDefault();

                    if (path2 != null && _random.NextDouble() < MERGE_CHANCE)
                    {
                        var toNode2 = allNodes.First(n => n.Id == path2.ToNodeId);

                        // Calculate split point
                        var midPoint = Vector2.Lerp(toNode1.Position, toNode2.Position, 0.5f);
                        float splitProgress = (float)_random.NextDouble() * (PATH_SPLIT_POINT_MAX - PATH_SPLIT_POINT_MIN) + PATH_SPLIT_POINT_MIN;
                        var splitPoint = Vector2.Lerp(fromNode.Position, midPoint, splitProgress);

                        // Generate trunk and branches
                        var trunkPoints = GenerateWigglyPathPoints(fromNode.Position, splitPoint);
                        var branch1Points = GenerateWigglyPathPoints(splitPoint, toNode1.Position);
                        var branch2Points = GenerateWigglyPathPoints(splitPoint, toNode2.Position);

                        // Assign points, skipping the first point of branches to avoid duplicates
                        path1.RenderPoints = trunkPoints.Concat(branch1Points.Skip(1)).ToList();
                        path2.RenderPoints = trunkPoints.Concat(branch2Points.Skip(1)).ToList();

                        processedPathIds.Add(path1.Id);
                        processedPathIds.Add(path2.Id);
                        availablePaths.Remove(path1);
                        availablePaths.Remove(path2);
                    }
                    else
                    {
                        // No merge found for path1, remove it from consideration for this group
                        availablePaths.Remove(path1);
                    }
                }
            }

            // Generate points for any remaining unmerged paths
            foreach (var path in paths)
            {
                if (!processedPathIds.Contains(path.Id))
                {
                    var fromNode = allNodes.First(n => n.Id == path.FromNodeId);
                    var toNode = allNodes.First(n => n.Id == path.ToNodeId);
                    path.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, toNode.Position);
                }

                // Generate pixel points for all paths (merged or not)
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
                        path.PixelPoints.AddRange(segmentPoints.Skip(1));
                    }
                }
            }
        }


        private static List<Vector2> GenerateWigglyPathPoints(Vector2 start, Vector2 end)
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

                var finalPoint = pointOnLine + perpendicular * randomOffset * taper;
                points.Add(finalPoint);
            }

            points.Add(end);
            return points;
        }

        private static void AssignEvents(List<SplitMapNode>[] nodesByFloor, SplitData splitData, int totalFloors)
        {
            // Assign Origin
            var startNode = nodesByFloor[0].First();
            startNode.NodeType = SplitNodeType.Origin;

            // Assign Boss
            var bossNode = nodesByFloor[totalFloors - 1].First();
            bossNode.NodeType = SplitNodeType.MajorBattle;
            if (splitData.PossibleMajorBattles.Any())
            {
                bossNode.EventData = splitData.PossibleMajorBattles[_random.Next(splitData.PossibleMajorBattles.Count)];
            }

            // Determine Reward Floors
            var rewardFloorIndices = new HashSet<int>();
            if (splitData.NumberOfRewardFloors > 0)
            {
                float interval = (float)(totalFloors - 1) / (splitData.NumberOfRewardFloors + 1);
                for (int i = 1; i <= splitData.NumberOfRewardFloors; i++)
                {
                    int floorIndex = (int)Math.Round(i * interval);
                    if (floorIndex > 0 && floorIndex < totalFloors - 1) // Ensure rewards are not on start/boss floors
                    {
                        rewardFloorIndices.Add(floorIndex);
                    }
                }
            }

            // Assign events to all other nodes
            for (int floor = 1; floor < totalFloors - 1; floor++)
            {
                if (rewardFloorIndices.Contains(floor))
                {
                    foreach (var node in nodesByFloor[floor])
                    {
                        node.NodeType = SplitNodeType.Reward;
                    }
                }
                else
                {
                    foreach (var node in nodesByFloor[floor])
                    {
                        float roll = (float)_random.NextDouble();
                        if (roll < BATTLE_EVENT_WEIGHT && splitData.PossibleBattles.Any())
                        {
                            node.NodeType = SplitNodeType.Battle;
                            node.EventData = splitData.PossibleBattles[_random.Next(splitData.PossibleBattles.Count)];
                        }
                        else if (splitData.PossibleNarrativeEventIDs.Any())
                        {
                            node.NodeType = SplitNodeType.Narrative;
                            node.EventData = splitData.PossibleNarrativeEventIDs[_random.Next(splitData.PossibleNarrativeEventIDs.Count)];
                        }
                        else // Fallback to battle if no narratives are available
                        {
                            node.NodeType = SplitNodeType.Battle;
                            if (splitData.PossibleBattles.Any())
                                node.EventData = splitData.PossibleBattles[_random.Next(splitData.PossibleBattles.Count)];
                        }
                    }
                }
            }
        }
    }
}
#nullable restore