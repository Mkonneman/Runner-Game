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
    /// The merchants shop, opened from the title to spend gems on upgrades
    /// </summary>
    public class ShopScreen : GameScreen
    {
        // Art for the shop
        private Texture2D _background;
        private Texture2D _merchant;
        private Texture2D _gem;
        private Texture2D[] _icons;
        private Texture2D _pixel;

        // Shop music, the title music fades out then this fades in (sounds so much smoother)
        private Song _shopMusic;
        private const float MusicFadeSpeed = 2f;   // volume per second (half a second each way)

        // Flickering lamp glows with green sparks floating up from them
        private Texture2D _glow;
        private static readonly Vector2[] Lamps = { new(237, 163), new(397, 140), new(497, 252), new(285, 330) };
        private SparkParticleSystem _sparks;
        private float _time;

        // The merchant stands close to the screen (feet cut off), breathing, with glowing eyes that blink now and then
        private static readonly Vector2 MerchantFeet = new(10, 550);           // bottom left of the art, below the screen
        private static readonly Vector2[] Eyes = { new(192, 128), new(230, 128) }; // eye centers in merchant.png
        private float _blinkTimer = 3f;

        // Icon files in the same order as the Upgrade enum
        private static readonly string[] IconNames = { "Images/Shop/upgrade_boots", "Images/Shop/upgrade_dash", "Images/Shop/upgrade_heart", "Images/Shop/upgrade_hammer", "Images/Shop/upgrade_shield", "Images/Shop/upgrade_points" };

        // Item panels are 3 across and 2 down next to the merchant
        private const int PanelWidth = 150;
        private const int PanelHeight = 130;
        private const int GridX = 300;
        private const int GridY = 90;

        // Which item is picked (the last one is the Back button)
        private int _selected;
        private MouseState _lastMouse;

        // Short message after trying to buy ("Not enough gems")
        private string _message = "";
        private float _messageTime;

        public ShopScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.3);
            TransitionOffTime = TimeSpan.FromSeconds(0.3);
        }

        public override void Activate()
        {
            var content = ScreenManager.Game.Content;
            _background = content.Load<Texture2D>("Images/Shop/shop_bg");
            _merchant = content.Load<Texture2D>("Images/Shop/merchant");
            _shopMusic = content.Load<Song>("Audio/Music/plunder");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _icons = Array.ConvertAll(IconNames, name => content.Load<Texture2D>(name));
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _sparks = new SparkParticleSystem(ScreenManager.Game, new Color(120, 255, 140), 1.5f, 0.35f, new Vector2(0, -25), new Vector2(0, -25), wobble: 10, wobbleSpeed: 4);
            _sparks.Initialize();

            // So the click that opened the shop doesn't also buy something
            _lastMouse = Mouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _messageTime -= dt;

            // When leaving the shop music fades with the screen, otherwise fade the old song out and ours in
            if (IsExiting) MediaPlayer.Volume = MathF.Min(MediaPlayer.Volume, TransitionAlpha * Settings.MusicVolume);
            else if (MediaPlayer.Queue.ActiveSong != _shopMusic || MediaPlayer.State != MediaState.Playing)
            {
                MediaPlayer.Volume = MathF.Max(0, MediaPlayer.Volume - MusicFadeSpeed * dt);
                if (MediaPlayer.Volume == 0 || MediaPlayer.State != MediaState.Playing)
                {
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(_shopMusic);
                }
            }
            else MediaPlayer.Volume = MathF.Min(Settings.MusicVolume, MediaPlayer.Volume + MusicFadeSpeed * dt);
            _time += dt;

            // Blink every 2-5 seconds (the eyes go dark while it's below zero)
            _blinkTimer -= dt;
            if (_blinkTimer < -0.12f) _blinkTimer = RandomHelper.NextFloat(2f, 5f);

            // A few sparks a second, drifting up and wobbling until they fade
            if (RandomHelper.Next(0, 9) == 0)
                _sparks.Emit(Lamps[RandomHelper.Next(0, Lamps.Length - 1)] + new Vector2(RandomHelper.NextFloat(-8, 8), 0));
            _sparks.Update(gameTime);
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            int count = Shop.Items.Length;

            // Arrows move around the grid (Down from the bottom row goes to Back)
            if (input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D)) _selected = Math.Min(_selected + 1, count);
            if (input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A)) _selected = Math.Max(_selected - 1, 0);
            if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _selected = Math.Min(_selected + 3, count);
            if (input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W)) _selected = _selected == count ? count - 1 : Math.Max(_selected - 3, 0);
            bool choose = input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space);

            // Hovering picks an item, clicking chooses it
            var mouse = Mouse.GetState();
            for (int i = 0; i <= count; i++)
            {
                if (ItemRectangle(i).Contains(mouse.Position))
                {
                    if (mouse.Position != _lastMouse.Position) _selected = i;
                    if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released)
                    {
                        _selected = i;
                        choose = true;
                    }
                }
            }
            _lastMouse = mouse;

            // Esc always leaves
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
            {
                ExitScreen();
                return;
            }

            if (!choose) return;

            if (_selected == count)
            {
                ExitScreen();
            }
            else
            {
                var upgrade = (Upgrade)_selected;
                if (Shop.Level(upgrade) == Shop.MaxLevel(upgrade)) _message = "That's maxed out";
                else _message = Shop.Buy(upgrade) ? $"{Shop.Items[_selected].Name} is now level {Shop.Level(upgrade)}!" : "Not enough gems";
                _messageTime = 2f;
            }
        }

        // Shows what you have now and what the next level gives (just Now once its maxed)
        private static string LevelInfo(Upgrade upgrade)
        {
            var item = Shop.Items[(int)upgrade];
            int level = Shop.Level(upgrade);
            string now = $"Now: {item.Show(item.Values[level])}";
            return level == Shop.MaxLevel(upgrade) ? now + "  (max)" : $"{now}\nNext: {item.Show(item.Values[level + 1])}";
        }

        public override void Unload()
        {
            // The title starts its own music again (fading in) once the shop is gone
            MediaPlayer.Stop();
        }

        // Clickable area of an item panel (index 6 is the Back button)
        private Rectangle ItemRectangle(int i)
        {
            if (i == Shop.Items.Length) return new Rectangle(660, 410, 120, 48);
            return new Rectangle(GridX + i % 3 * (PanelWidth + 10), GridY + i / 3 * (PanelHeight + 10), PanelWidth, PanelHeight);
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var fade = Color.White * TransitionAlpha;

            // PointClamp keeps the pixel art sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_background, Vector2.Zero, fade);

            // Breathing, he stretches up and down slowly from his feet (Kinda creepy, I love it)
            float breath = 1f + 0.015f * MathF.Sin(_time * 2.2f);
            spriteBatch.Draw(_merchant, MerchantFeet, null, fade, 0f, new Vector2(0, _merchant.Height), new Vector2(1f, breath), SpriteEffects.None, 0f);

            // Blinking puts dark lids over the eyes
            if (_blinkTimer < 0)
            {
                foreach (var eye in Eyes)
                    spriteBatch.Draw(_pixel, new Rectangle((int)EyePosition(eye, breath).X - 12, (int)EyePosition(eye, breath).Y - 9, 24, 18), new Color(20, 20, 24) * TransitionAlpha);
            }
            spriteBatch.End();

            // Lamp glows flicker (two waves plus a little noise) and the sparks glow on top, added light so they brighten the art
            spriteBatch.Begin(blendState: BlendState.Additive);
            for (int i = 0; i < Lamps.Length; i++)
            {
                float flicker = 0.35f + 0.12f * MathF.Sin(_time * 11 + i * 2) + 0.08f * MathF.Sin(_time * 23 + i) + RandomHelper.NextFloat(-0.04f, 0.04f);
                spriteBatch.Draw(_glow, Lamps[i], null, new Color(80, 255, 120) * flicker * TransitionAlpha, 0f, new Vector2(8), 5f, SpriteEffects.None, 0f);
            }
            // His eyes glow, pulsing slowly (off while blinking)
            if (_blinkTimer >= 0)
            {
                foreach (var eye in Eyes)
                    spriteBatch.Draw(_glow, EyePosition(eye, breath), null, new Color(220, 255, 60) * (0.45f + 0.15f * MathF.Sin(_time * 3)) * TransitionAlpha, 0f, new Vector2(8), 2.2f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();

            _sparks.Opacity = TransitionAlpha;
            _sparks.Draw(gameTime);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Gems in the top left
            spriteBatch.Draw(_gem, new Vector2(20, 18), fade);
            spriteBatch.DrawString(font, Shop.Gems.ToString(), new Vector2(60, 14), Color.Cyan * TransitionAlpha, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

            DrawText("Shop", new Vector2(400 - font.MeasureString("Shop").X * 0.6f, 14), Color.Yellow, 1.2f);

            // Item panels with the icon, name, green level bars and the next price (or Max)
            for (int i = 0; i < Shop.Items.Length; i++)
            {
                var rect = ItemRectangle(i);
                var item = Shop.Items[i];
                int level = Shop.Level((Upgrade)i);
                int maxLevel = Shop.MaxLevel((Upgrade)i);
                bool maxed = level == maxLevel;
                bool selected = i == _selected;

                spriteBatch.Draw(_pixel, rect, (selected ? new Color(30, 90, 60) : new Color(10, 20, 30)) * 0.85f * TransitionAlpha);
                spriteBatch.Draw(_icons[i], new Vector2(rect.Center.X - 24, rect.Y + 8), (maxed ? Color.Gray : Color.White) * TransitionAlpha);
                DrawText(item.Name, new Vector2(rect.Center.X - font.MeasureString(item.Name).X * 0.2f, rect.Y + 58), selected ? Color.Yellow : Color.White, 0.4f);

                // One bar per level, lit green once bought
                for (int b = 0; b < maxLevel; b++)
                {
                    var bar = new Rectangle(rect.Center.X - (maxLevel * 36 - 6) / 2 + b * 36, rect.Y + 84, 30, 7);
                    spriteBatch.Draw(_pixel, bar, (b < level ? Color.LimeGreen : Color.Black * 0.6f) * TransitionAlpha);
                }

                if (maxed)
                {
                    DrawText("Max", new Vector2(rect.Center.X - font.MeasureString("Max").X * 0.2f, rect.Y + 100), Color.LimeGreen, 0.4f);
                }
                else
                {
                    int cost = Shop.Price((Upgrade)i);
                    string price = cost.ToString();
                    float priceWidth = font.MeasureString(price).X * 0.4f;
                    spriteBatch.Draw(_gem, new Rectangle((int)(rect.Center.X - (priceWidth + 22) / 2), rect.Y + 102, 18, 18), fade);
                    DrawText(price, new Vector2(rect.Center.X - (priceWidth + 22) / 2 + 22, rect.Y + 100), Shop.Gems >= cost ? Color.Cyan : Color.IndianRed, 0.4f);
                }
            }

            // What the picked item does (or the last buy message)
            string info = _messageTime > 0 ? _message : _selected < Shop.Items.Length ? LevelInfo((Upgrade)_selected) : "Back to the title";
            int boxWidth = 3 * PanelWidth + 20;
            float infoScale = MathF.Min(0.4f, (boxWidth - 20) / font.MeasureString(info).X);
            spriteBatch.Draw(_pixel, new Rectangle(GridX, 368, boxWidth, 40), Color.Black * 0.7f * TransitionAlpha);
            DrawText(info, new Vector2(GridX + 10, 370), Color.White, infoScale);

            // Back button
            var back = ItemRectangle(Shop.Items.Length);
            bool backSelected = _selected == Shop.Items.Length;
            spriteBatch.Draw(_pixel, back, (backSelected ? new Color(30, 90, 60) : new Color(10, 20, 30)) * 0.85f * TransitionAlpha);
            DrawText("Back", new Vector2(back.Center.X - font.MeasureString("Back").X * 0.3f, back.Y + 8), backSelected ? Color.Yellow : Color.White, 0.6f);

            spriteBatch.End();
        }

        // Where an eye ends up on screen after the breathing stretch
        private Vector2 EyePosition(Vector2 eye, float breath) => MerchantFeet + new Vector2(eye.X, -(_merchant.Height - eye.Y) * breath);

        // Text with a shadow so it reads on the busy background
        private void DrawText(string text, Vector2 position, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
