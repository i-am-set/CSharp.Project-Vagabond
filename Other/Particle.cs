using Microsoft.Xna.Framework;

namespace ProjectVagabond.Particles
{
    public struct Particle
    {
        public bool IsAlive;

        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Acceleration;

        public Color Color;
        public float Alpha;

        public float Size;
        public float Rotation;
        public float RotationSpeed;

        public float Age;
        public float Lifetime;

        public float StartSize;
        public float EndSize;

        /// <summary>
        /// Resets the particle to a default state, ready for emission.
        /// </summary>
        public void Reset()
        {
            IsAlive = false;
            Age = 0f;
            Lifetime = 0f;
        }
    }
}