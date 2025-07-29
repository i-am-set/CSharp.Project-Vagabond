using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Dice
{
    /// <summary>
    /// A static helper class to determine the outcome of a die roll based on its
    /// final orientation in 3D space.
    /// </summary>
    public static class DiceResultHelper
    {
        // Pre-calculated face normals for a D4 (tetrahedron).
        // These assume a model where the vertices are at positions like (1,1,1), (1,-1,-1), etc.
        // The normal of a face is the vector pointing away from the opposite vertex.
        private static readonly (Vector3 axis, int value)[] D4Faces;

        static DiceResultHelper()
        {
            // The value 1/sqrt(3) is used to normalize vectors like (1,1,1).
            float invSqrt3 = 1f / (float)Math.Sqrt(3);
            D4Faces = new[]
            {
                (axis: new Vector3( 1,  1,  1) * invSqrt3, value: 1),
                (axis: new Vector3( 1, -1, -1) * invSqrt3, value: 2),
                (axis: new Vector3(-1,  1, -1) * invSqrt3, value: 3),
                (axis: new Vector3(-1, -1,  1) * invSqrt3, value: 4)
            };
        }

        /// <summary>
        /// Calculates the face value and alignment for a die of a specific type.
        /// </summary>
        /// <param name="dieType">The type of die (D6, D4, etc.).</param>
        /// <param name="orientation">The final world transformation matrix of the die.</param>
        /// <returns>A tuple containing the integer face value and the alignment (dot product, 1.0 is perfectly flat).</returns>
        public static (int value, float alignment) GetFaceValueAndAlignment(DieType dieType, Matrix orientation)
        {
            switch (dieType)
            {
                case DieType.D4:
                    return GetD4DownFaceValueAndAlignment(orientation);
                case DieType.D6:
                default:
                    return GetD6UpFaceValueAndAlignment(orientation);
            }
        }

        /// <summary>
        /// Calculates the face value of a die of a specific type.
        /// This is a convenience method that ignores the alignment.
        /// </summary>
        /// <param name="dieType">The type of die (D6, D4, etc.).</param>
        /// <param name="orientation">The final world transformation matrix of the die.</param>
        /// <returns>An integer representing the resulting face.</returns>
        public static int GetFaceValue(DieType dieType, Matrix orientation)
        {
            return GetFaceValueAndAlignment(dieType, orientation).value;
        }

        /// <summary>
        /// Calculates the face value of a D6 that is pointing upwards and its alignment with the world's 'up' vector.
        /// </summary>
        private static (int value, float alignment) GetD6UpFaceValueAndAlignment(Matrix orientation)
        {
            var worldUp = Vector3.Up;

            // Define the D6 faces by their local axis and corresponding value.
            var faces = new[]
            {
                (axis: orientation.Up,       value: 6),
                (axis: orientation.Down,     value: 1),
                (axis: orientation.Right,    value: 3),
                (axis: orientation.Left,     value: 4),
                (axis: orientation.Backward, value: 5),
                (axis: orientation.Forward,  value: 2)
            };

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
        /// Calculates the face value of a D4 that is pointing downwards and its alignment with the world's 'down' vector.
        /// </summary>
        private static (int value, float alignment) GetD4DownFaceValueAndAlignment(Matrix orientation)
        {
            // For a tetrahedron, the result is the face on the bottom.
            // So we check for alignment with the world's "down" direction.
            var worldDown = Vector3.Down;

            // Transform the D4's local face normals into world space.
            var transformedAxis = Vector3.TransformNormal(D4Faces[0].axis, orientation);
            var bestFace = D4Faces[0];
            float maxDot = Vector3.Dot(transformedAxis, worldDown);

            for (int i = 1; i < D4Faces.Length; i++)
            {
                transformedAxis = Vector3.TransformNormal(D4Faces[i].axis, orientation);
                float currentDot = Vector3.Dot(transformedAxis, worldDown);
                if (currentDot > maxDot)
                {
                    maxDot = currentDot;
                    bestFace = D4Faces[i];
                }
            }

            return (bestFace.value, maxDot);
        }
    }
}