using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The merchants shop, buy new gear and upgrade what you own (equipping happens in the Gear screen)
    /// </summary>
    public class ShopScreen : GameScreen
    {
        // Art for the shop
        private Texture2D _background;
        private Texture2D _merchant;
        private Texture2D _gem;
        private Texture2D _button;
        private Texture2D[] _icons;   // one per item, same order as Shop.Items
        private Texture2D _pixel;

        // Shop music, the title music fades out then this fades in (sounds so much smoother)
        private Song _shopMusic;
        private SoundEffect _buySound;
        private const float MusicFadeSpeed = 2f;   // volume per second (half a second each way)

        // Flickering lamp glows with green sparks floating up from them
        private Texture2D _glow;
        private static readonly Vector2[] Lamps = { new(237, 163), new(397, 140), new(497, 252), new(285, 330) };
        private SparkParticleSystem _sparks;
        private float _time;

        // The merchant stands close to the screen (feet cut off), breathing, with glowing eyes that blink now and then
        private static readonly Vector2 MerchantFeet = new(60, 590);           // bottom left of the shopkeeper, below the screen so she's cut off under the knees
        private static readonly Vector2[] Eyes = { };  // the new shopkeeper has normal eyes, no glow (the old merchant had 192,128 and 230,128)
        private float _blinkTimer = 3f;

        // Tabs for each gear slot across the top, items in a 4 wide grid under them, then Gear and Back at the bottom
        private static readonly string[] TabNames = { "Weapons", "Abilities", "Armor", "Skins" };
        private const int GridX = 330;
        private const int GridY = 108;
        private const int Columns = 4;
        private const int PanelWidth = 118;
        private const int PanelHeight = 126;
        private static readonly Rectangle GearButton = new Rectangle(330, 420, 150, 44);
        private static readonly Rectangle BackButton = new Rectangle(684, 420, 150, 44);
        private static Rectangle TabRectangle(int t) => new Rectangle(GridX + t * 126, 60, 120, 38);  // 4 tabs fit across the grid
        private static Rectangle PanelRectangle(int i) => new Rectangle(GridX + i % Columns * (PanelWidth + 8), GridY + i / Columns * (PanelHeight + 6), PanelWidth, PanelHeight);

        // Where the selection is, row -1 is the tabs, 0 and 1 are items, 2 is the bottom buttons
        private int _tab;
        private int _row;
        private int _col;
        private MouseState _lastMouse;

        // Short message after trying to buy ("Not enough gems")
        private string _message = "";
        private string _cheat = "";
        private float _messageTime;

        public ShopScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.3);
            TransitionOffTime = TimeSpan.FromSeconds(0.3);
        }

        // Items on the current tab (only the ones in the shop right now)
        private Item[] TabItems => Shop.Items.Where(i => i.Slot == (Slot)_tab && i.InShop).ToArray();
        private int ItemRows => (TabItems.Length + Columns - 1) / Columns;
        private int SelectedIndex => _row >= 0 && _row < ItemRows ? _row * Columns + _col : -1;

        public override void Activate()
        {
            var content = ScreenManager.Game.Content;
            _background = content.Load<Texture2D>("Images/Shop/shop_bg");
            _merchant = content.Load<Texture2D>("Images/Shop/merchant");
            _shopMusic = content.Load<Song>("Audio/Music/plunder");
            _buySound = content.Load<SoundEffect>("Audio/Sfx/buy");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _button = content.Load<Texture2D>("Images/Menu/button");
            _icons = Array.ConvertAll(Shop.Items, item => content.Load<Texture2D>(item.Icon));
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _sparks = new SparkParticleSystem(ScreenManager.Game, new Color(120, 255, 140), 1.5f, 0.35f, new Vector2(0, -25), new Vector2(0, -25), wobble: 10, wobbleSpeed: 4);
            _sparks.Initialize();

            // So the click that opened the shop doesn't also buy something
            _lastMouse = VirtualMouse.GetState();
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

        private void SwitchTab(int tab)
        {
            _tab = (tab + TabNames.Length) % TabNames.Length;
            if (_row >= 0 && _row < 2) { _row = 0; _col = 0; }
        }

        // Keeps the column inside the row so you never land on an empty spot
        private int RowLength(int row) => row == 2 ? 2 : row < 0 ? TabNames.Length : Math.Min(Columns, TabItems.Length - row * Columns);

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Cheat code, type RICH in the shop for basically infinite gems
            foreach (var key in new[] { Keys.R, Keys.I, Keys.C, Keys.H })
                if (input.IsNewKeyPress(key)) _cheat = (_cheat + key).Length > 16 ? key.ToString() : _cheat + key;
            if (_cheat.EndsWith("RICH"))
            {
                _cheat = "";
                Shop.AddGems(999999 - Shop.Gems);
                _message = "Cheat on, gems maxed!";
                _messageTime = 2.5f;
                _buySound.Play(0.8f, 0.3f, 0f);
            }

            // Q / E or the bumpers flip tabs from anywhere
            if (input.IsNewKeyPress(Keys.Q) || input.IsNewButtonPress(Buttons.LeftShoulder)) SwitchTab(_tab - 1);
            if (input.IsNewKeyPress(Keys.E) || input.IsNewButtonPress(Buttons.RightShoulder)) SwitchTab(_tab + 1);

            // Arrows move around tabs -> items -> buttons
            if (input.IsNewKeyPress(Keys.Left))
            {
                if (_row < 0) SwitchTab(_tab - 1); else _col = Math.Max(0, _col - 1);
            }
            if (input.IsNewKeyPress(Keys.Right))
            {
                if (_row < 0) SwitchTab(_tab + 1); else _col = Math.Min(RowLength(_row) - 1, _col + 1);
            }
            if (input.IsNewKeyPress(Keys.Down)) _row = _row < 0 ? 0 : _row < ItemRows - 1 ? _row + 1 : 2;
            else if (input.IsNewKeyPress(Keys.Up)) _row = _row == 2 ? ItemRows - 1 : _row - 1 < 0 ? -1 : _row - 1;
            if (_row < 0) _col = _tab;
            _col = Math.Clamp(_col, 0, RowLength(_row) - 1);
            bool choose = input.IsNewKeyPress(Keys.Enter);

            // Hovering picks, clicking chooses
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool moved = mouse.Position != _lastMouse.Position;
            _lastMouse = mouse;
            void Hover(Rectangle rect, int row, int col)
            {
                if (!rect.Contains(mouse.Position)) return;
                if (moved || click) { _row = row; _col = col; }
                if (click) choose = true;
            }
            for (int t = 0; t < TabNames.Length; t++)
                if (TabRectangle(t).Contains(mouse.Position) && click) { SwitchTab(t); _row = -1; _col = t; }
            for (int i = 0; i < TabItems.Length; i++) Hover(PanelRectangle(i), i / Columns, i % Columns);
            Hover(GearButton, 2, 0);
            Hover(BackButton, 2, 1);

            // Esc always leaves
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
            {
                ExitScreen();
                return;
            }
            if (!choose) return;

            if (_row < 0) { _row = 0; _col = 0; }
            else if (_row == 2)
            {
                if (_col == 0) ScreenManager.AddScreen(new GearScreen());
                else ExitScreen();
            }
            else if (SelectedIndex >= 0 && SelectedIndex < TabItems.Length) TryBuy(TabItems[SelectedIndex]);
        }

        // Buys it if you dont have it, otherwise upgrades it
        private void TryBuy(Item item)
        {
            bool owned = Shop.Owns(item.Id);
            if (owned && item.Slot == Slot.Skin)
            {
                Shop.Equip(item.Id);  // owned skins just get worn
                _message = $"Wearing the {item.Name}";
            }
            else if (Shop.IsMaxed(item.Id)) _message = owned && item.MaxLevel == 1 ? $"You already have the {item.Name}" : "That's maxed out";
            else if (Shop.Buy(item.Id))
            {
                _message = owned ? $"{item.Name} is now level {Shop.Level(item.Id)}!" : $"Got the {item.Name}!";
                if (!owned && !Shop.IsEquipped(item.Id))
                    ScreenManager.AddScreen(new PromptScreen($"Got the {item.Name}!", "Equip it now?", "Equip", "Not now", () => Shop.Equip(item.Id), () => { }));
                _buySound.Play(0.8f, 0f, 0f);  // cha ching
            }
            else _message = "Not enough gems";
            _messageTime = 2.5f;
        }

        // What the picked item does now and what the next level gives
        private static string ItemInfo(Item item)
        {
            if (!Shop.Owns(item.Id)) return item.Show(item.Values[0]);
            string now = item.Show(Shop.Value(item.Id));
            return Shop.IsMaxed(item.Id) ? now : $"{now}\nNext: {item.Show(item.Values[Shop.Level(item.Id)])}";
        }

        public override void Unload()
        {
            // The title starts its own music again (fading in) once the shop is gone
            MediaPlayer.Stop();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var fade = Color.White * TransitionAlpha;

            // PointClamp keeps the pixel art sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), fade);  // art is 800 wide, stretched a lil to fill 16:9

            // Breathing, he stretches up and down slowly from his feet (Kinda creepy, I love it)
            float breath = 1f + 0.015f * MathF.Sin(_time * 2.2f);
            spriteBatch.Draw(_merchant, MerchantFeet, null, fade, 0f, new Vector2(0, _merchant.Height), new Vector2(1f, breath), SpriteEffects.None, 0f);
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
            DrawText("Shop", new Vector2(GridX + 250 - ScreenManager.TitleFont.MeasureString("Shop").X * 0.4f, 4), Color.Yellow, 0.8f, ScreenManager.TitleFont);

            // Tabs, the open one is bright
            for (int t = 0; t < TabNames.Length; t++)
                DrawButton(TabRectangle(t), TabNames[t], t == _tab, _row < 0 && t == _tab ? Color.Yellow : t == _tab ? Color.White : Color.Gray);

            // Item panels with the icon, name, level bars and the price (or Owned / Max)
            var items = TabItems;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var rect = PanelRectangle(i);
                bool selected = i == SelectedIndex;
                bool owned = Shop.Owns(item.Id);
                MenuButton.Draw(spriteBatch, _button, rect, MenuButton.Tint(selected) * TransitionAlpha);
                if (Shop.IsEquipped(item.Id))
                    spriteBatch.Draw(_pixel, new Rectangle(rect.X + 6, rect.Y + 6, 10, 10), Color.LimeGreen * TransitionAlpha);  // green dot = equipped

                var rarity = Rarity.Of(item.Id);
                var iconArea = new Rectangle(rect.Center.X - 24, rect.Y + 8, 48, 48);
                Rarity.DrawGlow(spriteBatch, MenuButton.Inside(iconArea), rarity, (owned ? 1f : 0.5f) * TransitionAlpha);
                spriteBatch.Draw(_icons[(int)item.Id], MenuButton.Inside(new Rectangle(rect.Center.X - 24, rect.Y + 8, _icons[(int)item.Id].Width, _icons[(int)item.Id].Height)), (owned ? Color.White : Color.Gray) * TransitionAlpha);
                Rarity.DrawSparks(spriteBatch, MenuButton.Inside(iconArea), rarity, TransitionAlpha);
                var titleFont = ScreenManager.TitleFont;
                float nameScale = MathF.Min(0.34f, (rect.Width - 14) / titleFont.MeasureString(item.Name).X);
                var nameSize = titleFont.MeasureString(item.Name) * nameScale;
                var namePos = new Vector2(rect.Center.X - nameSize.X / 2, rect.Y + 60);
                DrawText(item.Name, namePos, selected ? Color.Lerp(rarity, Color.White, 0.35f) : rarity, nameScale, titleFont);  // name in its rarity color
                Rarity.DrawSparks(spriteBatch, new Rectangle((int)namePos.X, (int)namePos.Y, (int)nameSize.X, (int)nameSize.Y), rarity, TransitionAlpha, 4);

                // One bar per level, lit green once bought (single level items skip these)
                if (owned && item.MaxLevel > 1)
                {
                    int barWidth = (rect.Width - 24 - (item.MaxLevel - 1) * 4) / item.MaxLevel;
                    for (int b = 0; b < item.MaxLevel; b++)
                    {
                        var bar = MenuButton.Inside(new Rectangle(rect.X + 12 + b * (barWidth + 4), rect.Y + 86, barWidth, 6));  // squashes with the card
                        spriteBatch.Draw(_pixel, new Rectangle(bar.X - 2, bar.Y - 2, bar.Width + 4, bar.Height + 4), Color.Black * TransitionAlpha);  // outline
                        spriteBatch.Draw(_pixel, bar, (b < Shop.Level(item.Id) ? Color.LimeGreen : new Color(40, 40, 50)) * TransitionAlpha);
                    }
                }

                if (Shop.IsMaxed(item.Id))
                {
                    string done = item.MaxLevel == 1 ? "Owned" : "Max";
                    DrawText(done, new Vector2(rect.Center.X - font.MeasureString(done).X * 0.2f, rect.Y + 98), Color.LimeGreen, 0.4f);
                }
                else
                {
                    int cost = Shop.Price(item.Id);
                    string price = cost.ToString();
                    float priceWidth = font.MeasureString(price).X * 0.4f;
                    spriteBatch.Draw(_gem, MenuButton.Inside(new Rectangle((int)(rect.Center.X - (priceWidth + 22) / 2), rect.Y + 100, 18, 18)), fade);
                    DrawText(price, new Vector2(rect.Center.X - (priceWidth + 22) / 2 + 22, rect.Y + 98), Shop.Gems >= cost ? Color.Cyan : Color.IndianRed, 0.4f);
                }
            }

            // What the picked thing does (or the last buy message)
            string info = _messageTime > 0 ? _message
                : SelectedIndex >= 0 && SelectedIndex < items.Length ? ItemInfo(items[SelectedIndex])
                : _row == 2 ? (_col == 0 ? "Equip your gear" : "Back to the title")
                : "Q / E (or LB / RB) to switch tabs";
            int infoY = GridY + ItemRows * (PanelHeight + 6) + 2;
            float infoScale = MathF.Min(0.4f, 480 / font.MeasureString(info).X);
            spriteBatch.Draw(_pixel, new Rectangle(GridX, infoY, 500, 44), Color.Black * 0.7f * TransitionAlpha);
            DrawText(info, new Vector2(GridX + 10, infoY + 3), Color.White, infoScale);

            DrawButton(GearButton, "Gear", _row == 2 && _col == 0);
            DrawButton(BackButton, "Back", _row == 2 && _col == 1);

            // Hovering an item pops up its upgrade path, every level's stat in that level's rarity color
            var mouse = VirtualMouse.GetState().Position;
            for (int i = 0; i < items.Length; i++)
                if (PanelRectangle(i).Contains(mouse)) { DrawUpgradePath(items[i], mouse); break; }

            spriteBatch.End();
        }

        // Mini popup in the stone box, one line per level, the current one marked, ones you don't have yet dimmed
        private void DrawUpgradePath(Item item, Point mouse)
        {
            var font = ScreenManager.MenuFont;
            const float Scale = 0.3f;
            int level = Shop.Level(item.Id), lineH = 18;
            var lines = new string[item.MaxLevel];
            for (int l = 0; l < item.MaxLevel; l++) lines[l] = $"{(l + 1 == level ? ">" : " ")} Lv {l + 1}  {item.Show(item.Values[l])}";
            float width = font.MeasureString(item.Name).X * 0.36f;
            foreach (var line in lines) width = MathF.Max(width, font.MeasureString(line).X * Scale);
            int w = (int)width + 28, h = 34 + item.MaxLevel * lineH;
            var box = new Rectangle(Math.Min(mouse.X + 16, Game1.VirtualWidth - w - 6), Math.Min(mouse.Y + 16, Game1.VirtualHeight - h - 6), w, h);
            MenuButton.Draw(ScreenManager.SpriteBatch, _button, box, Color.White * 0.95f * TransitionAlpha);
            DrawText(item.Name, new Vector2(box.X + 14, box.Y + 7), Rarity.Of(item.Id), 0.36f);
            for (int l = 0; l < item.MaxLevel; l++)
            {
                var color = Rarity.Colors[Rarity.TierAt(item.Id, l + 1)];
                DrawText(lines[l], new Vector2(box.X + 14, box.Y + 30 + l * lineH), l < level ? color : color * 0.5f, Scale);
            }
        }

        // Stone button with centered text
        private void DrawButton(Rectangle rect, string label, bool selected, Color? textColor = null)
        {
            MenuButton.Draw(ScreenManager.SpriteBatch, _button, rect, MenuButton.Tint(selected) * TransitionAlpha);
            var size = ScreenManager.MenuFont.MeasureString(label) * 0.5f;
            DrawText(label, new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2), textColor ?? (selected ? Color.Yellow : Color.White), 0.5f);
        }

        // Where an eye ends up on screen after the breathing stretch
        private Vector2 EyePosition(Vector2 eye, float breath) => MerchantFeet + new Vector2(eye.X, -(_merchant.Height - eye.Y) * breath);

        // Text with a shadow so it reads on the busy background
        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
