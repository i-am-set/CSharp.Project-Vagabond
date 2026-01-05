using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class BirdManager
    {
        private class Bird
        {
            public Vector2 Position; // World Space
            public float Speed;
            public float Depth; // Parallax factor
            public float BobTimer;
            public float BobFrequency;
            public bool IsActive;

            public void Reset()
            {
                Position = Vector2.Zero;
                Speed = 0;
                Depth = 1;
                BobTimer = 0;
                BobFrequency = 1;
                IsActive = false;
            }
        }

        private class Flock
        {
            public List<Bird> Birds = new List<Bird>();
            public bool IsActive;
            public int Direction; // 1 for right, -1 for left

            public Flock()
            {
                // Pre-allocate max birds per flock to avoid resizing
                for (int i = 0; i < 10; i++) Birds.Add(new Bird());
            }

            public void Reset()
            {
                IsActive = false;
                Direction = 0;
                foreach (var bird in Birds) bird.Reset();
            }
        }

        private readonly List<Flock> _flockPool = new List<Flock>();
        private readonly Queue<int> _spawnSideDeck = new Queue<int>();
        private readonly Random _random = new Random();
        private readonly Global _global;

        // --- Tuning ---
        // Calculated based on Forest Split: (10 columns * 96 width) + (128 padding) = 992px
        private const float BASELINE_MAP_WIDTH = 992f;
        private const int BASELINE_FLOCK_COUNT = 6;

        private const float MAP_EDGE_BUFFER = 50f; // Pixels off-map (world space) before spawning/despawning

        // Speed Tuning
        private const float MIN_SPEED = 10f;
        private const float MAX_SPEED = 25f;

        // Parallax Tuning
        private const float MIN_DEPTH = 1.1f;
        private const float MAX_DEPTH = 2.5f;
        private const float INTRA_FLOCK_DEPTH_VARIANCE = 0.05f;

        // Bobbing Tuning
        private const float MIN_BOB_FREQ = 8f;
        private const float MAX_BOB_FREQ = 12f;

        // Spawn Tuning
        private const float VERTICAL_SPAWN_BUFFER = 20f;

        public BirdManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Initialize(SplitMap map, Vector2 playerPosition)
        {
            _flockPool.Clear();
            _spawnSideDeck.Clear();

            // --- DYNAMIC POOL SIZING ---
            // Calculate how many flocks we need to maintain the same density as the Forest split.
            float widthRatio = map.MapWidth / BASELINE_MAP_WIDTH;
            int targetFlockCount = (int)Math.Ceiling(BASELINE_FLOCK_COUNT * widthRatio);

            // Ensure at least one flock exists
            targetFlockCount = Math.Max(1, targetFlockCount);

            // 1. Create the Object Pool based on dynamic size
            for (int i = 0; i < targetFlockCount; i++)
            {
                _flockPool.Add(new Flock());
            }

            // 2. Initial Scatter Spawn
            // Spawn them randomly across the map width so the sky isn't empty at start.
            foreach (var flock in _flockPool)
            {
                float randomX = (float)_random.NextDouble() * map.MapWidth;
                // Random direction for initial spawn
                int direction = _random.Next(2) == 0 ? 1 : -1;
                ActivateFlock(flock, playerPosition, Vector2.Zero, direction, worldXOverride: randomX);
            }
        }

        public void Update(GameTime gameTime, SplitMap? map, Vector2 playerPosition, Vector2 cameraOffset)
        {
            if (map == null) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var flock in _flockPool)
            {
                if (!flock.IsActive)
                {
                    // Respawn Logic
                    int direction = GetNextSpawnDirection();

                    // Calculate World Spawn X
                    // If moving Right (1), spawn on Left map edge (-Buffer)
                    // If moving Left (-1), spawn on Right map edge (MapWidth + Buffer)
                    float worldSpawnX = (direction == 1) ? -MAP_EDGE_BUFFER : (map.MapWidth + MAP_EDGE_BUFFER);

                    ActivateFlock(flock, playerPosition, cameraOffset, direction, worldXOverride: worldSpawnX);
                    continue;
                }

                bool anyBirdVisible = false;

                foreach (var bird in flock.Birds)
                {
                    if (!bird.IsActive) continue;

                    // Move
                    bird.Position.X += bird.Speed * flock.Direction * dt;
                    bird.BobTimer += dt;

                    // --- CULLING LOGIC ---
                    // Check if bird is still within the WORLD MAP bounds (plus buffer).
                    bool isValid;
                    if (flock.Direction > 0) // Moving Right
                    {
                        // Valid if it hasn't passed the far right edge of the map
                        isValid = bird.Position.X < (map.MapWidth + MAP_EDGE_BUFFER);
                    }
                    else // Moving Left
                    {
                        // Valid if it hasn't passed the far left edge of the map
                        isValid = bird.Position.X > -MAP_EDGE_BUFFER;
                    }

                    if (isValid)
                    {
                        anyBirdVisible = true;
                    }
                    else
                    {
                        bird.IsActive = false; // Cull individual bird
                    }
                }

                // If all birds in the flock have left the map boundaries, deactivate the flock so it can be respawned
                if (!anyBirdVisible)
                {
                    flock.IsActive = false;
                }
            }
        }

        private void ActivateFlock(Flock flock, Vector2 playerPosition, Vector2 cameraOffset, int direction, float? worldXOverride = null, float? visualXOverride = null)
        {
            flock.Reset();
            flock.IsActive = true;
            flock.Direction = direction;

            int flockSize = _random.Next(3, 9);
            bool isVShape = _random.NextDouble() > 0.3;
            float flockBaseDepth = (float)(_random.NextDouble() * (MAX_DEPTH - MIN_DEPTH) + MIN_DEPTH);

            // Speed based on depth (further away = slower parallax movement, but we scale speed to look consistent)
            float depthRatio = (flockBaseDepth - MIN_DEPTH) / (MAX_DEPTH - MIN_DEPTH);
            float flockBaseSpeed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, depthRatio);

            // Calculate safe spawn height range
            float safeHeight = Global.VIRTUAL_HEIGHT - (VERTICAL_SPAWN_BUFFER * 2);
            float halfSafeHeight = safeHeight / 2f;
            float spawnY = playerPosition.Y + (float)(_random.NextDouble() * safeHeight - halfSafeHeight);

            // Determine Leader Position
            float leaderWorldX;

            if (worldXOverride.HasValue)
            {
                // Direct world spawn (used for map edge spawning and initialization)
                leaderWorldX = worldXOverride.Value;
            }
            else if (visualXOverride.HasValue)
            {
                // Reverse parallax calculation (kept for compatibility, though unused in full-map mode)
                float parallaxOffset = cameraOffset.X * (flockBaseDepth - 1.0f);
                leaderWorldX = visualXOverride.Value - parallaxOffset;
            }
            else
            {
                leaderWorldX = 0; // Fallback
            }

            Vector2 leaderPos = new Vector2(leaderWorldX, spawnY);

            for (int i = 0; i < flockSize; i++)
            {
                var bird = flock.Birds[i];
                bird.IsActive = true;
                bird.Depth = flockBaseDepth + (float)(_random.NextDouble() * INTRA_FLOCK_DEPTH_VARIANCE * 2 - INTRA_FLOCK_DEPTH_VARIANCE);
                bird.Speed = flockBaseSpeed * (float)(1.0 + (_random.NextDouble() * 0.1 - 0.05));
                bird.BobFrequency = (float)(_random.NextDouble() * (MAX_BOB_FREQ - MIN_BOB_FREQ) + MIN_BOB_FREQ);
                bird.BobTimer = (float)(_random.NextDouble() * Math.PI * 2);

                Vector2 offset = Vector2.Zero;

                if (isVShape)
                {
                    if (i > 0)
                    {
                        float spacingX = 8f;
                        float spacingY = 5f;
                        int row = (i + 1) / 2;
                        int side = i % 2 == 0 ? 1 : -1;

                        // Offset X is behind the leader (negative direction)
                        offset.X = -direction * row * spacingX;
                        offset.Y = side * row * spacingY;

                        offset.X += (float)(_random.NextDouble() * 2 - 1);
                        offset.Y += (float)(_random.NextDouble() * 2 - 1);
                    }
                }
                else
                {
                    float bunchRadiusX = 25f;
                    float bunchRadiusY = 15f;
                    offset.X = (float)(_random.NextDouble() * 2 - 1) * bunchRadiusX;
                    offset.Y = (float)(_random.NextDouble() * 2 - 1) * bunchRadiusY;
                }

                bird.Position = leaderPos + offset;
            }
        }

        private int GetNextSpawnDirection()
        {
            if (_spawnSideDeck.Count == 0)
            {
                // Refill deck with balanced options (e.g., 2 Left, 2 Right)
                var options = new List<int> { -1, -1, 1, 1 };

                // Shuffle
                int n = options.Count;
                while (n > 1)
                {
                    n--;
                    int k = _random.Next(n + 1);
                    int value = options[k];
                    options[k] = options[n];
                    options[n] = value;
                }

                foreach (var opt in options) _spawnSideDeck.Enqueue(opt);
            }

            return _spawnSideDeck.Dequeue();
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var flock in _flockPool)
            {
                if (!flock.IsActive) continue;

                foreach (var bird in flock.Birds)
                {
                    if (!bird.IsActive) continue;

                    // Parallax Calculation
                    Vector2 parallaxOffset = cameraOffset * (bird.Depth - 1.0f);

                    // Bobbing Calculation
                    float bobOffset = MathF.Sin(bird.BobTimer * bird.BobFrequency) > 0 ? -1f : 0f;

                    Vector2 drawPos = bird.Position + parallaxOffset;
                    drawPos.Y += bobOffset;

                    // Draw Outline (Black)
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(0, -1), _global.Palette_Black); // Top
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(0, 1), _global.Palette_Black);  // Bottom
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(-1, 0), _global.Palette_Black); // Left
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(1, 0), _global.Palette_Black);  // Right

                    // 1x1 Bright White Dot
                    spriteBatch.DrawSnapped(pixel, drawPos, _global.Palette_BlueWhite);
                }
            }
        }
    }
}