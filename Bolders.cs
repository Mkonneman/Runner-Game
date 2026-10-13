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
        // Size of the boulder sprite (boulder.png 128x128)
        private const int FRAME_WIDTH = 128;
        private const int FRAME_HEIGHT = 128;

        // Scale (128px art drawn 1:1 = same size as the old 32px x 4)
        private float scale = 1f;

        // How fast the bolder falls down the screen
        private const float FALL_SPEED = 250f;

        // Rock color variations - the gray sprite gets tinted by one of these
        private static readonly Color[] RockColors =
        {
            Color.White,                 // plain gray stone
            new Color(255, 150, 100),    // lava rock
            new Color(150, 200, 255),    // ice rock
            new Color(170, 230, 140),    // mossy rock
            new Color(200, 160, 255),    // crystal rock
            new Color(210, 180, 130),    // sandstone
        };
        private static readonly Random random = new();
        private Color tint;

        // Lets add tilt to the rock to give it life
        private float rotation;
        private float spinSpeed; 

        /// <summary>
        /// This will be helpful for deleting the bolder once it has reached the bottom
        /// </summary>
        public bool IsOffScreen { get; private set; }

        // Texture and position 
        private Texture2D texture;
        private Vector2 position = new Vector2(400, 475);

        // Position
        public Vector2 Position => position;

        // Tint  
        public Color Tint => tint;


        /// <summary>
        /// Creates a new bolder above the top of the screen, centered on the given lane's X position.
        /// The game class decides which lane to spawn in and just passes that X in here.
        /// </summary>
        /// <param name="laneX">The X position of the lane to spawn in</param>
        public Bolders(float laneX)
        {
            // Start just above the screen so it falls into view instead of popping in. (Gonna add random position later instead of lanes)
            position = new Vector2(laneX, -(FRAME_HEIGHT * scale));

            // Random color tints for the rocks (oooo color...)
            tint = RockColors[random.Next(RockColors.Length)];

            // Random starting tilt and a gentle spin either direction (Its falling!!....)
            rotation = MathHelper.ToRadians(random.Next(-20, 21));
            spinSpeed = MathHelper.ToRadians(random.Next(15, 46)) * (random.Next(2) == 0 ? -1 : 1);
        }

        /// <summary>
        /// This is colision detection of the bolder as a circle 
        /// </summary>
        public BoundingCircle Bounds
        {
            get
            {
                float height = FRAME_HEIGHT * scale;
                float width = FRAME_WIDTH * scale;
                var center = new Vector2(position.X, position.Y - height / 2f);
                return new BoundingCircle(center, width * 0.38f);
            }
        }

        /// <summary>
        /// Loads the sprite texture using the provided ContentManager
        /// </summary>
        /// <param name="content">The ContentManager to load with</param>
        public void LoadContent(ContentManager content)
        {
            texture = content.Load<Texture2D>("spike_rock");
        }

        /// <summary>
        /// Updates the sprite's position
        /// </summary>
        /// <param name="gameTime">The GameTime</param>
        public void Update(GameTime gameTime, int screenHeight)
        {
            // Might add a speed up gimic to certain rocks 
            position.Y += FALL_SPEED * (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Rotation...
            rotation += spinSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;

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
            // We need the rock to rotate in place not from the bottom so it dosnt float away
            var origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
            var drawPos = new Vector2(position.X, position.Y - FRAME_HEIGHT * scale / 2f);

            // Rotation angle + Color tint (2 birds one line...)
            spriteBatch.Draw(texture, drawPos, null, tint, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
