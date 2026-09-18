using CollisionExercise.Collisions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Runner_Game
{
    /// <summary>
    /// A class to help spawn the obsticals that the player will avoid
    /// </summary>
    public class Bolders
    {
        // Animating the Bolder
        private const float ANIMATION_SPEED = 0.08f; // seconds per frame - lower = faster
        private const int FRAME_WIDTH = 32;
        private const int FRAME_HEIGHT = 32;
        private const int FRAME_COUNT = 8;
        private double animationTimer;
        private int animationFrame;      

        // Scale
        private float scale = 4f;

        // How fast the bolder falls down the screen
        private const float FALL_SPEED = 250f;

        /// <summary>
        /// This will be helpful for deleting the bolder once it has reached the bottom
        /// </summary>
        public bool IsOffScreen { get; private set; }


        // Texture and position 
        private Texture2D texture;
        private Vector2 position = new Vector2(400, 475);

        /// <summary>
        /// Creates a new bolder above the top of the screen, centered on the given lane's X position.
        /// The game class decides which lane to spawn in and just passes that X in here.
        /// </summary>
        /// <param name="laneX">The X position of the lane to spawn in</param>
        public Bolders(float laneX)
        {
            // Start just above the screen so it falls into view instead of popping in.
            position = new Vector2(laneX, -(FRAME_HEIGHT * scale));
        }

        /// <summary>
        /// This is colision detection of the bolder as a circle 
        /// </summary>
        public BoundingCircle Bounds
        {
            get
            {
                float height = FRAME_HEIGHT * scale * 2f;
                float width = FRAME_WIDTH * scale;
                var center = new Vector2(position.X, position.Y - height / 2f);
                return new BoundingCircle(center, width / 2f);
            }
        }

        /// <summary>
        /// Loads the sprite texture using the provided ContentManager
        /// </summary>
        /// <param name="content">The ContentManager to load with</param>
        public void LoadContent(ContentManager content)
        {
            texture = content.Load<Texture2D>("boulder_roll");
        }

        /// <summary>
        /// Updates the sprite's position based on user input
        /// </summary>
        /// <param name="gameTime">The GameTime</param>
        public void Update(GameTime gameTime, int screenHeight)
        {
            position.Y += FALL_SPEED * (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (position.Y - 128 > screenHeight)
            {
                IsOffScreen = true;
            }
        }


        /// <summary>
        /// Draws the sprite using the supplied SpriteBatch
        /// </summary>
        /// <param name="gameTime">The game time</param>
        /// <param name="spriteBatch">The spritebatch to render with</param>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            animationTimer += gameTime.ElapsedGameTime.TotalSeconds;

            if (animationTimer > ANIMATION_SPEED)
            {
                animationFrame++;
                if (animationFrame > FRAME_COUNT - 1) animationFrame = 0;
                animationTimer -= ANIMATION_SPEED;
            }

            // Determine the source rectangle
            var source = new Rectangle(animationFrame * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
            var origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT); // bottom-center of one frame

            // Draw the runner using the current animation frame
            spriteBatch.Draw(texture, position, source, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

        }

    }
}
