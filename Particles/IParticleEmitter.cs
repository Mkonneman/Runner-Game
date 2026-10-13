using Microsoft.Xna.Framework;

namespace Runner_Game
{
    /// <summary>
    /// Something a particle system can follow and spawn particles from helpful for the player
    /// </summary>
    public interface IParticleEmitter
    {
        /// The emitter's position
        Vector2 Position { get; }

        /// The emitter's velocity
        Vector2 Velocity { get; }
    }
}
