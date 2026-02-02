#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        // --- Configuration ---
        /// <summary>
        /// If true, node columns will be shifted vertically so their center of mass aligns with the screen center.
        /// If false, nodes are placed purely randomly within valid slots.
        /// </summary>
        private const bool ENABLE_VERTICAL_CENTERING = true;

        private static readonly Random _random = new Random();
        private static readonly SeededPerlin _baldSpotNoise;
        private static readonly SeededPerlin _nodeExclusionNoise;
        // --- Generation Tuning ---
        private const int MIN_NODES_PER_COLUMN = 2;
        private const int MAX_NODES_PER_COLUMN = 4;
        public const int COLUMN_WIDTH = 96; // 6 * GRID_SIZE
        public const int HORIZONTAL_PADDING = 64; // 4 * GRID_SIZE
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float SECONDARY_PATH_CHANCE = 0.4f; // Chance for a node to have a second outgoing path
        private const float NODE_HORIZONTAL_VARIANCE_PIXELS = 20f;
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;
        public static readonly List<int> _validYPositions = new List<int>();

        // --- Force-Directed Layout Tuning ---
        private const int PHYSICS_ITERATIONS = 50;
        private const float PATH_REPULSION_RADIUS = 20f; // Minimum desired distance between paths
        private const float PATH_REPULSION_FORCE = 2.5f; // Strength of the push
        private const float PATH_SMOOTHING_FACTOR = 0.25f; // Tries to straighten the line slightly to fix kinks

        // Updated Weights
        private static readonly List<(SplitNodeType type, float weight)> _nodeTypeWeights = new()
        {
            (SplitNodeType.Battle, 45f),
            (SplitNodeType.Narrative, 20f),
            (SplitNodeType.Shop, 10f),
            (SplitNodeType.Rest, 15f),
            (SplitNodeType.Recruit, 10f)
        };

        // --- Minimum Node Counts (Tunable) ---
        private const int MIN_NODE_COUNT_SHOP = 2;
        private const int MIN_NODE_COUNT_REST = 2;
        private const int MIN_NODE_COUNT_RECRUIT = 2;
        private const int MIN_NODE_COUNT_NARRATIVE = 2;
        private const int MIN_NODE_COUNT_BATTLE = 2;

        // --- Tree Generation Tuning ---
        private const int TREE_DENSITY_STEP = 2; // Check for a tree every pixel for max density.
        private const float TREE_NOISE_SCALE = 8.0f; // Controls the size of clearings. Higher value = smaller, more frequent clearings.
        private const float TREE_PLACEMENT_THRESHOLD = 0.45f; // Noise value must be above this to place a tree. Higher value = more clearings.
        private const float TREE_EXCLUSION_RADIUS_NODE = 20f;
        private const float TREE_EXCLUSION_RADIUS_PATH = 8f;

        // --- Bald Spot Generation Tuning ---
        private const float BALD_SPOT_NOISE_SCALE = 0.1f; // Higher value = smaller, more frequent spots.
        private const float BALD_SPOT_THRESHOLD = 0.65f; // Noise must be ABOVE this to create a bald spot. Higher value = fewer spots.

        // --- Node Exclusion Zone Tuning ---
        private const float NODE_EXCLUSION_NOISE_SCALE = 2.5f; // Controls the number of "spikes". Higher value = more spikes.
        private const float NODE_EXCLUSION_NOISE_STRENGTH = 0.4f; // Controls the jaggedness. 0 = perfect circle, 1 = very spikey.

        // --- Node Placement Tuning ---
        private const int NODE_SPREAD_BIAS_START_INDEX = 2; // Index of the first slot in the central bias zone (inclusive)
        private const int NODE_SPREAD_BIAS_END_INDEX = 6;   // Index of the last slot in the central bias zone (inclusive)

        // Helper class for topological sorting
        private class Connection
        {
            public SplitMapNode From;
            public SplitMapNode To;
        }

        static SplitMapGenerator()
        {
            _baldSpotNoise = new SeededPerlin(_random.Next());
            _nodeExclusionNoise = new SeededPerlin(_random.Next());

            const int gridSize = Global.SPLIT_MAP_GRID_SIZE;
            const int numPositions = 9;
            int totalHeight = (numPositions - 1) * gridSize;
            int startY = (Global.VIRTUAL_HEIGHT - totalHeight) / 2;
            for (int i = 0; i < numPositions; i++)
            {
                _validYPositions.Add(startY + i * gridSize);
            }
        }

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

            if (ENABLE_VERTICAL_CENTERING)
            {
                CenterColumnNodes(startNodes);
            }

            allNodesByColumn.Add(startNodes);

            // --- Intermediate Columns ---
            for (int i = 1; i < totalColumns - 1; i++)
            {
                var previousColumn = allNodesByColumn[i - 1];
                Vector2 previousColumnAvgPos = previousColumn.Any() ? new Vector2(previousColumn.Average(n => n.Position.X), previousColumn.Average(n => n.Position.Y)) : startNodePosition;
                var newNodes = PlaceNodesForColumn(i, totalColumns, mapWidth, previousColumn.Count, previousColumnAvgPos);

                if (ENABLE_VERTICAL_CENTERING)
                {
                    CenterColumnNodes(newNodes);
                }

                allNodesByColumn.Add(newNodes);
                AssignEvents(newNodes, splitData, i, totalColumns);
            }

            // --- Final Column: Boss Node ---
            var lastRegularColumn = allNodesByColumn.Last();
            Vector2 lastAvgPos = lastRegularColumn.Any() ? new Vector2(lastRegularColumn.Average(n => n.Position.X), lastRegularColumn.Average(n => n.Position.Y)) : startNodePosition;
            var endNodes = PlaceNodesForColumn(totalColumns - 1, totalColumns, mapWidth, 0, lastAvgPos);
            var endNode = endNodes.First();
            endNode.NodeType = SplitNodeType.MajorBattle;
            if (splitData.PossibleMajorBattles.Any())
            {
                endNode.EventData = splitData.PossibleMajorBattles[_random.Next(splitData.PossibleMajorBattles.Count)];
            }

            if (ENABLE_VERTICAL_CENTERING)
            {
                CenterColumnNodes(endNodes);
            }

            allNodesByColumn.Add(endNodes);

            // --- Connect All Columns ---
            var allNodes = allNodesByColumn.SelectMany(c => c).ToList();
            for (int i = 0; i < allNodesByColumn.Count - 1; i++)
            {
                var newPaths = ConnectColumnPair(allNodesByColumn[i], allNodesByColumn[i + 1], allPaths, allNodes);
                allPaths.AddRange(newPaths);
            }

            // --- Post-Processing: Bad Luck Protection (Rule #3) ---
            // Ensure player isn't forced into too many consecutive fights without a rest.
            EnforceRestAfterStreak(allNodesByColumn, allPaths);

            // --- Post-Processing: Enforce Minimum Node Counts (New Rule) ---
            // Ensures we have at least X of each node type, converting Combat nodes if necessary.
            EnforceNodeMinimums(allNodes, allPaths, splitData);

            // --- Post-Processing: Anti-Clumping (Rule #2 from prompt) ---
            // Ensure we don't have Shop->Shop or Rest->Rest.
            SanitizeNodeRepetition(allNodesByColumn, allPaths, splitData);

            // --- Post-Processing: Restricted Column Failsafe (Rule #1) ---
            // One final pass to absolutely guarantee no Shops/Rests in columns 1 and 2.
            SanitizeRestrictedColumns(allNodes, splitData);

            // --- Post-Processing: Force-Directed Repulsion ---
            // This pushes paths apart to prevent ugly tangents and overlaps.
            ApplyForceDirectedLayout(allPaths, allNodes);

            // --- Final Assembly ---
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;

            if (startNodeId == -1) return null;

            GeneratePathRenderPoints(allPaths, allNodes);
            var bakedScenery = BakeTreesToTexture(allNodes, allPaths, mapWidth);

            return new SplitMap(allNodes, allPaths, bakedScenery, totalColumns, startNodeId, mapWidth);
        }

        /// <summary>
        /// Checks if the map meets the minimum requirements for each node type.
        /// If not, it converts random Battle nodes into the missing types, respecting placement rules.
        /// </summary>
        private static void EnforceNodeMinimums(List<SplitMapNode> allNodes, List<SplitMapPath> allPaths, SplitData splitData)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            // Define requirements
            var requirements = new Dictionary<SplitNodeType, int>
            {
                { SplitNodeType.Shop, MIN_NODE_COUNT_SHOP },
                { SplitNodeType.Rest, MIN_NODE_COUNT_REST },
                { SplitNodeType.Recruit, MIN_NODE_COUNT_RECRUIT },
                { SplitNodeType.Narrative, MIN_NODE_COUNT_NARRATIVE },
                { SplitNodeType.Battle, MIN_NODE_COUNT_BATTLE }
            };

            foreach (var req in requirements)
            {
                SplitNodeType targetType = req.Key;
                int minCount = req.Value;

                // Count current nodes of this type
                int currentCount = allNodes.Count(n => n.NodeType == targetType);

                while (currentCount < minCount)
                {
                    // Find candidates to convert
                    // Must be Battle nodes (we steal from combat)
                    // Must NOT be MajorBattle or Origin
                    var candidates = allNodes.Where(n =>
                        n.NodeType == SplitNodeType.Battle &&
                        n.NodeType != SplitNodeType.MajorBattle &&
                        n.NodeType != SplitNodeType.Origin
                    ).ToList();

                    // Apply specific placement rules
                    if (targetType == SplitNodeType.Shop || targetType == SplitNodeType.Rest)
                    {
                        // Shops and Rests cannot be in the first 2 columns (0, 1, 2)
                        candidates = candidates.Where(n => n.Floor > 2).ToList();
                    }

                    if (!candidates.Any())
                    {
                        // No valid candidates left to convert. Break to avoid infinite loop.
                        break;
                    }

                    // Smart Selection: Try to pick a node that isn't connected to a node of the same type
                    // to avoid creating clumps that SanitizeNodeRepetition would just delete later.
                    var smartCandidates = candidates.Where(n =>
                    {
                        // Check incoming neighbors
                        bool incomingConflict = n.IncomingPathIds
                            .Select(pid => allPaths.FirstOrDefault(p => p.Id == pid))
                            .Where(p => p != null)
                            .Select(p => allNodes.FirstOrDefault(node => node.Id == p.FromNodeId))
                            .Any(neighbor => neighbor != null && neighbor.NodeType == targetType);

                        // Check outgoing neighbors
                        bool outgoingConflict = n.OutgoingPathIds
                            .Select(pid => allPaths.FirstOrDefault(p => p.Id == pid))
                            .Where(p => p != null)
                            .Select(p => allNodes.FirstOrDefault(node => node.Id == p.ToNodeId))
                            .Any(neighbor => neighbor != null && neighbor.NodeType == targetType);

                        return !incomingConflict && !outgoingConflict;
                    }).ToList();

                    // If we have smart candidates, use them. Otherwise fall back to any valid candidate.
                    var finalPool = smartCandidates.Any() ? smartCandidates : candidates;
                    var nodeToConvert = finalPool[_random.Next(finalPool.Count)];

                    // Convert the node
                    nodeToConvert.NodeType = targetType;
                    nodeToConvert.EventData = null; // Clear battle data

                    // Assign specific data if needed
                    if (targetType == SplitNodeType.Narrative)
                    {
                        nodeToConvert.EventData = progressionManager.GetRandomNarrative()?.EventID;
                    }

                    currentCount++;
                }
            }
        }

        /// <summary>
        /// Iterates through the map column by column to ensure no Shop connects to a Shop, and no Rest connects to a Rest.
        /// This uses a deterministic top-down pass to guarantee termination without infinite loops.
        /// </summary>
        private static void SanitizeNodeRepetition(List<List<SplitMapNode>> columns, List<SplitMapPath> allPaths, SplitData splitData)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            // Iterate from the second column to the end (Column 0 is Origin, no parents)
            for (int i = 1; i < columns.Count; i++)
            {
                foreach (var node in columns[i])
                {
                    // Skip fixed nodes
                    if (node.NodeType == SplitNodeType.MajorBattle || node.NodeType == SplitNodeType.Origin) continue;

                    // Check parents for conflicts
                    bool conflict = false;
                    foreach (var pathId in node.IncomingPathIds)
                    {
                        var path = allPaths.FirstOrDefault(p => p.Id == pathId);
                        if (path == null) continue;

                        // Find parent node (it must be in the previous column)
                        var parent = columns[i - 1].FirstOrDefault(n => n.Id == path.FromNodeId);
                        if (parent == null) continue;

                        if (node.NodeType == SplitNodeType.Shop && parent.NodeType == SplitNodeType.Shop)
                        {
                            conflict = true;
                            break;
                        }
                        if (node.NodeType == SplitNodeType.Rest && parent.NodeType == SplitNodeType.Rest)
                        {
                            conflict = true;
                            break;
                        }
                    }

                    if (conflict)
                    {
                        // Resolve conflict immediately by changing this node to a safe type.
                        // We don't need to check children because we will process them in the next iteration of 'i'.
                        RerollNodeSafe(node, splitData, progressionManager);
                    }
                }
            }
        }

        /// <summary>
        /// A final failsafe pass to ensure no Rest or Shop nodes exist in the first few columns.
        /// </summary>
        private static void SanitizeRestrictedColumns(List<SplitMapNode> allNodes, SplitData splitData)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            foreach (var node in allNodes)
            {
                // Columns 0, 1, 2 are restricted.
                if (node.Floor <= 2)
                {
                    if (node.NodeType == SplitNodeType.Rest || node.NodeType == SplitNodeType.Shop)
                    {
                        RerollNodeSafe(node, splitData, progressionManager);
                    }
                }
            }
        }

        /// <summary>
        /// Changes a node to a safe type (Battle, Narrative, Recruit) to resolve conflicts.
        /// Guaranteed to not pick Shop or Rest.
        /// </summary>
        private static void RerollNodeSafe(SplitMapNode node, SplitData splitData, ProgressionManager progressionManager)
        {
            var validTypes = new List<(SplitNodeType type, float weight)>();

            if (splitData.PossibleBattles != null && splitData.PossibleBattles.Any())
                validTypes.Add((SplitNodeType.Battle, 50f));

            if (splitData.PossibleNarrativeEventIDs != null && splitData.PossibleNarrativeEventIDs.Any())
                validTypes.Add((SplitNodeType.Narrative, 30f));

            validTypes.Add((SplitNodeType.Recruit, 20f));

            // Fallback to Battle if nothing else is available (should never happen in valid data)
            if (!validTypes.Any())
            {
                node.NodeType = SplitNodeType.Battle;
                node.Difficulty = BattleDifficulty.Normal;
                node.EventData = progressionManager.GetRandomBattle(node.Difficulty);
                return;
            }

            float totalWeight = validTypes.Sum(x => x.weight);
            double roll = _random.NextDouble() * totalWeight;
            SplitNodeType chosenType = validTypes.Last().type;

            foreach (var choice in validTypes)
            {
                if (roll < choice.weight)
                {
                    chosenType = choice.type;
                    break;
                }
                roll -= choice.weight;
            }

            node.NodeType = chosenType;
            node.EventData = null; // Reset data

            switch (chosenType)
            {
                case SplitNodeType.Battle:
                    node.Difficulty = (BattleDifficulty)_random.Next(3);
                    node.EventData = progressionManager.GetRandomBattle(node.Difficulty);
                    break;
                case SplitNodeType.Narrative:
                    node.EventData = progressionManager.GetRandomNarrative()?.EventID;
                    break;
                case SplitNodeType.Recruit:
                    // No event data needed for now
                    break;
            }
        }

        /// <summary>
        /// Traverses the generated graph to ensure that after 3 consecutive battles,
        /// at least one child node is a Rest site.
        /// </summary>
        private static void EnforceRestAfterStreak(List<List<SplitMapNode>> columns, List<SplitMapPath> allPaths)
        {
            // Map NodeID -> Consecutive Battle Count
            var streaks = new Dictionary<int, int>();

            // Initialize Start Node (Column 0)
            foreach (var node in columns[0])
            {
                streaks[node.Id] = (node.NodeType == SplitNodeType.Battle) ? 1 : 0;
            }

            // Iterate through columns 1 to N-1 (skipping the final boss column)
            for (int i = 1; i < columns.Count - 1; i++)
            {
                foreach (var node in columns[i])
                {
                    // Calculate max streak from parents
                    int maxParentStreak = 0;
                    foreach (var pathId in node.IncomingPathIds)
                    {
                        var path = allPaths.FirstOrDefault(p => p.Id == pathId);
                        if (path != null && streaks.TryGetValue(path.FromNodeId, out int parentStreak))
                        {
                            maxParentStreak = Math.Max(maxParentStreak, parentStreak);
                        }
                    }

                    // Calculate current streak
                    int currentStreak = (node.NodeType == SplitNodeType.Battle) ? maxParentStreak + 1 : 0;
                    streaks[node.Id] = currentStreak;

                    // Rule #3 Check: If streak >= 3, ensure children offer relief
                    if (currentStreak >= 3)
                    {
                        var children = new List<SplitMapNode>();
                        foreach (var pathId in node.OutgoingPathIds)
                        {
                            var path = allPaths.FirstOrDefault(p => p.Id == pathId);
                            if (path != null)
                            {
                                // Find the child node in the next column
                                var child = columns[i + 1].FirstOrDefault(n => n.Id == path.ToNodeId);
                                if (child != null) children.Add(child);
                            }
                        }

                        // If there are children, and NONE of them are non-combat (Rest/Shop/Event/Recruit)
                        // Note: MajorBattle is excluded from this check as it's the end.
                        if (children.Any() && children.All(c => c.NodeType == SplitNodeType.Battle))
                        {
                            // Force one random child to be a Rest site
                            var luckyChild = children[_random.Next(children.Count)];
                            luckyChild.NodeType = SplitNodeType.Rest;
                            luckyChild.EventData = null; // Clear any battle data

                            // Note: We don't need to update streaks for the next column immediately,
                            // because the loop will handle it when it reaches column i+1.
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shifts the nodes in a column vertically so that their collective center aligns with the screen center.
        /// </summary>
        private static void CenterColumnNodes(List<SplitMapNode> nodes)
        {
            if (!nodes.Any()) return;

            // 1. Calculate the vertical bounds of the current node clump
            float minY = nodes.Min(n => n.Position.Y);
            float maxY = nodes.Max(n => n.Position.Y);
            float currentCenterY = (minY + maxY) / 2f;

            // 2. Determine the target center (middle of the screen)
            float screenCenterY = Global.VIRTUAL_HEIGHT / 2f;

            // 3. Calculate the shift needed
            float shiftY = screenCenterY - currentCenterY;

            // 4. Snap the shift to the grid size to maintain pixel-perfect alignment
            int gridSize = Global.SPLIT_MAP_GRID_SIZE;
            float snappedShift = MathF.Round(shiftY / gridSize) * gridSize;

            // 5. Apply the shift to all nodes in the column
            foreach (var node in nodes)
            {
                float newY = node.Position.Y + snappedShift;
                // Clamp to ensure nodes don't go off-screen, respecting a margin
                float margin = gridSize * 2;
                newY = Math.Clamp(newY, margin, Global.VIRTUAL_HEIGHT - margin);
                node.Position = new Vector2(node.Position.X, newY);
            }
        }

        /// <summary>
        /// Applies a physics-based simulation to push path segments away from each other.
        /// </summary>
        private static void ApplyForceDirectedLayout(List<SplitMapPath> allPaths, List<SplitMapNode> allNodes)
        {
            // Group paths by the column index (Floor) of their starting node.
            // Paths only interact with other paths in the same column group to save performance.
            var pathsByColumn = allPaths
                .GroupBy(p => allNodes.First(n => n.Id == p.FromNodeId).Floor)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (int iter = 0; iter < PHYSICS_ITERATIONS; iter++)
            {
                var forces = new Dictionary<SplitMapPath, Vector2[]>();

                // Initialize force arrays
                foreach (var path in allPaths)
                {
                    forces[path] = new Vector2[path.RenderPoints.Count];
                }

                // 1. Calculate Repulsion Forces (Between different paths in the same column)
                foreach (var columnGroup in pathsByColumn.Values)
                {
                    for (int i = 0; i < columnGroup.Count; i++)
                    {
                        var pathA = columnGroup[i];
                        for (int j = i + 1; j < columnGroup.Count; j++)
                        {
                            var pathB = columnGroup[j];

                            // Check every point against every point (excluding start/end anchors)
                            for (int a = 1; a < pathA.RenderPoints.Count - 1; a++)
                            {
                                for (int b = 1; b < pathB.RenderPoints.Count - 1; b++)
                                {
                                    Vector2 posA = pathA.RenderPoints[a];
                                    Vector2 posB = pathB.RenderPoints[b];

                                    float distSq = Vector2.DistanceSquared(posA, posB);
                                    if (distSq < PATH_REPULSION_RADIUS * PATH_REPULSION_RADIUS && distSq > 0.001f)
                                    {
                                        float dist = MathF.Sqrt(distSq);
                                        Vector2 dir = (posA - posB) / dist;
                                        float force = (PATH_REPULSION_RADIUS - dist) / PATH_REPULSION_RADIUS * PATH_REPULSION_FORCE;

                                        forces[pathA][a] += dir * force;
                                        forces[pathB][b] -= dir * force;
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Calculate Smoothing Forces (Internal tension to fix kinks)
                foreach (var path in allPaths)
                {
                    for (int k = 1; k < path.RenderPoints.Count - 1; k++)
                    {
                        Vector2 current = path.RenderPoints[k];
                        Vector2 prev = path.RenderPoints[k - 1];
                        Vector2 next = path.RenderPoints[k + 1];

                        // Move towards the midpoint of neighbors (Laplacian smoothing)
                        Vector2 target = (prev + next) / 2f;
                        Vector2 force = (target - current) * PATH_SMOOTHING_FACTOR;
                        forces[path][k] += force;
                    }
                }

                // 3. Apply Forces
                foreach (var path in allPaths)
                {
                    for (int k = 1; k < path.RenderPoints.Count - 1; k++)
                    {
                        path.RenderPoints[k] += forces[path][k];
                    }
                }
            }
        }

        public static void GenerateNextFloor(SplitMap map, SplitData splitData, int parentNodeId)
        {
            // This method is now obsolete as the entire map is generated at once.
            // It's kept for potential future use but is not called in the current flow.
        }

        private static List<SplitMapNode> PlaceNodesForColumn(int columnIndex, int totalColumns, float mapWidth, int previousColumnNodeCount, Vector2 parentPosition)
        {
            var newNodes = new List<SplitMapNode>();
            int numNodes;

            if (columnIndex == 0 || columnIndex == totalColumns - 1)
            {
                numNodes = 1;
            }
            else
            {
                do
                {
                    numNodes = _random.Next(MIN_NODES_PER_COLUMN, MAX_NODES_PER_COLUMN + 1);
                } while (numNodes == previousColumnNodeCount && numNodes != 1);
            }

            float x = HORIZONTAL_PADDING + (columnIndex * COLUMN_WIDTH);
            var availableYSlots = new List<int>(_validYPositions);

            for (int i = 0; i < numNodes; i++)
            {
                if (!availableYSlots.Any()) break;

                float finalX = x + ((float)_random.NextDouble() * 2f - 1f) * NODE_HORIZONTAL_VARIANCE_PIXELS;
                int finalY;

                // If this is the first node of a multi-node column, bias the pick towards the center.
                if (i == 0 && numNodes > 1)
                {
                    var centralSlots = availableYSlots.Where(y =>
                    {
                        int index = _validYPositions.IndexOf(y);
                        return index >= NODE_SPREAD_BIAS_START_INDEX && index <= NODE_SPREAD_BIAS_END_INDEX;
                    }).ToList();

                    if (centralSlots.Any())
                    {
                        finalY = centralSlots[_random.Next(centralSlots.Count)];
                    }
                    else
                    {
                        // Fallback if central slots are somehow all taken (highly unlikely).
                        finalY = availableYSlots[_random.Next(availableYSlots.Count)];
                    }
                }
                else // For the only node in a column, or subsequent nodes, pick randomly.
                {
                    finalY = availableYSlots[_random.Next(availableYSlots.Count)];
                }

                // Find the original index of the chosen slot to identify its neighbors
                int originalIndex = _validYPositions.IndexOf(finalY);

                // Remove the chosen slot and its immediate neighbors from the available pool for this column
                availableYSlots.Remove(finalY);
                if (originalIndex > 0)
                {
                    availableYSlots.Remove(_validYPositions[originalIndex - 1]);
                }
                if (originalIndex < _validYPositions.Count - 1)
                {
                    availableYSlots.Remove(_validYPositions[originalIndex + 1]);
                }

                newNodes.Add(new SplitMapNode(columnIndex, new Vector2(finalX, finalY)));
            }
            return newNodes;
        }

        private static List<SplitMapPath> ConnectColumnPair(List<SplitMapNode> previousColumn, List<SplitMapNode> nextColumn, List<SplitMapPath> allExistingPaths, List<SplitMapNode> allNodes)
        {
            var connections = new List<Connection>();
            var sortedPrev = previousColumn.OrderBy(n => n.Position.Y).ToList();
            var sortedNext = nextColumn.OrderBy(n => n.Position.Y).ToList();

            // 1. Primary Connections: Ensure every previous node connects to at least one next node
            foreach (var prev in sortedPrev)
            {
                var target = sortedNext.OrderBy(n => Vector2.DistanceSquared(prev.Position, n.Position)).First();
                connections.Add(new Connection { From = prev, To = target });
            }

            // 2. Reachability: Ensure every next node has at least one incoming connection
            foreach (var next in sortedNext)
            {
                if (!connections.Any(c => c.To == next))
                {
                    var source = sortedPrev.OrderBy(n => Vector2.DistanceSquared(n.Position, next.Position)).First();
                    connections.Add(new Connection { From = source, To = next });
                }
            }

            // 3. Branching: Randomly add extra connections for variety
            foreach (var prev in sortedPrev)
            {
                if (_random.NextDouble() < SECONDARY_PATH_CHANCE)
                {
                    // Find potential targets that aren't already connected to this node
                    var existingTargets = connections.Where(c => c.From == prev).Select(c => c.To).ToHashSet();
                    var potentialTargets = sortedNext.Where(n => !existingTargets.Contains(n)).ToList();

                    if (potentialTargets.Any())
                    {
                        // Pick a random target from the remaining ones
                        var extraTarget = potentialTargets[_random.Next(potentialTargets.Count)];
                        connections.Add(new Connection { From = prev, To = extraTarget });
                    }
                }
            }

            // 4. Untangle: Topological Sort / Swap Destinations
            // This loop detects crossing paths and swaps their destinations to uncross them.
            bool changed = true;
            int iterations = 0;
            while (changed && iterations < 50)
            {
                changed = false;
                iterations++;
                // Sort connections by the Y position of the starting node
                connections.Sort((a, b) => a.From.Position.Y.CompareTo(b.From.Position.Y));

                for (int i = 0; i < connections.Count; i++)
                {
                    for (int j = i + 1; j < connections.Count; j++)
                    {
                        var c1 = connections[i];
                        var c2 = connections[j];

                        // Ignore if they share a start or end node (diverging/converging is fine)
                        if (c1.From == c2.From || c1.To == c2.To) continue;

                        // Check for crossing:
                        // Since list is sorted by From.Y, we know c1.From is "above" (or equal to) c2.From.
                        // If c1.To is "below" c2.To, then the lines must cross.
                        if (c1.To.Position.Y > c2.To.Position.Y)
                        {
                            // Swap targets to uncross
                            var temp = c1.To;
                            c1.To = c2.To;
                            c2.To = temp;
                            changed = true;
                        }
                    }
                }
            }

            // Note: If iterations >= 50, we simply accept the current state. 
            // A few crossed paths are better than a crash or a failed generation.

            // 5. Remove Duplicates (created by swapping)
            // We use a HashSet of tuples to filter unique connections
            var uniqueConnections = new HashSet<(int FromId, int ToId)>();
            var finalConnections = new List<Connection>();

            foreach (var conn in connections)
            {
                if (uniqueConnections.Add((conn.From.Id, conn.To.Id)))
                {
                    finalConnections.Add(conn);
                }
            }

            // 6. Generate Geometry
            var newPaths = new List<SplitMapPath>();
            foreach (var conn in finalConnections)
            {
                var path = new SplitMapPath(conn.From.Id, conn.To.Id);
                // Generate wiggly points now that topology is clean
                path.RenderPoints = GenerateWigglyPathPoints(conn.From.Position, conn.To.Position, new List<int> { conn.From.Id, conn.To.Id }, allNodes);
                newPaths.Add(path);

                // Update node connectivity
                conn.From.OutgoingPathIds.Add(path.Id);
                conn.To.IncomingPathIds.Add(path.Id);
            }

            return newPaths;
        }

        private static void GeneratePathRenderPoints(List<SplitMapPath> paths, List<SplitMapNode> allNodes)
        {
            // The wiggly RenderPoints are now generated during connection.
            // This method is now only responsible for generating the pixel-perfect points for drawing.
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
                float taper = MathF.Sin(progress * MathF.PI);

                Vector2 totalRepulsion = Vector2.Zero;
                foreach (var otherNode in allNodes)
                {
                    if (ignoreNodeIds.Contains(otherNode.Id)) continue;

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

        private static void AssignEvents(List<SplitMapNode> nodesToAssign, SplitData splitData, int columnIndex, int totalColumns)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            // Rule #2: The column before the boss (totalColumns - 2) must be all Rest nodes.
            if (columnIndex == totalColumns - 2)
            {
                foreach (var node in nodesToAssign)
                {
                    node.NodeType = SplitNodeType.Rest;
                    node.EventData = null;
                }
                return;
            }

            var availableChoices = new List<(SplitNodeType type, float weight)>(_nodeTypeWeights);

            // Rule #1: No Rest or Shop nodes in the first two columns (0, 1, and 2).
            // Column 0 is Origin. Column 1 is first choice. Column 2 is second choice.
            // User requested "dont start generating until column 3 and on".
            // So indices 0, 1, 2 are restricted.
            if (columnIndex <= 2)
            {
                availableChoices.RemoveAll(c => c.type == SplitNodeType.Rest || c.type == SplitNodeType.Shop);
            }

            // Filter out types if their corresponding data is missing in the current split
            if (splitData.PossibleNarrativeEventIDs == null || !splitData.PossibleNarrativeEventIDs.Any())
            {
                availableChoices.RemoveAll(c => c.type == SplitNodeType.Narrative);
            }
            if (splitData.PossibleBattles == null || !splitData.PossibleBattles.Any())
            {
                availableChoices.RemoveAll(c => c.type == SplitNodeType.Battle);
            }

            if (!availableChoices.Any())
            {
                // Fallback if no choices are possible
                return;
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
                    case SplitNodeType.Recruit:
                    case SplitNodeType.Rest:
                    case SplitNodeType.Shop:
                        node.EventData = null;
                        break;
                }
            }
        }

        private static RenderTarget2D BakeTreesToTexture(List<SplitMapNode> allNodes, List<SplitMapPath> allPaths, float mapWidth)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var spriteBatch = ServiceLocator.Get<SpriteBatch>();
            var global = ServiceLocator.Get<Global>();
            var pixel = ServiceLocator.Get<Texture2D>();

            int textureWidth = (int)mapWidth + HORIZONTAL_PADDING * 2;
            int textureHeight = Global.VIRTUAL_HEIGHT;

            var renderTarget = new RenderTarget2D(graphicsDevice, textureWidth, textureHeight);
            var treePositions = GenerateTrees(allNodes, allPaths, mapWidth);

            graphicsDevice.SetRenderTarget(renderTarget);
            graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Y-sort trees before drawing to the texture
            foreach (var treePos in treePositions.OrderBy(p => p.Y))
            {
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, -1), global.Palette_DarkShadow); // Top
                spriteBatch.DrawSnapped(pixel, treePos, global.Palette_Black);      // Middle
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, 1), global.Palette_Black); // Bottom
            }

            spriteBatch.End();

            graphicsDevice.SetRenderTarget(null); // Reset to back buffer

            return renderTarget;
        }

        private static List<Vector2> GenerateTrees(List<SplitMapNode> allNodes, List<SplitMapPath> allPaths, float mapWidth)
        {
            var treePositions = new List<Vector2>();
            var noiseManager = ServiceLocator.Get<NoiseMapManager>();

            int endX = (int)mapWidth + HORIZONTAL_PADDING;
            int endY = Global.VIRTUAL_HEIGHT;

            // Pre-calculate exclusion zones for performance
            var noSpawnZone = new bool[endX, endY];
            float pathRadiusSq = TREE_EXCLUSION_RADIUS_PATH * TREE_EXCLUSION_RADIUS_PATH;

            foreach (var node in allNodes)
            {
                int minX = (int)Math.Max(0, node.Position.X - TREE_EXCLUSION_RADIUS_NODE);
                int maxX = (int)Math.Min(endX - 1, node.Position.X + TREE_EXCLUSION_RADIUS_NODE);
                int minY = (int)Math.Max(0, node.Position.Y - TREE_EXCLUSION_RADIUS_NODE);
                int maxY = (int)Math.Min(endY - 1, node.Position.Y + TREE_EXCLUSION_RADIUS_NODE);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 currentPixel = new Vector2(x, y);
                        float distance = Vector2.Distance(currentPixel, node.Position);

                        Vector2 direction = currentPixel - node.Position;
                        float angle = MathF.Atan2(direction.Y, direction.X);

                        float noiseInputX = MathF.Cos(angle * NODE_EXCLUSION_NOISE_SCALE);
                        float noiseInputY = MathF.Sin(angle * NODE_EXCLUSION_NOISE_SCALE);
                        float noise = (_nodeExclusionNoise.Noise(noiseInputX, noiseInputY) + 1f) * 0.5f;

                        float modulatedRadius = TREE_EXCLUSION_RADIUS_NODE * (1.0f - NODE_EXCLUSION_NOISE_STRENGTH * noise);

                        if (distance < modulatedRadius)
                        {
                            noSpawnZone[x, y] = true;
                        }
                    }
                }
            }

            foreach (var path in allPaths)
            {
                foreach (var point in path.RenderPoints)
                {
                    int minX = (int)Math.Max(0, point.X - TREE_EXCLUSION_RADIUS_PATH);
                    int maxX = (int)Math.Min(endX - 1, point.X + TREE_EXCLUSION_RADIUS_PATH);
                    int minY = (int)Math.Max(0, point.Y - TREE_EXCLUSION_RADIUS_PATH);
                    int maxY = (int)Math.Min(endY - 1, point.Y + TREE_EXCLUSION_RADIUS_PATH);

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            if (Vector2.DistanceSquared(new Vector2(x, y), point) < pathRadiusSq)
                            {
                                noSpawnZone[x, y] = true;
                            }
                        }
                    }
                }
            }

            // Generate trees
            for (int y = 0; y < endY; y += TREE_DENSITY_STEP)
            {
                for (int x = 0; x < endX; x += TREE_DENSITY_STEP)
                {
                    if (noSpawnZone[x, y]) continue;

                    float lushnessNoise = noiseManager.GetNoiseValue(NoiseMapType.Lushness, x * TREE_NOISE_SCALE, y * TREE_NOISE_SCALE);
                    if (lushnessNoise > TREE_PLACEMENT_THRESHOLD)
                    {
                        // Now check the bald spot noise layer
                        float baldnessNoise = (_baldSpotNoise.Noise(x * BALD_SPOT_NOISE_SCALE, y * BALD_SPOT_NOISE_SCALE) + 1f) * 0.5f; // Normalize to 0-1
                        if (baldnessNoise <= BALD_SPOT_THRESHOLD)
                        {
                            treePositions.Add(new Vector2(x, y));
                        }
                    }
                }
            }
            return treePositions;
        }

        // --- Line Intersection Helper ---
        private static int Orientation(Vector2 p, Vector2 q, Vector2 r)
        {
            float val = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
            if (Math.Abs(val) < 1e-10) return 0; // Collinear
            return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
        }

        private static bool OnSegment(Vector2 p, Vector2 q, Vector2 r)
        {
            return (q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) &&
                    q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y));
        }

        private static bool LineSegmentsIntersect(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            // Ignore common endpoints
            if (p1 == p2 || p1 == q2 || q1 == p2 || q1 == q2)
            {
                return false;
            }

            int o1 = Orientation(p1, q1, p2);
            int o2 = Orientation(p1, q1, q2);
            int o3 = Orientation(p2, q2, p1);
            int o4 = Orientation(p2, q2, q1);

            if (o1 != o2 && o3 != o4) return true;

            // Special Cases for collinear points
            if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
            if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
            if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
            if (o4 == 0 && OnSegment(p2, q1, q2)) return true;

            return false;
        }

        private static bool PathSegmentsIntersect(List<Vector2> path1, List<Vector2> path2)
        {
            if (path1.Count < 2 || path2.Count < 2) return false;

            for (int i = 0; i < path1.Count - 1; i++)
            {
                for (int j = 0; j < path2.Count - 1; j++)
                {
                    if (LineSegmentsIntersect(path1[i], path1[i + 1], path2[j], path2[j + 1]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}