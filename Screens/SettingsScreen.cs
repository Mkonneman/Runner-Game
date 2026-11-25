using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Settings popup over the title for the music, sound effects and the smash ring
    /// </summary>
    public class SettingsScreen : GameScreen
    {
        private Texture2D _pixel;
        private Texture2D _button;
        private SoundEffect _testSound;   // plays when the sound effect volume changes so you can hear it

        // 3 columns: sound, gameplay, testing. Rows are numbered straight through, then Controls / Reset side by side and Back
        private static readonly string[] Labels = { "Music", "Sound effects", "UI sounds", "Screen shake", "Rumble", "Hit stop", "Smash ring", "Skin effects", "Infinite money", "Start at", "Hitboxes", "Equip popup" };
        private static readonly string[] ColumnNames = { "Sound", "Gameplay", "Testing" };
        private static readonly int[][] Columns = { new[] { 0, 1, 2 }, new[] { 3, 4, 5, 6, 7, 11 }, new[] { 8, 9, 10 } };
        private static readonly Rectangle Panel = new Rectangle(17, 8, 820, 464);  // covers the whole title so nothing peeks over the top
        private const int ColumnX = 37, ColumnWidth = 250, ColumnGap = 265, RowY = 118, RowSpacing = 36;  // 6 rows in the gameplay column
        private static readonly Rectangle ControlsButton = new Rectangle(227, 344, 190, 40);
        private static readonly Rectangle ResetButton = new Rectangle(437, 344, 190, 40);
        private static readonly Rectangle BackButton = new Rectangle(367, 396, 120, 40);
        private static int ColumnOf(int row) => Array.FindIndex(Columns, c => Array.IndexOf(c, row) >= 0);
        private static int SpotInColumn(int row) => Array.IndexOf(Columns[ColumnOf(row)], row);
        private const int ControlsRow = 12;
        private const int ResetRow = 13;
        private const int BackRow = 14;
        private int _selected;
        private bool _confirmReset;   // Reset needs a second press so you dont wipe everything by accident
        private string _message = "";
        private MouseState _lastMouse;

        public SettingsScreen()
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
            _testSound = ScreenManager.Game.Content.Load<SoundEffect>("Audio/Sfx/crash");

            // So the click that opened the menu doesn't also press something
            _lastMouse = VirtualMouse.GetState();
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Up/Down picks a row, Left/Right changes it (Controls and Reset sit side by side so Left/Right hops between them)
            int was = _selected;
            bool sideBySide = _selected == ControlsRow || _selected == ResetRow;
            // Up/Down stays in the column, off the bottom drops to the buttons. Q / E (or the d-pad / stick sideways) hops columns
            if (input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W))
                _selected = _selected == BackRow ? ControlsRow : _selected == ControlsRow ? Columns[0][^1] : _selected == ResetRow ? Columns[2][^1] : SpotInColumn(_selected) > 0 ? Columns[ColumnOf(_selected)][SpotInColumn(_selected) - 1] : _selected;
            else if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S))
                _selected = sideBySide ? BackRow : _selected == BackRow ? BackRow : _selected != Columns[ColumnOf(_selected)][^1] ? Columns[ColumnOf(_selected)][SpotInColumn(_selected) + 1] : ColumnOf(_selected) == 2 ? ResetRow : ControlsRow;
            bool left = input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A);
            bool right = input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D);
            // Keyboard arrows change the setting, the controller's sideways d-pad / stick moves columns instead (bumpers change it)
            bool keyLeft = (input.CurrentKeyboardState.IsKeyDown(Keys.Left) && input.LastKeyboardState.IsKeyUp(Keys.Left)) || input.IsNewKeyPress(Keys.A);
            bool keyRight = (input.CurrentKeyboardState.IsKeyDown(Keys.Right) && input.LastKeyboardState.IsKeyUp(Keys.Right)) || input.IsNewKeyPress(Keys.D);
            int hop = (input.IsNewKeyPress(Keys.E) || (right && !keyRight) ? 1 : 0) - (input.IsNewKeyPress(Keys.Q) || (left && !keyLeft) ? 1 : 0);
            if (hop != 0 && _selected < Labels.Length)
            {
                int column = Math.Clamp(ColumnOf(_selected) + hop, 0, 2);
                _selected = Columns[column][Math.Min(SpotInColumn(_selected), Columns[column].Length - 1)];
            }
            if (sideBySide)
            {
                if (left) _selected = ControlsRow;
                if (right) _selected = ResetRow;
            }
            else
            {
                if (keyLeft || input.IsNewButtonPress(Buttons.LeftShoulder)) Change(_selected, -1);
                if (keyRight || input.IsNewButtonPress(Buttons.RightShoulder)) Change(_selected, 1);
            }
            bool back = input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back) || (_selected == BackRow && input.IsNewKeyPress(Keys.Enter));
            bool reset = _selected == ResetRow && (input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space));
            bool controls = _selected == ControlsRow && (input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space));

            // Mouse support, hovering a row picks it and clicking the < or > changes it
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            for (int i = 0; i < Labels.Length; i++)
            {
                if (mouse.Position != _lastMouse.Position && RowRectangle(i).Contains(mouse.Position)) _selected = i;
                if (click && ArrowRectangle(i, -1).Contains(mouse.Position)) Change(i, -1);
                if (click && ArrowRectangle(i, 1).Contains(mouse.Position)) Change(i, 1);
            }
            if (mouse.Position != _lastMouse.Position && ControlsButton.Contains(mouse.Position)) _selected = ControlsRow;
            if (mouse.Position != _lastMouse.Position && ResetButton.Contains(mouse.Position)) _selected = ResetRow;
            if (click && ControlsButton.Contains(mouse.Position)) controls = true;
            if (mouse.Position != _lastMouse.Position && BackButton.Contains(mouse.Position)) _selected = BackRow;
            if (click && ResetButton.Contains(mouse.Position)) reset = true;
            if (click && BackButton.Contains(mouse.Position)) back = true;
            _lastMouse = mouse;
            if (_selected != was) Sfx.Play("menu_move", 0.4f, 0f);

            if (controls)
            {
                Sfx.UiPick();
                ScreenManager.AddScreen(new KeybindsScreen());
                return;
            }

            // Moving off the reset button cancels the are you sure
            if (_selected != ResetRow) _confirmReset = false;
            if (reset)
            {
                Sfx.UiPick();
                if (_confirmReset)
                {
                    SaveSystem.ResetPlayerData();
                    _message = "Player data reset";
                    _confirmReset = false;
                }
                else _confirmReset = true;
            }

            if (back)
            {
                SaveSystem.Save();  // Keeps the new volume and ring settings
                Sfx.UiBack();
                ExitScreen();
            }
        }

        // Steps a setting (volumes go by 10%, the rest by 25%), each one gives a little taste of what it does
        private void Change(int row, int direction)
        {
            if (row != 1 && row != 2) Sfx.UiMove();  // those two play their own sample
            switch (row)
            {
                case 0: Settings.MusicVolume = Step(Settings.MusicVolume, 0.1f * direction); break;
                case 1: Settings.SfxVolume = Step(Settings.SfxVolume, 0.1f * direction); _testSound.PlaySfx(); break;
                case 2: Settings.UiVolume = Step(Settings.UiVolume, 0.1f * direction); Sfx.Play("menu_pick", 0.6f, 0f); break;
                case 3: Settings.ShakeAmount = Step(Settings.ShakeAmount, 0.25f * direction); break;
                case 4: Settings.Rumble = Step(Settings.Rumble, 0.25f * direction); Rumble.Play("test"); break;
                case 5: Settings.HitStop = !Settings.HitStop; break;
                case 6: Settings.SmashRingOpacity = Step(Settings.SmashRingOpacity, 0.25f * direction); break;
                case 7: Settings.SkinFxAmount = Step(Settings.SkinFxAmount, 0.25f * direction); break;
                case 8: Settings.InfiniteMoney = !Settings.InfiniteMoney; break;
                case 9: Settings.TestStart = (Settings.TestStart + direction + Settings.StartNames.Length) % Settings.StartNames.Length; break;  // either arrow flips it
                case 10: Settings.ShowHitboxes = !Settings.ShowHitboxes; break;
                case 11: Settings.EquipPrompt = !Settings.EquipPrompt; break;
            }
        }

        // Rounded so repeated 0.1 steps land exactly on 0 and 1
        private static float Step(float value, float amount) => MathHelper.Clamp(MathF.Round((value + amount) * 100) / 100, 0, 1);

        // What shows between the arrows
        private static string Text(int row) => row switch
        {
            5 => Settings.HitStop ? "On" : "Off",
            8 => Settings.InfiniteMoney ? "On" : "Off",
            9 => Settings.StartNames[Settings.TestStart],
            10 => Settings.ShowHitboxes ? "On" : "Off",
            11 => Settings.EquipPrompt ? "On" : "Off",
            _ => Percent(row switch { 0 => Settings.MusicVolume, 1 => Settings.SfxVolume, 2 => Settings.UiVolume, 3 => Settings.ShakeAmount, 4 => Settings.Rumble, 6 => Settings.SmashRingOpacity, _ => Settings.SkinFxAmount }, row >= 3),
        };
        private static string Percent(float value, bool offAtZero) => offAtZero && value == 0 ? "Off" : $"{value * 100:0}%";

        // Whole row (for hover) and its < / > buttons
        private static Rectangle RowRectangle(int row) => new Rectangle(ColumnX + ColumnOf(row) * ColumnGap, RowY + SpotInColumn(row) * RowSpacing - 4, ColumnWidth, 30);
        private static Rectangle ArrowRectangle(int row, int direction) { var r = RowRectangle(row); return new Rectangle(direction < 0 ? r.X + 150 : r.Right - 28, r.Y + 4, 22, 22); }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.Game.Content.Load<SpriteFont>("Fonts/SettingsFont");  // settings keeps the original Pixellari, easiest to read here
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var mouse = VirtualMouse.GetState().Position;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);  // keeps the button art crisp

            // Darken the title and put a panel behind the menu
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, Panel, new Color(10, 20, 30) * 0.9f * TransitionAlpha);
            DrawText("Settings", new Vector2(427 - ScreenManager.TitleFont.MeasureString("Settings").X * 0.5f, 18), Color.Yellow, 1f, ScreenManager.TitleFont);

            // Column headers with a line under each
            for (int c = 0; c < ColumnNames.Length; c++)
            {
                float x = ColumnX + c * ColumnGap;
                DrawText(ColumnNames[c], new Vector2(x + ColumnWidth / 2f - font.MeasureString(ColumnNames[c]).X * 0.25f, 76), Color.Gold, 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle((int)x, 101, ColumnWidth, 2), Color.Gold * 0.5f * TransitionAlpha);
            }

            for (int i = 0; i < Labels.Length; i++)
            {
                bool selected = i == _selected;
                var row = RowRectangle(i);
                if (selected) spriteBatch.Draw(_pixel, row, new Color(30, 90, 60) * 0.85f * TransitionAlpha);
                float labelScale = MathF.Min(0.42f, 138 / font.MeasureString(Labels[i]).X);  // long names shrink to fit
                DrawText(Labels[i], new Vector2(row.X + 8, row.Y + 5), selected ? Color.Yellow : Color.White, labelScale);

                // < value >
                string text = Text(i);
                foreach (int direction in new[] { -1, 1 })
                {
                    var arrow = ArrowRectangle(i, direction);
                    MenuButton.Draw(spriteBatch, _button, arrow, MenuButton.Tint(arrow.Contains(mouse)) * TransitionAlpha, selected: false);  // clicks squash it, Enter doesnt
                    DrawText(direction < 0 ? "<" : ">", new Vector2(arrow.X + 6, arrow.Y + 1), Color.White, 0.42f);
                }
                float valueScale = MathF.Min(0.42f, 48 / font.MeasureString(text).X);
                float middle = (ArrowRectangle(i, -1).Right + ArrowRectangle(i, 1).Left) / 2f;
                DrawText(text, new Vector2(middle - font.MeasureString(text).X * valueScale / 2, row.Y + 5), Color.Cyan, valueScale);
            }
            string hint = "Keyboard: Up / Down to pick, Left / Right to change, Q / E to switch columns";
            DrawText(hint, new Vector2(427 - font.MeasureString(hint).X * 0.15f, 442), Color.Gray, 0.3f);
            hint = "Controller: D-pad / stick to move and switch columns, LB / RB to change";
            DrawText(hint, new Vector2(427 - font.MeasureString(hint).X * 0.15f, 456), Color.Gray, 0.3f);

            // Controls button opens the keybinds
            bool controlsSelected = _selected == ControlsRow;
            MenuButton.Draw(spriteBatch, _button, ControlsButton, MenuButton.Tint(controlsSelected) * TransitionAlpha);
            DrawText("Controls", new Vector2(ControlsButton.Center.X - font.MeasureString("Controls").X * 0.25f, ControlsButton.Y + 7), controlsSelected ? Color.Yellow : Color.White, 0.5f);

            // Reset button, turns red and asks again before wiping anything
            bool resetSelected = _selected == ResetRow;
            string resetText = _confirmReset ? "Are you sure?" : _message != "" ? _message : "Reset data";
            MenuButton.Draw(spriteBatch, _button, ResetButton, (_confirmReset ? new Color(255, 110, 110) : MenuButton.Tint(resetSelected)) * TransitionAlpha);  // goes red when it's asking
            DrawText(resetText, new Vector2(ResetButton.Center.X - font.MeasureString(resetText).X * 0.25f, ResetButton.Y + 7), resetSelected ? Color.Yellow : Color.White, 0.5f);

            // Back button
            bool backSelected = _selected == BackRow;
            MenuButton.Draw(spriteBatch, _button, BackButton, MenuButton.Tint(backSelected) * TransitionAlpha);
            DrawText("Back", new Vector2(BackButton.Center.X - font.MeasureString("Back").X * 0.3f, BackButton.Y + 6), backSelected ? Color.Yellow : Color.White, 0.6f);

            spriteBatch.End();
        }

        // Text with a shadow
        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.Game.Content.Load<SpriteFont>("Fonts/SettingsFont");
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
