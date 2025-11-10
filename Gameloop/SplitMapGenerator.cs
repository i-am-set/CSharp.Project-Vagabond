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

        // --- Generation Tuning ---
        private const int MIN_NODES_PER_COLUMN = 2;
        private const int MAX_NODES_PER_COLUMN = 3;
        private const int COLUMN_WIDTH = 96; // 6 * GRID_SIZE
        private const int HORIZONTAL_PADDING = 64; // 4 * GRID_SIZE
        private const float BATTLE_EVENT_WEIGHT = 0.7f; // 70% chance for a battle
        private const float NARRATIVE_EVENT_WEIGHT = 0.3f; // 30% chance for a narrative
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float SECONDARY_PATH_CHANCE = 0.4f; // Chance for a node to have a second outgoing path
        private const float NODE_HORIZONTAL_VARIANCE_PIXELS = 20f;
        private const float MAX_CONNECTION_DISTANCE = 120f; // Max distance for a path to be considered "close"
        private const float PATH_SPLIT_POINT_MIN = 0.2f;
        private const float PATH_SPLIT_POINT_MAX = 0.8f;
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;
        private const float REWARD_NODE_CHANCE = 0.05f;
        private static readonly List<int> _validYPositions = new List<int>();

        // --- Tree Generation Tuning ---
        private const int TREE_DENSITY_STEP = 2; // Check for a tree every pixel for max density.
        private const float TREE_NOISE_SCALE = 8.0f; // Controls the size of clearings. Higher value = smaller, more frequent clearings.
        private const float TREE_PLACEMENT_THRESHOLD = 0.45f; // Noise value must be above this to place a tree. Higher value = more clearings.
        private const float TREE_EXCLUSION_RADIUS_NODE = 16f;
        private const float TREE_EXCLUSION_RADIUS_PATH = 8f;

        // --- Bald Spot Generation Tuning ---
        private const float BALD_SPOT_NOISE_SCALE = 0.1f; // Higher value = smaller, more frequent spots.
        private const float BALD_SPOT_THRESHOLD = 0.65f; // Noise must be ABOVE this to create a bald spot. Higher value = fewer spots.


        static SplitMapGenerator()
        {
            _baldSpotNoise = new SeededPerlin(_random.Next());

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

            // --- Final Assembly ---
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;

            if (startNodeId == -1) return null;

            GeneratePathRenderPoints(allPaths, allNodes);
            var bakedScenery = BakeTreesToTexture(allNodes, allPaths, mapWidth);

            return new SplitMap(allNodes, allPaths, bakedScenery, totalColumns, startNodeId, mapWidth);
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
                if (!availableYSlots.Any()) break; // Stop if we run out of valid, non-adjacent slots.

                float finalX = x + ((float)_random.NextDouble() * 2f - 1f) * NODE_HORIZONTAL_VARIANCE_PIXELS;

                // Pick a random available slot
                int ySlotIndexInAvailableList = _random.Next(availableYSlots.Count);
                int finalY = availableYSlots[ySlotIndexInAvailableList];

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
            var newPathsInThisColumn = new List<SplitMapPath>();
            var sortedPrev = previousColumn.OrderBy(n => n.Position.Y).ToList();
            var sortedNext = nextColumn.OrderBy(n => n.Position.Y).ToList();

            // Pass 1: Primary connections (each previous node connects to its closest valid next node)
            foreach (var prevNode in sortedPrev)
            {
                var bestTarget = sortedNext
                    .Select(target => new {
                        Node = target,
                        Distance = Vector2.DistanceSquared(prevNode.Position, target.Position),
                        Intersects = allExistingPaths.Concat(newPathsInThisColumn).Any(existing =>
                            LineSegmentsIntersect(prevNode.Position, target.Position,
                                allNodes.First(n => n.Id == existing.FromNodeId).Position,
                                allNodes.First(n => n.Id == existing.ToNodeId).Position))
                    })
                    .Where(item => !item.Intersects)
                    .OrderBy(item => item.Distance)
                    .FirstOrDefault()?.Node;

                if (bestTarget != null)
                {
                    var path = new SplitMapPath(prevNode.Id, bestTarget.Id);
                    newPathsInThisColumn.Add(path);
                    prevNode.OutgoingPathIds.Add(path.Id);
                    bestTarget.IncomingPathIds.Add(path.Id);
                }
            }

            // Pass 2: Ensure reachability for all nodes in the next column
            foreach (var nextNode in sortedNext)
            {
                if (!nextNode.IncomingPathIds.Any())
                {
                    var bestSource = sortedPrev
                        .Select(source => new {
                            Node = source,
                            Distance = Vector2.DistanceSquared(source.Position, nextNode.Position),
                            Intersects = allExistingPaths.Concat(newPathsInThisColumn).Any(existing =>
                                LineSegmentsIntersect(source.Position, nextNode.Position,
                                    allNodes.First(n => n.Id == existing.FromNodeId).Position,
                                    allNodes.First(n => n.Id == existing.ToNodeId).Position))
                        })
                        .Where(item => !item.Intersects)
                        .OrderBy(item => item.Distance)
                        .FirstOrDefault()?.Node;

                    if (bestSource != null)
                    {
                        var path = new SplitMapPath(bestSource.Id, nextNode.Id);
                        newPathsInThisColumn.Add(path);
                        bestSource.OutgoingPathIds.Add(path.Id);
                        nextNode.IncomingPathIds.Add(path.Id);
                    }
                }
            }

            // Pass 3: Secondary connections
            foreach (var prevNode in sortedPrev)
            {
                if (_random.NextDouble() < SECONDARY_PATH_CHANCE)
                {
                    var connectedNextNodeIds = prevNode.OutgoingPathIds.Select(id => allExistingPaths.Concat(newPathsInThisColumn).First(p => p.Id == id).ToNodeId).ToHashSet();
                    var availableTargets = sortedNext.Where(n => !connectedNextNodeIds.Contains(n.Id)).OrderBy(x => _random.Next()).ToList();

                    foreach (var targetNode in availableTargets)
                    {
                        bool intersects = allExistingPaths.Concat(newPathsInThisColumn).Any(existing =>
                            LineSegmentsIntersect(prevNode.Position, targetNode.Position,
                                allNodes.First(n => n.Id == existing.FromNodeId).Position,
                                allNodes.First(n => n.Id == existing.ToNodeId).Position));

                        if (!intersects)
                        {
                            var path = new SplitMapPath(prevNode.Id, targetNode.Id);
                            newPathsInThisColumn.Add(path);
                            prevNode.OutgoingPathIds.Add(path.Id);
                            targetNode.IncomingPathIds.Add(path.Id);
                            break; // Only add one secondary path per node
                        }
                    }
                }
            }

            return newPathsInThisColumn;
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
                    SplitMapPath? mainPath = outgoingPaths
                        .Select(p => new { Path = p, ToNode = allNodes.FirstOrDefault(n => n.Id == p.ToNodeId) })
                        .Where(x => x.ToNode != null)
                        .OrderBy(x => Math.Abs(x.ToNode.Position.Y - fromNode.Position.Y))
                        .FirstOrDefault()?.Path;

                    if (mainPath == null)
                    {
                        foreach (var path in outgoingPaths)
                        {
                            var toNode = allNodes.FirstOrDefault(n => n.Id == path.ToNodeId);
                            if (toNode != null)
                            {
                                path.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, toNode.Position, new List<int> { fromNode.Id, toNode.Id }, allNodes);
                            }
                        }
                        continue;
                    }

                    var mainPathToNode = allNodes.First(n => n.Id == mainPath.ToNodeId);
                    mainPath.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, mainPathToNode.Position, new List<int> { fromNode.Id, mainPathToNode.Id }, allNodes);

                    var branchPaths = outgoingPaths.Where(p => p != mainPath).ToList();
                    foreach (var branchPath in branchPaths)
                    {
                        var branchToNode = allNodes.FirstOrDefault(n => n.Id == branchPath.ToNodeId);
                        if (branchToNode == null) continue;

                        if (mainPath.RenderPoints.Count < 5)
                        {
                            branchPath.RenderPoints = GenerateWigglyPathPoints(fromNode.Position, branchToNode.Position, new List<int> { fromNode.Id, branchToNode.Id }, allNodes);
                            continue;
                        }

                        int minIndex = (int)(mainPath.RenderPoints.Count * PATH_SPLIT_POINT_MIN);
                        int maxIndex = (int)(mainPath.RenderPoints.Count * PATH_SPLIT_POINT_MAX);
                        if (minIndex >= maxIndex)
                        {
                            minIndex = 1;
                            maxIndex = mainPath.RenderPoints.Count - 2;
                        }
                        int splitIndex = _random.Next(minIndex, maxIndex);
                        Vector2 splitPoint = mainPath.RenderPoints[splitIndex];

                        var trunkPoints = mainPath.RenderPoints.Take(splitIndex + 1).ToList();
                        var branchIgnoreIds = new List<int> { fromNode.Id, branchToNode.Id };
                        var branchPoints = GenerateWigglyPathPoints(splitPoint, branchToNode.Position, branchIgnoreIds, allNodes);
                        branchPath.RenderPoints = trunkPoints.Concat(branchPoints.Skip(1)).ToList();
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
            float nodeRadiusSq = TREE_EXCLUSION_RADIUS_NODE * TREE_EXCLUSION_RADIUS_NODE;
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
                        if (Vector2.DistanceSquared(new Vector2(x, y), node.Position) < nodeRadiusSq)
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
    }
}
#nullable restore