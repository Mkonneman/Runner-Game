using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The title screen (the first screen on the stack)
    /// </summary>
    public class TitleScreen : GameScreen
    {
        // Background and music for the title
        private Texture2D _backgroundTexture;
        private Song _titleMusic;

        // Timer for the blinking "Press Enter" text
        private float _blinkTimer;

        public TitleScreen()
        {
            // Fades in and out over half a second
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void Activate()
        {
            var content = ScreenManager.Game.Content;
            _backgroundTexture = content.Load<Texture2D>("caveSprite");
            _titleMusic = content.Load<Song>("Final Dash");
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            _blinkTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Keep the music going while the title is showing
            // (it gets stopped when the player crashes)
            if (!coveredByOtherScreen && MediaPlayer.State != MediaState.Playing)
            {
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Play(_titleMusic);
            }
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Enter or Space pushes the gameplay screen on top of the title
            if (input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space) || input.IsNewButtonPress(Buttons.Start))
            {
                ScreenManager.AddScreen(new GameplayScreen());
            }

            // Esc quits the game
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
            {
                ScreenManager.Game.Exit();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Darker background so the text stands out (fades with the transition)
            spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * 0.35f * TransitionAlpha);

            DrawCentered("Runner Game", 120, Color.Yellow, 2f);
            DrawCentered("Dodge the bolders!", 230, Color.White, 0.8f);

            // Blinks on and off about twice a second
            if ((int)(_blinkTimer * 2) % 2 == 0)
            {
                DrawCentered("Press Enter to Start", 320, Color.Yellow, 1f);
            }

            DrawCentered("A / D or Left / Right to move", 400, Color.White, 0.6f);
            DrawCentered("Esc to Quit", 440, Color.White, 0.6f);

            spriteBatch.End();
        }

        // Draws text centered left to right on the screen
        private void DrawCentered(string text, float y, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
