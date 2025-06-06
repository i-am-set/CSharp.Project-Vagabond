using System;

namespace ProjectVagabond
{
    public class NoiseGenerator
    {
    }

    public class FastNoiseLite
    {
        private int _seed;
        private float _frequency = 0.01f;
        private NoiseType _noiseType = NoiseType.Perlin;
        
        public enum NoiseType { Perlin }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetSeed(int seed) { _seed = seed; }
        public void SetFrequency(float frequency) { _frequency = frequency; }
        public void SetNoiseType(NoiseType noiseType) { _noiseType = noiseType; }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public float GetNoise(float x, float y)
        {
            float noise1 = (float)(Math.Sin(x * _frequency + _seed) * Math.Cos(y * _frequency + _seed));
            float noise2 = (float)(Math.Sin(x * _frequency * 2 + _seed * 2) * Math.Cos(y * _frequency * 2 + _seed * 2)) * 0.5f;
            float noise3 = (float)(Math.Sin(x * _frequency * 4 + _seed * 3) * Math.Cos(y * _frequency * 4 + _seed * 3)) * 0.25f;
            
            return (noise1 + noise2 + noise3) / 1.75f;
        }
    }
}
