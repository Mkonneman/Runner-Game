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
        public float Scale { get => scale; set => scale = value; }  // the arena shrinks him so the room looks bigger

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
        public int Facing => facing;
        public bool FlipToFacing;
        private float knock;                    // sideways shove from a hit, wears off fast
        public void Knock(float speed) { knock = speed; if (!isJumping) { isJumping = true; verticalSpeed = -JUMP_SPEED * 0.45f; } }  // little hop backward               // arena: he turns around when walking left
        private SpriteEffects Flip => FlipToFacing && facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

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

        // Hover Boots upgrades: one extra jump in the air, or hold jump on the way down to float
        public bool DoubleJump, AirHover;
        private bool usedDouble;
        private float hoverLeft = HoverTime;
        private const float HoverTime = 1.2f;    // seconds of float per jump

        public bool IsOnGround => !isJumping;

        // Where his feet land, the arena moves it down over a hiding pocket (walk off the edge = drop in)
        public float GroundY = GROUND_Y;
        // How far left/right he can go, the arena squeezes these while he's down in a pocket
        public float MinX = MIN_X, MaxX = MAX_X;

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
            position.Y = GroundY;
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
                float height = (Crouching ? 15 : 29) * scale;  // crouched he's about half as tall (a bit under so rocks skim over)
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
            Skin = Shop.Equipped(Slot.Skin);
            string skin = Shop.SkinFolder(Skin);  // whatever skin is on
            texture = content.Load<Texture2D>(skin + "/run");
            shootTexture = content.Load<Texture2D>(skin + "/shoot");  // pack pistol pose with the rocket launcher stitched on
            crouchTexture = content.Load<Texture2D>("Images/Gameplay/player_crouch");
            movesTexture = content.Load<Texture2D>(skin + "/moves");
        }

        /// <summary>
        /// Updates the sprite's position based on user input
        /// </summary>
        /// <param name="gameTime">The GameTime</param>
        // When set he walks himself to this X and ignores the controls (the end door cutscene)
        public float? AutoWalkTo;

        // Seconds left showing the rocket launcher pose (frame 0 kicked back with a flash, then frame 1)
        public float ShootTime;
        public const float ShootPose = 0.4f;
        private Texture2D shootTexture, crouchTexture, movesTexture;
        public ItemId? Skin { get; private set; }  // the skin these textures came from

        // player_moves.png, the same explorer from the hero pack: 0 stand, 1-5 crouch (down then back up), 6-13 jump (rising to falling), 14-15 landing
        private const int M_STAND = 0, M_CROUCH = 1, M_AIR = 6, M_AIR_FRAMES = 8, M_LAND = 14;
        private float crouchAmount;   // 0 standing, 1 all the way down
        private float landTime;       // little squat right after touching down
        private bool wasJumping;

        // Set by the arena while the crouch button is held (only on the ground)
        public bool Crouching;

        // In the arena he can stand still, then he holds the legs-together frame instead of running in place
        public bool IdleWhenStill;
        private const int IDLE_FRAME = 6;
        private bool moving;
        public bool IsMoving => moving || !IdleWhenStill;  // caves always count as running

        // Where the launcher muzzle is in the world (pixel 46,17 of the shoot frame)
        public Vector2 Muzzle => position + new Vector2((46 - FRAME_WIDTH / 2f) * (FlipToFacing ? facing : 1), 17 - FRAME_HEIGHT) * scale;

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
            if (Crouching) direction = 0;  // crouched he stays put (hiding in the dip)
            float speed = MOVE_SPEED * direction;
            if (dashTimeLeft > 0)
            {
                dashTimeLeft -= dt;
                speed = DASH_SPEED * facing;
            }
            float oldX = position.X;
            position.X = MathHelper.Clamp(position.X + speed * dt, MinX, MaxX);
            position.X = MathHelper.Clamp(position.X + knock * dt, MinX, MaxX);
            knock *= MathF.Pow(0.02f, dt);  // slides to a stop in about half a second
            moving = position.X != oldX;

            // Double jump, checked first so the ground jump below doesn't count as the second one
            if (DoubleJump && AutoWalkTo == null && isJumping && !isFalling && !usedDouble && Controls.IsPressed(PlayerAction.Jump))
            {
                usedDouble = true;
                jumpCut = true;
                verticalSpeed = -JUMP_SPEED * 0.8f;
            }

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

            // Ground dropped away under him (walked into a pocket), so he falls in
            if (!isJumping && !isFalling && position.Y < GroundY - 1)
            {
                isJumping = true;
                verticalSpeed = 0;
            }

            // In the air: gravity pulls him back down until he lands
            if (isJumping)
            {
                verticalSpeed += GRAVITY * dt;
                if (AirHover && !isFalling && verticalSpeed > 60 && hoverLeft > 0 && Controls.IsDown(PlayerAction.Jump)) { verticalSpeed = 60; hoverLeft -= dt; }  // slow drift down
                position.Y += verticalSpeed * dt;
                if (position.Y >= GroundY && !isFalling)
                {
                    position.Y = GroundY;
                    isJumping = false;
                    usedDouble = false;
                    hoverLeft = HoverTime;
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
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            animationTimer += dt;
            var origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT); // bottom-center of one frame

            // Crouch eases down through the pack frames and back up when let go
            bool down = Crouching && !isJumping;
            crouchAmount = MathHelper.Clamp(crouchAmount + (down ? dt : -dt) / 0.12f, 0, 1);
            if (wasJumping && !isJumping) landTime = 0.1f;
            wasJumping = isJumping;
            landTime -= dt;

            int move = -1;  // frame from player_moves, -1 = use the run sheet
            if (isJumping)
            {
                // Rising frames on the way up, falling ones on the way down, picked by how fast he's going
                float t = MathHelper.Clamp((verticalSpeed / (JUMP_SPEED * JumpBoost) + 1) / 2, 0, 0.999f);  // -1 top speed up .. +1 down
                move = M_AIR + (int)(t * M_AIR_FRAMES);
            }
            else if (crouchAmount > 0)
                move = down ? M_CROUCH + Math.Min(2, (int)(crouchAmount * 3)) : M_CROUCH + 3 + (crouchAmount > 0.5f ? 0 : 1);
            else if (landTime > 0) move = M_LAND + (landTime > 0.05f ? 0 : 1);
            else if (IdleWhenStill && !moving) move = M_STAND;
            else if (animationTimer > ANIMATION_SPEED)
            {
                animationFrame++;
                if (animationFrame > FRAME_COUNT - 1) animationFrame = 0;
                animationTimer -= ANIMATION_SPEED;
            }
            if (move >= 0) animationTimer = 0;

            ShootTime -= dt;
            if (ShootTime > 0)
            {
                int shot = ShootTime > ShootPose - 0.08f ? 0 : 1;  // flash + kick for the first couple frames
                spriteBatch.Draw(shootTexture, position, new Rectangle(shot * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT), Color, 0f, origin, scale, Flip, 0f);
            }
            else if (move >= 0) spriteBatch.Draw(movesTexture, position, new Rectangle(move * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT), Color, 0f, origin, scale, Flip, 0f);
            else spriteBatch.Draw(texture, position, new Rectangle(animationFrame * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT), Color, 0f, origin, scale, Flip, 0f);
        }

    }
}