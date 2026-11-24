using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        private Texture2D _slotArt, _bagArt;  // crystal socket for the slots / bag spots, crystal frame around the bag
        private Texture2D _button;
        private Texture2D _player;
        private Texture2D[] _icons;

        private static readonly string[] SlotNames = { "Weapon", "Ability", "Armor", "Skin" };
        private static readonly Color PlayerTint = new Color(185, 205, 255);  // same blue as in the cave

        // Layout, player on the left, slots beside him, the bag on the right, info along the bottom
        private const int Cell = 48;
        private const int BagColumns = 6;
        private const int BagRows = 4;  // 24 spots, enough for every item even with all 3 slots empty
        private static readonly Rectangle Panel = new Rectangle(87, 20, 680, 445);
        private static readonly Rectangle BagArea = new Rectangle(417, 84, BagColumns * Cell + 16, BagRows * Cell + 16);
        private static readonly Rectangle InfoBox = new Rectangle(137, 318, 580, 82);
        private static readonly Rectangle BackButton = new Rectangle(367, 410, 120, 44);
        private static Rectangle SlotRectangle(int slot) => new Rectangle(250, 70 + slot * 58, 56, 56);  // 4 stacked so they clear the info box
        private Rectangle BagRectangle(int i) => new Rectangle(BagArea.X + 8 + i % BagColumns * Cell, BagArea.Y + 8 + (i / BagColumns - _bagScroll) * Cell, Cell - 6, Cell - 6);
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
            IsPopup = true;
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
            _button = content.Load<Texture2D>("Images/Menu/button");
            _slotArt = content.Load<Texture2D>("Images/Menu/gear_slot");
            _bagArt = content.Load<Texture2D>("Images/Menu/gear_bag");
            _player = content.Load<Texture2D>("Images/Gameplay/player_run");
            _icons = Array.ConvertAll(Shop.Items, item => content.Load<Texture2D>(item.Icon));
            _lastMouse = VirtualMouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            _messageTime -= (float)gameTime.ElapsedGameTime.TotalSeconds;
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
        private float Fit(string text, float scale, float width) => MathF.Min(scale, width / MathF.Max(1, ScreenManager.MenuFont.MeasureString(text).X));

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            float a = TransitionAlpha;
            var bag = Bag();

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.6f * a);
            spriteBatch.Draw(_pixel, new Rectangle(Panel.X + 8, Panel.Y + 8, Panel.Width - 16, Panel.Height - 16), new Color(10, 20, 30) * 0.95f * a);
            Hud.Frame(spriteBatch, Panel, 18, Color.White * a);  // same crystal frame as the shop cards
            DrawText("Gear", new Vector2(427 - ScreenManager.TitleFont.MeasureString("Gear").X * 0.3f, 22), Color.Yellow, 0.6f, ScreenManager.TitleFont);

            // The miner, big and running in place
            _player = ScreenManager.Game.Content.Load<Texture2D>(Shop.SkinFolder(Shop.Equipped(Slot.Skin)) + "/run");  // shows the skin you have on (cached after the first load)
            int frame = (int)(gameTime.TotalGameTime.TotalSeconds * 8) % (_player.Width / 56);
            spriteBatch.Draw(_player, new Rectangle(60, 112, 56 * 4, 37 * 4), new Rectangle(frame * 56, 0, 56, 37), PlayerTint * a);

            // Slots with their names, glow green when what you're carrying fits
            var carrying = _dragging ?? _held;
            for (int s = 0; s < SlotNames.Length; s++)
            {
                var rect = SlotRectangle(s);
                bool focus = _area == 0 && _index == s;
                bool fits = carrying is ItemId c && (int)Shop.Get(c).Slot == s;
                MenuButton.Draw(spriteBatch, _slotArt, rect, (fits ? new Color(140, 255, 160) : focus ? Color.Yellow : Color.White) * a, true, focus);
                DrawText(SlotNames[s], new Vector2(rect.Right + 8, rect.Y + 16), focus ? Color.Yellow : Color.White, 0.35f);
                if (Shop.Equipped((Slot)s) is ItemId on && on != _dragging)
                {
                    var iconArea = new Rectangle(rect.X + 8, rect.Y + 8, 40, 40);  // inside the socket's rim
                    Rarity.DrawGlow(spriteBatch, MenuButton.Inside(iconArea), Rarity.Of(on), a);
                    spriteBatch.Draw(_icons[(int)on], MenuButton.Inside(iconArea), (on == _held ? Color.White * 0.4f : Color.White) * a);
                    Rarity.DrawSparks(spriteBatch, MenuButton.Inside(iconArea), Rarity.Of(on), a);
                }
                else if (Shop.Equipped((Slot)s) == null)
                    DrawText("empty", new Vector2(rect.X + 9, rect.Y + 22), Color.Gray, 0.3f);
            }

            // The bag

            DrawText("Bag", new Vector2(BagArea.X, BagArea.Y - 24), Color.White, 0.4f);
            Hud.Frame(spriteBatch, _bagArt, 22, new Rectangle(BagArea.X - 6, BagArea.Y - 6, BagArea.Width + 12, BagArea.Height + 12), 16, Color.White * a);
            Hud.ScrollBar(spriteBatch, new Rectangle(BagArea.Right + 10, BagArea.Y + 6, 5, BagArea.Height - 12), _bagScroll, BagRows, (bag.Length + BagColumns - 1) / BagColumns, Color.Lerp(Color.Cyan, Color.White, 0.4f), a);
            for (int i = 0; i < bag.Length; i++)
            {
                if (!BagShowing(i)) continue;
                var rect = BagRectangle(i);
                bool focus = _area == 1 && _index == i;
                // Every spot is a little crystal socket, the picked one lights up yellow
                spriteBatch.Draw(_slotArt, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), (focus ? Color.Yellow : Color.White * 0.7f) * a);
                var inside = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                if (bag[i].Id != _dragging)
                {
                    Rarity.DrawGlow(spriteBatch, MenuButton.Inside(inside), Rarity.Of(bag[i].Id), a);
                    spriteBatch.Draw(_icons[(int)bag[i].Id], MenuButton.Inside(inside), (bag[i].Id == _held ? Color.White * 0.4f : Color.White) * a);
                    Rarity.DrawSparks(spriteBatch, MenuButton.Inside(inside), Rarity.Of(bag[i].Id), a);
                }
            }
            if (bag.Length == 0) DrawText("Buy more in the Shop!", new Vector2(BagArea.X + 20, BagArea.Center.Y - 8), Color.Gray, 0.35f);

            // Info on the selected item, current stat and what the next upgrade gives
            Hud.Frame(spriteBatch, new Rectangle(InfoBox.X - 8, InfoBox.Y - 6, InfoBox.Width + 16, InfoBox.Height + 12), 14, Color.White * 0.97f * a);
            if (_messageTime > 0) DrawText(_message, new Vector2(InfoBox.X + 20, InfoBox.Y + 10), Color.Yellow, Fit(_message, 0.38f, InfoBox.Width - 40));
            else if (_selected is ItemId sel)
            {
                var item = Shop.Get(sel);
                int level = Shop.Level(sel);
                string title = $"{item.Name}   Lv {level}/{item.MaxLevel}   ({SlotNames[(int)item.Slot]})";
                float titleScale = Fit(title, 0.38f, InfoBox.Width - 40 - item.MaxLevel * 22);  // leaves room for the level gems
                DrawText(title, new Vector2(InfoBox.X + 20, InfoBox.Y + 8), Rarity.Of(sel), titleScale);
                var titleSize = font.MeasureString(item.Name) * titleScale;
                Rarity.DrawSparks(spriteBatch, new Rectangle(InfoBox.X + 20, InfoBox.Y + 8, (int)titleSize.X, (int)titleSize.Y), Rarity.Of(sel), a, 4);
                // Level gems on the right like the shop's upgrade card, owned ones lit in their rarity color, the next one pulses
                // links first so every gem sits on top of them
                Vector2 GemAt(int l) => new Vector2(InfoBox.Right - 28 - (item.MaxLevel - 1 - l) * 22, InfoBox.Y + 17);
                for (int l = 1; l < item.MaxLevel; l++)
                    spriteBatch.Draw(_pixel, new Rectangle((int)GemAt(l).X - 22, (int)GemAt(l).Y - 1, 22, 3), (l < level ? Rarity.Colors[Rarity.TierAt(sel, l + 1)] : new Color(40, 40, 50)) * a);
                for (int l = 0; l < item.MaxLevel && item.MaxLevel > 1; l++)
                {
                    var at = GemAt(l);
                    var color = Rarity.Colors[Rarity.TierAt(sel, l + 1)];
                    float gemSize = 11 * (l == level ? 1f + 0.12f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 6) : 1f);
                    spriteBatch.Draw(_pixel, at, null, Color.Black * a, MathHelper.PiOver4, new Vector2(0.5f), gemSize + 4, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_pixel, at, null, (l < level ? color : l == level ? color * 0.45f : new Color(60, 60, 70)) * a, MathHelper.PiOver4, new Vector2(0.5f), gemSize, SpriteEffects.None, 0f);
                }
                string stat = item.Show(Shop.Value(sel));
                DrawText(stat, new Vector2(InfoBox.X + 20, InfoBox.Y + 32), Color.White, Fit(stat, 0.33f, InfoBox.Width - 40));
                string next = Shop.IsMaxed(sel) ? "Fully upgraded" : $"Next upgrade: {item.Show(item.Values[level])}  ({Shop.Price(sel)} gems in the Shop)";
                DrawText(next, new Vector2(InfoBox.X + 20, InfoBox.Y + 54), Color.LightGray, Fit(next, 0.3f, InfoBox.Width - 40));
            }
            else DrawText("Drag gear onto a slot, or pick it up with Enter", new Vector2(InfoBox.X + 20, InfoBox.Y + 30), Color.Gray, Fit("Drag gear onto a slot, or pick it up with Enter", 0.35f, InfoBox.Width - 40));

            MenuButton.Draw(spriteBatch, _button, BackButton, MenuButton.Tint(_area == 2 || BackButton.Contains(VirtualMouse.GetState().Position)) * a);
            var size = font.MeasureString("Back") * 0.5f;
            DrawText("Back", new Vector2(BackButton.Center.X - size.X / 2, BackButton.Center.Y - size.Y / 2), _area == 2 ? Color.Yellow : Color.White, 0.5f);

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
            font ??= ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
