using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Speed-line streaks left behind when the runner dashes between lanes.
    /// Streaks only appear on the frame he moves, and trail opposite his direction.
    /// </summary>
    public class DashParticleSystem : ParticleSystem
    {
        private readonly IParticleEmitter _emitter;

        // Direction of the current dash: -1 = moved left, +1 = moved right
        private int _direction;

        /// The streaks' color
        public Color TrailColor { get; set; } = new Color(140, 200, 255);

        /// Turn off to stop new streaks (existing ones still fade out)
        public bool IsEmitting { get; set; } = true;

        public DashParticleSystem(Game game, IParticleEmitter emitter) : base(game, 200)
        {
            _emitter = emitter;
        }

        protected override void InitializeConstants()
        {
            textureFilename = "Images/Particles/streak";

            // Streaks per AddParticles call (1 Per Dash)
            minNumParticles = 10;
            maxNumParticles = 14;

            // streaks add together and glow 
            blendState = BlendState.Additive;
            DrawOrder = AdditiveBlendDrawOrder;
        }

        protected override void InitializeParticle(Particle p, Vector2 where)
        {
            // Streaks drift back the way it came, so they trail behind him
            var velocity = new Vector2(-_direction * RandomHelper.NextFloat(80, 220), 0);

            // streak.png has its bright head on the left; flip it to point the way he moved
            float rotation = _direction > 0 ? MathHelper.Pi : 0f;

            p.Initialize(where, velocity, Vector2.Zero, TrailColor,
                lifetime: RandomHelper.NextFloat(0.2f, 0.35f),
                scale: RandomHelper.NextFloat(1.5f, 3.0f),
                rotation: rotation); // Dont want the particles spinning 
        }

        protected override void UpdateParticle(Particle particle, float dt)
        {
            base.UpdateParticle(particle, dt);

            // Fade out over its life (translucent the whole time)
            float normalizedLifetime = particle.TimeSinceStart / particle.Lifetime;
            particle.Color = particle.BaseColor * ((1 - normalizedLifetime) * 0.7f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Only spawn on the frame the runner actually moves
            if (!IsEmitting || _emitter.Velocity.X == 0) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _direction = Math.Sign(_emitter.Velocity.X);

            // Fill the gap he just crossed (from where he was to where he is now),
            // around his body height
            float fromX = _emitter.Position.X - _emitter.Velocity.X * dt;
            float toX = _emitter.Position.X;
            int left = (int)MathF.Min(fromX, toX);
            int width = (int)MathF.Abs(toX - fromX);
            int top = (int)_emitter.Position.Y - 110;

            AddParticles(new Rectangle(left, top, width, 90));
        }
    }
}
