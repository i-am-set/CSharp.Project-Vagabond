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
        private static readonly Random _random = new Random();
        private static readonly SeededPerlin _baldSpotNoise;
        private static readonly SeededPerlin _nodeExclusionNoise;
        // --- Generation Tuning ---
        private const int MIN_NODES_PER_COLUMN = 2;
        private const int MAX_NODES_PER_COLUMN = 3;
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

        private static readonly List<(SplitNodeType type, float weight)> _nodeTypeWeights = new()
    {
        (SplitNodeType.Narrative, 20f),
        (SplitNodeType.Village, 8f),
        (SplitNodeType.Cottage, 10f),
        (SplitNodeType.WatchPost, 4f),
        (SplitNodeType.Farm, 5f),
        (SplitNodeType.Church, 3f),
        (SplitNodeType.Town, 2f),
        (SplitNodeType.WizardTower, 1f),
        (SplitNodeType.GuardOutpost, 2f),
        (SplitNodeType.Kingdom, 0.5f)
    };

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
            allNodesByColumn.Add(startNodes);

            // --- Intermediate Columns ---
            for (int i = 1; i < totalColumns - 1; i++)
            {
                var previousColumn = allNodesByColumn[i - 1];
                Vector2 previousColumnAvgPos = previousColumn.Any() ? new Vector2(previousColumn.Average(n => n.Position.X), previousColumn.Average(n => n.Position.Y)) : startNodePosition;
                var newNodes = PlaceNodesForColumn(i, totalColumns, mapWidth, previousColumn.Count, previousColumnAvgPos);
                allNodesByColumn.Add(newNodes);
                AssignEvents(newNodes, splitData, totalColumns);
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
            allNodesByColumn.Add(endNodes);

            // --- Connect All Columns ---
            var allNodes = allNodesByColumn.SelectMany(c => c).ToList();
            for (int i = 0; i < allNodesByColumn.Count - 1; i++)
            {
                var newPaths = ConnectColumnPair(allNodesByColumn[i], allNodesByColumn[i + 1], allPaths, allNodes);
                allPaths.AddRange(newPaths);
            }

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

        private static void AssignEvents(List<SplitMapNode> nodesToAssign, SplitData splitData, int totalColumns)
        {
            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            var availableChoices = new List<(SplitNodeType type, float weight)>(_nodeTypeWeights);

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
                    // New and existing non-event nodes fall through here
                    case SplitNodeType.Reward:
                    case SplitNodeType.Kingdom:
                    case SplitNodeType.Town:
                    case SplitNodeType.Village:
                    case SplitNodeType.Church:
                    case SplitNodeType.Farm:
                    case SplitNodeType.Cottage:
                    case SplitNodeType.GuardOutpost:
                    case SplitNodeType.WizardTower:
                    case SplitNodeType.WatchPost:
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
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, -1), global.Palette_DarkerGray); // Top
                spriteBatch.DrawSnapped(pixel, treePos, global.Palette_DarkestGray);      // Middle
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, 1), global.Palette_DarkestGray); // Bottom
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
#nullable restore