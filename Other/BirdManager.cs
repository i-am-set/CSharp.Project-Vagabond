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
        private const int FIXED_POOL_SIZE = 10;

        // Spawning Timing
        private const float SPAWN_INTERVAL_MIN = 1.5f;
        private const float SPAWN_INTERVAL_MAX = 4.0f;
        private float _spawnTimer = 0f;
        private float _nextSpawnInterval = 0f;

        // View Logic
        private const float VIEW_PADDING = 400f;

        private const float MAP_VIEW_HEIGHT = 82f;

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
        private const float VERTICAL_SPAWN_BUFFER = 15f;

        public BirdManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Initialize(SplitMap map, Vector2 playerPosition, Vector2 cameraOffset)
        {
            _flockPool.Clear();
            _spawnSideDeck.Clear();

            for (int i = 0; i < FIXED_POOL_SIZE; i++)
            {
                _flockPool.Add(new Flock());
            }

            // Initial Scatter Spawn
            for (int i = 0; i < 2; i++)
            {
                var flock = _flockPool[i];
                float randomX = (float)_random.NextDouble() * Global.VIRTUAL_WIDTH;
                // Adjust randomX to World Space based on camera
                float worldX = randomX - cameraOffset.X;

                int direction = _random.Next(2) == 0 ? 1 : -1;

                ActivateFlock(flock, playerPosition, cameraOffset, direction, worldXOverride: worldX);
            }

            ResetSpawnTimer();
        }

        public void Update(GameTime gameTime, SplitMap? map, Vector2 playerPosition, Vector2 cameraOffset)
        {
            if (map == null) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 1. Calculate Camera View Bounds (World Space)
            float worldViewLeft = -cameraOffset.X;
            float worldViewRight = worldViewLeft + Global.VIRTUAL_WIDTH;

            // 2. Spawn Logic
            _spawnTimer += dt;
            if (_spawnTimer >= _nextSpawnInterval)
            {
                var availableFlock = _flockPool.FirstOrDefault(f => !f.IsActive);

                if (availableFlock != null)
                {
                    int direction = GetNextSpawnDirection();

                    float spawnX = (direction == 1)
                        ? worldViewLeft - VIEW_PADDING
                        : worldViewRight + VIEW_PADDING;

                    ActivateFlock(availableFlock, playerPosition, cameraOffset, direction, worldXOverride: spawnX);

                    ResetSpawnTimer();
                }
            }

            // 3. Update & Cull Logic
            foreach (var flock in _flockPool)
            {
                if (!flock.IsActive) continue;

                bool anyBirdVisible = false;

                foreach (var bird in flock.Birds)
                {
                    if (!bird.IsActive) continue;

                    bird.Position.X += bird.Speed * flock.Direction * dt;
                    bird.BobTimer += dt;

                    bool inView = bird.Position.X > (worldViewLeft - VIEW_PADDING) &&
                                  bird.Position.X < (worldViewRight + VIEW_PADDING);

                    if (inView)
                    {
                        anyBirdVisible = true;
                    }
                    else
                    {
                        bird.IsActive = false;
                    }
                }

                if (!anyBirdVisible)
                {
                    flock.IsActive = false;
                }
            }
        }

        private void ResetSpawnTimer()
        {
            _spawnTimer = 0f;
            _nextSpawnInterval = (float)(_random.NextDouble() * (SPAWN_INTERVAL_MAX - SPAWN_INTERVAL_MIN) + SPAWN_INTERVAL_MIN);
        }

        private void ActivateFlock(Flock flock, Vector2 playerPosition, Vector2 cameraOffset, int direction, float? worldXOverride = null)
        {
            flock.Reset();
            flock.IsActive = true;
            flock.Direction = direction;

            int flockSize = _random.Next(3, 9);
            bool isVShape = _random.NextDouble() > 0.3;
            float flockBaseDepth = (float)(_random.NextDouble() * (MAX_DEPTH - MIN_DEPTH) + MIN_DEPTH);

            float depthRatio = (flockBaseDepth - MIN_DEPTH) / (MAX_DEPTH - MIN_DEPTH);
            float flockBaseSpeed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, depthRatio);

            // --- HEIGHT CALCULATION ---
            // 1. Pick a random SCREEN Y within the visible map strip
            float minScreenY = VERTICAL_SPAWN_BUFFER;
            float maxScreenY = MAP_VIEW_HEIGHT - VERTICAL_SPAWN_BUFFER;
            if (maxScreenY < minScreenY) maxScreenY = minScreenY;

            float targetScreenY = (float)(_random.NextDouble() * (maxScreenY - minScreenY) + minScreenY);

            // 2. Reverse Parallax to find WORLD Y
            // Formula: ScreenY = WorldY + (CameraY * Depth)
            // Therefore: WorldY = ScreenY - (CameraY * Depth)
            float spawnWorldY = targetScreenY - (cameraOffset.Y * flockBaseDepth);

            float leaderWorldX = worldXOverride ?? 0f;
            Vector2 leaderPos = new Vector2(leaderWorldX, spawnWorldY);

            for (int i = 0; i < flockSize; i++)
            {
                var bird = flock.Birds[i];
                bird.IsActive = true;
                // Slight depth variance per bird
                bird.Depth = flockBaseDepth + (float)(_random.NextDouble() * INTRA_FLOCK_DEPTH_VARIANCE * 2 - INTRA_FLOCK_DEPTH_VARIANCE);

                // Recalculate Y for individual bird depth to keep them visually planar if desired, 
                // or let them drift. Letting them drift with depth variance adds 3D feel.

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
                var options = new List<int> { -1, -1, 1, 1 };
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
                    // Final Screen Pos = WorldPos + CameraOffset + (CameraOffset * (Depth - 1))
                    //                  = WorldPos + (CameraOffset * Depth)
                    Vector2 parallaxOffset = cameraOffset * (bird.Depth - 1.0f);

                    float bobOffset = MathF.Sin(bird.BobTimer * bird.BobFrequency) > 0 ? -1f : 0f;

                    Vector2 drawPos = bird.Position + parallaxOffset;
                    drawPos.Y += bobOffset;

                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(0, -1), _global.Palette_Black); // Top
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(0, 1), _global.Palette_Black);  // Bottom
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(-1, 0), _global.Palette_Black); // Left
                    spriteBatch.DrawSnapped(pixel, drawPos + new Vector2(1, 0), _global.Palette_Black);  // Right

                    spriteBatch.DrawSnapped(pixel, drawPos, _global.Palette_Sun);
                }
            }
        }
    }
}