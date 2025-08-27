using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;
using System.Security.Cryptography;

namespace ProjectVagabond.Particles
{
    /// <summary>
    /// Manages a 2D grid of forces that can influence particles.
    /// The field is generated using 3D Simplex noise, where the Z-axis represents time,
    /// creating a smoothly evolving, turbulent flow.
    /// </summary>
    public class VectorField
    {
        // A self-contained implementation of Simplex noise for generating the vector field.
        private static class SimplexNoise
        {
            private static readonly int[] grad3 = { 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0, 1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1, 0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1 };
            private static readonly int[] p;

            static SimplexNoise()
            {
                // The permutation array, doubled to 512 to avoid index wrapping
                p = new int[512];
                var perm = new int[256];
                for (int i = 0; i < 256; i++)
                    perm[i] = i;

                // Shuffle the permutation array using a Fisher-Yates shuffle.
                // This is more portable than RandomNumberGenerator.Shuffle which requires .NET 6+.
                var rng = new Random();
                for (int i = perm.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int temp = perm[i];
                    perm[i] = perm[j];
                    perm[j] = temp;
                }

                // Double the permutation array to avoid modulo operations
                for (int i = 0; i < 256; i++)
                {
                    p[i] = p[i + 256] = perm[i];
                }
            }

            private static int FastFloor(float x) => x > 0 ? (int)x : (int)x - 1;
            private static float Dot(int g, float x, float y, float z) => grad3[g * 3] * x + grad3[g * 3 + 1] * y + grad3[g * 3 + 2] * z;

            public static float Noise(float x, float y, float z)
            {
                const float F3 = 1.0f / 3.0f;
                const float G3 = 1.0f / 6.0f;

                float s = (x + y + z) * F3;
                int i = FastFloor(x + s);
                int j = FastFloor(y + s);
                int k = FastFloor(z + s);

                float t = (i + j + k) * G3;
                float X0 = i - t;
                float Y0 = j - t;
                float Z0 = k - t;

                float x0 = x - X0;
                float y0 = y - Y0;
                float z0 = z - Z0;

                int i1, j1, k1;
                int i2, j2, k2;

                if (x0 >= y0)
                {
                    if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
                    else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
                    else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
                }
                else
                {
                    if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
                    else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
                    else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
                }

                float x1 = x0 - i1 + G3;
                float y1 = y0 - j1 + G3;
                float z1 = z0 - k1 + G3;
                float x2 = x0 - i2 + 2.0f * G3;
                float y2 = y0 - j2 + 2.0f * G3;
                float z2 = z0 - k2 + 2.0f * G3;
                float x3 = x0 - 1.0f + 3.0f * G3;
                float y3 = y0 - 1.0f + 3.0f * G3;
                float z3 = z0 - 1.0f + 3.0f * G3;

                int ii = i & 255;
                int jj = j & 255;
                int kk = k & 255;

                int gi0 = p[ii + p[jj + p[kk]]] % 12;
                int gi1 = p[ii + i1 + p[jj + j1 + p[kk + k1]]] % 12;
                int gi2 = p[ii + i2 + p[jj + j2 + p[kk + k2]]] % 12;
                int gi3 = p[ii + 1 + p[jj + 1 + p[kk + 1]]] % 12;

                float t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
                float n0 = (t0 < 0) ? 0.0f : (float)Math.Pow(t0, 4) * Dot(gi0, x0, y0, z0);

                float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
                float n1 = (t1 < 0) ? 0.0f : (float)Math.Pow(t1, 4) * Dot(gi1, x1, y1, z1);

                float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
                float n2 = (t2 < 0) ? 0.0f : (float)Math.Pow(t2, 4) * Dot(gi2, x2, y2, z2);

                float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
                float n3 = (t3 < 0) ? 0.0f : (float)Math.Pow(t3, 4) * Dot(gi3, x3, y3, z3);

                return 32.0f * (n0 + n1 + n2 + n3);
            }
        }

        public Rectangle Bounds { get; }
        public Point GridSize { get; }
        public float NoiseScale { get; }
        public float ForceMagnitude { get; }
        public float TimeEvolutionSpeed { get; }
        public float UpwardBias { get; }

        private readonly Vector2[,] _field;
        private float _time;
        private readonly float _cellWidth;
        private readonly float _cellHeight;

        public VectorField(Rectangle bounds, Point gridSize, float noiseScale, float forceMagnitude, float timeEvolutionSpeed, float upwardBias)
        {
            Bounds = bounds;
            GridSize = gridSize;
            NoiseScale = noiseScale;
            ForceMagnitude = forceMagnitude;
            TimeEvolutionSpeed = timeEvolutionSpeed;
            UpwardBias = Math.Clamp(upwardBias, 0f, 1f);
            _field = new Vector2[gridSize.X, gridSize.Y];
            _cellWidth = (float)bounds.Width / (gridSize.X - 1);
            _cellHeight = (float)bounds.Height / (gridSize.Y - 1);
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime * TimeEvolutionSpeed;

            for (int y = 0; y < GridSize.Y; y++)
            {
                for (int x = 0; x < GridSize.X; x++)
                {
                    float angle = SimplexNoise.Noise(x * NoiseScale, y * NoiseScale, _time) * MathHelper.TwoPi;
                    var noiseVector = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    var upwardVector = new Vector2(0, -1); // Negative Y is up in screen coordinates

                    // Blend the noise with the upward bias. Normalizing ensures consistent magnitude.
                    var finalVector = Vector2.Lerp(noiseVector, upwardVector, UpwardBias);
                    if (finalVector.LengthSquared() > 0)
                    {
                        _field[x, y] = Vector2.Normalize(finalVector);
                    }
                    else
                    {
                        _field[x, y] = Vector2.Zero;
                    }
                }
            }
        }

        public Vector2 GetForceAt(Vector2 position)
        {
            // Translate world position to grid-local float coordinates
            float gridX = (position.X - Bounds.X) / _cellWidth;
            float gridY = (position.Y - Bounds.Y) / _cellHeight;

            // Clamp to be within the grid bounds
            gridX = Math.Clamp(gridX, 0, GridSize.X - 1.001f);
            gridY = Math.Clamp(gridY, 0, GridSize.Y - 1.001f);

            // Get integer grid indices for the four corners
            int x0 = (int)gridX;
            int y0 = (int)gridY;
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            // Get fractional parts for interpolation
            float tx = gridX - x0;
            float ty = gridY - y0;

            // Bilinear interpolation
            Vector2 top = Vector2.Lerp(_field[x0, y0], _field[x1, y0], tx);
            Vector2 bottom = Vector2.Lerp(_field[x0, y1], _field[x1, y1], tx);
            Vector2 interpolatedForce = Vector2.Lerp(top, bottom, ty);

            return interpolatedForce * ForceMagnitude;
        }

        public void DebugDraw(SpriteBatch spriteBatch)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            for (int y = 0; y < GridSize.Y; y++)
            {
                for (int x = 0; x < GridSize.X; x++)
                {
                    var startPos = new Vector2(Bounds.X + x * _cellWidth, Bounds.Y + y * _cellHeight);
                    var endPos = startPos + _field[x, y] * 10f; // Scale vector for visibility
                    spriteBatch.DrawLineSnapped(startPos, endPos, Color.Cyan * 0.5f);
                    spriteBatch.Draw(pixel, new Rectangle((int)startPos.X - 1, (int)startPos.Y - 1, 2, 2), Color.Yellow * 0.5f);
                }
            }
        }
    }
}