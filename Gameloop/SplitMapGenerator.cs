#nullable enable
using Microsoft.Xna.Framework;
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
        private const int HORIZONTAL_PADDING = 40;
        private const int VERTICAL_PADDING = 50;
        private const float BATTLE_EVENT_WEIGHT = 0.7f; // 70% chance for a battle
        private const float NARRATIVE_EVENT_WEIGHT = 0.3f; // 30% chance for a narrative

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

            return new SplitMap(allNodes, paths, totalFloors, startNodeId, mapHeight);
        }

        private static List<SplitMapNode>[] PlaceNodes(int totalFloors, float mapHeight)
        {
            var nodesByFloor = new List<SplitMapNode>[totalFloors];
            int previousFloorNodeCount = 0;
            float mapHorizontalOffset = (Global.VIRTUAL_WIDTH - MAP_WIDTH) / 2f;

            for (int floor = 0; floor < totalFloors; floor++)
            {
                nodesByFloor[floor] = new List<SplitMapNode>();
                int numNodes;

                if (floor == 0 || floor == totalFloors - 1)
                {
                    numNodes = 1; // Start and Boss floors always have one node
                }
                else
                {
                    do
                    {
                        numNodes = _random.Next(1, 5); // 1 to 4 nodes
                    } while (numNodes == previousFloorNodeCount && totalFloors > 3); // Avoid same count as previous floor
                }

                float y = mapHeight - VERTICAL_PADDING - (floor * FLOOR_HEIGHT);

                for (int i = 0; i < numNodes; i++)
                {
                    float x;
                    if (numNodes == 1)
                    {
                        x = (MAP_WIDTH / 2f) + mapHorizontalOffset;
                    }
                    else
                    {
                        float spacing = (float)(MAP_WIDTH - HORIZONTAL_PADDING * 2) / (numNodes - 1);
                        x = HORIZONTAL_PADDING + (i * spacing) + mapHorizontalOffset;
                    }
                    nodesByFloor[floor].Add(new SplitMapNode(floor, new Vector2(x, y)));
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

                // Pass 1: Ensure every upper node is reachable from below.
                foreach (var upperNode in upperFloor)
                {
                    var closestLower = lowerFloor.OrderBy(n => Vector2.DistanceSquared(n.Position, upperNode.Position)).First();
                    var path = new SplitMapPath(closestLower.Id, upperNode.Id);
                    paths.Add(path);
                    closestLower.OutgoingPathIds.Add(path.Id);
                    upperNode.IncomingPathIds.Add(path.Id);
                }

                // Pass 2: Ensure every lower node can reach the next floor.
                foreach (var lowerNode in lowerFloor)
                {
                    if (!lowerNode.OutgoingPathIds.Any())
                    {
                        var closestUpper = upperFloor.OrderBy(n => Vector2.DistanceSquared(n.Position, lowerNode.Position)).First();
                        var path = new SplitMapPath(lowerNode.Id, closestUpper.Id);
                        paths.Add(path);
                        lowerNode.OutgoingPathIds.Add(path.Id);
                        closestUpper.IncomingPathIds.Add(path.Id);
                    }
                }

                // Pass 3: Add secondary paths for more branching.
                foreach (var lowerNode in lowerFloor)
                {
                    if (_random.NextDouble() < 0.75 && upperFloor.Count > 1)
                    {
                        var connectedUpperNodeIds = lowerNode.OutgoingPathIds.Select(pId => paths.First(p => p.Id == pId).ToNodeId).ToHashSet();
                        var secondClosest = upperFloor
                            .Where(n => !connectedUpperNodeIds.Contains(n.Id))
                            .OrderBy(n => Vector2.DistanceSquared(n.Position, lowerNode.Position))
                            .FirstOrDefault();

                        if (secondClosest != null)
                        {
                            var path = new SplitMapPath(lowerNode.Id, secondClosest.Id);
                            paths.Add(path);
                            lowerNode.OutgoingPathIds.Add(path.Id);
                            secondClosest.IncomingPathIds.Add(path.Id);
                        }
                    }
                }
            }
            return paths;
        }

        private static void AssignEvents(List<SplitMapNode>[] nodesByFloor, SplitData splitData, int totalFloors)
        {
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
                        else if (splitData.PossibleNarratives.Any())
                        {
                            node.NodeType = SplitNodeType.Narrative;
                            node.EventData = splitData.PossibleNarratives[_random.Next(splitData.PossibleNarratives.Count)];
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