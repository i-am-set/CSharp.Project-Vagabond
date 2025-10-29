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
        private const int MIN_NODES_PER_FLOOR = 1;
        private const int MAX_NODES_PER_FLOOR = 3;
        public const int MAP_WIDTH = 280;
        private const int FLOOR_HEIGHT = 60;
        private const int HORIZONTAL_PADDING = 40; // Minimal padding to keep nodes off the absolute edge
        private const int VERTICAL_PADDING = 50;
        private const float BATTLE_EVENT_WEIGHT = 0.7f; // 70% chance for a battle
        private const float NARRATIVE_EVENT_WEIGHT = 0.3f; // 30% chance for a narrative
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float SECONDARY_PATH_CHANCE = 0.4f; // Chance for a node to have a second outgoing path
        private const float NODE_HORIZONTAL_VARIANCE_FACTOR = 1.0f; // Node can move within 100% of its "lane"
        private const float NODE_VERTICAL_VARIANCE_PIXELS = 15f;
        private const float MAX_CONNECTION_DISTANCE = 120f; // Max distance for a path to be considered "close"
        private const float PATH_SPLIT_POINT_MIN = 0.2f;
        private const float PATH_SPLIT_POINT_MAX = 0.8f;
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;
        private const float REWARD_NODE_CHANCE = 0.05f;
        private const float HORIZONTAL_SPREAD = 180f;


        public static SplitMap? GenerateInitial(SplitData splitData)
        {
            SplitMapNode.ResetIdCounter();
            SplitMapPath.ResetIdCounter();

            int totalFloors = _random.Next(splitData.SplitLengthMin, splitData.SplitLengthMax + 1);
            float mapHeight = (totalFloors - 1) * FLOOR_HEIGHT + VERTICAL_PADDING * 2;

            // Generate only the first two floors initially
            var nodesByFloor = new List<SplitMapNode>[2];
            var startNodePosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, mapHeight - VERTICAL_PADDING);
            nodesByFloor[0] = PlaceNodesForFloor(0, totalFloors, mapHeight, 0, startNodePosition);
            nodesByFloor[1] = PlaceNodesForFloor(1, totalFloors, mapHeight, nodesByFloor[0].Count, nodesByFloor[0].First().Position);

            var paths = ConnectNodes(nodesByFloor[0], nodesByFloor[1]);
            AssignEvents(nodesByFloor[1], splitData, totalFloors); // Only assign events to the newly created floor 1

            // Assign origin type to the start node
            nodesByFloor[0].First().NodeType = SplitNodeType.Origin;

            var allNodes = nodesByFloor.SelectMany(floor => floor).ToList();
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;

            if (startNodeId == -1) return null;

            GeneratePathRenderPoints(paths, allNodes);

            return new SplitMap(allNodes, paths, totalFloors, startNodeId, mapHeight);
        }

        public static void GenerateNextFloor(SplitMap map, SplitData splitData, int parentNodeId)
        {
            if (map == null || !map.Nodes.TryGetValue(parentNodeId, out var parentNode)) return;

            int newFloorIndex = parentNode.Floor + 1;

            if (newFloorIndex >= map.TargetFloorCount) return; // Don't generate past the target

            // 1. Place new nodes relative to the parent
            int previousFloorNodeCount = map.Nodes.Values.Count(n => n.Floor == parentNode.Floor);
            var newNodes = PlaceNodesForFloor(newFloorIndex, map.TargetFloorCount, map.MapHeight, previousFloorNodeCount, parentNode.Position);

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
            AssignEvents(newNodes, splitData, map.TargetFloorCount);

            // 4. Generate render points for new paths
            var allNodesForRender = map.Nodes.Values.ToList();
            GeneratePathRenderPoints(newPaths, allNodesForRender);
        }

        private static List<SplitMapNode> PlaceNodesForFloor(int floor, int totalFloors, float mapHeight, int previousFloorNodeCount, Vector2 parentPosition)
        {
            var newNodes = new List<SplitMapNode>();
            const int nodeRadius = 8;
            int numNodes;

            if (floor == 0 || floor == totalFloors - 1)
            {
                numNodes = 1;
            }
            else
            {
                do
                {
                    numNodes = _random.Next(MIN_NODES_PER_FLOOR, MAX_NODES_PER_FLOOR + 1); // Generates 1, 2, or 3 nodes.
                } while (numNodes == previousFloorNodeCount);
            }

            float y = mapHeight - VERTICAL_PADDING - (floor * FLOOR_HEIGHT);
            float laneWidth = (numNodes > 0) ? HORIZONTAL_SPREAD / numNodes : 0;
            float laneStartX = parentPosition.X - HORIZONTAL_SPREAD / 2f;

            for (int i = 0; i < numNodes; i++)
            {
                float finalX;
                float finalY = y + ((float)_random.NextDouble() * 2f - 1f) * NODE_VERTICAL_VARIANCE_PIXELS;

                if (numNodes == 1)
                {
                    finalX = parentPosition.X;
                }
                else
                {
                    float laneCenterX = laneStartX + (i * laneWidth) + (laneWidth / 2f);
                    float maxOffset = (laneWidth / 2f - nodeRadius) * NODE_HORIZONTAL_VARIANCE_FACTOR;
                    float randomXOffset = ((float)_random.NextDouble() * 2f - 1f) * maxOffset;
                    finalX = laneCenterX + randomXOffset;
                }
                newNodes.Add(new SplitMapNode(floor, new Vector2(finalX, finalY)));
            }
            return newNodes;
        }

        private static List<SplitMapPath> ConnectNodes(List<SplitMapNode> lowerFloor, List<SplitMapNode> upperFloor)
        {
            var paths = new List<SplitMapPath>();

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
            return paths;
        }

        private static Dictionary<SplitMapNode, float> CalculateWeights(SplitMapNode lowerNode, List<SplitMapNode> potentialUpperNodes)
        {
            var weights = new Dictionary<SplitMapNode, float>();
            if (!potentialUpperNodes.Any()) return weights;

            foreach (var upperNode in potentialUpperNodes)
            {
                float horizontalDistance = Math.Abs(lowerNode.Position.X - upperNode.Position.X);
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
                    Vector2 averageDestination = Vector2.Zero;
                    var destinationNodes = new List<SplitMapNode>();
                    foreach (var path in outgoingPaths)
                    {
                        var toNode = allNodes.FirstOrDefault(n => n.Id == path.ToNodeId);
                        if (toNode != null)
                        {
                            averageDestination += toNode.Position;
                            destinationNodes.Add(toNode);
                        }
                    }
                    if (!destinationNodes.Any()) continue;
                    averageDestination /= destinationNodes.Count;

                    float splitProgress = (float)_random.NextDouble() * (PATH_SPLIT_POINT_MAX - PATH_SPLIT_POINT_MIN) + PATH_SPLIT_POINT_MIN;
                    var splitPoint = Vector2.Lerp(fromNode.Position, averageDestination, splitProgress);

                    var trunkIgnoreIds = new List<int> { fromNode.Id };
                    trunkIgnoreIds.AddRange(destinationNodes.Select(n => n.Id));
                    var trunkPoints = GenerateWigglyPathPoints(fromNode.Position, splitPoint, trunkIgnoreIds, allNodes);

                    foreach (var path in outgoingPaths)
                    {
                        var toNode = allNodes.FirstOrDefault(n => n.Id == path.ToNodeId);
                        if (toNode != null)
                        {
                            var branchIgnoreIds = new List<int> { fromNode.Id, toNode.Id };
                            var branchPoints = GenerateWigglyPathPoints(splitPoint, toNode.Position, branchIgnoreIds, allNodes);
                            path.RenderPoints = trunkPoints.Concat(branchPoints.Skip(1)).ToList();
                        }
                    }
                }
            }

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

        private static void AssignEvents(List<SplitMapNode> nodesToAssign, SplitData splitData, int totalFloors)
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
                if (node.Floor == totalFloors - 1)
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