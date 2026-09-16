using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace Runner_Game
{
    /// <summary>
    /// A class for the player to controle the runner
    /// </summary>
    public class Runner
    {

        // Creates input dectecing variables to detect and hold what input is gathered 
        protected KeyboardState currentKeyboardState;
        protected KeyboardState previousKeyboardState;


        // Texture and position 
        private Texture2D texture;
        private Vector2 position = new Vector2(200, 200);

        // The current lane the player is in 
        private int currentLane = 2;

        /// <summary>
        /// Loads the sprite texture using the provided ContentManager
        /// </summary>
        /// <param name="content">The ContentManager to load with</param>
        public void LoadContent(ContentManager content)
        {
            texture = content.Load<Texture2D>("player_run");
        }

        /// <summary>
        /// Updates the sprite's position based on user input
        /// </summary>
        /// <param name="gameTime">The GameTime</param>
        public void Update(GameTime gameTime)
        {
            // Grab Keyboard input 
            currentKeyboardState = Keyboard.GetState();

            // If Left or Right inputed move the player into various lanes 
            if (currentKeyboardState.IsKeyDown(Keys.Left) && previousKeyboardState.IsKeyUp(Keys.Left) || currentKeyboardState.IsKeyDown(Keys.A) && previousKeyboardState.IsKeyUp(Keys.A))
            {
                if (currentLane > 1)
                {
                    currentLane--;
                    position.X -= 100;
                }
            }
            if (currentKeyboardState.IsKeyDown(Keys.Right) && previousKeyboardState.IsKeyUp(Keys.Right) || currentKeyboardState.IsKeyDown(Keys.D) && previousKeyboardState.IsKeyUp(Keys.D))
            {
                if (currentLane < 3)
                {
                    currentLane++;
                    position.X += 100;
                }
            }

            previousKeyboardState = currentKeyboardState; // Keeps buttons from being triggered multiple times
        }


        /// <summary>
        /// Draws the sprite using the supplied SpriteBatch
        /// </summary>
        /// <param name="gameTime">The game time</param>
        /// <param name="spriteBatch">The spritebatch to render with</param>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(texture, position, null, Color.White, 0f, new Vector2(64, 64), 0.5f, SpriteEffects.None, 0f);
        }

    }
}
