using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// How to play popup over the title, a slideshow with a picture and some text on each slide
    /// </summary>
    public class HowToPlayScreen : GameScreen
    {
        private Texture2D _pixel;
        private MouseState _lastMouse;

        // Pictures pulled straight from the game art
        private Texture2D _player, _wallStone, _wallCrystal, _rock, _spike, _merchant, _gem, _dashIcon, _smashIcon;
        private static readonly Rectangle SpikeSource = new Rectangle(27, 0, 74, 80); // same crop as the game uses

        // Title and lines for each slide (the picture is picked in DrawPicture)
        private static readonly (string Title, string[] Lines)[] Slides =
        {
            ("Movement", new[] { "A / D or Left / Right to move", "Space, W or Up to jump", "Shift to dash", "S or Down to smash", "", "The icons in the top right", "show when they're ready" }),
            ("Walls", new[] { "Walls kill you in one hit!", "", "A red ! shows up right", "before one comes in", "", "Smash it with S before", "you run into it" }),
            ("Rocks", new[] { "Rocks fall from the ceiling", "and take 25% of your health", "", "Get under one and smash", "it for extra points" }),
            ("Spikes", new[] { "Crystal spikes slide in", "along the floor", "", "They take 50% of your health", "so jump over them!" }),
            ("The Shop", new[] { "Every 10 points = 1 gem", "", "Spend gems on upgrades", "at the Shop on the title", "", "Faster dashes, more health,", "healing and more!" }),
        };
        private int _slide;
        private float _time;  // drives the little animations on the pictures

        private static readonly Rectangle PrevButton = new Rectangle(150, 400, 60, 44);
        private static readonly Rectangle NextButton = new Rectangle(590, 400, 60, 44);
        private static readonly Rectangle BackButton = new Rectangle(340, 400, 120, 44);
        private static readonly Rectangle PictureBox = new Rectangle(150, 95, 230, 285);

        public HowToPlayScreen()
        {
            // Popup so the title stays visible behind it
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.2);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            var content = ScreenManager.Game.Content;
            _player = content.Load<Texture2D>("Images/Gameplay/player_run");
            _wallStone = content.Load<Texture2D>("Images/Gameplay/wall_stone");
            _wallCrystal = content.Load<Texture2D>("Images/Gameplay/wall_crystal");
            _rock = content.Load<Texture2D>("Images/Gameplay/spike_rock");
            _spike = content.Load<Texture2D>("Images/Gameplay/stalactite");
            _merchant = content.Load<Texture2D>("Images/Shop/merchant");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _dashIcon = content.Load<Texture2D>("Images/HUD/CooldownShiftButton");
            _smashIcon = content.Load<Texture2D>("Images/HUD/SmashCooldownButton");

            // So the click that opened it doesn't also press something
            _lastMouse = Mouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            var mouse = Mouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            _lastMouse = mouse;

            // Left / Right flip slides, Enter goes forward (and closes on the last one)
            bool next = input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D) || input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space) || (click && NextButton.Contains(mouse.Position));
            bool prev = input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A) || (click && PrevButton.Contains(mouse.Position));
            bool back = input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back) || (click && BackButton.Contains(mouse.Position));

            if (prev) _slide = Math.Max(0, _slide - 1);
            if (next)
            {
                if (_slide == Slides.Length - 1) back = true;
                else _slide++;
            }
            if (back) ExitScreen();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var mouse = Mouse.GetState().Position;
            var (title, lines) = Slides[_slide];

            // PointClamp so the pixel art pictures stay sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Darken the title and put a panel behind everything
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(120, 20, 560, 440), new Color(10, 20, 30) * 0.92f * TransitionAlpha);
            DrawText(title, new Vector2(400 - ScreenManager.TitleFont.MeasureString(title).X * 0.45f, 30), Color.Yellow, 0.9f, ScreenManager.TitleFont);

            // Picture on the left, text on the right
            spriteBatch.Draw(_pixel, PictureBox, new Color(25, 40, 60) * TransitionAlpha);
            DrawPicture(spriteBatch);
            for (int i = 0; i < lines.Length; i++)
                DrawText(lines[i], new Vector2(400, 120 + i * 32), Color.White, 0.42f);

            // Dots so you know which slide you're on
            for (int i = 0; i < Slides.Length; i++)
                spriteBatch.Draw(_pixel, new Rectangle(400 - Slides.Length * 10 + i * 20, 385, 10, 10), (i == _slide ? Color.Yellow : Color.Gray) * TransitionAlpha);

            // < Back > buttons
            DrawButton(PrevButton, "<", _slide > 0, mouse);
            DrawButton(BackButton, "Back", true, mouse);
            DrawButton(NextButton, _slide == Slides.Length - 1 ? "OK" : ">", true, mouse);

            spriteBatch.End();
        }

        // The picture for the current slide, inside PictureBox
        private void DrawPicture(SpriteBatch spriteBatch)
        {
            var fade = Color.White * TransitionAlpha;
            var center = new Vector2(PictureBox.Center.X, PictureBox.Center.Y);
            switch (_slide)
            {
                case 0:
                    // Him running in place (same animation as the game) with the ability icons under him
                    int frame = (int)(_time / 0.08f) % 8;
                    spriteBatch.Draw(_player, center + new Vector2(0, 10), new Rectangle(frame * 56, 0, 56, 37), new Color(185, 205, 255) * TransitionAlpha, 0f, new Vector2(28, 37), 3.5f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_dashIcon, new Rectangle((int)center.X - 60, (int)center.Y + 40, 48, 48), fade);
                    spriteBatch.Draw(_smashIcon, new Rectangle((int)center.X + 12, (int)center.Y + 40, 48, 48), fade);
                    break;
                case 1:
                    // Both wall looks with a blinking red ! like the warning in game
                    spriteBatch.Draw(_wallStone, new Rectangle((int)center.X - 70, PictureBox.Y + 20, 54, 245), new Rectangle(0, 0, 72, 330), fade);
                    spriteBatch.Draw(_wallCrystal, new Rectangle((int)center.X + 16, PictureBox.Y + 20, 54, 245), new Rectangle(0, 0, 72, 330), fade);
                    if ((int)(_time * 4) % 2 == 0) DrawText("!", new Vector2(center.X - 8, PictureBox.Y + 4), Color.Red, 1.2f);
                    break;
                case 2:
                    // A rock bobbing down like it's falling
                    float drop = (_time * 60) % 60 - 30;
                    spriteBatch.Draw(_rock, center + new Vector2(0, drop), null, fade, 0f, new Vector2(64), 1.4f, SpriteEffects.None, 0f);
                    break;
                case 3:
                    // The crystal spike, tinted blue like in the cave
                    spriteBatch.Draw(_spike, new Vector2(center.X, PictureBox.Bottom - 40), SpikeSource, new Color(150, 200, 255) * TransitionAlpha, 0f, new Vector2(SpikeSource.Width / 2f, SpikeSource.Height), 2.5f, SpriteEffects.None, 0f);
                    break;
                case 4:
                    // The merchant and a spinning-ish gem (Kinda just bobs, close enough)
                    spriteBatch.Draw(_merchant, new Rectangle(PictureBox.X + 15, PictureBox.Y + 10, 175, 265), fade);
                    spriteBatch.Draw(_gem, new Vector2(PictureBox.Right - 50, PictureBox.Y + 30 + MathF.Sin(_time * 3) * 6), null, fade, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                    break;
            }
        }

        // A box button, dim when it can't be used
        private void DrawButton(Rectangle button, string label, bool enabled, Point mouse)
        {
            var font = ScreenManager.MenuFont;
            bool hover = enabled && button.Contains(mouse);
            ScreenManager.SpriteBatch.Draw(_pixel, button, (hover ? new Color(30, 90, 60) : new Color(20, 40, 55)) * (enabled ? 1f : 0.4f) * TransitionAlpha);
            Vector2 size = font.MeasureString(label) * 0.6f;
            DrawText(label, new Vector2(button.Center.X - size.X / 2, button.Center.Y - size.Y / 2), (hover ? Color.Yellow : Color.White) * (enabled ? 1f : 0.4f), 0.6f);
        }

        // Text with a shadow
        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
