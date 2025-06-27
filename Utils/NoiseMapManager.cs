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

    public struct MapData
    {
        public float TerrainHeight { get; set; }
        public float Lushness { get; set; }
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public float Resources { get; set; }
        public float Difficulty { get; set; }

        private float waterLevel = Global.Instance.WaterLevel;
        private float flatlandsLevel = Global.Instance.FlatlandsLevel;
        private float hillsLevel = Global.Instance.HillsLevel;
        private float mountainsLevel = Global.Instance.MountainsLevel;

        public MapData()
        {
        }

        public readonly string TerrainType => GetTerrainType(TerrainHeight);
        public readonly char TerrainSymbol => GetTerrainSymbol(TerrainHeight);
        public readonly string VegetationType => GetVegetationType(Lushness, Temperature, Humidity);
        public readonly int EnergyCost => GetTerrainEnergyCost(TerrainHeight);

        private readonly string GetTerrainType(float height)
        {
            return height switch
            {
                var h when h < waterLevel => "WATER",
                var h when h < flatlandsLevel => "FLATLANDS",
                var h when h < hillsLevel => "HILLS",
                var h when h < mountainsLevel => "MOUNTAINS",
                _ => "PEAKS"
            };
        }

        private readonly int GetTerrainEnergyCost(float height)
        {
            return height switch
            {
                var h when h < waterLevel => 3,
                var h when h < flatlandsLevel => 1,
                var h when h < hillsLevel => 2,
                var h when h < mountainsLevel => 4,
                _ => 5
            };
        }

        private readonly char GetTerrainSymbol(float height)
        {
            return height switch
            {
                var h when h < waterLevel => '░',
                var h when h < flatlandsLevel => '·',
                var h when h < hillsLevel => '^',
                var h when h < mountainsLevel => 'n',
                _ => 'A'
            };
        }

        private readonly string GetVegetationType(float lushness, float temp, float humidity)
        {
            if (lushness < 0.1f) return "BARREN";
            if (temp < 0.3f) return "TUNDRA";
            if (humidity < 0.2f && temp > 0.7f) return "DESERT";

            return lushness switch
            {
                < 0.2f => "SPARSE",
                < 0.5f => "GRASSLAND",
                < 0.8f => "FOREST",
                _ => "JUNGLE"
            };
        }
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
            if (!_layerConfigs.ContainsKey(type) || !_layerConfigs[type].Enabled)
                return 0f;

            return GenerateLayeredNoise(_layerConfigs[type], x, y);
        }

        private float GenerateLayeredNoise(NoiseLayerConfig config, float x, float y)
        {
            if (!_perlinGenerators.ContainsKey(config.Type))
                return 0f;
    
            SeededPerlin perlin = _perlinGenerators[config.Type];
            float totalValue = 0f;
            float totalWeight = 0f;
            float amplitude = config.Amplitude;
            float frequency = config.Scale;

            for (int octave = 0; octave < config.Octaves; octave++)
            {
                float sampleX = (x + config.Offset.X) * frequency;
                float sampleY = (y + config.Offset.Y) * frequency;
    
                float octaveValue = perlin.Noise(sampleX, sampleY);
                totalValue += octaveValue * amplitude;
                totalWeight += amplitude;

                amplitude *= config.Persistence;
                frequency *= config.Lacunarity;
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

    // Keep the existing FastNoiseLite for backward compatibility if needed
    public class FastNoiseLite
    {
        private int _seed;
        private float _frequency = 0.01f;
        private NoiseType _noiseType = NoiseType.Perlin;
        
        public enum NoiseType { Perlin }

        public void SetSeed(int seed) { _seed = seed; }
        public void SetFrequency(float frequency) { _frequency = frequency; }
        public void SetNoiseType(NoiseType noiseType) { _noiseType = noiseType; }

        public float GetNoise(float x, float y)
        {
            return PerlinNoise(x * _frequency, y * _frequency);
        }

        private float PerlinNoise(float x, float y)
        {
            // Get grid coordinates
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            // Get interpolation weights
            float sx = x - x0;
            float sy = y - y0;

            // Apply smoothstep for better interpolation
            sx = Smoothstep(sx);
            sy = Smoothstep(sy);

            // Get gradient vectors for each corner
            float n0 = DotGridGradient(x0, y0, x, y);
            float n1 = DotGridGradient(x1, y0, x, y);
            float ix0 = Lerp(n0, n1, sx);

            n0 = DotGridGradient(x0, y1, x, y);
            n1 = DotGridGradient(x1, y1, x, y);
            float ix1 = Lerp(n0, n1, sx);

            return Lerp(ix0, ix1, sy);
        }

        private float DotGridGradient(int ix, int iy, float x, float y)
        {
            // Get pseudo-random gradient vector
            var gradient = GetGradient(ix, iy);
            
            // Calculate distance vector
            float dx = x - ix;
            float dy = y - iy;

            // Calculate dot product
            return dx * gradient.x + dy * gradient.y;
        }

        private (float x, float y) GetGradient(int ix, int iy)
        {
            // Use the seed to create different gradient patterns
            uint hash = Hash(ix, iy, _seed);
            
            // Convert hash to angle (0 to 2π)
            float angle = (hash & 0xFF) * (2.0f * (float)Math.PI) / 255.0f;
            
            return ((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        private uint Hash(int x, int y, int seed)
        {
            // Simple hash function that incorporates the seed
            uint hash = (uint)seed;
            hash = hash * 374761393U + (uint)x;
            hash = hash * 668265263U + (uint)y;
            hash = (hash ^ (hash >> 13)) * 1274126177U;
            return hash ^ (hash >> 16);
        }

        private float Smoothstep(float t)
        {
            return t * t * (3.0f - 2.0f * t);
        }

        private float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }
    }
}