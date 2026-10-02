using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The actual game (pushed on top of the title screen)
    /// </summary>
    public class GameplayScreen : GameScreen
    {
        private ContentManager _content;

        // The Player
        private Runner _runner;

        // Background
        private Texture2D _backgroundTexture;

        // Audio Handling for the game
        private Song _runnerMusic;
        private SoundEffect _crashSound;

        // Makes multiple bolders
        private List<Bolders> _bolders = new();

        // Array that moves the bolder (Same as the player but simplified)
        private readonly float[] _laneX = { 200f, 400f, 600f };
        private Random _random = new();
        private float _spawnTimer;
        private const float SpawnInterval = 1.5f; // seconds between spawns (Tunned)

        // Bool for ending the game
        private bool _isGameOver;

        // Time survived this run (only counts while playing, not on the menu)
        private TimeSpan _runTime;

        public GameplayScreen()
        {
            // Fades in and out over half a second
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void Activate()
        {
            _content = ScreenManager.Game.Content;

            // loads the texture of the background
            _backgroundTexture = _content.Load<Texture2D>("caveSprite");

            // Loads the music and sounds for the game
            _runnerMusic = _content.Load<Song>("Final Dash");
            _crashSound = _content.Load<SoundEffect>("crash");

            // Fresh player for each run
            _runner = new();
            _runner.LoadContent(_content);

            // Start the music from the beginning
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(_runnerMusic);
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            // Only run the game while this screen is on top and the run is going
            if (!IsActive || _isGameOver) return;

            _runTime += gameTime.ElapsedGameTime;

            _runner.Update(gameTime);

            // Spawn a new bolder in a random lane every SpawnInterval seconds.
            _spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                float laneX = _laneX[_random.Next(_laneX.Length)];
                var bolder = new Bolders(laneX);
                bolder.LoadContent(_content); // safe to call per-instance - MonoGame caches the texture after the first load
                _bolders.Add(bolder);
            }

            int screenHeight = ScreenManager.GraphicsDevice.Viewport.Height;

            // Move every bolder, then remove ones that leave the screen or hit the player.
            for (int i = _bolders.Count - 1; i >= 0; i--)
            {
                _bolders[i].Update(gameTime, screenHeight);

                if (_bolders[i].Bounds.CollidesWith(_runner.Bounds))
                {
                    _bolders.RemoveAt(i); // despawn the one that hit
                    _isGameOver = true;   // and end the game
                    _crashSound.Play();
                    MediaPlayer.Stop(); // Stop the music
                    continue;
                }

                if (_bolders[i].IsOffScreen)
                {
                    _bolders.RemoveAt(i);
                }
            }
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // After a crash, Enter exits this screen and the title screen shows again
            if (_isGameOver && (input.IsNewKeyPress(Keys.Enter) || input.IsNewButtonPress(Buttons.Start)))
            {
                ExitScreen();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Background Texture
            spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * 0.6f * TransitionAlpha);

            // Draw Runner (Player)
            _runner.Draw(gameTime, spriteBatch);

            // Draw the bolders
            foreach (var bolder in _bolders)
            {
                bolder.Draw(gameTime, spriteBatch);
            }

            // Draw instructions and Game Timer
            spriteBatch.DrawString(font, "Use A or D / Left or Right to dodge!", new Vector2(25, 25), Color.Yellow * TransitionAlpha);
            spriteBatch.DrawString(font, $"{_runTime:c}", new Vector2(50, 75), Color.Yellow * TransitionAlpha);

            // Display for the final time reached once collision is detected
            if (_isGameOver)
            {
                spriteBatch.DrawString(font, $"Game Over! Time reached: {_runTime:c}", new Vector2(25, 120), Color.Red * TransitionAlpha);
                spriteBatch.DrawString(font, "Press Enter for the menu", new Vector2(25, 170), Color.White * TransitionAlpha);
            }

            spriteBatch.End();
        }
    }
}
