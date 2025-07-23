using System;

namespace ProjectVagabond.Particles
{
    /// <summary>
    /// Represents a float value that can be either a constant or a random value within a specified range.
    /// </summary>
    public struct FloatRange
    {
        public float Min { get; set; }
        public float Max { get; set; }

        public FloatRange(float value)
        {
            Min = Max = value;
        }

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Gets a value from the range. If Min and Max are the same, it returns that value.
        /// Otherwise, it returns a random value between Min and Max.
        /// </summary>
        public float GetValue(Random random)
        {
            if (Min == Max)
            {
                return Min;
            }
            return (float)(random.NextDouble() * (Max - Min) + Min);
        }
    }
}