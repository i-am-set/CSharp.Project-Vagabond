using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public enum NoiseMapType
    {
        Lushness,
        Resources
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

    public class NoiseMapManager
    {
        private Dictionary<NoiseMapType, NoiseLayerConfig> _layerConfigs;
        private Dictionary<NoiseMapType, SeededPerlin> _perlinGenerators;

        public NoiseMapManager(int masterSeed = 999)
        {
            _layerConfigs = new Dictionary<NoiseMapType, NoiseLayerConfig>();
            _perlinGenerators = new Dictionary<NoiseMapType, SeededPerlin>();

            InitializeDefaultLayers(Environment.TickCount * RandomNumberGenerator.GetInt32(1, masterSeed));
        }

        private void InitializeDefaultLayers(int masterSeed)
        {
            Random seedGenerator = new Random(masterSeed);

            AddLayer(NoiseMapType.Lushness, new NoiseLayerConfig
            {
                Type = NoiseMapType.Lushness,
                Scale = 0.15f,
                Amplitude = 1.0f,
                Octaves = 2,
                Persistence = 0.6f,
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
    }
}
