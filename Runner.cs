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

        // Texture and position 
        private Texture2D texture;
        private Vector2 position = new Vector2(427, 475);
        public Vector2 Position { get => position; set => position = value; } // Settable so the title screen can run him into the cave

        // Tint for the sprite in the title
        public Color Color { get; set; } = Color.White;
        public Vector2 Velocity { get; private set; }


        // Decided to free the player to move around
        private const float MOVE_SPEED = 320f;
        private const float MIN_X = 60f;
        private const float MAX_X = 794f;
        private int facing = 1;                 // This is for the dash

        // Dash: This is quick burst in the direction he's moving, its tied to a cooldown
        private const float DASH_SPEED = 1400f;
        private const float DASH_TIME = 0.15f;   // How long the burst lasts (Might let this be upgraded)
        public float DashCooldown = Shop.Value(ItemId.Dash);   // Seconds before he can dash again (upgradable in the shop)
        public bool CanDash = true;                            // only when Dash is the equipped ability
        private float dashTimeLeft;
        public bool IsDashing => dashTimeLeft > 0;
        public float DashCooldownLeft { get; private set; }  // 0 = ready (The cooldown UI reads this)

        // Jumping (Made for the floor spikes)
        private const float JUMP_SPEED = 1000f;  // Upward speed 
        private const float GRAVITY = 2600f;     // Gravity
        private const float GROUND_Y = 475f;     // His feet on the cave floor
        private const int JUMP_FRAME = 2;        // Freezes the player run animation when jumping
        private float verticalSpeed;
        private bool isJumping;
        private bool isFalling;                  // Fell in a gap, no landing anymore

        // Spring Boots, how much higher a full held jump goes (1 = normal jump, no short hops)
        public float JumpBoost = 1f;
        private bool jumpCut;                    // let go early so the jump got cut short already

        public bool IsOnGround => !isJumping;

        /// <summary>
        /// Drops him into a gap (gravity takes over and he never lands)
        /// </summary>
        public void Fall()
        {
            isFalling = true;
            isJumping = true;
            verticalSpeed = 0;
        }

        /// <summary>
        /// Puts him back on his feet (Phoenix Heart saved him)
        /// </summary>
        public void Revive()
        {
            isFalling = false;
            isJumping = false;
            verticalSpeed = 0;
            position.Y = GROUND_Y;
        }

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
        // When set he walks himself to this X and ignores the controls (the end door cutscene)
        public float? AutoWalkTo;

        public void Update(GameTime gameTime)
        {
            // Helps detect the players velocity for particles
            Vector2 startPosition = position;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Hold left or right to move, kept on screen (keys come from Controls so they can be rebound)
            int direction = 0;
            if (Controls.IsDown(PlayerAction.Left)) direction -= 1;
            if (Controls.IsDown(PlayerAction.Right)) direction += 1;
            if (AutoWalkTo is float target) direction = MathF.Abs(target - position.X) > 3 ? MathF.Sign(target - position.X) : 0;
            if (direction != 0) facing = direction;

            // Dash goes the way he's moving (or facing), once the cooldown is done
            DashCooldownLeft = MathF.Max(0, DashCooldownLeft - dt);
            if (AutoWalkTo == null && CanDash && DashCooldownLeft == 0 && Controls.IsPressed(PlayerAction.Dash))
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

            // Jump (only from the ground)
            if (AutoWalkTo == null && !isJumping && !isFalling && Controls.IsPressed(PlayerAction.Jump))
            {
                isJumping = true;
                jumpCut = false;
                verticalSpeed = -JUMP_SPEED * JumpBoost;
            }

            // With Spring Boots letting go of jump on the way up cuts it short, so tap = hop and hold = big jump
            if (JumpBoost > 1f && isJumping && !isFalling && !jumpCut && verticalSpeed < 0 && !Controls.IsDown(PlayerAction.Jump))
            {
                verticalSpeed *= 0.45f;
                jumpCut = true;
            }

            // In the air: gravity pulls him back down until he lands
            if (isJumping)
            {
                verticalSpeed += GRAVITY * dt;
                position.Y += verticalSpeed * dt;
                if (position.Y >= GROUND_Y && !isFalling)
                {
                    position.Y = GROUND_Y;
                    isJumping = false;
                }
            }

            // Velocity = distance moved / time, only while dashing (the dash trail uses it)
            Velocity = dashTimeLeft > 0 && dt > 0 ? (position - startPosition) / dt : Vector2.Zero;
        }



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