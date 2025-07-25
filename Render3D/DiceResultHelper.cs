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
        /// Calculates the face value of a D6 that is pointing upwards.
        /// </summary>
        /// <param name="orientation">The final world transformation matrix of the die.</param>
        /// <returns>An integer from 1 to 6 representing the top face.</returns>
        public static int GetUpFaceValue(Matrix orientation)
        {
            var worldUp = Vector3.Up;

            // Define the die faces by their local axis and corresponding value.
            // This layout assumes a standard D6 where opposite faces sum to 7.
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

            // Find the face whose local "up" direction is most aligned with the world's "up" direction.
            // This is determined by finding the highest dot product.
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

            return bestFace.value;
        }
    }
}
