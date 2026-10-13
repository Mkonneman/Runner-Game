using System;
using Microsoft.Xna.Framework;

namespace Runner_Game
{
    /// <summary>
    /// Shared random number helpers for the particle systems
    /// </summary>
    public static class RandomHelper
    {
        private static readonly Random random = new();

        /// A random int in [min, max]
        public static int Next(int min, int max) => random.Next(min, max + 1);

        /// Random float in [min, max)
        public static float NextFloat(float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// A random unit-length direction
        public static Vector2 RandomDirection()
        {
            float angle = NextFloat(0, MathHelper.TwoPi);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        /// A random position inside a rectangle 
        public static Vector2 RandomPosition(Rectangle bounds) => new Vector2(NextFloat(bounds.Left, bounds.Right), NextFloat(bounds.Top, bounds.Bottom));
    }
}
