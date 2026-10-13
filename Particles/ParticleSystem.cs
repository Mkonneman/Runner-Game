using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// A class representing a generic particle system
    /// </summary>
    public class ParticleSystem : DrawableGameComponent
    {
        /// The draw order for particles using Alpha Blending
        public const int AlphaBlendDrawOrder = 100;

        /// The draw order for particles using Additive Blending
        public const int AdditiveBlendDrawOrder = 200;

        /// A SpriteBatch to share amongst the various particle systems
        protected static SpriteBatch spriteBatch;

        ///A ContentManager to share amongst the various particle systems
        protected static ContentManager contentManager;

        /// The collection of particles
        Particle[] particles;

        /// A Queue containing unused particles 
        Queue<Particle> freeParticles;

        /// The texture this particle system uses
        Texture2D texture;

        /// The origin when we're drawing textures
        Vector2 origin;

        /// The available particles in the system
        public int FreeParticleCount => freeParticles.Count;

        /// <summary>
        /// A transform applied to the whole particle batch (Used for screen shake).
        /// </summary>
        public Matrix TransformMatrix { get; set; } = Matrix.Identity;

        /// The BlendState to use with this particle system
        protected BlendState blendState = BlendState.AlphaBlend;

        /// The filename of the texture to use for the particles
        protected string textureFilename;

        /// The minimum number of particles to add when AddParticles() is called
        protected int minNumParticles;

        /// The maximum number of particles to add when AddParticles() is called
        protected int maxNumParticles;

        /// <summary>
        /// Constructs a new instance of a particle system
        /// </summary>
        public ParticleSystem(Game game, int maxParticles) : base(game)
        {
            // Create our particles
            particles = new Particle[maxParticles];
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i] = new Particle();
            }
            // Add all free particles to the queue
            freeParticles = new Queue<Particle>(particles);
            // Run the InitializeConstants hook
            InitializeConstants();
        }

        /// <summary>
        /// Used to do the initial configuration of the particle engine. The
        /// protected constants textureFilename, minNumParticles, and maxNumParticles
        /// should be set in the override.
        /// </summary>
        protected virtual void InitializeConstants() { }

        /// <summary>
        /// Randomizes some properties for a particle, then calls initialize on it.
        /// Override in subclasses to change how particles are created.
        /// </summary>
        protected virtual void InitializeParticle(Particle p, Vector2 where)
        {
            p.Initialize(where);
        }

        /// <summary>
        /// Updates the individual particles. Can be overridden in derived classes
        /// </summary>
        protected virtual void UpdateParticle(Particle particle, float dt)
        {
            // Update particle's linear motion values
            particle.Velocity += particle.Acceleration * dt;
            particle.Position += particle.Velocity * dt;

            // Update the particle's angular motion values
            particle.AngularVelocity += particle.AngularAcceleration * dt;
            particle.Rotation += particle.AngularVelocity * dt;

            // Update the time the particle has been alive
            particle.TimeSinceStart += dt;
        }

        /// <summary>
        /// Loads the texture and calculates the origin
        /// </summary>
        protected override void LoadContent()
        {
            if (contentManager == null) contentManager = new ContentManager(Game.Services, "Content");
            if (spriteBatch == null) spriteBatch = new SpriteBatch(Game.GraphicsDevice);

            if (string.IsNullOrEmpty(textureFilename))
            {
                string message = "textureFilename wasn't set properly, so the " +
                    "particle system doesn't know what texture to load. Make " +
                    "sure your particle system's InitializeConstants function " +
                    "properly sets textureFilename.";
                throw new InvalidOperationException(message);
            }
            texture = contentManager.Load<Texture2D>(textureFilename);

            origin.X = texture.Width / 2;
            origin.Y = texture.Height / 2;

            base.LoadContent();
        }

        /// <summary>
        /// Updates all of the active particles
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (Particle p in particles)
            {
                if (p.Active)
                {
                    UpdateParticle(p, dt);
                    if (!p.Active)
                    {
                        freeParticles.Enqueue(p);
                    }
                }
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// Draws all of the active particles with this system's BlendState
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            // BlendState set in InitializeConstants
            spriteBatch.Begin(blendState: blendState, transformMatrix: TransformMatrix);

            foreach (Particle p in particles)
            {
                if (!p.Active)
                    continue;

                // Color = recoloring + translucency, Rotation = sprite rotation
                spriteBatch.Draw(texture, p.Position, null, p.Color, p.Rotation, origin, p.Scale, SpriteEffects.None, 0.0f);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Adds an effect at a point, using as many free particles as are available
        /// </summary>
        protected void AddParticles(Vector2 where)
        {
            int numParticles = RandomHelper.Next(minNumParticles, maxNumParticles);

            for (int i = 0; i < numParticles && freeParticles.Count > 0; i++)
            {
                Particle p = freeParticles.Dequeue();
                InitializeParticle(p, where);
            }
        }

        /// <summary>
        /// Adds an effect at random points inside a rectangle
        /// </summary>
        protected void AddParticles(Rectangle where)
        {
            int numParticles = RandomHelper.Next(minNumParticles, maxNumParticles);

            for (int i = 0; i < numParticles && freeParticles.Count > 0; i++)
            {
                Particle p = freeParticles.Dequeue();
                InitializeParticle(p, RandomHelper.RandomPosition(where));
            }
        }
    }
}
