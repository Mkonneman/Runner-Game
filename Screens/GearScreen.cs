using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Dress up the miner, the player with 3 gear slots next to him and a bag of everything else you own
    /// Mouse drags items in and out, keys / controller pick one up with Enter and put it down with Enter
    /// </summary>
    public class GearScreen : GameScreen
    {
        private Texture2D _pixel;
        private Texture2D _background;  // dark cave gear room, it's a full screen now not a popup
        private Texture2D _card, _portrait, _heart;  // shop's crystal card for the slots and bag spots
        private Texture2D _button;
        private SpriteFont _backFont;
        private Song _music;
        private Texture2D _glow;  // torch glow  // Minor Motif
        private Texture2D _player;
        private Texture2D[] _icons;

        private static readonly string[] SlotNames = { "Weapon", "Ability", "Armor", "Skin" };
        private static readonly Color PlayerTint = new Color(185, 205, 255);  // same blue as in the cave

        // Layout like an RPG inventory: miner in his cave nook with HP under him, the 4 gear slots in a column, the bag grid on the right, details under it
        private const int Cell = 48;
        private const int BagColumns = 7;
        private const int BagRows = 4;  // 28 spots
        private static readonly Rectangle Panel = new Rectangle(36, 34, 782, 418);  // roomy enough that nothing touches the crystal frame
        private static readonly Rectangle Portrait = new Rectangle(86, 78, 172, 131);
        private static readonly Rectangle BagArea = new Rectangle(380, 80, BagColumns * Cell, BagRows * Cell);
        private static readonly Rectangle InfoBox = new Rectangle(380, 316, 388, 100);
        private static readonly Rectangle BackButton = new Rectangle(97, 360, 150, 40);
        private static Rectangle SlotRectangle(int slot) => new Rectangle(287, 80 + slot * 82, 52, 52);  // stacked, name under each
        private Rectangle BagRectangle(int i) => new Rectangle(BagArea.X + i % BagColumns * Cell, BagArea.Y + (i / BagColumns - _bagScroll) * Cell, Cell - 4, Cell - 4);
        private bool BagShowing(int i) => i / BagColumns >= _bagScroll && i / BagColumns < _bagScroll + BagRows;
        private int _bagScroll;  // first bag row showing, the bag scrolls once there's more than fits

        // Keyboard focus, area 0 = slots, 1 = bag, 2 = back button
        private int _area;
        private int _index;
        private ItemId? _held;          // picked up with Enter (keys / controller)
        private ItemId? _dragging;      // being dragged with the mouse
        private Vector2 _dragStart;
        private bool _dragMoved;
        private ItemId? _hover;         // under the mouse, gets the little overview popup
        private ItemId? _selected;      // last thing clicked or focused, the info box talks about it
        private string _message = "";
        private float _messageTime;
        private MouseState _lastMouse;

        public GearScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.2);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        // Everything you own that isn't on you right now (hidden items stay out)
        private static Item[] Bag() => Shop.Items.Where(i => Shop.Owns(i.Id) && i.InShop && !Shop.IsEquipped(i.Id) && i.Id != ItemId.SkinExplorer).ToArray();  // default skin is just an empty skin slot

        public override void Activate()
        {
            var content = ScreenManager.Game.Content;
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = content.Load<Texture2D>("Images/Menu/title_button");
            _backFont = content.Load<SpriteFont>("Fonts/SettingsFont");
            _background = content.Load<Texture2D>("Images/Menu/gear_bg");
            _music = content.Load<Song>("Audio/Music/minor_motif");
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _card = content.Load<Texture2D>("Images/Shop/item_card");
            _portrait = content.Load<Texture2D>("Images/Menu/gear_portrait");
            _heart = content.Load<Texture2D>("Images/Menu/gear_heart");
            _player = content.Load<Texture2D>("Images/Gameplay/player_run");
            _icons = Array.ConvertAll(Shop.Items, item => content.Load<Texture2D>(item.Icon));
            _lastMouse = VirtualMouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            _messageTime -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (!IsExiting) Music.Play(_music, 3.5f, resume: true);  // the screen underneath brings its own song back when we leave
        }

        // Puts an item into a slot (only its own slot), says why if it can't
        private bool PutIn(ItemId id, int slot)
        {
            var item = Shop.Get(id);
            if ((int)item.Slot != slot) { Say($"{item.Name} goes in the {SlotNames[(int)item.Slot]} slot"); Sfx.UiBack(); return false; }
            Shop.Equip(id);
            Sfx.UiPick();
            Say($"Equipped {item.Name}");
            return true;
        }

        private void TakeOff(int slot)
        {
            if (Shop.Equipped((Slot)slot) is not ItemId id) return;
            Shop.Unequip((Slot)slot);
            Sfx.UiBack();
            Say($"Put away {Shop.Get(id).Name}");
        }

        private void Say(string text) { _message = text; _messageTime = 2f; }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back))
            {
                Sfx.UiBack();
                if (_held != null) { _held = null; return; }  // first Esc just drops what you're holding
                ExitScreen();
                return;
            }
            var was = (_area, _index);
            HandleKeys(input);
            if (_area == 1 && (_area, _index) != was) _bagScroll = Math.Clamp(_bagScroll, _index / BagColumns - BagRows + 1, _index / BagColumns);  // keys past the edge scroll the bag
            HandleMouse();
            if ((_area, _index) != was && !IsExiting) Sfx.UiMove();
        }

        private void HandleKeys(InputState input)
        {
            var bag = Bag();
            if (input.IsNewKeyPress(Keys.Up))
            {
                if (_area == 0) _index = Math.Max(0, _index - 1);
                else if (_area == 1 && _index >= BagColumns) _index -= BagColumns;
                else if (_area == 2) { _area = 0; _index = SlotNames.Length - 1; }
            }
            if (input.IsNewKeyPress(Keys.Down))
            {
                if (_area == 0) { if (_index < SlotNames.Length - 1) _index++; else _area = 2; }
                else if (_area == 1) { if (_index + BagColumns < Math.Max(1, bag.Length)) _index += BagColumns; else _area = 2; }
            }
            if (input.IsNewKeyPress(Keys.Right))
            {
                if (_area == 0) { _area = 1; _index = Math.Min((_bagScroll + _index) * BagColumns, Math.Max(0, bag.Length - 1)); }
                else if (_area == 1) _index = Math.Min(_index + 1, Math.Max(0, bag.Length - 1));
            }
            if (input.IsNewKeyPress(Keys.Left))
            {
                if (_area == 1) { if (_index % BagColumns == 0) { _area = 0; _index = Math.Clamp(_index / BagColumns - _bagScroll, 0, SlotNames.Length - 1); } else _index--; }
            }
            if (_area == 1) _index = Math.Clamp(_index, 0, Math.Max(0, bag.Length - 1));

            // What's under the cursor gets talked about in the info box
            ItemId? focused = _area == 0 ? Shop.Equipped((Slot)_index) : _area == 1 && _index < bag.Length ? bag[_index].Id : null;
            if (focused != null && _held == null) _selected = focused;

            if (!input.IsNewKeyPress(Keys.Enter)) return;
            if (_area == 2) { Sfx.UiBack(); ExitScreen(); return; }

            if (_held is ItemId held)
            {
                // Put it down, into a slot or back in the bag
                if (_area == 0) { if (PutIn(held, _index)) _held = null; }
                else { TakeOff((int)Shop.Get(held).Slot); _held = null; }
            }
            else if (focused is ItemId pick)
            {
                _held = pick;
                _selected = pick;
                Sfx.UiPick();
                // Jump straight to its slot from the bag so it's one more Enter to equip
                if (_area == 1) { _area = 0; _index = (int)Shop.Get(pick).Slot; }
                else { _area = 1; _index = 0; }
            }
        }

        private void HandleMouse()
        {
            var mouse = VirtualMouse.GetState();
            var position = mouse.Position;
            bool pressed = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool released = mouse.LeftButton == ButtonState.Released && _lastMouse.LeftButton == ButtonState.Pressed;
            int wheel = mouse.ScrollWheelValue - _lastMouse.ScrollWheelValue;
            _lastMouse = mouse;

            // What's under the mouse
            var bag = Bag();
            int bagRows = (bag.Length + BagColumns - 1) / BagColumns;
            if (wheel != 0) _bagScroll -= Math.Sign(wheel);
            _bagScroll = Math.Clamp(_bagScroll, 0, Math.Max(0, bagRows - BagRows));  // also pulls it back when the bag shrinks
            _hover = null;
            int overSlot = -1;
            for (int s = 0; s < SlotNames.Length; s++) if (SlotRectangle(s).Contains(position)) { overSlot = s; _hover = Shop.Equipped((Slot)s); }
            for (int i = 0; i < bag.Length; i++) if (BagShowing(i) && BagRectangle(i).Contains(position)) _hover = bag[i].Id;

            if (pressed)
            {
                if (BackButton.Contains(position)) { Sfx.UiBack(); ExitScreen(); return; }
                if (_hover is ItemId grab)
                {
                    _dragging = grab;
                    _selected = grab;
                    _held = null;
                    _dragStart = position.ToVector2();
                    _dragMoved = false;
                }
            }
            if (_dragging != null && Vector2.Distance(position.ToVector2(), _dragStart) > 6) _dragMoved = true;

            if (released && _dragging is ItemId dropped)
            {
                bool fromSlot = Shop.IsEquipped(dropped);
                if (_dragMoved)
                {
                    if (overSlot >= 0) PutIn(dropped, overSlot);
                    else if (fromSlot && !SlotRectangle((int)Shop.Get(dropped).Slot).Contains(position)) TakeOff((int)Shop.Get(dropped).Slot);  // dragged off = put away
                }
                else if (!fromSlot) PutIn(dropped, (int)Shop.Get(dropped).Slot);  // just a click on a bag item equips it
                _dragging = null;
            }
        }

        // Text scale that keeps it inside this many pixels (never bigger than the normal size)
        private float Fit(string text, float scale, float width) => MathF.Min(scale, width / MathF.Max(1, _backFont.MeasureString(text).X));

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = _backFont;
            float a = TransitionAlpha;
            var bag = Bag();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.White * a);
            // soft flickering glow on the two wall torches
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            foreach (var (torch, seed) in new[] { (new Vector2(58, 226), 0f), (new Vector2(767, 226), 2.3f) })
            {
                float flicker = 0.22f + 0.05f * MathF.Sin(time * 5 + seed) + 0.03f * MathF.Sin(time * 13 + seed * 2);
                spriteBatch.Draw(_glow, torch, null, new Color(255, 140, 50, 0) * flicker * a, 0f, new Vector2(8), 5.5f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(_pixel, new Rectangle(Panel.X + 8, Panel.Y + 8, Panel.Width - 16, Panel.Height - 16), new Color(10, 20, 34) * 0.88f * a);  // navy like the shop, a touch see-through
            Hud.Frame(spriteBatch, Panel, 18, Color.White * a);  // crystal frame
            DrawText("Gear", new Vector2(427 - ScreenManager.TitleFont.MeasureString("Gear").X * 0.3f, 6), Color.Yellow, 0.6f, ScreenManager.TitleFont);

            // The miner running in place in his little cave nook
            spriteBatch.Draw(_portrait, Portrait, Color.White * a);
            _player = ScreenManager.Game.Content.Load<Texture2D>(Shop.SkinFolder(Shop.Equipped(Slot.Skin)) + "/run");  // shows the skin you have on (cached after the first load)
            int frame = (int)(gameTime.TotalGameTime.TotalSeconds * 8) % (_player.Width / 56);
            spriteBatch.Draw(_player, new Rectangle(Portrait.Center.X - 70, Portrait.Bottom - 12 - 92, 140, 92), new Rectangle(frame * 56, 0, 56, 37), PlayerTint * a);

            // Health from the armor you have on, same math as the run
            float maxHealth = Shop.MaxHealth(Shop.Equipped(Slot.Armor));
            float beat = (float)gameTime.TotalGameTime.TotalSeconds % 2.2f;  // two quick thumps then a rest
            static float Thump(float t, float at) => t >= at && t < at + 0.16f ? MathF.Sin(MathHelper.Pi * (t - at) / 0.16f) : 0f;
            float pump = 22 * (1 + 0.22f * Thump(beat, 0f) + 0.15f * Thump(beat, 0.24f));
            spriteBatch.Draw(_heart, new Vector2(Portrait.X + 15, Portrait.Bottom + 25), null, Color.White * a, 0f, new Vector2(_heart.Width / 2f, _heart.Height / 2f), pump / _heart.Width, SpriteEffects.None, 0f);
            DrawText($"HP  {maxHealth * 100:0}", new Vector2(Portrait.X + 36, Portrait.Bottom + 14), Color.White, 0.42f, ScreenManager.NumberFont);

            // Slots with their names under them, glow green when what you're carrying fits
            var carrying = _dragging ?? _held;
            for (int s = 0; s < SlotNames.Length; s++)
            {
                var rect = SlotRectangle(s);
                bool focus = _area == 0 && _index == s;
                bool fits = carrying is ItemId c && (int)Shop.Get(c).Slot == s;
                MenuButton.Draw(spriteBatch, _card, rect, (fits ? new Color(140, 255, 160) : focus ? Color.Yellow : Color.White) * a, true, focus);
                float nameScale = Fit(SlotNames[s], 0.3f, 80);
                DrawText(SlotNames[s], new Vector2(rect.Center.X - _backFont.MeasureString(SlotNames[s]).X * nameScale / 2, rect.Bottom + 2), focus ? Color.Yellow : Color.LightGray, nameScale);
                if (Shop.Equipped((Slot)s) is ItemId on && on != _dragging)
                {
                    var iconArea = new Rectangle(rect.X + 9, rect.Y + 9, 34, 34);  // inside the socket's rim
                    Rarity.DrawGlow(spriteBatch, MenuButton.Inside(iconArea), Rarity.Of(on), a);
                    spriteBatch.Draw(_icons[(int)on], MenuButton.Inside(iconArea), (on == _held ? Color.White * 0.4f : Color.White) * a);
                    Rarity.DrawSparks(spriteBatch, MenuButton.Inside(iconArea), Rarity.Of(on), a);
                }
            }

            // The bag, a full grid of stone cells (empty ones too), the picked one glows cyan
            Hud.ScrollBar(spriteBatch, new Rectangle(BagArea.Right + 4, BagArea.Y + 4, 5, BagArea.Height - 12), _bagScroll, BagRows, (bag.Length + BagColumns - 1) / BagColumns, Color.Lerp(Color.Cyan, Color.White, 0.4f), a);
            for (int spot = _bagScroll * BagColumns; spot < (_bagScroll + BagRows) * BagColumns; spot++)
            {
                var rect = BagRectangle(spot);
                bool focus = _area == 1 && _index == spot && spot < Math.Max(1, bag.Length);
                MenuButton.Draw(spriteBatch, _card, rect, (focus ? Color.White : new Color(120, 135, 175)) * a, false);
                if (spot >= bag.Length || bag[spot].Id == _dragging) continue;
                var inside = new Rectangle(rect.X + 6, rect.Y + 6, rect.Width - 12, rect.Height - 12);
                Rarity.DrawGlow(spriteBatch, MenuButton.Inside(inside), Rarity.Of(bag[spot].Id), a);
                spriteBatch.Draw(_icons[(int)bag[spot].Id], MenuButton.Inside(inside), (bag[spot].Id == _held ? Color.White * 0.4f : Color.White) * a);
                Rarity.DrawSparks(spriteBatch, MenuButton.Inside(inside), Rarity.Of(bag[spot].Id), a);
            }
            if (bag.Length == 0) DrawText("Buy more in the Shop!", new Vector2(BagArea.X + 20, BagArea.Center.Y - 8), Color.Gray, 0.35f);

            // Details under a stone divider: name and level, type, what it does now, the next upgrade
            spriteBatch.Draw(_pixel, new Rectangle(InfoBox.X, InfoBox.Y - 20, InfoBox.Width, 2), Color.Cyan * 0.35f * a);  // thin divider like the run stats
            if (_messageTime > 0) DrawText(_message, new Vector2(InfoBox.X, InfoBox.Y + 4), Color.Yellow, Fit(_message, 0.38f, InfoBox.Width));
            else if (_selected is ItemId sel)
            {
                var item = Shop.Get(sel);
                int level = Shop.Level(sel);
                string title = $"{item.Name}   Level {level}";
                float titleScale = Fit(title, 0.4f, InfoBox.Width - item.MaxLevel * 22 - 10);  // leaves room for the level gems
                DrawText(title, new Vector2(InfoBox.X, InfoBox.Y), Rarity.Of(sel), titleScale);
                var titleSize = font.MeasureString(item.Name) * titleScale;
                Rarity.DrawSparks(spriteBatch, new Rectangle(InfoBox.X, InfoBox.Y, (int)titleSize.X, (int)titleSize.Y), Rarity.Of(sel), a, 4);
                // Level gems on the right like the shop's upgrade card, owned ones lit in their rarity color, the next one pulses
                // links first so every gem sits on top of them
                Vector2 GemAt(int l) => new Vector2(InfoBox.Right - 10 - (item.MaxLevel - 1 - l) * 22, InfoBox.Y + 10);
                for (int l = 1; l < item.MaxLevel; l++)
                    spriteBatch.Draw(_pixel, new Rectangle((int)GemAt(l).X - 22, (int)GemAt(l).Y - 1, 22, 3), (l < level ? Rarity.Colors[Rarity.TierAt(sel, l + 1)] : new Color(40, 40, 50)) * a);
                for (int l = 0; l < item.MaxLevel && item.MaxLevel > 1; l++)
                {
                    var at = GemAt(l);
                    var color = Rarity.Colors[Rarity.TierAt(sel, l + 1)];
                    float gemSize = 11 * (l == Math.Min(level, item.MaxLevel - 1) ? 1f + 0.12f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 6) : 1f);
                    spriteBatch.Draw(_pixel, at, null, Color.Black * a, MathHelper.PiOver4, new Vector2(0.5f), gemSize + 4, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_pixel, at, null, (l < level ? color : l == level ? color * 0.45f : new Color(60, 60, 70)) * a, MathHelper.PiOver4, new Vector2(0.5f), gemSize, SpriteEffects.None, 0f);
                }
                DrawText($"Type: {SlotNames[(int)item.Slot]}", new Vector2(InfoBox.X, InfoBox.Y + 26), Color.Gray, 0.32f);
                string stat = item.Show(Shop.Value(sel));
                DrawText(stat, new Vector2(InfoBox.X, InfoBox.Y + 48), Color.White, Fit(stat, 0.33f, InfoBox.Width));
                string next = Shop.IsMaxed(sel) ? "Fully upgraded" : $"Next upgrade: {item.Show(item.Values[level])}  ({Shop.Price(sel)} gems in the Shop)";
                DrawText(next, new Vector2(InfoBox.X, InfoBox.Y + 70), Color.LightGray, Fit(next, 0.3f, InfoBox.Width));
            }
            else DrawText("Drag gear onto a slot, or pick it up with Enter", new Vector2(InfoBox.X, InfoBox.Y + 30), Color.Gray, Fit("Drag gear onto a slot, or pick it up with Enter", 0.35f, InfoBox.Width));

            // Rock Back button like the other menus
            bool backHot = _area == 2 || BackButton.Contains(VirtualMouse.GetState().Position);
            MenuButton.Draw(spriteBatch, _button, BackButton, MenuButton.Tint(backHot) * a, true, _area == 2);
            var size = _backFont.MeasureString("Back") * 0.5f;
            DrawText("Back", new Vector2(BackButton.Center.X - size.X / 2, BackButton.Center.Y - size.Y / 2), backHot ? Color.Yellow : Color.White, 0.5f, _backFont);

            // The dragged icon follows the mouse
            var mouse = VirtualMouse.GetState().Position;
            if (_dragging is ItemId drag)
            {
                var dragArea = new Rectangle(mouse.X - 24, mouse.Y - 24, 48, 48);
                Rarity.DrawGlow(spriteBatch, dragArea, Rarity.Of(drag), a);
                spriteBatch.Draw(_icons[(int)drag], dragArea, Color.White * a);
            }
            // Controller: RT badge over the picked item, holding RT pops up its overview like a mouse hover
            ItemId? padPick = _area == 0 && _index < SlotNames.Length ? Shop.Equipped((Slot)_index) : _area == 1 && _index < bag.Length ? bag[_index].Id : null;
            var pickRect = _area == 0 ? SlotRectangle(Math.Min(_index, SlotNames.Length - 1)) : BagRectangle(Math.Min(_index, Math.Max(0, bag.Length - 1)));
            if (Controls.UsingPad && _dragging == null && padPick != null)
            {
                Hud.PadHint(spriteBatch, font, "RT", new Vector2(pickRect.Right - 4, pickRect.Y + 2), a);
                if (GamePad.GetState(PlayerIndex.One).Triggers.Right > 0.5f) { _hover = padPick; mouse = new Point(pickRect.Center.X, pickRect.Bottom); }
            }

            // Quick overview when hovering (not while dragging)
            if (_dragging != null) { }
            else if (_hover is ItemId hover)
            {
                var item = Shop.Get(hover);
                string line = item.Show(Shop.Value(hover));
                float scale = Fit(line, 0.3f, Game1.VirtualWidth - 40);
                var lineSize = font.MeasureString(line) * scale;
                int tipW = (int)MathF.Max(lineSize.X, font.MeasureString(item.Name).X * 0.33f) + 20;
                var tip = new Rectangle(Math.Min(mouse.X + 14, Game1.VirtualWidth - tipW - 8), mouse.Y + 14, tipW, 50);
                spriteBatch.Draw(_pixel, tip, new Color(12, 18, 28) * 0.92f * a);  // same tile as the run stats
                spriteBatch.Draw(_pixel, new Rectangle(tip.X, tip.Y, tip.Width, 3), Rarity.Of(hover) * a);  // rarity strip on top
                DrawText(item.Name, new Vector2(tip.X + 10, tip.Y + 7), Rarity.Of(hover), 0.33f);
                DrawText(line, new Vector2(tip.X + 10, tip.Y + 28), Color.LightGray, scale);
            }
            spriteBatch.End();
        }

        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= _backFont;  // Pixellari, the original font
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
