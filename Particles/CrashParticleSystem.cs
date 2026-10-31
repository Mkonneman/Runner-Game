using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Rocks crashing into a player should generate an effect, so it does
    /// </summary>
    public class CrashParticleSystem : ParticleSystem
    {
        private Color _color = Color.White;

        public CrashParticleSystem(Game game, int maxCrashes) : base(game, maxCrashes * 30) { }

        protected override void InitializeConstants()
        {
            textureFilename = "Images/Particles/shard";
            minNumParticles = 18;
            maxNumParticles = 26;
            DrawOrder = AlphaBlendDrawOrder;
        }

        /// <summary>
        /// Spawns a burst of shards at a point, in the given color (Uses the rocks tint...Fancy right?)
        /// </summary>
        public void PlaceCrash(Vector2 where, Color color)
        {
            _color = color;
            AddParticles(where);
        }

        /// <summary>
        /// Setup for how I want the particles to burst outward
        /// </summary>
        /// <param name="p"> The particle fed in </param>
        /// <param name="where"> Where the shard spawns </param>
        protected override void InitializeParticle(Particle p, Vector2 where)
        {
            // Mostly upward and outward, so shards pop up then fall
            float angle = RandomHelper.NextFloat(MathHelper.Pi * 1.05f, MathHelper.Pi * 1.95f);
            var direction = new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle));
            var velocity = direction * RandomHelper.NextFloat(250, 550);

            var gravity = new Vector2(0, 1100);

            p.Initialize(where, velocity, gravity, _color,
                lifetime: RandomHelper.NextFloat(0.6f, 1.0f),
                scale: RandomHelper.NextFloat(1.0f, 2.0f),
                rotation: RandomHelper.NextFloat(0, MathHelper.TwoPi),
                angularVelocity: RandomHelper.NextFloat(-14f, 14f));
        }

        /// <summary>
        /// Helps showcase the fade
        /// </summary>
        /// <param name="Images/Particles/particle"> Particle fed in </param>
        /// <param name="dt"> Keeps motion the same speed at any frame rate </param>
        protected override void UpdateParticle(Particle particle, float dt)
        {
            base.UpdateParticle(particle, dt);

            // Fully solid for most of its life, then fades out quickly at the end
            float normalizedLifetime = particle.TimeSinceStart / particle.Lifetime;
            float alpha = normalizedLifetime < 0.7f ? 1f : 1f - (normalizedLifetime - 0.7f) / 0.3f;
            particle.Color = particle.BaseColor * alpha;
        }
    }
}
