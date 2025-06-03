using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public class NoiseMapManager
    {
        private Dictionary<NoiseMapType, NoiseLayerConfig> layerConfigs;
        private Dictionary<NoiseMapType, Dictionary<string, float>> noiseCache;
        private Dictionary<NoiseMapType, SeededPerlin> perlinGenerators;
        private const int CACHE_SIZE_LIMIT = 1000;

        public NoiseMapManager(int masterSeed = 12345)
        {
            layerConfigs = new Dictionary<NoiseMapType, NoiseLayerConfig>();
            noiseCache = new Dictionary<NoiseMapType, Dictionary<string, float>>();
            perlinGenerators = new Dictionary<NoiseMapType, SeededPerlin>();
        
            InitializeDefaultLayers(Environment.TickCount*RandomNumberGenerator.GetInt32(0, masterSeed));
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
                Lacunarity = 2.0f,
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
            layerConfigs[type] = config;
            if (!noiseCache.ContainsKey(type))
            {
                noiseCache[type] = new Dictionary<string, float>();
            }
        
            perlinGenerators[type] = new SeededPerlin(config.Seed);
        }

        public float GetNoiseValue(NoiseMapType type, float x, float y)
        {
            if (!layerConfigs.ContainsKey(type) || !layerConfigs[type].Enabled)
                return 0f;

            string cacheKey = $"{x},{y}";
            if (noiseCache[type].ContainsKey(cacheKey))
            {
                return noiseCache[type][cacheKey];
            }

            float value = GenerateLayeredNoise(layerConfigs[type], x, y);
        
            if (noiseCache[type].Count >= CACHE_SIZE_LIMIT)
            {
                noiseCache[type].Clear();
            }
        
            noiseCache[type][cacheKey] = value;
            return value;
        }

        private float GenerateLayeredNoise(NoiseLayerConfig config, float x, float y)
        {
            if (!perlinGenerators.ContainsKey(config.Type))
                return 0f;
    
            SeededPerlin perlin = perlinGenerators[config.Type];
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
            return new MapData
            {
                TerrainHeight = GetNoiseValue(NoiseMapType.TerrainHeight, x, y),
                Lushness = GetNoiseValue(NoiseMapType.Lushness, x, y),
                Temperature = GetNoiseValue(NoiseMapType.Temperature, x, y),
                Humidity = GetNoiseValue(NoiseMapType.Humidity, x, y),
                Resources = GetNoiseValue(NoiseMapType.Resources, x, y),
                Difficulty = GetNoiseValue(NoiseMapType.Difficulty, x, y)
            };
        }

        public void ClearCache()
        {
            foreach (var cache in noiseCache.Values)
            {
                cache.Clear();
            }
        }
    }
}
