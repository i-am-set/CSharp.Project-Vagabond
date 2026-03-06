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

        public static bool RectangleIntersectsCircle(Rectangle rect, Vector2 center, float radius)
        {
            float closestX = Math.Clamp(center.X, rect.Left, rect.Right);
            float closestY = Math.Clamp(center.Y, rect.Top, rect.Bottom);
            float distanceX = center.X - closestX;
            float distanceY = center.Y - closestY;
            return (distanceX * distanceX + distanceY * distanceY) <= (radius * radius);
        }

        public static bool AABBIntersectsOBB(Rectangle rect, Vector2 obbOrigin, Vector2 obbDirection, float width, float length)
        {
            Vector2 center = new Vector2(rect.Center.X, rect.Center.Y);
            return PointInOBB(center, obbOrigin, obbDirection, width + rect.Width, length + rect.Height);
        }
    }
}