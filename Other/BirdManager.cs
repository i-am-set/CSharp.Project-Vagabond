#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Progression;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class BirdManager
    {
        private class Bird
        {
            public Vector2 Position; // World Space
            public float Speed;
            public float Depth; // Parallax factor (1.0 = map layer, >1.0 = closer to camera)
            public int Direction; // 1 for right, -1 for left

            // Animation
            public float BobTimer;
            public float BobFrequency;
        }

        private readonly List<Bird> _birds = new List<Bird>();
        private readonly Random _random = new Random();
        private readonly Global _global;

        // Tuning
        private const int MAX_BIRDS = 30; // Increased to accommodate flocks
        private const float FLOCK_SPAWN_CHANCE = 0.002f; // Chance per frame to spawn a flock

        // Speed Tuning
        private const float MIN_SPEED = 10f;
        private const float MAX_SPEED = 25f; // Reduced max speed

        // Parallax Tuning
        private const float MIN_DEPTH = 1.1f;
        private const float MAX_DEPTH = 1.5f;

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
            _birds.Clear();
            // Pre-warm: Spawn a couple of flocks randomly
            int initialFlocks = _random.Next(1, 3);
            for (int i = 0; i < initialFlocks; i++)
            {
                SpawnFlock(map, playerPosition, randomX: true);
            }
        }

        public void Update(GameTime gameTime, SplitMap? map, Vector2 playerPosition)
        {
            if (map == null) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 1. Spawn new flocks
            if (_birds.Count < MAX_BIRDS && _random.NextDouble() < FLOCK_SPAWN_CHANCE)
            {
                SpawnFlock(map, playerPosition, randomX: false);
            }

            // 2. Update existing birds
            for (int i = _birds.Count - 1; i >= 0; i--)
            {
                var bird = _birds[i];

                // Move Horizontally
                bird.Position.X += bird.Speed * bird.Direction * dt;

                // Update Bob Timer
                bird.BobTimer += dt;

                // Culling
                float cullMargin = 300f;

                bool outOfBoundsX = bird.Position.X < -cullMargin || bird.Position.X > map.MapWidth + cullMargin;
                bool outOfBoundsY = Math.Abs(bird.Position.Y - playerPosition.Y) > Global.VIRTUAL_HEIGHT * 1.5f;

                if (outOfBoundsX || outOfBoundsY)
                {
                    _birds.RemoveAt(i);
                }
            }
        }

        private void SpawnFlock(SplitMap map, Vector2 playerPosition, bool randomX)
        {
            int flockSize = _random.Next(3, 8); // 3 to 7 birds per flock
            bool isVShape = _random.NextDouble() > 0.4; // 60% chance for V-shape

            // Shared flock properties (so they move together)
            float depth = (float)(_random.NextDouble() * (MAX_DEPTH - MIN_DEPTH) + MIN_DEPTH);
            float speed = (float)(_random.NextDouble() * (MAX_SPEED - MIN_SPEED) + MIN_SPEED);
            int direction = _random.Next(2) == 0 ? 1 : -1;

            // Calculate safe spawn height range
            float safeHeight = Global.VIRTUAL_HEIGHT - (VERTICAL_SPAWN_BUFFER * 2);
            float halfSafeHeight = safeHeight / 2f;
            float spawnY = playerPosition.Y + (float)(_random.NextDouble() * safeHeight - halfSafeHeight);

            float spawnX;
            if (randomX)
            {
                spawnX = (float)_random.NextDouble() * map.MapWidth;
            }
            else
            {
                float margin = 100f; // Larger margin for flocks
                spawnX = direction == 1 ? -margin : map.MapWidth + margin;
            }

            Vector2 leaderPos = new Vector2(spawnX, spawnY);

            for (int i = 0; i < flockSize; i++)
            {
                var bird = new Bird();
                bird.Depth = depth;
                bird.Speed = speed;
                bird.Direction = direction;

                // Randomize bob slightly so they aren't perfectly synced
                bird.BobFrequency = (float)(_random.NextDouble() * (MAX_BOB_FREQ - MIN_BOB_FREQ) + MIN_BOB_FREQ);
                bird.BobTimer = (float)(_random.NextDouble() * Math.PI * 2);

                Vector2 offset = Vector2.Zero;

                if (isVShape)
                {
                    // V-Formation Logic
                    if (i > 0)
                    {
                        float spacingX = 8f;
                        float spacingY = 4f;
                        int row = (i + 1) / 2;
                        int side = i % 2 == 0 ? 1 : -1; // Alternate sides

                        // Offset X is behind the leader (negative direction)
                        offset.X = -direction * row * spacingX;
                        offset.Y = side * row * spacingY;

                        // Add a tiny bit of noise to make it look organic
                        offset.X += (float)(_random.NextDouble() * 2 - 1);
                        offset.Y += (float)(_random.NextDouble() * 2 - 1);
                    }
                }
                else
                {
                    // Random Bunch Logic
                    float bunchRadiusX = 20f;
                    float bunchRadiusY = 10f;
                    offset.X = (float)(_random.NextDouble() * 2 - 1) * bunchRadiusX;
                    offset.Y = (float)(_random.NextDouble() * 2 - 1) * bunchRadiusY;
                }

                bird.Position = leaderPos + offset;
                _birds.Add(bird);
            }
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var bird in _birds)
            {
                // Parallax Calculation
                Vector2 parallaxOffset = cameraOffset * (bird.Depth - 1.0f);

                // Bobbing Calculation: Strict 1-pixel toggle
                float bobOffset = MathF.Sin(bird.BobTimer * bird.BobFrequency) > 0 ? -1f : 0f;

                Vector2 drawPos = bird.Position + parallaxOffset;
                drawPos.Y += bobOffset;

                // 1x1 Bright White Dot
                spriteBatch.DrawSnapped(pixel, drawPos, _global.Palette_BrightWhite);
            }
        }
    }
}
#nullable restore