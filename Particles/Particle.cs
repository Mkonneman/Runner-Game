using Microsoft.Xna.Framework;

namespace Runner_Game
{
    /// <summary>
    /// A single particle, holds inportant data about its traits
    /// </summary>
    public class Particle
    {
        /// The current position of the particle
        public Vector2 Position;

        /// The current velocity of the particle
        public Vector2 Velocity;

        /// The current acceleration of the particle
        public Vector2 Acceleration;

        /// The current rotation of the particle
        public float Rotation;

        /// The current angular velocity of the particle
        public float AngularVelocity;

        /// The current angular acceleration of the particle
        public float AngularAcceleration;

        /// The scale of the particle
        public float Scale = 1.0f;

        /// The lifetime of the particle in seconds
        public float Lifetime;

        /// The time since the particle was initialized
        public float TimeSinceStart;

        /// The color (And the Alpha) of the particle
        public Color Color;

        /// The color the particle started with, so systems can fade it without losing the tint 
        public Color BaseColor;

        /// If this particle is still alive and should be rendered
        public bool Active => TimeSinceStart < Lifetime;

        /// <summary>
        /// Sets the particle up for first use, restoring defaults
        /// </summary>
        public void Initialize(Vector2 where)
        {
            Position = where;
            Velocity = Vector2.Zero;
            Acceleration = Vector2.Zero;
            Rotation = 0;
            AngularVelocity = 0;
            AngularAcceleration = 0;
            Scale = 1;
            Color = Color.White;
            BaseColor = Color.White;
            Lifetime = 1;
            TimeSinceStart = 0f;
        }

        /// <summary>
        /// Sets the particle up for first use with the given values
        /// </summary>
        public void Initialize(Vector2 position, Vector2 velocity, Vector2 acceleration,
            Color color, float lifetime = 1, float scale = 1, float rotation = 0,
            float angularVelocity = 0, float angularAcceleration = 0)
        {
            Position = position;
            Velocity = velocity;
            Acceleration = acceleration;
            Color = color;
            BaseColor = color;
            Lifetime = lifetime;
            Scale = scale;
            Rotation = rotation;
            AngularVelocity = angularVelocity;
            AngularAcceleration = angularAcceleration;
            TimeSinceStart = 0f;
        }
    }
}
