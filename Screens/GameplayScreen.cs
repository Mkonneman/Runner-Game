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

        // Array that moves the bolder (its more simplified will be changed later)
        private readonly float[] _laneX = { 200f, 400f, 600f };
        private float _spawnTimer;
        private const float SpawnInterval = 1.5f; // seconds between spawns

        // Bool for ending the game
        private bool _isGameOver;

        // Time survived this run
        private TimeSpan _runTime;

        // Lets add some "Screen" shake
        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector2 _shakeOffset;

        // Particle systems
        private DashParticleSystem _dash;
        private RockDustParticleSystem _rockDust;
        private CrashParticleSystem _crash;

        public GameplayScreen()
        {
            // Fades in and out over half a second
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>
        /// Starts a screen shake. Intensity is the max pixel offset.
        /// </summary>
        private void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        private void UpdateShake(float dt)
        {
            if (_shakeTime > 0)
            {
                _shakeTime -= dt;
                float strength = _shakeIntensity * (_shakeTime / _shakeDuration); // fades out
                _shakeOffset = new Vector2(RandomHelper.NextFloat(-1, 1), RandomHelper.NextFloat(-1, 1)) * strength;
            }
            else
            {
                _shakeOffset = Vector2.Zero;
            }
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

            // Particle systems are updated/drawn by this screen instead of Game.Components, this allows the shakes to be applyed
            _dash = new DashParticleSystem(ScreenManager.Game, _runner);
            _dash.Initialize();
            _rockDust = new RockDustParticleSystem(ScreenManager.Game);
            _rockDust.Initialize();
            _crash = new CrashParticleSystem(ScreenManager.Game, 5);
            _crash.Initialize();

            // Start the music from the beginning
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(_runnerMusic);
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            // Shake and particles keep running so after the crash they can settle
            UpdateShake((float)gameTime.ElapsedGameTime.TotalSeconds);
            _dash.Update(gameTime);
            _rockDust.Update(gameTime);
            _crash.Update(gameTime);

            // Only run the game while this screen is on top and the run is going
            if (!IsActive || _isGameOver) return;

            _runTime += gameTime.ElapsedGameTime;

            _runner.Update(gameTime);

            // Spawn a new bolder in a random lane every SpawnInterval seconds
            _spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                float laneX = _laneX[RandomHelper.Next(0, _laneX.Length - 1)];
                var bolder = new Bolders(laneX);
                bolder.LoadContent(_content); 
                _bolders.Add(bolder);
            }

            int screenHeight = ScreenManager.GraphicsDevice.Viewport.Height;

            // Move every bolder, then remove ones that leave the screen or hit the player.
            for (int i = _bolders.Count - 1; i >= 0; i--)
            {
                _bolders[i].Update(gameTime, screenHeight);

                // Dust kicked off the top of each falling rock, in the rock's color
                if (RandomHelper.Next(0, 1) == 0)
                {
                    var rock = _bolders[i].Bounds;
                    _rockDust.PlaceDust(rock.Center - new Vector2(0, rock.Radius), _bolders[i].Tint);
                }
                    

                if (_bolders[i].Bounds.CollidesWith(_runner.Bounds))
                {
                    // Burst of dust in the rock's color where it hit (before removing it)
                    _crash.PlaceCrash(_runner.Position - new Vector2(0, 60), _bolders[i].Tint);
                    _dash.IsEmitting = false; // Stop the trail when he's hit
                    Shake(12f, 0.5f);         // Big shake on crash
                    _bolders.RemoveAt(i); // Despawn the one that hit
                    _isGameOver = true;   // End the game
                    _crashSound.Play();  // Play Crash sound
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
            // End message menu detection
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

            // The world that will get shaked 
            var shake = Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0);
            spriteBatch.Begin(transformMatrix: shake);

            // Background is a bit bigger to cover up the void that is behind it
            spriteBatch.Draw(_backgroundTexture, new Rectangle(-16, -16, viewport.Width + 32, viewport.Height + 32), Color.White * 0.6f * TransitionAlpha);

            // Draw Runner (aka the player)
            _runner.Draw(gameTime, spriteBatch);

            // Draw the bolders
            foreach (var bolder in _bolders)
            {
                bolder.Draw(gameTime, spriteBatch);
            }

            spriteBatch.End();

            // Partical Stuff
            _rockDust.TransformMatrix = shake;
            _dash.TransformMatrix = shake;
            _crash.TransformMatrix = shake;

            _rockDust.Draw(gameTime); // AlphaBlend first
            _dash.Draw(gameTime);     // Additive on top
            _crash.Draw(gameTime);

            // Text dosnt need to shake 
            spriteBatch.Begin();

            // Draw instructions and Game Timer
            spriteBatch.DrawString(font, "Use A or D / Left or Right to dodge!", new Vector2(25, 25), Color.Yellow * TransitionAlpha);
            spriteBatch.DrawString(font, $"{_runTime:c}", new Vector2(50, 75), Color.Yellow * TransitionAlpha);

            // Display for the game! (Will probably be another screen later)
            if (_isGameOver)
            {
                spriteBatch.DrawString(font, $"Game Over! Time reached: {_runTime:c}", new Vector2(25, 120), Color.Red * TransitionAlpha);
                spriteBatch.DrawString(font, "Press Enter for the menu", new Vector2(25, 170), Color.White * TransitionAlpha);
            }

            spriteBatch.End();
        }
    }
}
