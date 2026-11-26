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
        private Texture2D _button, _card;
        private MouseState _lastMouse;

        // Pictures pulled straight from the game art
        private Texture2D _player, _wallStone, _rock, _spike, _shopIcon, _gem, _dashIcon, _smashIcon, _door, _golem, _golemRage, _boulder, _glow, _icicle, _vines, _mushroom;
        private static readonly Rectangle SpikeSource = new Rectangle(27, 0, 74, 80); // same crop as the game uses

        // Each slide: title, a one line summary under it, then the text
        // [words] are gold, {Jump} / {Smash} / {Dash} / {Left} / {Right} / {Pause} turn into that button's icon (keyboard or controller), "" is a small gap
        private static readonly (string Title, string Tag, string[] Lines)[] Slides =
        {
            ("Controls", "Run, jump, smash. Don't stop.", new[] { "{Left} {Right}  move", "{Jump}  jump", "{Smash}  smash what's in front", "{Dash}  use your ability", "", "{Pause}  pause" }),
            ("Walls", "One touch and the run is over", new[] { "A red [!] flashes on the right", "just before a wall slides in", "", "{Smash} it to rubble", "before you reach it" }),
            ("Falling Rocks", "Look up!", new[] { "Rocks drop from the ceiling", "and take [25%] of your health", "", "Get under one and {Smash}", "it for bonus points" }),
            ("Spikes", "Watch your feet", new[] { "Crystal spikes slide along", "the floor and take [50%]", "of your health", "", "{Jump} over them, or smash", "them if you're quick" }),
            ("The Caves", "Three caves, each one harder", new[] { "[Blue], then [orange],", "then [green]", "", "Grab diamonds along the", "way, they turn into gems", "", "Reach the [door] at the end!" }),
            ("Cave Tricks", "Every cave has its own", new[] { "[Blue]: icicles drop when", "you run under them", "", "[Orange]: crystals hang from", "the roof, don't jump into them", "", "[Green]: vines grab you,", "mushrooms launch you" }),
            ("Vine Swing", "Some pits are too wide to jump", new[] { "Jump toward the vine and", "press {Jump} again near", "the end to grab it", "", "He lets go on the far side,", "or press {Jump} to jump off early" }),
            ("Combos", "Keep breaking, keep scoring", new[] { "Smash things back to back", "", "Every [4] breaks adds [x0.5]", "to your points, up to [x3]", "", "Getting hit or going [6s]", "without a break resets it" }),
            ("The Hallway", "No weapons past the door", new[] { "Boulders roll down the", "narrow stone hallway", "", "Hold {Smash} to crouch", "in the dips and let", "them roll over you" }),
            ("Stone Golem", "The boss of the caves", new[] { "{Smash} him to drain his", "health, but don't run into him", "", "Break his [fists] and the", "[walls] he summons", "", "Jump his [eye laser]", "as it sweeps the floor" }),
            ("Golem Shields", "Sometimes hits just bounce off", new[] { "When he curls up or his", "crystals glow [blue],", "he can't be hurt", "", "[Blinking marks] on the floor", "show where rocks will land" }),
            ("Golem Rage", "Half health is where it starts", new[] { "Watch the [mark] on his bar", "", "At half health his stone", "turns [purple], he speeds up", "and throws [3 fists] at once" }),
            ("The Shop", "Spend your gems", new[] { "Every [10 points] = [1 gem]", "", "Buy and upgrade weapons,", "abilities, armor and skins", "", "Upgrades climb the rarity", "colors, [red] is legendary" }),
            ("Gear & Skins", "Dress for the run", new[] { "Equip a weapon, ability,", "armor and skin in [Gear]", "", "Drag an item onto a slot", "to wear it", "", "Every skin has its", "own sparkles" }),
        };
        private int _slide;
        private float _time;  // drives the little animations on the pictures

        private static readonly Rectangle Panel = new Rectangle(132, 12, 590, 456);
        private static readonly Rectangle PrevButton = new Rectangle(177, 404, 60, 44);
        private static readonly Rectangle NextButton = new Rectangle(617, 404, 60, 44);
        private static readonly Rectangle BackButton = new Rectangle(367, 404, 120, 44);
        private static readonly Rectangle PictureBox = new Rectangle(177, 116, 230, 262);
        private const int TextX = 430, LineH = 30, GapH = 12;

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
            _button = content.Load<Texture2D>("Images/Menu/button");  // same stone button as the title
            _card = content.Load<Texture2D>("Images/Shop/item_card");  // crystal card behind the picture, same as the shop

            _player = content.Load<Texture2D>(Shop.SkinFolder(Shop.Equipped(Slot.Skin)) + "/run");  // whatever skin you're wearing
            _door = content.Load<Texture2D>("Images/Gameplay/door");
            _golem = content.Load<Texture2D>("Images/Boss/golem");
            _golemRage = content.Load<Texture2D>("Images/Boss/golem_rage");
            _boulder = content.Load<Texture2D>("Images/Arena/boulder");
            _wallStone = content.Load<Texture2D>("Images/Gameplay/wall_stone");
            _rock = content.Load<Texture2D>("Images/Gameplay/spike_rock");
            _spike = content.Load<Texture2D>("Images/Gameplay/stalactite");
            _shopIcon = content.Load<Texture2D>("Images/Menu/shop_icon");
            _gem = content.Load<Texture2D>("Images/Shop/gem");
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _dashIcon = content.Load<Texture2D>("Images/HUD/CooldownShiftButton");
            _smashIcon = content.Load<Texture2D>("Images/HUD/SmashCooldownButton");
            _icicle = content.Load<Texture2D>("Images/Gameplay/icicle");
            _vines = content.Load<Texture2D>("Images/Gameplay/vines");
            _mushroom = content.Load<Texture2D>("Images/Gameplay/mushroom");

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

            if (prev && _slide > 0) { _slide--; Sfx.UiMove(); }
            if (next)
            {
                if (_slide == Slides.Length - 1) back = true;
                else { _slide++; Sfx.UiPick(); }
            }
            if (back) { Sfx.UiBack(); ExitScreen(); }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var mouse = VirtualMouse.GetState().Position;
            var (title, tag, lines) = Slides[_slide];

            // PointClamp so the pixel art pictures stay sharp
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Darken the title, crystal panel over it, title with a thin line under it
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f * TransitionAlpha);
            Hud.Frame(spriteBatch, Panel, 20, Color.White * TransitionAlpha);
            DrawText(title, new Vector2(427 - ScreenManager.TitleFont.MeasureString(title).X * 0.4f, 32), Color.Yellow, 0.8f, ScreenManager.TitleFont);
            float tagW = ScreenManager.MenuFont.MeasureString(tag).X * 0.36f;
            DrawText(tag, new Vector2(427 - tagW / 2, 80), new Color(150, 220, 255), 0.36f);
            spriteBatch.Draw(_pixel, new Rectangle(190, 106, 474, 1), Color.Cyan * 0.35f * TransitionAlpha);

            // Picture on a crystal card on the left, text centered up and down next to it
            spriteBatch.Draw(_card, PictureBox, Color.White * TransitionAlpha);
            DrawPicture(spriteBatch);
            int height = 0;
            foreach (var line in lines) height += line == "" ? GapH : LineH;
            float y = PictureBox.Center.Y - height / 2f;
            foreach (var line in lines)
            {
                if (line == "") { y += GapH; continue; }
                DrawRich(line, TextX, y, Panel.Right - 34 - TextX);
                y += LineH;
            }

            // Round dots so you know which slide you're on, the current one is bigger and gold
            for (int i = 0; i < Slides.Length; i++)
                Hud.Dot(spriteBatch, new Vector2(427 - (Slides.Length - 1) * 9 + i * 18, 390), i == _slide ? 9 : 6, (i == _slide ? Color.Gold : Color.Gray * 0.7f) * TransitionAlpha);

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
            switch (Slides[_slide].Title)
            {
                case "Controls":
                    // Him running in place (same animation as the game) with the ability icons under him
                    int frame = (int)(_time / 0.08f) % 8;
                    spriteBatch.Draw(_player, center + new Vector2(0, 10), new Rectangle(frame * 56, 0, 56, 37), new Color(185, 205, 255) * TransitionAlpha, 0f, new Vector2(28, 37), 3.5f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_dashIcon, new Rectangle((int)center.X - 60, (int)center.Y + 40, 48, 48), fade);
                    spriteBatch.Draw(_smashIcon, new Rectangle((int)center.X + 12, (int)center.Y + 40, 48, 48), fade);
                    break;
                case "Walls":
                    // The stone wall with a blinking red ! like the warning in game
                    spriteBatch.Draw(_wallStone, new Rectangle((int)center.X - 27, PictureBox.Y + 34, 54, 206), new Rectangle(0, 0, 72, 330), fade);
                    if ((int)(_time * 4) % 2 == 0) DrawText("!", new Vector2(center.X - 8, PictureBox.Y + 4), Color.Red, 1.2f);
                    break;
                case "Falling Rocks":
                    // A rock bobbing down like it's falling
                    float drop = (_time * 60) % 60 - 30;
                    spriteBatch.Draw(_rock, center + new Vector2(0, drop), null, fade, 0f, new Vector2(64), 1.4f, SpriteEffects.None, 0f);
                    break;
                case "Spikes":
                    // The crystal spike, tinted blue like in the cave
                    spriteBatch.Draw(_spike, new Vector2(center.X, PictureBox.Bottom - 40), SpikeSource, new Color(150, 200, 255) * TransitionAlpha, 0f, new Vector2(SpikeSource.Width / 2f, SpikeSource.Height), 2.5f, SpriteEffects.None, 0f);
                    break;
                case "The Caves":
                    // The door at the end of the green cave
                    spriteBatch.Draw(_door, new Rectangle(PictureBox.X + 43, PictureBox.Y + 30, 144, 185), new Rectangle(0, 0, _door.Width / 6, _door.Height), new Color(62, 62, 68) * TransitionAlpha);  // same dark grey as in the cave
                    break;
                case "Cave Tricks":
                    // Icicle hanging and wobbling, the vine clump, a mushroom squashing
                    float wob = MathF.Sin(_time * 30) * ((int)(_time * 1.5f) % 2 == 0 ? 1.5f : 0);
                    spriteBatch.Draw(_icicle, new Vector2(PictureBox.X + 58 + wob, PictureBox.Y + 22), null, fade, 0f, new Vector2(_icicle.Width / 2f, 0), 0.9f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_vines, new Vector2(PictureBox.Right - 70, PictureBox.Y + 14), null, fade, 0f, new Vector2(_vines.Width / 2f, 0), 0.7f, SpriteEffects.None, 0f);
                    float sq = MathF.Max(0, MathF.Sin(_time * 4)) * 0.2f;
                    spriteBatch.Draw(_mushroom, new Vector2(center.X, PictureBox.Bottom - 26), null, fade, 0f, new Vector2(_mushroom.Width / 2f, _mushroom.Height), new Vector2(1.1f + sq * 0.6f, 1.1f - sq), SpriteEffects.None, 0f);
                    break;
                case "Vine Swing":
                    // The swing vine going back and forth with him hanging off the end
                    var anchor = new Vector2(center.X, PictureBox.Y + 20);
                    float swing = MathF.Sin(_time * 2.2f) * 0.6f;
                    var tip = anchor + new Vector2(MathF.Sin(swing), MathF.Cos(swing)) * 150;
                    Line(spriteBatch, anchor, tip, 7, new Color(10, 30, 12));
                    Line(spriteBatch, anchor, tip, 4, new Color(70, 190, 60));
                    spriteBatch.Draw(_player, tip + new Vector2(0, 90), new Rectangle(0, 0, 56, 37), new Color(185, 205, 255) * TransitionAlpha, swing * -0.5f, new Vector2(28, 37), 2.6f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_vines, anchor - new Vector2(0, 30), null, fade, 0f, new Vector2(_vines.Width / 2f, 0), 0.6f, SpriteEffects.None, 0f);
                    break;
                case "Combos":
                    // The combo meter climbing x1.5 to x3 over and over, same pips and colors as the HUD
                    int step = (int)(_time / 1.2f) % 4;
                    float m = 1.5f + 0.5f * step, t = _time % 1.2f;
                    var color = m >= 3 ? Color.OrangeRed : m >= 2.5f ? Color.OrangeRed : m >= 2 ? Color.Orange : Color.Gold;
                    string combo = $"x{m:0.0}";
                    var big = ScreenManager.BigFont;
                    float scale = 1.3f * (1f + 0.4f * MathF.Exp(-8f * t) * MathF.Cos(t * 25f));  // same springy pop as the HUD
                    var size = big.MeasureString(combo) * scale;
                    spriteBatch.Draw(_glow, center - new Vector2(0, 20), null, color * 0.35f * TransitionAlpha, 0f, new Vector2(_glow.Width / 2f, _glow.Height / 2f), 160f / _glow.Width, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(big, combo, center - new Vector2(size.X / 2, size.Y / 2 + 20) + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(big, combo, center - new Vector2(size.X / 2, size.Y / 2 + 20), color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    int lit = (int)(t / 0.3f) + 1;
                    for (int i = 0; i < 4; i++)
                        Hud.Pill(spriteBatch, new Rectangle((int)center.X - 76 + i * 40, (int)center.Y + 40, 32, 8), (i < lit ? color : Color.Gray * 0.4f) * TransitionAlpha);
                    break;
                case "The Hallway":
                    // A boulder rolling past
                    float roll = (_time * 60) % 150 - 75;  // stays inside the frame
                    spriteBatch.Draw(_boulder, center + new Vector2(-roll, 60), null, fade, -roll / 30f, new Vector2(_boulder.Width / 2f, _boulder.Height / 2f), 70f / _boulder.Width, SpriteEffects.None, 0f);
                    break;
                case "Stone Golem":
                case "Golem Shields":
                case "Golem Rage":
                    // The golem idling, glowing while he can't be hurt, or purple and raging
                    bool shield = Slides[_slide].Title == "Golem Shields", rage = Slides[_slide].Title == "Golem Rage";
                    int g = shield ? 3 + (int)(_time / 0.12f) % 3 : (int)(_time / 0.15f) % 4;
                    var feet = new Vector2(center.X, PictureBox.Bottom - 30);
                    if (rage) spriteBatch.Draw(_glow, feet - new Vector2(0, 75), null, new Color(150, 80, 255) * (0.35f + 0.12f * MathF.Sin(_time * 6)) * TransitionAlpha, 0f, new Vector2(_glow.Width / 2f, _glow.Height / 2f), 220f / _glow.Width, SpriteEffects.None, 0f);
                    spriteBatch.Draw(rage ? _golemRage : _golem, feet, new Rectangle(g * 100, (shield ? 1 : 0) * 100, 100, 100), fade, 0f, new Vector2(50, 70), 2.3f, SpriteEffects.FlipHorizontally, 0f);
                    break;
                case "The Shop":
                    // The shop icon, bobbing, with a couple of gems floating around it
                    float bob = MathF.Sin(_time * 2.5f) * 5;
                    spriteBatch.Draw(_glow, center + new Vector2(0, bob - 10), null, new Color(80, 220, 255) * 0.3f * TransitionAlpha, 0f, new Vector2(_glow.Width / 2f, _glow.Height / 2f), 200f / _glow.Width, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_shopIcon, center + new Vector2(0, bob - 10), null, fade, 0f, new Vector2(_shopIcon.Width / 2f, _shopIcon.Height / 2f), 4f, SpriteEffects.None, 0f);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = _time * 1.5f + k * MathHelper.TwoPi / 3;
                        spriteBatch.Draw(_gem, center + new Vector2(MathF.Cos(a) * 90, MathF.Sin(a) * 30 + 70), null, fade, 0f, new Vector2(16), 0.8f, SpriteEffects.None, 0f);
                    }
                    break;
                case "Gear & Skins":
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

        private void Line(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float thick, Color color)
        {
            var d = to - from;
            spriteBatch.Draw(_pixel, from, null, color * TransitionAlpha, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(d.Length(), thick), SpriteEffects.None, 0f);
        }

        // One line of slide text: [gold words] and {Action} button icons inline, shrinks if it's too wide
        private void DrawRich(string line, float x, float y, float maxWidth)
        {
            var font = ScreenManager.MenuFont;
            const float KeyH = 20, Pad = 5;
            var parts = System.Text.RegularExpressions.Regex.Split(line, @"(\[[^\]]+\]|\{[^}]+\})");
            float Width(string p, float s) => p.StartsWith('{') ? Hud.KeyWidth(font, KeyFor(p), KeyH) + Pad * 2 : font.MeasureString(p.Trim('[', ']')).X * s;
            float scale = 0.4f, total = 0;
            foreach (var p in parts) total += Width(p, scale);
            if (total > maxWidth) scale *= maxWidth / total;
            float mid = font.MeasureString("A").Y * scale / 2;
            foreach (var p in parts)
            {
                if (p == "") continue;
                if (p.StartsWith('{')) Hud.Key(ScreenManager.SpriteBatch, font, KeyFor(p), new Vector2(x + Width(p, scale) / 2, y + mid), TransitionAlpha, KeyH);
                else DrawText(p.Trim('[', ']'), new Vector2(x, y), p.StartsWith('[') ? Color.Gold : Color.White, scale);
                x += Width(p, scale);
            }
        }

        // {Jump} -> whatever Jump is bound to right now
        private static string KeyFor(string part) => part.Trim('{', '}') switch
        {
            "Pause" => Controls.UsingPad ? "Start" : "Esc",
            var name => Controls.Label(Enum.Parse<PlayerAction>(name)),
        };

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
