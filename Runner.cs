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
    /// A class for the player to controle the runner
    /// </summary>
    public class Runner : IParticleEmitter
    {
        // Animating the Player
        private const float ANIMATION_SPEED = 0.08f; // seconds per frame - lower = faster
        private const int FRAME_WIDTH = 56;
        private const int FRAME_HEIGHT = 37;
        private const int FRAME_COUNT = 8;
        private double animationTimer;
        private int animationFrame;

        // Scale
        private float scale = 4f;

        // Creates input dectecing variables to detect and hold what input is gathered 
        protected KeyboardState currentKeyboardState;
        protected KeyboardState previousKeyboardState;

        // Texture and position 
        private Texture2D texture;
        private Vector2 position = new Vector2(400, 475);
        public Vector2 Position { get => position; set => position = value; } // Settable so the title screen can run him into the cave

        // Tint for the sprite in the title
        public Color Color { get; set; } = Color.White;
        public Vector2 Velocity { get; private set; }


        // Decided to free the player to move around
        private const float MOVE_SPEED = 320f;
        private const float MIN_X = 60f;
        private const float MAX_X = 740f;
        private int facing = 1;                 // This is for the dash

        // Dash: This is quick burst in the direction he's moving, its tied to a cooldown
        private const float DASH_SPEED = 1400f;
        private const float DASH_TIME = 0.15f;   // How long the burst lasts (Might let this be upgraded)
        public readonly float DashCooldown = Shop.Value(Upgrade.Dash);   // Seconds before he can dash again (Also upgradable)
        private float dashTimeLeft;
        public bool IsDashing => dashTimeLeft > 0;  // Phase Dash checks this
        public float DashCooldownLeft { get; private set; }  // 0 = ready (The cooldown UI reads this)

        // Jumping (Made for the floor spikes)
        private const float JUMP_SPEED = 1000f;  // Upward speed 
        private const float GRAVITY = 2600f;     // Gravity
        private const float GROUND_Y = 475f;     // His feet on the cave floor
        private const int JUMP_FRAME = 2;        // Freezes the player run animation when jumping
        private float verticalSpeed;
        private bool isJumping;

        /// <summary>
        /// Gets the bounding rectangle of the sprite, adjusted for its position and scale
        /// </summary>
        public BoundingRectangle Bounds
        {
            get
            {
                // Hero art is 20x29 inside the 56x37 frame 
                float width = 14 * scale;
                float height = 29 * scale;
                float x = position.X - width / 2f;
                float y = position.Y - height - 1 * scale; // 1px gap under his feet in the frame
                return new BoundingRectangle(x, y, width, height);
            }
        }

        /// <summary>
        /// Loads the sprite texture using the provided ContentManager
        /// </summary>
        /// <param name="content">The ContentManager to load with</param>
        public void LoadContent(ContentManager content)
        {
            texture = content.Load<Texture2D>("Images/Gameplay/player_run");
        }

        /// <summary>
        /// Updates the sprite's position based on user input
        /// </summary>
        /// <param name="gameTime">The GameTime</param>
        public void Update(GameTime gameTime)
        {
            // Helps detect the players velocity for particles
            Vector2 startPosition = position;

            // Grab Keyboard input 
            currentKeyboardState = Keyboard.GetState();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Hold Left/A or Right/D to move, kept on screen
            int direction = 0;
            if (currentKeyboardState.IsKeyDown(Keys.Left) || currentKeyboardState.IsKeyDown(Keys.A)) direction -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.Right) || currentKeyboardState.IsKeyDown(Keys.D)) direction += 1;
            if (direction != 0) facing = direction;

            // Shift dashes the way he's moving (or facing), once the cooldown is done
            DashCooldownLeft = MathF.Max(0, DashCooldownLeft - dt);
            if (DashCooldownLeft == 0 && (IsNewKeyPress(Keys.LeftShift) || IsNewKeyPress(Keys.RightShift)))
            {
                dashTimeLeft = DASH_TIME;
                DashCooldownLeft = DashCooldown;
            }

            // Walk speed, or dash speed while the dash lasts
            float speed = MOVE_SPEED * direction;
            if (dashTimeLeft > 0)
            {
                dashTimeLeft -= dt;
                speed = DASH_SPEED * facing;
            }
            position.X = MathHelper.Clamp(position.X + speed * dt, MIN_X, MAX_X);

            // Space, W or Up jumps (only from the ground)
            if (!isJumping && (IsNewKeyPress(Keys.Space) || IsNewKeyPress(Keys.W) || IsNewKeyPress(Keys.Up)))
            {
                isJumping = true;
                verticalSpeed = -JUMP_SPEED;
            }

            // In the air: gravity pulls him back down until he lands
            if (isJumping)
            {
                verticalSpeed += GRAVITY * dt;
                position.Y += verticalSpeed * dt;
                if (position.Y >= GROUND_Y)
                {
                    position.Y = GROUND_Y;
                    isJumping = false;
                }
            }

            previousKeyboardState = currentKeyboardState; // Keeps buttons from being triggered multiple times

            // Velocity = distance moved / time, only while dashing (the dash trail uses it)
            Velocity = dashTimeLeft > 0 && dt > 0 ? (position - startPosition) / dt : Vector2.Zero;
        }

        // True only on the frame a key goes down (holding it doesn't repeat)
        private bool IsNewKeyPress(Keys key) => currentKeyboardState.IsKeyDown(key) && previousKeyboardState.IsKeyUp(key);


        /// <summary>
        /// Draws the sprite using the supplied SpriteBatch
        /// </summary>
        /// <param name="gameTime">The game time</param>
        /// <param name="spriteBatch">The spritebatch to render with</param>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            animationTimer += gameTime.ElapsedGameTime.TotalSeconds;

            // Jumping interrupts the run: hold one frame until he lands
            if (isJumping)
            {
                animationFrame = JUMP_FRAME;
                animationTimer = 0;
            }
            else if (animationTimer > ANIMATION_SPEED)
            {
                animationFrame++;
                if (animationFrame > FRAME_COUNT - 1) animationFrame = 0;
                animationTimer -= ANIMATION_SPEED;
            }

            // Determine the source rectangle
            var source = new Rectangle(animationFrame * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
            var origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT); // bottom-center of one frame

            // Draw the runner using the current animation frame
            spriteBatch.Draw(texture, position, source, Color, 0f, origin, scale, SpriteEffects.None, 0f);

        }

    }
}