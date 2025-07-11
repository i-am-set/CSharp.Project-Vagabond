using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public enum NoiseMapType
    {
        TerrainHeight,
        Lushness,
        Temperature,
        Humidity,
        Resources,
        Difficulty
    }

    public class NoiseLayerConfig
    {
        public NoiseMapType Type { get; set; }
        public float Scale { get; set; } = 0.2f;
        public float Amplitude { get; set; } = 1.0f;
        public int Octaves { get; set; } = 1;
        public float Persistence { get; set; } = 0.5f;
        public float Lacunarity { get; set; } = 2.0f;
        public Vector2 Offset { get; set; } = Vector2.Zero;
        public bool Enabled { get; set; } = true;
        public int Seed { get; set; } = 0;
    }

    /// <summary>
    /// A pure data struct holding the raw noise values for a specific map coordinate.
    /// The interpretation of this data (e.g., terrain type, passability) is handled
    /// by other classes like GameState.
    /// </summary>
    public struct MapData
    {
        public float TerrainHeight { get; set; }
        public float Lushness { get; set; }
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public float Resources { get; set; }
        public float Difficulty { get; set; }

    }

    public class NoiseMapManager
    {
        private Dictionary<NoiseMapType, NoiseLayerConfig> _layerConfigs;
        private Dictionary<Vector2, MapData> _mapDataCache;
        private Dictionary<NoiseMapType, SeededPerlin> _perlinGenerators;
        private const int MAP_DATA_CACHE_LIMIT = 50000;

        public NoiseMapManager(int masterSeed = 999)
        {
            _layerConfigs = new Dictionary<NoiseMapType, NoiseLayerConfig>();
            _mapDataCache = new Dictionary<Vector2, MapData>();
            _perlinGenerators = new Dictionary<NoiseMapType, SeededPerlin>();

            InitializeDefaultLayers(Environment.TickCount * RandomNumberGenerator.GetInt32(1, masterSeed));
        }

        private void InitializeDefaultLayers(int masterSeed)
        {
            Random seedGenerator = new Random(masterSeed);

            AddLayer(NoiseMapType.TerrainHeight, new NoiseLayerConfig
            {
                Type = NoiseMapType.TerrainHeight,
                Scale = 0.1f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.5f,
                Lacunarity = 1.25f,
                Seed = seedGenerator.Next()
            });

            AddLayer(NoiseMapType.Lushness, new NoiseLayerConfig
            {
                Type = NoiseMapType.Lushness,
                Scale = 0.15f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.6f,
                Seed = seedGenerator.Next()
            });

            AddLayer(NoiseMapType.Temperature, new NoiseLayerConfig
            {
                Type = NoiseMapType.Temperature,
                Scale = 0.08f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.4f,
                Seed = seedGenerator.Next()
            });

            AddLayer(NoiseMapType.Humidity, new NoiseLayerConfig
            {
                Type = NoiseMapType.Humidity,
                Scale = 0.12f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.5f,
                Seed = seedGenerator.Next()
            });

            AddLayer(NoiseMapType.Resources, new NoiseLayerConfig
            {
                Type = NoiseMapType.Resources,
                Scale = 0.25f,
                Amplitude = 1.0f,
                Octaves = 4,
                Persistence = 0.7f,
                Seed = seedGenerator.Next()
            });

            AddLayer(NoiseMapType.Difficulty, new NoiseLayerConfig
            {
                Type = NoiseMapType.Difficulty,
                Scale = 0.18f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.3f,
                Seed = seedGenerator.Next()
            });
        }

        public void AddLayer(NoiseMapType type, NoiseLayerConfig config)
        {
            _layerConfigs[type] = config;
            _perlinGenerators[type] = new SeededPerlin(config.Seed);
        }

        public float GetNoiseValue(NoiseMapType type, float x, float y)
        {
            if (!_layerConfigs.TryGetValue(type, out var config) || !config.Enabled)
                return 0f;

            return GenerateLayeredNoise(config, x, y);
        }

        private float GenerateLayeredNoise(NoiseLayerConfig config, float x, float y)
        {
            var configType = config.Type;
            if (!_perlinGenerators.ContainsKey(configType))
                return 0f;

            SeededPerlin perlin = _perlinGenerators[configType];
            float totalValue = 0f;
            float totalWeight = 0f;

            float amplitude = config.Amplitude;
            float frequency = config.Scale;
            var offset = config.Offset;
            var octaves = config.Octaves;
            var persistence = config.Persistence;
            var lacunarity = config.Lacunarity;

            for (int octave = 0; octave < octaves; octave++)
            {
                float sampleX = (x + offset.X) * frequency;
                float sampleY = (y + offset.Y) * frequency;

                float octaveValue = perlin.Noise(sampleX, sampleY);
                totalValue += octaveValue * amplitude;
                totalWeight += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalizedValue = totalValue / totalWeight;

            return (normalizedValue + 1f) * 0.5f;
        }

        public MapData GetMapData(int x, int y)
        {
            var key = new Vector2(x, y);
            if (_mapDataCache.TryGetValue(key, out var cachedData))
            {
                return cachedData;
            }

            var newData = new MapData
            {
                TerrainHeight = GetNoiseValue(NoiseMapType.TerrainHeight, x, y),
                Lushness = GetNoiseValue(NoiseMapType.Lushness, x, y),
                Temperature = GetNoiseValue(NoiseMapType.Temperature, x, y),
                Humidity = GetNoiseValue(NoiseMapType.Humidity, x, y),
                Resources = GetNoiseValue(NoiseMapType.Resources, x, y),
                Difficulty = GetNoiseValue(NoiseMapType.Difficulty, x, y)
            };

            if (_mapDataCache.Count >= MAP_DATA_CACHE_LIMIT)
            {
                _mapDataCache.Clear();
            }

            _mapDataCache.Add(key, newData);
            return newData;
        }

        public void ClearCache()
        {
            _mapDataCache.Clear();
        }
    }

    public class SeededPerlin
    {
        private int[] p = new int[512];
        private int[] permutation = new int[256];

        public SeededPerlin(int seed)
        {
            Random rng = new Random(seed);

            for (int i = 0; i < 256; i++)
            {
                permutation[i] = i;
            }

            for (int i = permutation.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int temp = permutation[i];
                permutation[i] = permutation[j];
                permutation[j] = temp;
            }

            for (int i = 0; i < 256; i++)
            {
                p[i] = p[256 + i] = permutation[i];
            }
        }

        public float Noise(float x, float y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= (float)Math.Floor(x);
            y -= (float)Math.Floor(y);

            float u = Fade(x);
            float v = Fade(y);

            int A = p[X] + Y;
            int B = p[X + 1] + Y;

            return Lerp(v, Lerp(u, Grad(p[A], x, y), Grad(p[B], x - 1, y)),
            Lerp(u, Grad(p[A + 1], x, y - 1), Grad(p[B + 1], x - 1, y - 1)));
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);
        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}