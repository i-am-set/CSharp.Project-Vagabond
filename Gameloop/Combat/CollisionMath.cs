using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Utils
{
    public static class CollisionMath
    {
        public static bool CircleIntersectsCircle(Vector2 center1, float radius1, Vector2 center2, float radius2)
        {
            float distSq = Vector2.DistanceSquared(center1, center2);
            float radSum = radius1 + radius2;
            return distSq <= (radSum * radSum);
        }

        public static bool PointInOBB(Vector2 point, Vector2 obbOrigin, Vector2 obbDirection, float width, float length)
        {
            Vector2 d = point - obbOrigin;

            float forwardDist = Vector2.Dot(d, obbDirection);

            if (forwardDist < 0 || forwardDist > length) return false;

            Vector2 right = new Vector2(-obbDirection.Y, obbDirection.X);
            float lateralDist = Math.Abs(Vector2.Dot(d, right));

            return lateralDist <= (width / 2f);
        }
    }
}