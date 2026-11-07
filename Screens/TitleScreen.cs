using System;
using System.Collections.Generic;
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
            ("Images/Menu/menu_sky", 0f, 0f),
            ("Images/Menu/menu_mountains", 15f, 10f),
            ("Images/Menu/menu_trees", 70f, 68f),
            ("Images/Menu/menu_ground", 220f, 0f),
        };

        // Background layers, cave and music for the title
        private Texture2D[] _layers;
        private Texture2D _cave;
        private Song _titleMusic;

        // The player running on the title
        private Runner _runner;
        private const float GroundY = 408f;      // top of the ground layer
        private const float RunnerStartX = 200f;
        private const float CaveMouthX = 552f;   // middle of the cave opening
        private const float RunSpeed = 220f;     // same as the ground so his steps match

        // How long the background has been scrolling
        private float _scrollTime;

        // Bools for running into the cave and starting the game
        private bool _enteringCave;
        private bool _gameStarted;

        // Timer for the blinking "Press Enter" text
        private float _blinkTimer;

        // Stone buttons in a row under the logo (click them, or Tab for the shop)
        private int _selectedButton = -1;  // which button the keys / controller picked (-1 means none, Enter just plays)
        private static readonly Rectangle ShopButton = new Rectangle(129, 245, 140, 48);
        private static readonly Rectangle GearButton = new Rectangle(281, 245, 140, 48);
        private static readonly Rectangle HowToPlayButton = new Rectangle(433, 245, 140, 48);
        private static readonly Rectangle SettingsButton = new Rectangle(585, 245, 140, 48);
        private Texture2D _button;
        private Texture2D _shopIcon;  // gem pouch with a hammer and gear (oooh)
        private Texture2D _logo;

        // Glowing red eyes in the cave, with sparks drifting up from them
        private static readonly Vector2[] CaveEyes = { new(95, 171), new(119, 171) };  // eye centers in menu_cave.png
        private Texture2D _glow;
        private SparkParticleSystem _eyeSparks;
        private Texture2D _pixel;
        private MouseState _lastMouse;

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
            _cave = content.Load<Texture2D>("Images/Menu/menu_cave");
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = content.Load<Texture2D>("Images/Menu/button");
            _shopIcon = content.Load<Texture2D>("Images/Menu/shop_icon");
            _logo = content.Load<Texture2D>("Images/Menu/logo");
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _eyeSparks = new SparkParticleSystem(ScreenManager.Game, new Color(255, 60, 40), 1.2f, 0.3f, new Vector2(0, -20), new Vector2(0, -20), wobble: 8, wobbleSpeed: 3);
            _eyeSparks.Initialize();
            _titleMusic = content.Load<Song>("Audio/Music/eraser");

            // Same runner as the game
            _runner = new Runner();
            _runner.LoadContent(content);
            ResetScene();
        }

        // Puts the runner back at the start (for when the player comes back from a run)
        private void ResetScene()
        {
            _runner.Position = new Vector2(RunnerStartX, GroundY + 36); // feet on the ground (the frame has a 1px gap under them)
            _enteringCave = false;
            _gameStarted = false;
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _blinkTimer += dt;

            // A few sparks a second from the cave's eyes, drifting up and wobbling until they fade
            if (RandomHelper.Next(0, 6) == 0)
                _eyeSparks.Emit(EyePosition(CaveEyes[RandomHelper.Next(0, 1)]) + new Vector2(RandomHelper.NextFloat(-4, 4), 0));
            _eyeSparks.Update(gameTime);

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
                MediaPlayer.Volume = 0;  // fades in below
                MediaPlayer.Play(_titleMusic);
            }

            // Fade the title music in (after the shop or a crash)
            if (!coveredByOtherScreen) MediaPlayer.Volume = MathF.Min(Settings.MusicVolume, MediaPlayer.Volume + 2f * (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Left / Right (or the d-pad / stick) picks one of the 3 buttons, Up or Down drops back to Play
            if (input.IsNewKeyPress(Keys.Right)) _selectedButton = Math.Min(_selectedButton + 1, 3);
            if (input.IsNewKeyPress(Keys.Left)) _selectedButton = Math.Max(_selectedButton - 1, 0);
            if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.Up)) _selectedButton = -1;

            // Enter / A presses the picked button, or starts the run if none is picked. Space and Start always start
            bool confirm = input.IsNewKeyPress(Keys.Enter);
            bool start = input.CurrentKeyboardState.IsKeyDown(Keys.Space) && input.LastKeyboardState.IsKeyUp(Keys.Space) || input.IsNewButtonPress(Buttons.Start) || input.IsNewButtonPress(Buttons.RightTrigger);
            if (confirm && _selectedButton < 0) start = true;
            if (start) _enteringCave = true;

            // Clicking works too, and Tab is still the shop shortcut
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            if (mouse.Position != _lastMouse.Position) _selectedButton = -1;  // mouse takes over the highlight
            _lastMouse = mouse;
            int open = confirm && !start ? _selectedButton : -1;
            if (click && ShopButton.Contains(mouse.Position) || input.IsNewKeyPress(Keys.Tab)) open = 0;
            if (click && GearButton.Contains(mouse.Position)) open = 1;
            if (click && HowToPlayButton.Contains(mouse.Position)) open = 2;
            if (click && SettingsButton.Contains(mouse.Position)) open = 3;
            if (!_enteringCave && open >= 0)
            {
                ScreenManager.AddScreen(open == 0 ? new ShopScreen() : open == 1 ? new GearScreen() : open == 2 ? new HowToPlayScreen() : new SettingsScreen());
                return;
            }

            // Esc quits the game (but not B on a controller, way too easy to hit by accident)
            if (input.CurrentKeyboardState.IsKeyDown(Keys.Escape) && input.LastKeyboardState.IsKeyUp(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
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

            // Cave eyes with a slow red pulse and sparks, additive so they actually glow (oooh spooky)
            spriteBatch.Begin(blendState: BlendState.Additive);
            float pulse = 0.55f + 0.2f * MathF.Sin(_blinkTimer * 2.5f);
            foreach (var eye in CaveEyes)
            {
                spriteBatch.Draw(_glow, EyePosition(eye), null, new Color(255, 40, 30) * pulse * TransitionAlpha, 0f, new Vector2(8), 2.5f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();

            _eyeSparks.Opacity = TransitionAlpha;
            _eyeSparks.Draw(gameTime);

            // Text gets its own Begin so it stays smooth
            spriteBatch.Begin();

            // Rock Rush logo at the top
            spriteBatch.Draw(_logo, new Vector2((viewport.Width - _logo.Width) / 2, 4), Color.White * TransitionAlpha);

            // Blinks on and off about twice a second (hidden once he heads in)
            if (!_enteringCave && (int)(_blinkTimer * 2) % 2 == 0)
            {
                DrawCentered(Controls.UsingPad ? "Press RT to Start" : "Press Enter to Start", 186, Color.Yellow, 0.75f, ScreenManager.BigFont);
            }

            // Buttons (lit up when the mouse is over them)
            DrawButton(ShopButton, "Shop", 0);
            DrawButton(GearButton, "Gear", 1);
            DrawButton(HowToPlayButton, "How to Play", 2);
            DrawButton(SettingsButton, "Settings", 3);

            spriteBatch.End();
        }

        // Where an eye of the cave is on screen (the cave is drawn at the right edge, sitting on the ground)
        private Vector2 EyePosition(Vector2 eye)
        {
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            return new Vector2(viewport.Width - _cave.Width + 40, GroundY + 30 - _cave.Height) + eye;
        }

        // A button with its label centered in it
        private void DrawButton(Rectangle button, string label, int index)
        {
            var font = ScreenManager.TitleFont;
            bool hover = button.Contains(VirtualMouse.GetState().Position) || index == _selectedButton;
            ScreenManager.SpriteBatch.Draw(_button, button, (hover ? Color.White : new Color(170, 180, 200)) * TransitionAlpha);

            // Label shrinks to fit long names like How to Play
            float scale = MathF.Min(0.65f, (button.Width - 28) / font.MeasureString(label).X);
            Vector2 size = font.MeasureString(label) * scale;
            // Shop gets the gem pouch on the left and its text nudged over to make room
            bool hasIcon = label == "Shop";
            if (hasIcon) ScreenManager.SpriteBatch.Draw(_shopIcon, new Vector2(button.X + 14, button.Center.Y - _shopIcon.Height / 2), Color.White * TransitionAlpha);
            var position = new Vector2(button.Center.X - size.X / 2 + (hasIcon ? 20 : 0), button.Center.Y - size.Y / 2);
            ScreenManager.SpriteBatch.DrawString(font, label, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, label, position, (hover ? Color.Yellow : Color.White) * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Draws text centered left to right on the screen (with a shadow so it stands out)
        private void DrawCentered(string text, float y, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
