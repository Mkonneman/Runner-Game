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
        private Texture2D _button;
        private MouseState _lastMouse;

        // Pictures pulled straight from the game art
        private Texture2D _player, _wallStone, _rock, _spike, _merchant, _gem, _dashIcon, _smashIcon, _door, _golem, _boulder;
        private static readonly Rectangle SpikeSource = new Rectangle(27, 0, 74, 80); // same crop as the game uses

        // Title and lines for each slide (the picture is picked in DrawPicture)
        private static readonly (string Title, string[] Lines)[] Slides =
        {
            ("Movement", new[] { "Left / Right to move", "Up to jump", "Shift for your ability", "Down to smash, or hold it", "to crouch in the arena", "", "Rebind keys in Settings" }),
            ("Walls", new[] { "Walls kill you in one hit!", "", "A red ! shows up right", "before one comes in", "", "Smash it before", "you run into it" }),
            ("Rocks", new[] { "Rocks fall from the ceiling", "and take 25% of your health", "", "Get under one and smash", "it for extra points" }),
            ("Spikes", new[] { "Crystal spikes slide in", "along the floor", "", "They take 50% of your health", "so jump over them!" }),
            ("The Caves", new[] { "Run through three caves:", "blue, orange, then green", "", "Each one gets harder", "", "Grab diamonds for gems,", "reach the door at the end!" }),
            ("The Hallway", new[] { "Past the door is a narrow", "stone hallway", "", "Boulders roll at you,", "crouch (Down) in the dips", "to let them pass over", "No weapons in here!" }),
            ("Stone Golem", new[] { "The boss waits in the arena", "Smash him to drain his health", "", "Jump or smash his fists", "Run from his punches", "Dodge his head laser,", "it sweeps from either side" }),
            ("Golem Shields", new[] { "Sometimes he curls up or", "his crystals glow blue", "", "He can't be hurt then!", "", "Watch for blinking marks,", "colored rocks drop there" }),
            ("The Shop", new[] { "Every 10 points = 1 gem", "", "Buy and upgrade gear,", "rarity goes grey, blue,", "purple, yellow then red", "", "Hover an item to see its path" }),
            ("Gear & Skins", new[] { "Pick a weapon, ability,", "armor and skin in Gear", "", "Skins come from the", "Skins tab in the Shop,", "each has its own particles", "(turn them down in Settings)" }),
        };
        private int _slide;
        private float _time;  // drives the little animations on the pictures

        private static readonly Rectangle PrevButton = new Rectangle(177, 400, 60, 44);
        private static readonly Rectangle NextButton = new Rectangle(617, 400, 60, 44);
        private static readonly Rectangle BackButton = new Rectangle(367, 400, 120, 44);
        private static readonly Rectangle PictureBox = new Rectangle(177, 95, 230, 285);

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
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");  // same stone button as the title

            var content = ScreenManager.Game.Content;
            _player = content.Load<Texture2D>(Shop.SkinFolder(Shop.Equipped(Slot.Skin)) + "/run");  // whatever skin you're wearing
            _door = content.Load<Texture2D>("Images/Gameplay/door");
            _golem = content.Load<Texture2D>("Images/Boss/golem");
            _boulder = content.Load<Texture2D>("Images/Arena/boulder");
            _wallStone = content.Load<Texture2D>("Images/Gameplay/wall_stone");
            _rock = content.Load<Texture2D>("Images/Gameplay/spike_rock");
            _spike = content.Load<Texture2D>("Images/Gameplay/stalactite");
            _merchant = content.Load<Texture2D>("Images/Shop/merchant");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _dashIcon = content.Load<Texture2D>("Images/HUD/CooldownShiftButton");
            _smashIcon = content.Load<Texture2D>("Images/HUD/SmashCooldownButton");

            // So the click that opened it doesn't also press something
            _lastMouse = VirtualMouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            var mouse = VirtualMouse.GetState();
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
            var mouse = VirtualMouse.GetState().Position;
            var (title, lines) = Slides[_slide];

            // PointClamp so the pixel art pictures stay sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Darken the title and put a panel behind everything
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(147, 20, 560, 440), new Color(10, 20, 30) * 0.92f * TransitionAlpha);
            DrawText(title, new Vector2(427 - ScreenManager.TitleFont.MeasureString(title).X * 0.45f, 30), Color.Yellow, 0.9f, ScreenManager.TitleFont);

            // Picture on the left, text on the right
            MenuButton.Draw(spriteBatch, _button, PictureBox, MenuButton.Tint(false) * TransitionAlpha);  // stone frame like the buttons
            DrawPicture(spriteBatch);
            for (int i = 0; i < lines.Length; i++)
                DrawText(lines[i], new Vector2(422, 120 + i * 32), Color.White, 0.4f);

            // Dots so you know which slide you're on
            for (int i = 0; i < Slides.Length; i++)
                spriteBatch.Draw(_pixel, new Rectangle(427 - Slides.Length * 10 + i * 20, 385, 10, 10), (i == _slide ? Color.Yellow : Color.Gray) * TransitionAlpha);

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
                    // The stone wall with a blinking red ! like the warning in game
                    spriteBatch.Draw(_wallStone, new Rectangle((int)center.X - 27, PictureBox.Y + 20, 54, 245), new Rectangle(0, 0, 72, 330), fade);
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
                    // The door at the end of the green cave
                    spriteBatch.Draw(_door, new Rectangle(PictureBox.X + 25, PictureBox.Y + 30, 180, 225), new Rectangle(0, 0, _door.Width / 6, _door.Height), fade);
                    break;
                case 5:
                    // A boulder rolling past
                    float roll = (_time * 60) % 150 - 75;  // stays inside the frame
                    spriteBatch.Draw(_boulder, center + new Vector2(-roll, 60), null, fade, -roll / 30f, new Vector2(_boulder.Width / 2f, _boulder.Height / 2f), 70f / _boulder.Width, SpriteEffects.None, 0f);
                    break;
                case 6:
                case 7:
                    // The golem, idling, or glowing while he can't be hurt
                    int g = _slide == 6 ? (int)(_time / 0.15f) % 4 : 3 + (int)(_time / 0.12f) % 3;
                    spriteBatch.Draw(_golem, new Vector2(center.X, PictureBox.Bottom - 30), new Rectangle(g * 100, (_slide == 6 ? 0 : 1) * 100, 100, 100), fade, 0f, new Vector2(50, 70), 2.6f, SpriteEffects.FlipHorizontally, 0f);
                    break;
                case 8:
                    // The merchant and a spinning-ish gem (Kinda just bobs, close enough)
                    spriteBatch.Draw(_merchant, new Rectangle(PictureBox.X + 15, PictureBox.Y + 10, 175, 265), fade);
                    spriteBatch.Draw(_gem, new Vector2(PictureBox.Right - 50, PictureBox.Y + 30 + MathF.Sin(_time * 3) * 6), null, fade, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                    break;
                case 9:
                    // Him in your skin, with a few sparkles circling
                    int f9 = (int)(_time / 0.08f) % 8;
                    spriteBatch.Draw(_player, center + new Vector2(0, 30), new Rectangle(f9 * 56, 0, 56, 37), fade, 0f, new Vector2(28, 37), 3.5f, SpriteEffects.None, 0f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = _time * 2 + k;
                        spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + MathF.Cos(a) * 80), (int)(center.Y - 20 + MathF.Sin(a * 1.3f) * 60), 4, 4), new Color(255, 230, 120) * TransitionAlpha);
                    }
                    break;
            }
        }

        // A box button, dim when it can't be used
        private void DrawButton(Rectangle button, string label, bool enabled, Point mouse)
        {
            var font = ScreenManager.MenuFont;
            bool hover = enabled && button.Contains(mouse);
            MenuButton.Draw(ScreenManager.SpriteBatch, _button, button, MenuButton.Tint(hover) * (enabled ? 1f : 0.4f) * TransitionAlpha);
            Vector2 size = font.MeasureString(label) * 0.6f;
            DrawText(label, new Vector2(button.Center.X - size.X / 2, button.Center.Y - size.Y / 2), (hover ? Color.Yellow : Color.White) * (enabled ? 1f : 0.4f), 0.6f);
        }

        // Text with a shadow
        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
