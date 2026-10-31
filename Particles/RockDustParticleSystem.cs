using Microsoft.Xna.Framework;

namespace Runner_Game
{
    /// <summary>
    /// Dust that trails off the top of falling rocks, tinted to match each rock.
    /// </summary>
    public class RockDustParticleSystem : ParticleSystem
    {
        // Color for the particles currently being added
        private Color _tint = Color.White;

        public RockDustParticleSystem(Game game) : base(game, 400) { }

        protected override void InitializeConstants()
        {
            textureFilename = "Images/Particles/particle";
            minNumParticles = 1;
            maxNumParticles = 1;

            // Overlapping dust layers into soft clouds
            DrawOrder = AlphaBlendDrawOrder;
        }

        /// <summary>
        /// Adds a puff of dust at a rock, in that rock's color
        /// </summary>
        public void PlaceDust(Vector2 where, Color tint)
        {
            _tint = tint;
            AddParticles(where);
        }

        protected override void InitializeParticle(Particle p, Vector2 where)
        {
            // Rocks fall down, so their dust drifts slightly up and spreads
            var offset = new Vector2(RandomHelper.NextFloat(-20, 20), RandomHelper.NextFloat(-6, 6));
            var velocity = new Vector2(RandomHelper.NextFloat(-25, 25), RandomHelper.NextFloat(-60, -20));

            p.Initialize(where + offset, velocity, Vector2.Zero, _tint,
                lifetime: RandomHelper.NextFloat(0.5f, 0.8f),
                scale: RandomHelper.NextFloat(2.5f, 4.0f),
                rotation: RandomHelper.NextFloat(0, MathHelper.TwoPi),
                angularVelocity: RandomHelper.NextFloat(-2f, 2f));
        }

        protected override void UpdateParticle(Particle particle, float dt)
        {
            base.UpdateParticle(particle, dt);

            // Fade out while keeping this rock's tint
            float normalizedLifetime = particle.TimeSinceStart / particle.Lifetime;
            particle.Color = particle.BaseColor * ((1 - normalizedLifetime) * 0.45f);

            particle.Scale += 0.8f * dt; // Puffs will grow as they fade
        }
    }
}
