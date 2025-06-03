using Microsoft.Xna.Framework;
using System;

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

        private const float waterLevel = 0.3f;
        private const float flatlandsLevel = 0.6f;
        private const float hillsLevel = 0.7f;
        private const float mountainsLevel = 0.8f;

        public readonly string TerrainType => GetTerrainType(TerrainHeight);
        public readonly char TerrainSymbol => GetTerrainSymbol(TerrainHeight);
        public readonly string VegetationType => GetVegetationType(Lushness, Temperature, Humidity);
        public readonly int EnergyCost => GetTerrainEnergyCost(TerrainHeight);

        private readonly string GetTerrainType(float height)
        {
            return height switch
            {
                < waterLevel => "WATER",
                < flatlandsLevel => "FLATLANDS",
                < hillsLevel => "HILLS",
                < mountainsLevel => "MOUNTAINS",
                _ => "PEAKS"
            };
        }

        private readonly int GetTerrainEnergyCost(float height)
        {
            return height switch
            {
                < waterLevel => 3,
                < flatlandsLevel => 1,
                < hillsLevel => 2,
                < mountainsLevel => 4,
                _ => 5
            };
        }

        private readonly char GetTerrainSymbol(float height)
        {
            return height switch
            {
                < waterLevel => '~',
                < flatlandsLevel => '.',
                < hillsLevel => '^',
                < mountainsLevel => 'A',
                _ => 'M'
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
