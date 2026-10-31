using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Small glowing sparks that drift away and fade (lantern embers, shop lamps and the cave eyes)
    /// Each screen makes its own with a color and motion, pretty handy!
    /// </summary>
    public class SparkParticleSystem : ParticleSystem
    {
        private readonly Color _color;
        private readonly float _lifetime;
        private readonly float _scale;
        private readonly Vector2 _minVelocity;
        private readonly Vector2 _maxVelocity;
        private readonly float _wobble;       // how far side to side they sway
        private readonly float _wobbleSpeed;  // how fast they sway

        /// Multiplied into the color so sparks fade with their screen's transition
        public float Opacity { get; set; } = 1f;

        public SparkParticleSystem(Game game, Color color, float lifetime, float scale, Vector2 minVelocity, Vector2 maxVelocity, float wobble = 0f, float wobbleSpeed = 0f)
            : base(game, 64)
        {
            _color = color;
            _lifetime = lifetime;
            _scale = scale;
            _minVelocity = minVelocity;
            _maxVelocity = maxVelocity;
            _wobble = wobble;
            _wobbleSpeed = wobbleSpeed;
        }

        protected override void InitializeConstants()
        {
            textureFilename = "Images/Particles/particle";
            minNumParticles = 1;
            maxNumParticles = 1;

            // Added light so they glow in the dark
            blendState = BlendState.Additive;
            DrawOrder = AdditiveBlendDrawOrder;
        }

        /// <summary>
        /// Adds one spark at a point
        /// </summary>
        public void Emit(Vector2 where) => AddParticles(where);

        protected override void InitializeParticle(Particle p, Vector2 where)
        {
            var velocity = new Vector2(RandomHelper.NextFloat(_minVelocity.X, _maxVelocity.X), RandomHelper.NextFloat(_minVelocity.Y, _maxVelocity.Y));

            // Sneaky trick, rotation holds a random sway offset since the glow is round and you cant see it turn
            p.Initialize(where, velocity, Vector2.Zero, _color, _lifetime, _scale, rotation: RandomHelper.NextFloat(0, MathHelper.TwoPi));
        }

        protected override void UpdateParticle(Particle particle, float dt)
        {
            base.UpdateParticle(particle, dt);

            // Sway side to side and fade out
            particle.Position.X += MathF.Sin(particle.TimeSinceStart * _wobbleSpeed + particle.Rotation) * _wobble * dt;
            particle.Color = particle.BaseColor * (1 - particle.TimeSinceStart / particle.Lifetime) * Opacity;
        }
    }
}
