using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Text;
using System.Threading.Tasks;

namespace CollisionExercise.Collisions
{
    /// <summary>
    /// A Struct that represents a bounding circle for collision detection.
    /// </summary>
    public class BoundingCircle
    {
        /// <summary>
        /// The new bounding Circle
        /// </summary>
        public Vector2 Center;
        public float Radius;
        public BoundingCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// Determines whether this bounding circle intersects with another bounding circle.
        /// </summary>
        /// <param name="other">The bounding circle to test for intersection.</param>
        /// <returns>true if the bounding circles intersect; otherwise, false.</returns>
        public bool CollidesWith(BoundingCircle other)
        {
            return CollisionHelper.Collides(this, other);
        }
        public bool CollidesWith(BoundingRectangle other)
        {
            return CollisionHelper.Collides(this, other);
        }
    }
}
