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
    public static class SplitMapGenerator
    {
        private static readonly Random _random = new Random();
        private static readonly SeededPerlin _baldSpotNoise;
        private static readonly SeededPerlin _nodeExclusionNoise;
        private static readonly SeededPerlin _pathWiggleNoise;
        public const int COLUMN_WIDTH = 64;
        public const int HORIZONTAL_PADDING = 64;
        private const float PATH_SEGMENT_LENGTH = 5f;
        private const float PATH_MAX_OFFSET = 4f;
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;

        private const int TREE_DENSITY_STEP = 2;
        private const float TREE_NOISE_SCALE = 8.0f;
        private const float TREE_PLACEMENT_THRESHOLD = 0.45f;
        private const float TREE_EXCLUSION_RADIUS_NODE = 15f;
        private const float TREE_EXCLUSION_RADIUS_PATH = 5f;

        private const float BALD_SPOT_NOISE_SCALE = 0.1f;
        private const float BALD_SPOT_THRESHOLD = 0.65f;

        private const float NODE_EXCLUSION_NOISE_SCALE = 2.5f;
        private const float NODE_EXCLUSION_NOISE_STRENGTH = 0.4f;

        static SplitMapGenerator()
        {
            _baldSpotNoise = new SeededPerlin(_random.Next());
            _nodeExclusionNoise = new SeededPerlin(_random.Next());
            _pathWiggleNoise = new SeededPerlin(_random.Next());
        }

        public static SplitMap? GenerateInitial(SplitData splitData)
        {
            SplitMapNode.ResetIdCounter();
            SplitMapPath.ResetIdCounter();

            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            const int totalColumns = 11;
            float mapWidth = (10 * COLUMN_WIDTH) + HORIZONTAL_PADDING * 2;

            const float WORLD_Y_CENTER = 91f;
            const float NODE_VERTICAL_GAP = 24f;

            var allNodes = new List<SplitMapNode>();
            var allPaths = new List<SplitMapPath>();
            var nodesByColumn = new Dictionary<int, List<SplitMapNode>>();

            // --- 1. GENERATE NODES ---
            for (int col = 0; col < totalColumns; col++)
            {
                nodesByColumn[col] = new List<SplitMapNode>();
                int nodeCount = (col == 0 || col == totalColumns - 1) ? 1 : _random.Next(2, 4);

                // Add +-4 pixel horizontal variance
                float x = HORIZONTAL_PADDING + (col * COLUMN_WIDTH) + _random.Next(-4, 5);

                float totalHeight = (nodeCount - 1) * NODE_VERTICAL_GAP;
                float startY = WORLD_Y_CENTER - (totalHeight / 2f);

                // Create a weighted pool for this column to guarantee no duplicates
                var pool = new List<(SplitNodeType Type, BattleDifficulty Diff, int Weight)>();
                pool.Add((SplitNodeType.Rest, BattleDifficulty.Normal, 10));
                pool.Add((SplitNodeType.Recruit, BattleDifficulty.Normal, 10));

                if (col <= 3)
                {
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Easy, 60));
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Normal, 20));
                }
                else if (col <= 6)
                {
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Easy, 15));
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Normal, 50));
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Hard, 15));
                }
                else
                {
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Normal, 30));
                    pool.Add((SplitNodeType.Battle, BattleDifficulty.Hard, 50));
                }

                for (int i = 0; i < nodeCount; i++)
                {
                    float y = startY + (i * NODE_VERTICAL_GAP);
                    var position = new Vector2(x, y);
                    var node = new SplitMapNode(col, position);

                    if (col == 0)
                    {
                        node.NodeType = SplitNodeType.Origin;
                    }
                    else if (col == totalColumns - 1)
                    {
                        node.NodeType = SplitNodeType.MajorBattle;
                        node.EventData = progressionManager.GetRandomMajorBattle();
                    }
                    else
                    {
                        // Weighted random selection without replacement
                        int totalWeight = pool.Sum(p => p.Weight);
                        int roll = _random.Next(totalWeight);
                        int currentWeight = 0;
                        int selectedIndex = 0;

                        for (int p = 0; p < pool.Count; p++)
                        {
                            currentWeight += pool[p].Weight;
                            if (roll < currentWeight)
                            {
                                selectedIndex = p;
                                break;
                            }
                        }

                        var selected = pool[selectedIndex];
                        pool.RemoveAt(selectedIndex); // Remove to prevent duplicates in this column

                        node.NodeType = selected.Type;
                        node.Difficulty = selected.Diff;

                        if (node.NodeType == SplitNodeType.Battle)
                        {
                            node.EventData = progressionManager.GetRandomBattle(node.Difficulty);
                        }
                    }

                    allNodes.Add(node);
                    nodesByColumn[col].Add(node);
                }
            }

            // --- 2. GENERATE PATHS ---
            for (int col = 0; col < totalColumns - 1; col++)
            {
                var currentNodes = nodesByColumn[col];
                var nextNodes = nodesByColumn[col + 1];

                int i = 0;
                int j = 0;
                CreatePath(currentNodes[i], nextNodes[j], allPaths, allNodes);

                bool hasOutgoingBranch = false;

                while (i < currentNodes.Count - 1 || j < nextNodes.Count - 1)
                {
                    bool canMoveI = i < currentNodes.Count - 1;
                    bool canMoveJ = j < nextNodes.Count - 1;

                    if (canMoveI && canMoveJ)
                    {
                        int r = _random.Next(3);
                        if (!hasOutgoingBranch && j == nextNodes.Count - 2) r = 1;

                        if (r == 0) i++;
                        else if (r == 1) { j++; hasOutgoingBranch = true; }
                        else { i++; j++; }
                    }
                    else if (canMoveI) i++;
                    else if (canMoveJ) { j++; hasOutgoingBranch = true; }

                    CreatePath(currentNodes[i], nextNodes[j], allPaths, allNodes);
                }
            }

            int startNodeId = nodesByColumn[0].First().Id;

            GeneratePathRenderPoints(allPaths, allNodes);
            var bakedScenery = BakeTreesToTexture(allNodes, allPaths, mapWidth);

            return new SplitMap(allNodes, allPaths, bakedScenery, totalColumns, startNodeId, mapWidth);
        }

        private static void CreatePath(SplitMapNode from, SplitMapNode to, List<SplitMapPath> allPaths, List<SplitMapNode> allNodes)
        {
            if (from.OutgoingPathIds.Any(pid => allPaths.Any(p => p.Id == pid && p.ToNodeId == to.Id))) return;

            var path = new SplitMapPath(from.Id, to.Id);
            path.RenderPoints = GenerateWigglyPathPoints(from.Position, to.Position, new List<int> { from.Id, to.Id }, allNodes, allPaths);
            from.OutgoingPathIds.Add(path.Id);
            to.IncomingPathIds.Add(path.Id);
            allPaths.Add(path);
        }

        private static void GeneratePathRenderPoints(List<SplitMapPath> paths, List<SplitMapNode> allNodes)
        {
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

        private static List<Vector2> GenerateWigglyPathPoints(Vector2 start, Vector2 end, List<int> ignoreNodeIds, List<SplitMapNode> allNodes, List<SplitMapPath> existingPaths)
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

            float pathSeed = (float)_random.NextDouble() * 1000f;

            for (int i = 1; i < numSegments; i++)
            {
                float progress = (float)i / numSegments;
                var pointOnLine = start + direction * progress * totalDistance;

                float noiseVal = _pathWiggleNoise.Noise(progress * 4f, pathSeed);
                float smoothOffset = noiseVal * PATH_MAX_OFFSET;

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
                            totalRepulsion += repulsionVector * (NODE_REPULSION_STRENGTH * Easing.EaseOutQuad(falloff));
                        }
                    }
                }

                const float PATH_REPULSION_RADIUS = 12f;
                const float PATH_REPULSION_STRENGTH = 6f;
                foreach (var existingPath in existingPaths)
                {
                    foreach (var p in existingPath.RenderPoints)
                    {
                        float dist = Vector2.Distance(pointOnLine, p);
                        if (dist < PATH_REPULSION_RADIUS && dist > 0.1f)
                        {
                            Vector2 repVec = pointOnLine - p;
                            repVec.Normalize();
                            float falloff = 1.0f - (dist / PATH_REPULSION_RADIUS);
                            totalRepulsion += repVec * (PATH_REPULSION_STRENGTH * falloff);
                        }
                    }
                }

                var finalPoint = pointOnLine + perpendicular * smoothOffset * taper + totalRepulsion * taper;
                points.Add(finalPoint);
            }

            points.Add(end);
            return points;
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

            foreach (var treePos in treePositions.OrderBy(p => p.Y))
            {
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, -1), global.Palette_DarkShadow);
                spriteBatch.DrawSnapped(pixel, treePos, global.Palette_Black);
                spriteBatch.DrawSnapped(pixel, treePos + new Vector2(0, 1), global.Palette_Black);
            }

            spriteBatch.End();

            graphicsDevice.SetRenderTarget(null);

            return renderTarget;
        }

        private static List<Vector2> GenerateTrees(List<SplitMapNode> allNodes, List<SplitMapPath> allPaths, float mapWidth)
        {
            var treePositions = new List<Vector2>();
            var noiseManager = ServiceLocator.Get<NoiseMapManager>();

            int endX = (int)mapWidth + HORIZONTAL_PADDING;
            int endY = Global.VIRTUAL_HEIGHT;

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

            for (int y = 0; y < endY; y += TREE_DENSITY_STEP)
            {
                for (int x = 0; x < endX; x += TREE_DENSITY_STEP)
                {
                    if (noSpawnZone[x, y]) continue;

                    float lushnessNoise = noiseManager.GetNoiseValue(NoiseMapType.Lushness, x * TREE_NOISE_SCALE, y * TREE_NOISE_SCALE);
                    if (lushnessNoise > TREE_PLACEMENT_THRESHOLD)
                    {
                        float baldnessNoise = (_baldSpotNoise.Noise(x * BALD_SPOT_NOISE_SCALE, y * BALD_SPOT_NOISE_SCALE) + 1f) * 0.5f;
                        if (baldnessNoise <= BALD_SPOT_THRESHOLD)
                        {
                            treePositions.Add(new Vector2(x, y));
                        }
                    }
                }
            }
            return treePositions;
        }
    }
}
