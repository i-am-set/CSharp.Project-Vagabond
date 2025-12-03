#nullable enable
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
            public int Direction; // 1 for right, -1 for left
            public float BobTimer;
            public float BobFrequency;
        }

        private class Flock
        {
            public List<Bird> Birds = new List<Bird>();
        }

        private readonly List<Flock> _flocks = new List<Flock>();
        private readonly Random _random = new Random();
        private readonly Global _global;

        // --- Tuning ---
        private const int TARGET_FLOCK_COUNT = 6;

        // Despawn buffer: 10 pixels passed the edge as requested.
        private const float DESPAWN_BUFFER = 10f;

        // Spawn buffer: Spawn further out so they don't "pop" in if the camera is near the edge.
        private const float SPAWN_BUFFER = 150f;

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
        private const float VERTICAL_SPAWN_BUFFER = 10f;

        public BirdManager()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Initialize(SplitMap map, Vector2 playerPosition)
        {
            _flocks.Clear();

            // Initial Spawn: Spread evenly across the entire map
            for (int i = 0; i < TARGET_FLOCK_COUNT; i++)
            {
                float randomX = (float)_random.NextDouble() * map.MapWidth;
                SpawnFlock(map, playerPosition, randomX);
            }
        }

        public void Update(GameTime gameTime, SplitMap? map, Vector2 playerPosition)
        {
            if (map == null) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 1. Update existing flocks
            for (int i = _flocks.Count - 1; i >= 0; i--)
            {
                var flock = _flocks[i];
                bool flockFinished = true;

                foreach (var bird in flock.Birds)
                {
                    // Move
                    bird.Position.X += bird.Speed * bird.Direction * dt;
                    bird.BobTimer += dt;

                    // Check if THIS bird is still valid based on its direction
                    bool birdFinished = false;
                    if (bird.Direction > 0) // Moving Right
                    {
                        if (bird.Position.X > map.MapWidth + DESPAWN_BUFFER) birdFinished = true;
                    }
                    else // Moving Left
                    {
                        if (bird.Position.X < -DESPAWN_BUFFER) birdFinished = true;
                    }

                    // If any bird is still flying, the flock is not finished
                    if (!birdFinished)
                    {
                        flockFinished = false;
                    }
                }

                if (flockFinished)
                {
                    _flocks.RemoveAt(i);
                }
            }

            // 2. Maintain Population
            while (_flocks.Count < TARGET_FLOCK_COUNT)
            {
                SpawnFlockAtMapEdge(map, playerPosition);
            }
        }

        private void SpawnFlockAtMapEdge(SplitMap map, Vector2 playerPosition)
        {
            // Randomly pick left or right side of the *entire map*
            bool spawnLeft = _random.Next(2) == 0;

            // Spawn well outside the map boundaries so they fly in
            float spawnX = spawnLeft ? -SPAWN_BUFFER : map.MapWidth + SPAWN_BUFFER;

            SpawnFlock(map, playerPosition, spawnX);
        }

        private void SpawnFlock(SplitMap map, Vector2 playerPosition, float spawnX)
        {
            int flockSize = _random.Next(3, 9);
            bool isVShape = _random.NextDouble() > 0.3;

            float flockBaseDepth = (float)(_random.NextDouble() * (MAX_DEPTH - MIN_DEPTH) + MIN_DEPTH);

            // Speed based on depth
            float depthRatio = (flockBaseDepth - MIN_DEPTH) / (MAX_DEPTH - MIN_DEPTH);
            float flockBaseSpeed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, depthRatio);

            // Direction Logic: Move towards the furthest end
            float distToLeft = spawnX;
            float distToRight = map.MapWidth - spawnX;
            int direction = (distToRight > distToLeft) ? 1 : -1;

            // Calculate safe spawn height range
            float safeHeight = Global.VIRTUAL_HEIGHT - (VERTICAL_SPAWN_BUFFER * 2);
            float halfSafeHeight = safeHeight / 2f;
            float spawnY = playerPosition.Y + (float)(_random.NextDouble() * safeHeight - halfSafeHeight);

            Vector2 leaderPos = new Vector2(spawnX, spawnY);
            var newFlock = new Flock();

            for (int i = 0; i < flockSize; i++)
            {
                var bird = new Bird();
                bird.Depth = flockBaseDepth + (float)(_random.NextDouble() * INTRA_FLOCK_DEPTH_VARIANCE * 2 - INTRA_FLOCK_DEPTH_VARIANCE);
                bird.Speed = flockBaseSpeed * (float)(1.0 + (_random.NextDouble() * 0.1 - 0.05));
                bird.Direction = direction;
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
                newFlock.Birds.Add(bird);
            }

            _flocks.Add(newFlock);
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var flock in _flocks)
            {
                foreach (var bird in flock.Birds)
                {
                    // Parallax Calculation
                    Vector2 parallaxOffset = cameraOffset * (bird.Depth - 1.0f);

                    // Bobbing Calculation
                    float bobOffset = MathF.Sin(bird.BobTimer * bird.BobFrequency) > 0 ? -1f : 0f;

                    Vector2 drawPos = bird.Position + parallaxOffset;
                    drawPos.Y += bobOffset;

                    // 1x1 Bright White Dot
                    spriteBatch.DrawSnapped(pixel, drawPos, _global.Palette_BrightWhite);
                }
            }
        }
    }
}
#nullable restore