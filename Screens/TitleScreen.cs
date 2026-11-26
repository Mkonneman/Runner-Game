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
        private const float RunnerStartX = 250f;  // just past the button column so he stays in view
        private const float CaveMouthX = 552f;   // middle of the cave opening
        private const float RunSpeed = 220f;     // same as the ground so his steps match
        private const float Rush = 1.8f;         // the walk into the cave plays this much faster so it doesnt drag

        // How long the background has been scrolling
        private float _scrollTime;

        // Bools for running into the cave and starting the game
        private bool _enteringCave;
        private float _caveSlide = CaveHidden;  // how far right of its spot the cave is, starts off screen
        private const float CaveHidden = 400f;
        private float UiAlpha => TransitionAlpha * _caveSlide / CaveHidden;  // menu fades out as the cave slides in
        private bool _gameStarted;

        // Coming back after beating the golem: the arena closed the circle, the title opens it back up on him
        private static bool _reopen;  // set by the arena, picked up once the title shows again
        public static void ReopenNext() => _reopen = true;

        // Timer for the blinking "Press Enter" text
        private float _blinkTimer;

        // Stone buttons in a row under the logo (click them, or Tab for the shop)
        private int _selectedButton = -1;  // which button the keys / controller picked (-1 means none, Enter just plays)
        // All the buttons stacked down the left edge, centered top to bottom (Endless slots in once the golem is beaten)
        private static Rectangle Stacked(int slot)
        {
            int count = Achievements.Has("golem") ? 6 : 5;
            const int H = 44, Gap = 10;
            return new Rectangle(14, (480 - (count * H + (count - 1) * Gap)) / 2 + slot * (H + Gap), 170, H);
        }
        private static bool EndlessOn => Achievements.Has("golem");
        private static Rectangle ShopButton => Stacked(0);
        private static Rectangle GearButton => Stacked(1);
        private static Rectangle TrophyButton => Stacked(2);
        private static Rectangle EndlessButton => Stacked(3);
        private static Rectangle HowToPlayButton => Stacked(EndlessOn ? 4 : 3);
        private static Rectangle SettingsButton => Stacked(EndlessOn ? 5 : 4);
        private static Rectangle[] ButtonRects => new[] { ShopButton, GearButton, HowToPlayButton, SettingsButton, TrophyButton, EndlessButton };  // same order as the button indexes

        // Arrow / d-pad step: the closest button that way (straight lines win over diagonals), up off the top goes back to Play
        private static int Step(int from, Point dir, bool endless)
        {
            if (from < 0) return dir.Y > 0 || dir.X != 0 ? 0 : -1;
            var c = ButtonRects[from].Center;
            int best = -1; float bestScore = float.MaxValue;
            for (int i = 0; i < ButtonRects.Length; i++)
            {
                if (i == from || (i == 5 && !endless)) continue;
                var d = ButtonRects[i].Center - c;
                float along = d.X * dir.X + d.Y * dir.Y, side = MathF.Abs(d.X * dir.Y) + MathF.Abs(d.Y * dir.X);
                if (along > 0 && along + side * 2 < bestScore) { bestScore = along + side * 2; best = i; }
            }
            return best >= 0 ? best : dir.Y < 0 ? -1 : from;
        }
        private Texture2D _button;
        private Texture2D _panel;  // backing behind the buttons, rebuilt if Endless shows up and the column grows

        // Rounded trapezoid turned on its side: full height on the left, inset top and bottom on the right
        private Texture2D BuildPanel(int w, int h)
        {
            const float R = 18, Inset = 22;
            var pts = new[] { new Vector2(R, R), new Vector2(w - R, R + Inset), new Vector2(w - R, h - R - Inset), new Vector2(R, h - R) };
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = 0; bool inside = true;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 a = pts[i], b = pts[(i + 1) % 4], e = b - a;
                        if (e.X * (p.Y - a.Y) - e.Y * (p.X - a.X) < 0) inside = false;  // clockwise in screen space, so outside is negative
                    }
                    if (!inside)
                    {
                        dist = float.MaxValue;
                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 a = pts[i], b = pts[(i + 1) % 4], e = b - a;
                            float t = MathHelper.Clamp(Vector2.Dot(p - a, e) / e.LengthSquared(), 0, 1);
                            dist = MathF.Min(dist, Vector2.Distance(p, a + e * t));
                        }
                    }
                    if (dist > R) continue;
                    var fill = new Color(8, 14, 28) * 0.72f;
                    var rim = new Color(70, 170, 210) * 0.55f;
                    data[y * w + x] = dist > R - 2 ? rim : fill;
                }
            var tex = new Texture2D(ScreenManager.GraphicsDevice, w, h);
            tex.SetData(data);
            return tex;
        }
        private SpriteFont _leaderFont;  // Pixel Operator Bold, chunky enough for the score lists
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> _icons = new();  // dark blue rock icon for each button, title screen only
        private Texture2D _logo;

        // Logo flair, embers drift up off it and little twinkles pop on the letters (pos, vel, age, life, twinkle?, color)
        private readonly List<(Vector2 Pos, Vector2 Vel, float Age, float Life, bool Star, Color Color)> _logoBits = new();
        private float _logoEmit, _logoTime;

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
            _button = content.Load<Texture2D>("Images/Menu/title_button");
            _leaderFont = content.Load<SpriteFont>("Fonts/LeaderFont");  // the other screens keep the old button
            foreach (var (label, file) in new[] { ("Shop", "shop"), ("Gear", "gear"), ("Trophies", "trophy"), ("How to Play", "help"), ("Settings", "settings"), ("Endless", "endless") })
                _icons[label] = content.Load<Texture2D>("Images/Menu/title_" + file);
            _logo = content.Load<Texture2D>("Images/Menu/logo");
            _glow = content.Load<Texture2D>("Images/Particles/particle");
            _eyeSparks = new SparkParticleSystem(ScreenManager.Game, new Color(255, 60, 40), 1.2f, 0.3f, new Vector2(0, -20), new Vector2(0, -20), wobble: 8, wobbleSpeed: 3);
            _eyeSparks.Initialize();
            _titleMusic = content.Load<Song>("Audio/Music/title_screen");  // new title theme

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
            _caveSlide = CaveHidden;
            _gameStarted = false;
        }

        private void UpdateLogoBits(float dt)
        {
            _logoTime += dt;
            float left = (854 - _logo.Width) / 2f;
            for (_logoEmit += dt * 30; _logoEmit >= 1; _logoEmit--)
            {
                if (RandomHelper.Next(0, 3) > 0)  // embers rising off the letters
                    _logoBits.Add((new Vector2(left + RandomHelper.NextFloat(20, _logo.Width - 20), RandomHelper.NextFloat(70, 200)), new Vector2(RandomHelper.NextFloat(-15, 15), RandomHelper.NextFloat(-60, -25)),
                        0, RandomHelper.NextFloat(0.8f, 1.6f), false, RandomHelper.Next(0, 1) == 0 ? new Color(255, 200, 60) : new Color(255, 110, 30)));
                else  // twinkle on the logo
                    _logoBits.Add((new Vector2(left + RandomHelper.NextFloat(20, _logo.Width - 20), RandomHelper.NextFloat(45, 190)), Vector2.Zero, 0, 0.5f, true, new Color(255, 245, 200)));
            }
            for (int i = _logoBits.Count - 1; i >= 0; i--)
            {
                var b = _logoBits[i];
                b.Age += dt; b.Pos += b.Vel * dt; b.Pos.X += MathF.Sin(b.Age * 6 + b.Pos.Y) * 8 * dt;  // embers wiggle up
                if (b.Age > b.Life) _logoBits.RemoveAt(i); else _logoBits[i] = b;
            }
        }

        private void DrawLogoBit(SpriteBatch spriteBatch, (Vector2 Pos, Vector2 Vel, float Age, float Life, bool Star, Color Color) b)
        {
            float t = b.Age / b.Life, fade = (1 - t) * UiAlpha;
            int x = (int)b.Pos.X, y = (int)b.Pos.Y;
            if (b.Star)
            {
                int arm = (int)(4 * MathF.Sin(t * MathF.PI)) + 1;
                spriteBatch.Draw(_pixel, new Rectangle(x - arm, y, arm * 2 + 1, 1), b.Color * MathF.Sin(t * MathF.PI) * UiAlpha);
                spriteBatch.Draw(_pixel, new Rectangle(x, y - arm, 1, arm * 2 + 1), b.Color * MathF.Sin(t * MathF.PI) * UiAlpha);
            }
            else spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 1, t < 0.5f ? 3 : 2, t < 0.5f ? 3 : 2), b.Color * fade);
        }

        // New run from wherever the Start at setting says (hallway and boss skip the caves)
        public static GameScreen NewRun()
        {
            RunStats.Reset();  // fresh stats and combo
            return Settings.TestStart >= 4 && !CaveZones.Endless ? new ArenaScreen(0) : new GameplayScreen();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            if (_runner.Skin != Shop.Equipped(Slot.Skin)) _runner.LoadContent(ScreenManager.Game.Content);  // picked a new skin in the shop or gear
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateLogoBits(dt);
            _blinkTimer += dt;

            // A few sparks a second from the cave's eyes, drifting up and wobbling until they fade
            if (RandomHelper.Next(0, 6) == 0)
                _eyeSparks.Emit(EyePosition(CaveEyes[RandomHelper.Next(0, 1)]) + new Vector2(RandomHelper.NextFloat(-4, 4), 0));
            _eyeSparks.Update(gameTime);

            // Reset once the game screen fully covers the title
            if (ScreenState == ScreenState.Hidden) ResetScene();
            if (_reopen && ScreenState != ScreenState.Hidden)
            {
                _reopen = false;
                Iris.Center = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
                Iris.Start();
            }

            // Background scrolls until Enter, then he runs into the cave and the game starts
            if (!_enteringCave)
            {
                _scrollTime += dt;
            }
            else if (_caveSlide > 0)  // keeps running while the cave scrolls in from the right
            {
                _scrollTime += dt * Rush;
                _caveSlide = MathF.Max(0, _caveSlide - LayerInfo[^1].Speed * dt * Rush);
            }
            else if (_runner.Position.X < CaveMouthX)
            {
                _runner.Position = new Vector2(MathF.Min(_runner.Position.X + RunSpeed * Rush * dt, CaveMouthX), _runner.Position.Y);
            }
            else if (!_gameStarted)
            {
                _gameStarted = true;
                ScreenManager.AddScreen(NewRun());
                Iris.Start();
            }

            // Keep the title music going while the title is showing
            // (it gets stopped when the player crashes, and the boss song keeps playing after the arena so swap that out too)
            // only once nothing is left above it, a run fading out after Play Again would let it steal the new run's song
            if (!coveredByOtherScreen && !_gameStarted && ScreenManager.GetScreens()[^1] == this) Music.Play(_titleMusic, 2f, resume: true);
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Arrows / d-pad move to the nearest button that way, like they sit on screen
            bool endless = Achievements.Has("golem");
            int was = _selectedButton;
            if (input.IsNewKeyPress(Keys.Down)) _selectedButton = Step(_selectedButton, new Point(0, 1), endless);
            else if (input.IsNewKeyPress(Keys.Up)) _selectedButton = Step(_selectedButton, new Point(0, -1), endless);
            else if (input.IsNewKeyPress(Keys.Right)) _selectedButton = Step(_selectedButton, new Point(1, 0), endless);
            else if (input.IsNewKeyPress(Keys.Left)) _selectedButton = Step(_selectedButton, new Point(-1, 0), endless);
            if (_selectedButton != was) Sfx.Play("menu_move", 0.4f, 0f);

            // Enter / A presses the picked button, or starts the run if none is picked. Space and Start always start
            bool confirm = input.IsNewKeyPress(Keys.Enter);
            bool start = input.CurrentKeyboardState.IsKeyDown(Keys.Space) && input.LastKeyboardState.IsKeyUp(Keys.Space) || input.IsNewButtonPress(Buttons.Start) || input.IsNewButtonPress(Buttons.RightTrigger);
            if (confirm && _selectedButton < 0) start = true;
            if (start && !_enteringCave) { CaveZones.Endless = false; _enteringCave = true; }  // the normal run

            // Clicking works too, and Tab is still the shop shortcut
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            if (mouse.Position != _lastMouse.Position) _selectedButton = -1;  // mouse takes over the highlight
            _lastMouse = mouse;
            int open = confirm && !start ? _selectedButton : -1;
            if (open == 5) { open = -1; click = true; mouse = new MouseState(EndlessButton.Center.X, EndlessButton.Center.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released); }  // Enter / A on Endless acts like clicking it
            if (click && ShopButton.Contains(mouse.Position) || input.IsNewKeyPress(Keys.Tab)) open = 0;
            if (click && GearButton.Contains(mouse.Position)) open = 1;
            if (click && HowToPlayButton.Contains(mouse.Position)) open = 2;
            if (click && SettingsButton.Contains(mouse.Position)) open = 3;
            if (click && TrophyButton.Contains(mouse.Position)) open = 4;
            if (!_enteringCave && click && Achievements.Has("golem") && EndlessButton.Contains(mouse.Position))
            {
                // Endless, the caves just keep cycling colors, no door or boss
                Sfx.Play("menu_pick", 0.5f, 0f);
                CaveZones.Endless = true;
                _enteringCave = true;
                return;
            }
            if (!_enteringCave && open >= 0)
            {
                Sfx.Play("menu_pick", 0.5f, 0f);
                ScreenManager.AddScreen(open == 0 ? new ShopScreen() : open == 1 ? new GearScreen() : open == 2 ? new HowToPlayScreen() : open == 3 ? new SettingsScreen() : new AchievementsScreen());
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
                    spriteBatch.Draw(_cave, new Vector2(viewport.Width - _cave.Width + 40 + _caveSlide, GroundY + 30 - _cave.Height), fade); // +40 hides its flat right edge off screen
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
            var logoPos = new Vector2((viewport.Width - _logo.Width) / 2, 34 + MathF.Sin(_logoTime * 2f) * 3);  // gentle bob
            spriteBatch.Draw(_glow, logoPos + new Vector2(_logo.Width / 2f, _logo.Height / 2f), null, new Color(255, 140, 30) * (0.25f + 0.08f * MathF.Sin(_logoTime * 3)) * UiAlpha, 0f, new Vector2(8), new Vector2(_logo.Width / 9f, _logo.Height / 11f), SpriteEffects.None, 0f);  // warm glow behind
            foreach (var b in _logoBits) if (!b.Star) DrawLogoBit(spriteBatch, b);
            spriteBatch.Draw(_logo, logoPos, Color.White * UiAlpha);
            foreach (var b in _logoBits) if (b.Star) DrawLogoBit(spriteBatch, b);

            // Blinks on and off about twice a second (hidden once he heads in)
            if (!_enteringCave && (int)(_blinkTimer * 2) % 2 == 0)
            {
                Hud.KeyLine(spriteBatch, ScreenManager.BigFont, "Press", Controls.UsingPad ? "RT" : "Enter", "to Start", new Vector2(viewport.Width / 2f, 250), 0.75f, Color.Yellow, UiAlpha);
            }

            DrawLeaderboard(spriteBatch, viewport);

            // Buttons (lit up when the mouse is over them)
            // backing panel: tall on the left, a bit shorter on the right
            var first = Stacked(0); var last = Stacked(EndlessOn ? 5 : 4);
            var back = new Rectangle(2, first.Y - 24, 198, last.Bottom + 30 - (first.Y - 24));  // extra room so the slanted right side still covers Shop and Settings
            if (_panel == null || _panel.Height != back.Height) { _panel?.Dispose(); _panel = BuildPanel(back.Width, back.Height); }
            ScreenManager.SpriteBatch.Draw(_panel, back, Color.White * UiAlpha);
            DrawButton(ShopButton, "Shop", 0);
            DrawButton(GearButton, "Gear", 1);
            DrawButton(HowToPlayButton, "How to Play", 2);
            DrawButton(SettingsButton, "Settings", 3);
            DrawButton(TrophyButton, "Trophies", 4);
            if (Achievements.Has("golem")) DrawButton(EndlessButton, "Endless", 5);

            spriteBatch.End();
        }

        // Top right crystal panel, two top 5 lists: best scores, then farthest runs. Gold / silver / bronze for the top 3
        private void DrawLeaderboard(SpriteBatch spriteBatch, Viewport viewport)
        {
            if (UiAlpha <= 0) return;
            var font = _leaderFont;
            const int W = 184, H = 240, Pad = 30, RowH = 13;  // big pad keeps the text off the crystal border
            var box = new Rectangle(viewport.Width - W - 8, (viewport.Height - H) / 2, W, H);  // right edge, centered top to bottom
            Hud.Frame(spriteBatch, box, 16, Color.White * 0.95f * UiAlpha);
            Color[] medals = { Color.Gold, new Color(200, 210, 225), new Color(215, 140, 80) };
            void Text(string text, float x, float y, Color color, float scale, bool right = false)
            {
                if (right) x -= font.MeasureString(text).X * scale;
                spriteBatch.DrawString(font, text, new Vector2(x + 1, y + 1), Color.Black * UiAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, text, new Vector2(x, y), color * UiAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            void List(string title, int[] values, string unit, int y)
            {
                Text(title, box.X + Pad, y, Color.Cyan, 0.32f);
                for (int i = 0; i < 5; i++)
                {
                    int rowY = y + 18 + i * RowH;
                    var color = i < values.Length ? (i < 3 ? medals[i] : Color.White) : Color.Gray * 0.5f;
                    if (i == 0 && values.Length > 0) Hud.Glow(spriteBatch, new Vector2(box.Center.X, rowY + 6), new Vector2(W - Pad * 2, 18), Color.Gold * 0.25f * UiAlpha);  // #1 glows
                    Text($"{i + 1}.", box.X + Pad, rowY, color, 0.28f);
                    Text(i < values.Length ? $"{values[i]}{unit}" : "---", box.Right - Pad, rowY, color, 0.28f, right: true);
                }
            }
            List("YOUR TOP RUNS", RunStats.TopScores, "", box.Y + 26);
            spriteBatch.Draw(_pixel, new Rectangle(box.X + Pad, box.Y + 120, W - Pad * 2, 1), Color.Cyan * 0.35f * UiAlpha);  // divider
            Text("GLOBAL STATS", box.X + Pad, box.Y + 128, Color.Cyan, 0.32f);
            const string Soon = "Coming soon";
            Text(Soon, box.Center.X - font.MeasureString(Soon).X * 0.3f / 2, box.Y + 168, Color.Gray, 0.3f);
        }

        // Where an eye of the cave is on screen (the cave is drawn at the right edge, sitting on the ground)
        private Vector2 EyePosition(Vector2 eye)
        {
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            return new Vector2(viewport.Width - _cave.Width + 40 + _caveSlide, GroundY + 30 - _cave.Height) + eye;
        }

        // A button with its label centered in it
        private void DrawButton(Rectangle button, string label, int index)
        {
            var font = ScreenManager.TitleFont;
            bool hover = button.Contains(VirtualMouse.GetState().Position) || index == _selectedButton;
            MenuButton.Hover(button, hover);  // grows and bounces for the mouse or the key / pad pick
            ScreenManager.SpriteBatch.Draw(_button, MenuButton.Squash(button, index == _selectedButton), (hover ? Color.White : new Color(170, 180, 200)) * UiAlpha);  // only the key-picked one squashes on Enter

            // Label shrinks to fit long names like How to Play
            float scale = MathF.Min(0.65f, (button.Width - 28) / font.MeasureString(label).X);
            Vector2 size = font.MeasureString(label) * scale;
            // Every button gets its rock icon on the left and the text nudged over to make room
            bool hasIcon = _icons.TryGetValue(label, out var icon);
            if (hasIcon)
            {
                scale = MathF.Min(scale, (button.Width - 66) / font.MeasureString(label).X);
                size = font.MeasureString(label) * scale;
                var iconRect = MenuButton.Inside(new Rectangle(button.X + 8, button.Center.Y - icon.Height / 2, icon.Width, icon.Height));
                ScreenManager.SpriteBatch.Draw(icon, iconRect, Color.White * UiAlpha);
            }
            var position = new Vector2(button.Center.X - size.X / 2 + (hasIcon ? 20 : 0), button.Center.Y - size.Y / 2);
            ScreenManager.SpriteBatch.DrawString(font, label, MenuButton.TextPos(position) + new Vector2(2), Color.Black * UiAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, label, MenuButton.TextPos(position), (hover ? Color.Yellow : Color.White) * UiAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }

        // Draws text centered left to right on the screen (with a shadow so it stands out)
        private void DrawCentered(string text, float y, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(3), Color.Black * UiAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * UiAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
