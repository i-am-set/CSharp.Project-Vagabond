using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using BepuNumeric = System.Numerics;

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
        /// <param name="vertices">The local-space vertices of the die's collider, used for robust D4 checking.</param>
        /// <returns>A tuple containing the integer face value and the alignment (dot product, 1.0 is perfectly flat).</returns>
        public static (int value, float alignment) GetFaceValueAndAlignment(DieType dieType, Matrix orientation, List<BepuNumeric.Vector3> vertices = null)
        {
            switch (dieType)
            {
                case DieType.D4:
                    // If vertices are provided, use the new robust method. Otherwise, fall back to the old one.
                    if (vertices != null && vertices.Any())
                    {
                        return GetD4ValueAndAlignmentByVertices(orientation, vertices);
                    }
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
        /// <param name="vertices">The local-space vertices of the die's collider, used for robust D4 checking.</param>
        /// <returns>An integer representing the resulting face.</returns>
        public static int GetFaceValue(DieType dieType, Matrix orientation, List<BepuNumeric.Vector3> vertices = null)
        {
            return GetFaceValueAndAlignment(dieType, orientation, vertices).value;
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

        /// <summary>
        /// Calculates the face value and alignment of a D4 by analyzing the world position of its collider vertices.
        /// This method is more robust for checking if the die is resting flat on a face.
        /// </summary>
        private static (int value, float alignment) GetD4ValueAndAlignmentByVertices(Matrix orientation, List<BepuNumeric.Vector3> localVertices)
        {
            var global = ServiceLocator.Get<Global>();

            // 1. Transform all local vertices into world space, keeping track of the original local vertex.
            var worldVertices = localVertices.Select(lv => new
            {
                Local = new Vector3(lv.X, lv.Y, lv.Z), // Convert to XNA Vector3
                World = Vector3.Transform(new Vector3(lv.X, lv.Y, lv.Z), orientation)
            }).ToList();

            // 2. Sort vertices by their world Y coordinate to find the lowest three and the top one.
            var sortedVertices = worldVertices.OrderBy(v => v.World.Y).ToList();

            var lowestThree = sortedVertices.Take(3).ToList();
            var topVertex = sortedVertices.Last();

            // 3. Calculate alignment based on the flatness of the bottom three vertices.
            float minY = lowestThree.Min(v => v.World.Y);
            float maxY = lowestThree.Max(v => v.World.Y);
            float deltaY = maxY - minY;

            // If the vertical distance between the lowest 3 points is below a threshold, it's flat.
            float alignment = (deltaY < global.DiceD4FlatnessThreshold) ? 1.0f : 0.0f;

            // 4. Determine the face value. The value of the bottom face is determined by the vertex pointing up.
            // We find which of the canonical D4 face normals is most aligned with our top vertex's local position vector.
            var topVertexLocalNormalized = Vector3.Normalize(topVertex.Local);

            var bestFace = D4Faces[0];
            float maxDot = Vector3.Dot(topVertexLocalNormalized, bestFace.axis);

            for (int i = 1; i < D4Faces.Length; i++)
            {
                var currentFace = D4Faces[i];
                float currentDot = Vector3.Dot(topVertexLocalNormalized, currentFace.axis);
                if (currentDot > maxDot)
                {
                    maxDot = currentDot;
                    bestFace = currentFace;
                }
            }

            return (bestFace.value, alignment);
        }
    }
}