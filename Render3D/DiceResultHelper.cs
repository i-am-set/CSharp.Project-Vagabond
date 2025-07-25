using Microsoft.Xna.Framework;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// A static helper class to determine the outcome of a die roll based on its
    /// final orientation in 3D space.
    /// </summary>
    public static class DiceResultHelper
    {
        /// <summary>
        /// Calculates the face value of a D6 that is pointing upwards and its alignment with the world's 'up' vector.
        /// </summary>
        /// <param name="orientation">The final world transformation matrix of the die.</param>
        /// <returns>A tuple containing the integer face value (1-6) and the alignment (dot product, 1.0 is perfectly flat).</returns>
        public static (int value, float alignment) GetUpFaceValueAndAlignment(Matrix orientation)
        {
            // This is the absolute "up" direction in the 3D world.
            var worldUp = Vector3.Up;

            // Define the die faces by their local axis and corresponding value.
            // This layout assumes a standard D6 where opposite faces sum to 7.
            // The orientation matrix transforms these local directions (e.g., the die's personal 'Up')
            // into their current direction in the world.
            // +Y (Up) is 6, so -Y (Down) must be 1.
            // +X (Right) is 3, so -X (Left) must be 4.
            // +Z (Backward) is 5, so -Z (Forward) must be 2.
            var faces = new[]
            {
                (axis: orientation.Up,       value: 6),
                (axis: orientation.Down,     value: 1),
                (axis: orientation.Right,    value: 3),
                (axis: orientation.Left,     value: 4),
                (axis: orientation.Backward, value: 5),
                (axis: orientation.Forward,  value: 2)
            };

            // We find the face whose local "up" direction is most aligned with the world's "up" direction.
            // The dot product of two normalized vectors gives a value from -1 to 1.
            // A value of 1 means they are pointing in the exact same direction (the face is perfectly flat).
            // A value close to 1 means it's nearly flat.
            // A value of 0 means they are perpendicular.
            var bestFace = faces[0];
            float maxDot = Vector3.Dot(bestFace.axis, worldUp);

            for (int i = 1; i < faces.Length; i++)
            {
                float currentDot = Vector3.Dot(faces[i].axis, worldUp);
                if (currentDot > maxDot)
                {
                    maxDot = currentDot;
                    bestFace = faces[i];
                }
            }

            return (bestFace.value, maxDot);
        }

        /// <summary>
        /// Calculates the face value of a D6 that is pointing upwards.
        /// This is a convenience method that ignores the alignment.
        /// </summary>
        /// <param name="orientation">The final world transformation matrix of the die.</param>
        /// <returns>An integer from 1 to 6 representing the top face.</returns>
        public static int GetUpFaceValue(Matrix orientation)
        {
            return GetUpFaceValueAndAlignment(orientation).value;
        }
    }
}