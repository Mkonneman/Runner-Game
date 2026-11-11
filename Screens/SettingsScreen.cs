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

        // Rows go music, sound effects, smash ring, skin effects, Controls, Reset, then Back
        private static readonly string[] Labels = { "Music", "Sound effects", "Smash ring", "Skin effects", "Infinite money", "Start at", "Hitboxes" };
        private const int RowY = 146;          // y of the first row
        private const int RowSpacing = 32;
        private static readonly Rectangle ControlsButton = new Rectangle(237, 378, 185, 44);
        private static readonly Rectangle ResetButton = new Rectangle(432, 378, 185, 44);
        private static readonly Rectangle BackButton = new Rectangle(367, 426, 120, 44);
        private const int ControlsRow = 7;
        private const int ResetRow = 8;
        private const int BackRow = 9;
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
            bool sideBySide = _selected == ControlsRow || _selected == ResetRow;
            if (input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W)) _selected = _selected == BackRow ? ControlsRow : sideBySide ? ControlsRow - 1 : Math.Max(_selected - 1, 0);
            else if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _selected = sideBySide ? BackRow : Math.Min(_selected + 1, BackRow);
            bool left = input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A);
            bool right = input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D);
            if (sideBySide)
            {
                if (left) _selected = ControlsRow;
                if (right) _selected = ResetRow;
            }
            else
            {
                if (left) Change(_selected, -1);
                if (right) Change(_selected, 1);
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

            if (controls)
            {
                ScreenManager.AddScreen(new KeybindsScreen());
                return;
            }

            // Moving off the reset button cancels the are you sure
            if (_selected != ResetRow) _confirmReset = false;
            if (reset)
            {
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
                ExitScreen();
            }
        }

        // Steps a setting (volumes go by 10% and the smash ring by 25%)
        private void Change(int row, int direction)
        {
            if (row == 0) Settings.MusicVolume = Step(Settings.MusicVolume, 0.1f * direction);
            if (row == 1)
            {
                Settings.SfxVolume = Step(Settings.SfxVolume, 0.1f * direction);
                _testSound.Play();
            }
            if (row == 2) Settings.SmashRingOpacity = Step(Settings.SmashRingOpacity, 0.25f * direction);
            if (row == 3) Settings.SkinFxAmount = Step(Settings.SkinFxAmount, 0.25f * direction);
            if (row == 4) Settings.InfiniteMoney = !Settings.InfiniteMoney;
            if (row == 6) Settings.ShowHitboxes = !Settings.ShowHitboxes;
            if (row == 5) Settings.TestStart = (Settings.TestStart + direction + Settings.StartNames.Length) % Settings.StartNames.Length;  // either arrow flips it
        }

        // Rounded so repeated 0.1 steps land exactly on 0 and 1
        private static float Step(float value, float amount) => MathHelper.Clamp(MathF.Round((value + amount) * 100) / 100, 0, 1);

        private static float Value(int row) => row == 0 ? Settings.MusicVolume : row == 1 ? Settings.SfxVolume : row == 2 ? Settings.SmashRingOpacity : row == 3 ? Settings.SkinFxAmount : Settings.InfiniteMoney ? 1 : 0;

        // Whole row (for hover) and its < / > buttons
        private static Rectangle RowRectangle(int row) => new Rectangle(237, RowY + row * RowSpacing - 2, 380, 30);
        private static Rectangle ArrowRectangle(int row, int direction) => new Rectangle(direction < 0 ? 447 : 567, RowY + row * RowSpacing - 2, 30, 30);

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var mouse = VirtualMouse.GetState().Position;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);  // keeps the button art crisp

            // Darken the title and put a panel behind the menu
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(217, 90, 420, 385), new Color(10, 20, 30) * 0.9f * TransitionAlpha);
            DrawText("Settings", new Vector2(427 - ScreenManager.TitleFont.MeasureString("Settings").X * 0.5f, 100), Color.Yellow, 1f, ScreenManager.TitleFont);

            for (int i = 0; i < Labels.Length; i++)
            {
                bool selected = i == _selected;
                if (selected) spriteBatch.Draw(_pixel, RowRectangle(i), new Color(30, 90, 60) * 0.85f * TransitionAlpha);
                DrawText(Labels[i], new Vector2(252, RowY + i * RowSpacing), selected ? Color.Yellow : Color.White, 0.52f);

                // < value >
                float value = Value(i);
                string text = i == 6 ? (Settings.ShowHitboxes ? "On" : "Off") : i == 5 ? Settings.StartNames[Settings.TestStart] : i == 4 ? (value > 0 ? "On" : "Off") : i >= 2 && value == 0 ? "Off" : $"{value * 100:0}%";
                foreach (int direction in new[] { -1, 1 })
                {
                    var arrow = ArrowRectangle(i, direction);
                    MenuButton.Draw(spriteBatch, _button, arrow, MenuButton.Tint(arrow.Contains(mouse)) * TransitionAlpha);
                    DrawText(direction < 0 ? "<" : ">", new Vector2(arrow.X + 9, arrow.Y + 2), Color.White, 0.55f);
                }
                DrawText(text, new Vector2(527 - font.MeasureString(text).X * 0.3f, RowY + i * RowSpacing), Color.Cyan, 0.52f);
            }

            // Controls button opens the keybinds
            bool controlsSelected = _selected == ControlsRow;
            MenuButton.Draw(spriteBatch, _button, ControlsButton, MenuButton.Tint(controlsSelected) * TransitionAlpha);
            DrawText("Controls", new Vector2(ControlsButton.Center.X - font.MeasureString("Controls").X * 0.25f, ControlsButton.Y + 10), controlsSelected ? Color.Yellow : Color.White, 0.5f);

            // Reset button, turns red and asks again before wiping anything
            bool resetSelected = _selected == ResetRow;
            string resetText = _confirmReset ? "Are you sure?" : _message != "" ? _message : "Reset data";
            MenuButton.Draw(spriteBatch, _button, ResetButton, (_confirmReset ? new Color(255, 110, 110) : MenuButton.Tint(resetSelected)) * TransitionAlpha);  // goes red when it's asking
            DrawText(resetText, new Vector2(ResetButton.Center.X - font.MeasureString(resetText).X * 0.25f, ResetButton.Y + 10), resetSelected ? Color.Yellow : Color.White, 0.5f);

            // Back button
            bool backSelected = _selected == BackRow;
            MenuButton.Draw(spriteBatch, _button, BackButton, MenuButton.Tint(backSelected) * TransitionAlpha);
            DrawText("Back", new Vector2(BackButton.Center.X - font.MeasureString("Back").X * 0.3f, BackButton.Y + 8), backSelected ? Color.Yellow : Color.White, 0.6f);

            spriteBatch.End();
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
