using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace CollisionExercise.Collisions
{
    public static class CollisionHelper
    {
        /// <summary>
        /// Detects if two bounding circles collide.
        /// </summary>
        /// <param name="a">Circle A</param>
        /// <param name="b">Circle B</param>
        /// <returns>True for collision False for otherwise</returns>
        public static bool Collides(BoundingCircle a, BoundingCircle b)
        {
            return Math.Pow(a.Radius + b.Radius, 2) >=
                Math.Pow(a.Center.X - b.Center.X, 2) +
                Math.Pow(a.Center.Y - b.Center.Y, 2);
        }

        /// <summary>
        /// Detects if two bounding rectangles collide.
        /// </summary>
        /// <param name="a">Rec A</param>
        /// <param name="b">Rec B</param>
        /// <returns>True for collision False for otherwise</returns>
        public static bool Collides(BoundingRectangle a, BoundingRectangle b)
        {
            return !(a.Right < b.Left || a.Left > b.Right &&
                   a.Top > b.Bottom || a.Bottom < b.Top);
        }
        /// <summary>
        /// Detects if a bounding circle collides with a bounding rectangle.
        /// </summary>
        /// <param name="c">Circle A</param>
        /// <param name="r">Rec R</param>
        /// <returns>True for collision False for otherwise</returns>
        public static bool Collides(BoundingCircle c, BoundingRectangle r)
        {
            float nearestX = MathHelper.Clamp(c.Center.X, r.Left, r.Right); 
            float nearestY = MathHelper.Clamp(c.Center.Y, r.Top, r.Bottom);
            return Math.Pow(c.Radius, 2) >=
                Math.Pow(nearestX - c.Center.X, 2) + 
                Math.Pow(nearestY - c.Center.Y, 2);
        }

        public static bool Collides(BoundingRectangle r, BoundingCircle c)
        {
            return Collides(c, r);
        }
    }
}
