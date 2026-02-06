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
    /// A static class responsible for procedurally generating a linear map for a split.
    /// </summary>
    public static class SplitMapGenerator
    {
        private static readonly Random _random = new Random();
        private static readonly SeededPerlin _baldSpotNoise;
        private static readonly SeededPerlin _nodeExclusionNoise;

        // --- Visual Tuning ---
        public const int COLUMN_WIDTH = 96; // 6 * GRID_SIZE
        public const int HORIZONTAL_PADDING = 64; // 4 * GRID_SIZE
        private const float PATH_SEGMENT_LENGTH = 10f; // Smaller value = more wiggles
        private const float PATH_MAX_OFFSET = 5f; // Max perpendicular deviation
        private const float NODE_REPULSION_RADIUS = 30f;
        private const float NODE_REPULSION_STRENGTH = 15f;

        // --- Tree Generation Tuning ---
        private const int TREE_DENSITY_STEP = 2; // Check for a tree every pixel for max density.
        private const float TREE_NOISE_SCALE = 8.0f; // Controls the size of clearings.
        private const float TREE_PLACEMENT_THRESHOLD = 0.45f; // Noise value must be above this to place a tree.
        private const float TREE_EXCLUSION_RADIUS_NODE = 20f;
        private const float TREE_EXCLUSION_RADIUS_PATH = 8f;

        // --- Bald Spot Generation Tuning ---
        private const float BALD_SPOT_NOISE_SCALE = 0.1f;
        private const float BALD_SPOT_THRESHOLD = 0.65f;

        // --- Node Exclusion Zone Tuning ---
        private const float NODE_EXCLUSION_NOISE_SCALE = 2.5f;
        private const float NODE_EXCLUSION_NOISE_STRENGTH = 0.4f;

        static SplitMapGenerator()
        {
            _baldSpotNoise = new SeededPerlin(_random.Next());
            _nodeExclusionNoise = new SeededPerlin(_random.Next());
        }

        public static SplitMap? GenerateInitial(SplitData splitData)
        {
            SplitMapNode.ResetIdCounter();
            SplitMapPath.ResetIdCounter();

            var progressionManager = ServiceLocator.Get<ProgressionManager>();

            // Fixed linear structure: 11 columns (0 to 10)
            const int totalColumns = 11;
            float mapWidth = (10 * COLUMN_WIDTH) + HORIZONTAL_PADDING * 2;
            float centerY = Global.VIRTUAL_HEIGHT / 2f;

            var allNodes = new List<SplitMapNode>();
            var allPaths = new List<SplitMapPath>();

            // Generate Nodes linearly
            for (int col = 0; col < totalColumns; col++)
            {
                float x = HORIZONTAL_PADDING + (col * COLUMN_WIDTH);
                var position = new Vector2(x, centerY);
                var node = new SplitMapNode(col, position);

                // Assign Type and Data
                if (col == 0)
                {
                    node.NodeType = SplitNodeType.Origin;
                }
                else if (col == totalColumns - 1) // Column 10
                {
                    node.NodeType = SplitNodeType.MajorBattle;
                    node.EventData = progressionManager.GetRandomMajorBattle();
                }
                else
                {
                    node.NodeType = SplitNodeType.Battle;

                    // Difficulty Progression
                    if (col <= 3) node.Difficulty = BattleDifficulty.Easy;
                    else if (col <= 6) node.Difficulty = BattleDifficulty.Normal;
                    else node.Difficulty = BattleDifficulty.Hard;

                    node.EventData = progressionManager.GetRandomBattle(node.Difficulty);
                }

                allNodes.Add(node);

                // Create Path from previous node
                if (col > 0)
                {
                    var prevNode = allNodes[col - 1];
                    var path = new SplitMapPath(prevNode.Id, node.Id);

                    // Generate visual points
                    // We pass an empty list for ignoreNodeIds because in a linear map we don't need complex exclusion logic,
                    // but we pass allNodes so the wiggle logic can still respect general repulsion if we ever add nearby props.
                    path.RenderPoints = GenerateWigglyPathPoints(prevNode.Position, node.Position, new List<int> { prevNode.Id, node.Id }, allNodes);

                    prevNode.OutgoingPathIds.Add(path.Id);
                    node.IncomingPathIds.Add(path.Id);
                    allPaths.Add(path);
                }
            }

            // Final Assembly
            int startNodeId = allNodes.FirstOrDefault(n => n.Floor == 0)?.Id ?? -1;
            if (startNodeId == -1) return null;

            GeneratePathRenderPoints(allPaths, allNodes);
            var bakedScenery = BakeTreesToTexture(allNodes, allPaths, mapWidth);

            return new SplitMap(allNodes, allPaths, bakedScenery, totalColumns, startNodeId, mapWidth);
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
    }
}