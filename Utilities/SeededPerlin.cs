#nullable enable
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A self-contained implementation of classic 2D Perlin noise with a settable seed.
    /// </summary>
    public class SeededPerlin
    {
        private readonly int[] p = new int[512];

        public SeededPerlin(int seed)
        {
            var rng = new Random(seed);
            var permutation = new int[256];
            for (int i = 0; i < 256; i++)
            {
                permutation[i] = i;
            }

            for (int i = permutation.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
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