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
        // Parallax Scrolling with various speeds oooo how fancy!
        private static readonly (string Name, float Speed, float Y)[] LayerInfo =
        {
            ("menu_sky", 0f, 0f),
            ("menu_mountains", 15f, 10f),
            ("menu_trees", 70f, 68f),
            ("menu_ground", 220f, 0f),
        };

        // Background layers, cave and music for the title
        private Texture2D[] _layers;
        private Texture2D _cave;
        private Song _titleMusic;

        // The player running on the title
        private Runner _runner;
        private const float GroundY = 408f;      // top of the ground layer
        private const float RunnerStartX = 200f;
        private const float CaveMouthX = 525f;   // middle of the cave opening
        private const float RunSpeed = 220f;     // same as the ground so his steps match

        // How long the background has been scrolling
        private float _scrollTime;

        // Bools for running into the cave and starting the game
        private bool _enteringCave;
        private bool _gameStarted;

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
            _layers = Array.ConvertAll(LayerInfo, layer => content.Load<Texture2D>(layer.Name));
            _cave = content.Load<Texture2D>("menu_cave");
            _titleMusic = content.Load<Song>("Final Dash");

            // Same runner as the game
            _runner = new Runner();
            _runner.LoadContent(content);
            ResetScene();
        }

        // Puts the runner back at the start (for when the player comes back from a run)
        private void ResetScene()
        {
            _runner.Position = new Vector2(RunnerStartX, GroundY + 4); // 1px gap under his feet in the frame
            _enteringCave = false;
            _gameStarted = false;
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _blinkTimer += dt;

            // Reset once the game screen fully covers the title
            if (ScreenState == ScreenState.Hidden) ResetScene();

            // Background scrolls until Enter, then he runs into the cave and the game starts
            if (!_enteringCave)
            {
                _scrollTime += dt;
            }
            else if (_runner.Position.X < CaveMouthX)
            {
                _runner.Position = new Vector2(MathF.Min(_runner.Position.X + RunSpeed * dt, CaveMouthX), _runner.Position.Y);
            }
            else if (!_gameStarted)
            {
                _gameStarted = true;
                ScreenManager.AddScreen(new GameplayScreen());
            }

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
            // Enter or Space sends the runner into the cave (the game starts when he gets there)
            if (input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space) || input.IsNewButtonPress(Buttons.Start))
            {
                _enteringCave = true;
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
            var fade = Color.White * TransitionAlpha;

            // PointClamp keeps the pixel art sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            for (int i = 0; i < _layers.Length; i++)
            {
                // Cave goes in front of the trees but behind the ground (hides its bottom)
                if (i == _layers.Length - 1)
                {
                    spriteBatch.Draw(_cave, new Vector2(viewport.Width - _cave.Width + 40, GroundY + 30 - _cave.Height), fade); // +40 hides its flat right edge off screen
                }

                // Tile the layer across the screen, shifted by how far it has scrolled
                var texture = _layers[i];
                float offset = _scrollTime * LayerInfo[i].Speed % texture.Width;
                for (float x = -MathF.Floor(offset); x < viewport.Width; x += texture.Width)
                {
                    spriteBatch.Draw(texture, new Vector2(x, LayerInfo[i].Y), fade);
                }
            }

            // Fades him into the dark as he enters the cave
            float inside = MathHelper.Clamp((_runner.Position.X - (CaveMouthX - 60)) / 60, 0, 1);
            _runner.Color = Color.Lerp(Color.White, Color.Black, inside) * TransitionAlpha;
            _runner.Draw(gameTime, spriteBatch);

            spriteBatch.End();

            // Text gets its own Begin so it stays smooth
            spriteBatch.Begin();

            DrawCentered("Runner Game", 14, Color.Yellow, 2f);
            DrawCentered("Dodge the bolders!", 108, Color.White, 0.8f);

            // Blinks on and off about twice a second (hidden once he heads in)
            if (!_enteringCave && (int)(_blinkTimer * 2) % 2 == 0)
            {
                DrawCentered("Press Enter to Start", 148, Color.Yellow, 1f);
            }

            DrawCentered("A / D or Left / Right to move", 198, Color.White, 0.6f);
            DrawCentered("Esc to Quit", 226, Color.White, 0.6f);

            spriteBatch.End();
        }

        // Draws text centered left to right on the screen (with a shadow so it stands out)
        private void DrawCentered(string text, float y, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
