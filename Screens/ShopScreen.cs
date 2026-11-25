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
        private Texture2D _gemGray;  // upgrade card: grey gem to tint per level
        private Texture2D _card;     // crystal card behind each item
        private Gem3D _gem3D;        // the spinning diamond from the caves, next to your gem count
        private Texture2D _button;
        private Texture2D _rockButton;  // Back gets the main menu rock
        private SpriteFont _backFont;   // the original Pixellari
        private Texture2D[] _icons;   // one per item, same order as Shop.Items
        private Texture2D _pixel;

        // Shop music, the title music fades out then this fades in (sounds so much smoother)
        private Song _shopMusic;
        private SoundEffect _buySound;

        // Flickering lamp glows with green sparks floating up from them
        private Texture2D _glow;
        private static readonly Vector2[] Lamps = { new(306, 75), new(218, 79), new(356, 163), new(424, 208) };  // the green lanterns in the cave art (the low one hides behind the shopkeeper)
        private SparkParticleSystem _sparks;
        private float _time;

        // The shopkeeper stands close to the screen (legs cut off) and breathes. Tried her as a 3D model, reverted back to the 2D art
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
        private static readonly Rectangle BackButton = new Rectangle(684, 420, 150, 44);
        private static Rectangle TabRectangle(int t) => new Rectangle(GridX + t * 126, 60, 120, 38);  // 4 tabs fit across the grid
        private Rectangle PanelRectangle(int i) => new Rectangle(GridX + i % Columns * (PanelWidth + 8), GridY + (i / Columns - _scroll) * (PanelHeight + 6), PanelWidth, PanelHeight);
        private bool Showing(int i) => i / Columns >= _scroll && i / Columns < _scroll + VisibleRows;
        private const int VisibleRows = 2;   // rows of cards on screen, the rest scroll
        private const int ButtonRow = 100;   // _row for the Back button
        private int _scroll;                 // first card row showing

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
        private Item[] TabItems => Shop.Items.Where(i => i.Slot == (Slot)_tab && i.InShop).OrderBy(i => i.BaseSkin ?? i.Id).ToArray();  // recolored skins right after the one they came from
        private int ItemRows => (TabItems.Length + Columns - 1) / Columns;
        private int SelectedIndex => _row >= 0 && _row < ItemRows ? _row * Columns + _col : -1;

        public override void Activate()
        {
            var content = ScreenManager.Game.Content;
            _background = content.Load<Texture2D>("Images/Shop/shop_bg");
            _merchant = content.Load<Texture2D>("Images/Shop/merchant");
            _shopMusic = content.Load<Song>("Audio/Music/dust_groove");  // same groove as pause and death
            _buySound = content.Load<SoundEffect>("Audio/Sfx/buy");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _card = content.Load<Texture2D>("Images/Shop/item_card");
            _gem3D = new Gem3D(ScreenManager.GraphicsDevice);
            var gemPixels = new Color[_gem.Width * _gem.Height];  // grey copy of the gem so each level can tint it its rarity color
            _gem.GetData(gemPixels);
            for (int i = 0; i < gemPixels.Length; i++) { var c = gemPixels[i]; byte v = (byte)Math.Min(255, (c.R + c.G + c.B) / 3 * 1.4f); gemPixels[i] = new Color(v * c.A / 255, v * c.A / 255, v * c.A / 255, c.A); }
            _gemGray = new Texture2D(ScreenManager.GraphicsDevice, _gem.Width, _gem.Height);
            _gemGray.SetData(gemPixels);
            _button = content.Load<Texture2D>("Images/Menu/button");
            _rockButton = content.Load<Texture2D>("Images/Menu/title_button");
            _backFont = content.Load<SpriteFont>("Fonts/SettingsFont");
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
            if (!IsExiting) Music.Play(_shopMusic, 3.5f);  // the groove eases in slow
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
            if (_row >= 0 && _row < ButtonRow) { _row = 0; _col = 0; }
            _scroll = 0;
        }

        // Keeps the column inside the row so you never land on an empty spot
        private int RowLength(int row) => row == ButtonRow ? 1 : row < 0 ? TabNames.Length : Math.Min(Columns, TabItems.Length - row * Columns);

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            var was = (_row, _col, _tab);
            HandleMenu(input);
            if ((_row, _col, _tab) != was && !IsExiting) Sfx.UiMove();
        }

        private void HandleMenu(InputState input)
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
                _buySound.PlaySfx(0.8f, 0.3f, 0f);
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
            if (input.IsNewKeyPress(Keys.Down)) _row = _row < 0 ? _scroll : _row < ItemRows - 1 ? _row + 1 : ButtonRow;
            else if (input.IsNewKeyPress(Keys.Up)) _row = _row == ButtonRow ? Math.Min(ItemRows, _scroll + VisibleRows) - 1 : _row - 1 < 0 ? -1 : _row - 1;
            if (_row < 0) _col = _tab;
            _col = Math.Clamp(_col, 0, RowLength(_row) - 1);
            if (_row >= 0 && _row < ItemRows) _scroll = Math.Clamp(_scroll, _row - VisibleRows + 1, _row);  // arrows past the edge scroll the cards
            _scroll = Math.Clamp(_scroll, 0, Math.Max(0, ItemRows - VisibleRows));
            bool choose = input.IsNewKeyPress(Keys.Enter);

            // Hovering picks, clicking chooses
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool moved = mouse.Position != _lastMouse.Position;
            int wheel = mouse.ScrollWheelValue - _lastMouse.ScrollWheelValue;
            _lastMouse = mouse;
            if (wheel != 0 && ItemRows > VisibleRows)
            {
                _scroll = Math.Clamp(_scroll - Math.Sign(wheel), 0, ItemRows - VisibleRows);
                if (_row >= 0 && _row < ItemRows) _row = Math.Clamp(_row, _scroll, _scroll + VisibleRows - 1);  // keep the pick on screen
            }
            void Hover(Rectangle rect, int row, int col)
            {
                if (!rect.Contains(mouse.Position)) return;
                if (moved || click) { _row = row; _col = col; }
                if (click) choose = true;
            }
            for (int t = 0; t < TabNames.Length; t++)
                if (TabRectangle(t).Contains(mouse.Position) && click) { SwitchTab(t); _row = -1; _col = t; }
            for (int i = 0; i < TabItems.Length; i++) if (Showing(i)) Hover(PanelRectangle(i), i / Columns, i % Columns);
            Hover(BackButton, ButtonRow, 0);

            // Esc always leaves
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
            {
                Sfx.UiBack();
                ExitScreen();
                return;
            }
            if (!choose) return;

            if (_row < 0) { _row = 0; _col = 0; }
            else if (_row == ButtonRow) { Sfx.UiBack(); ExitScreen(); }
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
                Sfx.UiPick();
            }
            else if (Shop.IsMaxed(item.Id)) { _message = owned && item.MaxLevel == 1 ? $"You already have the {item.Name}" : "That's maxed out"; Sfx.UiBack(); }
            else if (Shop.Buy(item.Id))
            {
                _message = owned ? $"{item.Name} is now level {Shop.Level(item.Id)}!" : $"Got the {item.Name}!";
                if (!owned && !Shop.IsEquipped(item.Id) && Settings.EquipPrompt)
                    ScreenManager.AddScreen(new PromptScreen($"Got the {item.Name}!", "Equip it now?", "Equip", "Not now", () => Shop.Equip(item.Id), () => { },
                        thirdLabel: "Don't show again", third: () => { Settings.EquipPrompt = false; SaveSystem.Save(); }));
                _buySound.PlaySfx(0.8f, 0f, 0f);  // cha ching
            }
            else { _message = "Not enough gems"; Sfx.UiBack(); }
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
            // Fades out, whatever's underneath (title or the death screen) brings its own song back
            Music.Stop();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var fade = Color.White * TransitionAlpha;

            // PointClamp keeps the pixel art sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), fade);  // art is already 854x480

            // Breathing, he stretches up and down slowly from his feet (Kinda creepy, I love it)
            float breath = 1f + 0.015f * MathF.Sin(_time * 2.2f);
            spriteBatch.Draw(_merchant, MerchantFeet, null, new Color(120, 120, 150) * TransitionAlpha, 0f, new Vector2(0, _merchant.Height), new Vector2(1f, breath), SpriteEffects.None, 0f);  // darker, she's down in a cave
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

            // Gem counter box, drawn before the gem so the gem sits on its left end
            string gems = Shop.Gems.ToString();
            var small = ScreenManager.SmallFont;
            const float GemTextScale = 0.6f;
            var gemBox = new Rectangle(30, 20, (int)(small.MeasureString(gems).X * GemTextScale) + 50, 28);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            Hud.Pill(spriteBatch, new Rectangle(gemBox.X - 2, gemBox.Y - 2, gemBox.Width + 4, gemBox.Height + 4), Color.Lerp(Color.Cyan, Color.White, 0.3f) * 0.85f * TransitionAlpha);
            Hud.Pill(spriteBatch, gemBox, new Color(14, 20, 38) * TransitionAlpha);
            spriteBatch.End();
            _gem3D.Draw(new Vector2(gemBox.X + 6, gemBox.Center.Y - 4), 22, _time * 2f, Matrix.Identity, TransitionAlpha);  // big, hides the left end of the box

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Gem count inside its box, right of the gem
            var gemsPos = new Vector2(gemBox.X + 34, gemBox.Center.Y - small.MeasureString(gems).Y * GemTextScale / 2);
            spriteBatch.DrawString(small, gems, gemsPos + new Vector2(1, 2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, GemTextScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(small, gems, gemsPos, Color.Lerp(Color.Cyan, Color.White, 0.35f) * TransitionAlpha, 0f, Vector2.Zero, GemTextScale, SpriteEffects.None, 0f);
            DrawText("Shop", new Vector2(GridX + 250 - ScreenManager.TitleFont.MeasureString("Shop").X * 0.4f, 4), Color.Yellow, 0.8f, ScreenManager.TitleFont);

            // Tabs, the open one is bright
            for (int t = 0; t < TabNames.Length; t++)
                DrawButton(TabRectangle(t), TabNames[t], t == _tab, _row < 0 && t == _tab ? Color.Yellow : t == _tab ? Color.White : Color.Gray);

            // Item panels with the icon, name, level bars and the price (or Owned / Max)
            var items = TabItems;
            var priceGems = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < items.Length; i++)
            {
                if (!Showing(i)) continue;
                var item = items[i];
                var rect = PanelRectangle(i);
                bool selected = i == SelectedIndex;
                bool owned = Shop.Owns(item.Id);
                MenuButton.Draw(spriteBatch, _card, rect, MenuButton.Tint(selected) * TransitionAlpha);
                if (Shop.IsEquipped(item.Id))
                    Hud.Dot(spriteBatch, MenuButton.Inside(new Rectangle(rect.X + 8, rect.Y + 8, 10, 10)).Center.ToVector2(), 10, Color.LimeGreen * TransitionAlpha);  // green dot = equipped, squashes with the card

                var rarity = Rarity.Of(item.Id);
                var iconArea = new Rectangle(rect.Center.X - 24, rect.Y + 8, 48, 48);
                Rarity.DrawGlow(spriteBatch, MenuButton.Inside(iconArea), rarity, (owned ? 1f : 0.5f) * TransitionAlpha);
                spriteBatch.Draw(_icons[(int)item.Id], MenuButton.Inside(new Rectangle(rect.Center.X - 24, rect.Y + 8, _icons[(int)item.Id].Width, _icons[(int)item.Id].Height)), (owned ? Color.White : Color.Gray) * TransitionAlpha);
                Rarity.DrawSparks(spriteBatch, MenuButton.Inside(iconArea), rarity, TransitionAlpha);
                var titleFont = ScreenManager.TitleFont;
                // Two word names stack on two lines, everything shrinks to fit inside the card's border
                var nameLines = SplitName(item.Name);
                float nameScale = nameLines.Length == 1 ? 0.34f : 0.3f;
                foreach (var line in nameLines) nameScale = MathF.Min(nameScale, (rect.Width - 30) / titleFont.MeasureString(line).X);
                float lineStep = titleFont.LineSpacing * nameScale * 0.85f;  // alagard has a lot of room under each line
                float nameTop = rect.Y + (nameLines.Length == 1 ? 60 : 56);
                float blockWidth = 0;
                for (int n = 0; n < nameLines.Length; n++)
                {
                    var lineSize = titleFont.MeasureString(nameLines[n]) * nameScale;
                    blockWidth = MathF.Max(blockWidth, lineSize.X);
                    DrawText(nameLines[n], new Vector2(rect.Center.X - lineSize.X / 2, nameTop + n * lineStep), selected ? Color.Lerp(rarity, Color.White, 0.35f) : rarity, nameScale, titleFont);  // name in its rarity color
                }
                var nameBlock = new Rectangle((int)(rect.Center.X - blockWidth / 2), (int)nameTop, (int)blockWidth, (int)(lineStep * (nameLines.Length - 1) + titleFont.LineSpacing * nameScale));
                Rarity.DrawSparks(spriteBatch, nameBlock, rarity, TransitionAlpha, 4);

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
                    float priceHeight = font.MeasureString(price).Y * 0.4f;
                    int gemY = (int)(rect.Y + 98 + priceHeight / 2 - 3) - 9;  // lined up with the middle of the number (the gem's point hangs low, so a touch higher)
                    priceGems.Add(MenuButton.Inside(new Rectangle((int)(rect.Center.X - (priceWidth + 22) / 2), gemY, 18, 18)).Center.ToVector2());  // 3D gem drawn after the batch
                    DrawOutlined(price, new Vector2(rect.Center.X - (priceWidth + 22) / 2 + 24, rect.Y + 98), Shop.Gems >= cost ? Color.Lerp(Color.Cyan, Color.White, 0.25f) : Color.IndianRed, 0.4f);
                }
            }

            // What the picked thing does (or the last buy message)
            string info = _messageTime > 0 ? _message
                : SelectedIndex >= 0 && SelectedIndex < items.Length ? ItemInfo(items[SelectedIndex])
                : _row == ButtonRow ? "Back"
                : "Q / E (or LB / RB) to switch tabs";
            Hud.ScrollBar(spriteBatch, new Rectangle(GridX + Columns * (PanelWidth + 8) - 2, GridY + 4, 6, VisibleRows * (PanelHeight + 6) - 14), _scroll, VisibleRows, ItemRows, Color.Lerp(Color.Cyan, Color.White, 0.4f), TransitionAlpha);  // only shows when there's more
            int infoY = GridY + Math.Min(ItemRows, VisibleRows) * (PanelHeight + 6) + 2;
            float infoScale = MathF.Min(0.4f, 480 / font.MeasureString(info).X);
            spriteBatch.Draw(_pixel, new Rectangle(GridX, infoY, 500, 44), Color.Black * 0.7f * TransitionAlpha);
            DrawText(info, new Vector2(GridX + 10, infoY + 3), Color.White, infoScale);

            DrawButton(BackButton, "Back", _row == ButtonRow, rock: true);

            // Spinning 3D gems by the prices (3D has to go between sprite batches), then the popup over everything
            spriteBatch.End();
            for (int g = 0; g < priceGems.Count; g++) _gem3D.Draw(priceGems[g], 8, _time * 2f + g * 0.7f, Matrix.Identity, TransitionAlpha);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Hovering an item pops up its upgrade path, every level's stat in that level's rarity color
            // Holding Tab / LT stretches it with a compare, and shows it for the keyboard / pad pick too
            var mouse = VirtualMouse.GetState().Position;
            bool compare = Keyboard.GetState().IsKeyDown(Keys.Tab) || GamePad.GetState(PlayerIndex.One).Triggers.Left > 0.5f;
            int shown = -1;
            for (int i = 0; i < items.Length; i++)
                if (Showing(i) && PanelRectangle(i).Contains(mouse)) { shown = i; break; }
            bool padPick = Controls.UsingPad && _row >= 0 && _row < ItemRows && SelectedIndex >= 0 && SelectedIndex < items.Length && Showing(SelectedIndex);
            if (padPick) Hud.PadHint(spriteBatch, font, "RT", new Vector2(PanelRectangle(SelectedIndex).Right - 14, PanelRectangle(SelectedIndex).Y + 10), TransitionAlpha);
            if (padPick && GamePad.GetState(PlayerIndex.One).Triggers.Right > 0.5f) { shown = SelectedIndex; mouse = PanelRectangle(shown).Center; }
            if (shown < 0 && compare && _row >= 0 && _row < ItemRows && SelectedIndex >= 0 && SelectedIndex < items.Length) { shown = SelectedIndex; mouse = PanelRectangle(shown).Center; }
            if (shown >= 0) DrawUpgradePath(items[shown], mouse, compare);

            spriteBatch.End();
        }

        // Upgrade card: crystal frame, the item's icon and name, a row of gems (one per level, lit up to where you are),
        // then what you have now and what the next level gives. Holding Tab / LT adds every level and what you're wearing
        private void DrawUpgradePath(Item item, Point mouse, bool compare)
        {
            var sb = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            const float Scale = 0.3f;
            const int Pad = 44, PadY = 32, LineH = 18, TrackH = 34;  // big pads so the crystal border shows (sides are thicker)
            int level = Shop.Level(item.Id);
            float time = (float)(Environment.TickCount64 % 100000) / 1000f;

            // The text lines under the gem row
            var lines = new System.Collections.Generic.List<(string Text, Color Color)>();
            var cost = Shop.Gems >= Shop.Price(item.Id) ? Color.Cyan : Color.IndianRed;
            // Bits every level shares ("Quick burst sideways") are said once up top, the rest only say what changes ("1.2s cooldown")
            var parts = Array.ConvertAll(item.Values, v => item.Show(v).Split(", "));
            var shared = parts[0].Where(p => parts.All(ps => ps.Contains(p))).ToArray();
            bool split = item.MaxLevel > 1 && shared.Length > 0 && shared.Length < parts[0].Length;
            string Diff(int l) => split ? string.Join(", ", parts[l].Where(p => !shared.Contains(p))) : item.Show(item.Values[l]);
            if (split) lines.Add((string.Join(", ", shared), Color.LightGray));
            if (level == 0) lines.Add(($"Buy: {Diff(0)}", Color.LimeGreen));
            else if (!compare)  // the full list below already shows where you are
            {
                lines.Add(($"Now:  {Diff(level - 1)}", Color.White));
                if (!Shop.IsMaxed(item.Id)) lines.Add(($"Next: {Diff(level)}", Color.LimeGreen));
            }
            if (compare)
                for (int l = 0; l < item.MaxLevel; l++)
                    lines.Add(($"Lv {l + 1}  {Diff(l)}" + (l == level - 1 ? "  < now" : ""), Rarity.Colors[Rarity.TierAt(item.Id, l + 1)] * (l < level ? 1f : 0.55f)));
            if (Shop.IsMaxed(item.Id) && level > 0) lines.Add(("Fully upgraded!", Color.Gold));
            else lines.Add(($"{Shop.Price(item.Id)} gems" + (level > 0 ? " to upgrade" : ""), cost));
            if (compare && Shop.Equipped(item.Slot) is ItemId on && on != item.Id) lines.Add(($"Wearing {Shop.Get(on).Name}: {Shop.Get(on).Show(Shop.Value(on))}", Color.LightGray));
            else if (item.MaxLevel > 1 || Shop.Equipped(item.Slot) is ItemId) lines.Add(("Hold Tab / LT for more", Color.Gray));

            // Size it to the longest line
            float width = font.MeasureString(item.Name).X * 0.4f + 40;
            foreach (var (text, _) in lines) width = MathF.Max(width, font.MeasureString(text).X * Scale);
            float fit = MathF.Min(1, (Game1.VirtualWidth - 12 - Pad * 2) / width);  // too wide for the screen, shrink the lines to fit
            width *= fit;
            int w = Math.Max(220, (int)width + Pad * 2), h = PadY * 2 + 30 + (item.MaxLevel > 1 ? TrackH : 0) + lines.Count * LineH;
            var box = new Rectangle(Math.Min(mouse.X + 16, Game1.VirtualWidth - w - 6), Math.Min(mouse.Y + 16, Game1.VirtualHeight - h - 6), w, h);
            Hud.Frame(sb, box, 18, Color.White * 0.97f * TransitionAlpha);

            // Header: icon, name in its rarity color, level count on the right
            var tier = Rarity.Of(item.Id);
            sb.Draw(_icons[(int)item.Id], new Rectangle(box.X + Pad, box.Y + PadY - 2, 28, 28), Color.White * TransitionAlpha);
            DrawText(item.Name, new Vector2(box.X + Pad + 34, box.Y + PadY + 2), tier, 0.4f, squish: false);
            if (item.MaxLevel > 1)
            {
                string lv = $"Lv {level}/{item.MaxLevel}";
                DrawText(lv, new Vector2(box.Right - Pad - font.MeasureString(lv).X * Scale, box.Y + PadY + 5), Color.LightGray, Scale, squish: false);
            }
            int y = box.Y + PadY + 32;

            // Gem row: line between the gems fills in the rarity colors, owned gems glow, the next one pulses
            if (item.MaxLevel > 1)
            {
                int left = box.X + Pad + 10, right = box.Right - Pad - 10, cy = y + TrackH / 2 - 2;
                float step = (right - left) / (float)(item.MaxLevel - 1);
                sb.Draw(_pixel, new Rectangle(left, cy - 1, right - left, 3), Color.Black * 0.6f * TransitionAlpha);
                for (int l = 1; l < level; l++)  // filled links first so the gems sit on top
                    sb.Draw(_pixel, new Rectangle((int)(left + step * (l - 1)), cy - 1, (int)step, 3), Rarity.Colors[Rarity.TierAt(item.Id, l + 1)] * TransitionAlpha);
                for (int l = 0; l < item.MaxLevel; l++)
                {
                    var color = Rarity.Colors[Rarity.TierAt(item.Id, l + 1)];
                    var at = new Vector2(left + step * l, cy);
                    bool owned = l < level, next = l == level;
                    float size = 18 * (next ? 1f + 0.12f * MathF.Sin(time * 6) : 1f);
                    if (owned) sb.Draw(_glow, at, null, color * 0.5f * TransitionAlpha, 0f, new Vector2(_glow.Width / 2f, _glow.Height / 2f), 26f / _glow.Width, SpriteEffects.None, 0f);
                    var tint = owned ? color : next ? color * 0.55f : new Color(60, 60, 70);
                    sb.Draw(_gemGray, at, null, tint * TransitionAlpha, 0f, new Vector2(_gemGray.Width / 2f, _gemGray.Height / 2f), size / _gemGray.Width, SpriteEffects.None, 0f);
                }
                y += TrackH;
            }

            for (int i = 0; i < lines.Count; i++) DrawText(lines[i].Text, new Vector2(box.X + Pad, y + i * LineH), lines[i].Color, Scale * fit, squish: false);
        }

        // Stone button with centered text
        private void DrawButton(Rectangle rect, string label, bool selected, Color? textColor = null, bool rock = false)
        {
            var font = rock ? _backFont : ScreenManager.MenuFont;
            MenuButton.Draw(ScreenManager.SpriteBatch, rock ? _rockButton : _button, rect, MenuButton.Tint(selected) * TransitionAlpha);
            var size = font.MeasureString(label) * 0.5f;
            DrawText(label, new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2), textColor ?? (selected ? Color.Yellow : Color.White), 0.5f, font);
        }

        // Where an eye ends up on screen after the breathing stretch
        private Vector2 EyePosition(Vector2 eye, float breath) => MerchantFeet + new Vector2(eye.X, -(_merchant.Height - eye.Y) * breath);

        // Text with a shadow so it reads on the busy background
        // Number with a black outline and shadow so it pops off the card (squashes with the card like DrawText)
        private void DrawOutlined(string text, Vector2 position, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            var pos = MenuButton.TextPos(position);
            var size = MenuButton.TextScale(position, scale);
            ScreenManager.SpriteBatch.DrawString(font, text, pos + new Vector2(2, 3), Color.Black * 0.6f * TransitionAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            foreach (var o in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1) })
                ScreenManager.SpriteBatch.DrawString(font, text, pos + o * 1.5f, Color.Black * TransitionAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, pos, color * TransitionAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
        }

        // squish = false for text that isn't in a button (popups drawn over a squashing panel shouldnt wobble with it)
        // Splits a name at the space closest to its middle (one word names stay whole)
        private static string[] SplitName(string name)
        {
            int best = -1;
            for (int i = 0; i < name.Length; i++)
                if (name[i] == ' ' && (best < 0 || Math.Abs(i - name.Length / 2) < Math.Abs(best - name.Length / 2))) best = i;
            return best < 0 ? new[] { name } : new[] { name[..best], name[(best + 1)..] };
        }

        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null, bool squish = true)
        {
            font ??= ScreenManager.MenuFont;
            var pos = squish ? MenuButton.TextPos(position) : position;
            var size = squish ? MenuButton.TextScale(position, scale) : new Vector2(scale);
            ScreenManager.SpriteBatch.DrawString(font, text, pos + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, pos, color * TransitionAlpha, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
        }
    }
}
