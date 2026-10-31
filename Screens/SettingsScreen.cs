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
        private SoundEffect _testSound;   // plays when the sound effect volume changes so you can hear it

        // Rows go music, sound effects, smash ring, then Back
        private static readonly string[] Labels = { "Music", "Sound effects", "Smash ring" };
        private const int RowY = 170;          // y of the first row
        private const int RowSpacing = 55;
        private static readonly Rectangle BackButton = new Rectangle(340, 345, 120, 44);
        private int _selected;
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
            _testSound = ScreenManager.Game.Content.Load<SoundEffect>("Audio/Sfx/crash");

            // So the click that opened the menu doesn't also press something
            _lastMouse = Mouse.GetState();
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Up/Down picks a row, Left/Right changes it
            if (input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W)) _selected = Math.Max(_selected - 1, 0);
            if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _selected = Math.Min(_selected + 1, Labels.Length);
            if (input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A)) Change(_selected, -1);
            if (input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D)) Change(_selected, 1);
            bool back = input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back) || (_selected == Labels.Length && input.IsNewKeyPress(Keys.Enter));

            // Mouse support, hovering a row picks it and clicking the < or > changes it
            var mouse = Mouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            for (int i = 0; i < Labels.Length; i++)
            {
                if (mouse.Position != _lastMouse.Position && RowRectangle(i).Contains(mouse.Position)) _selected = i;
                if (click && ArrowRectangle(i, -1).Contains(mouse.Position)) Change(i, -1);
                if (click && ArrowRectangle(i, 1).Contains(mouse.Position)) Change(i, 1);
            }
            if (mouse.Position != _lastMouse.Position && BackButton.Contains(mouse.Position)) _selected = Labels.Length;
            if (click && BackButton.Contains(mouse.Position)) back = true;
            _lastMouse = mouse;

            if (back) ExitScreen();
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
        }

        // Rounded so repeated 0.1 steps land exactly on 0 and 1
        private static float Step(float value, float amount) => MathHelper.Clamp(MathF.Round((value + amount) * 100) / 100, 0, 1);

        private static float Value(int row) => row == 0 ? Settings.MusicVolume : row == 1 ? Settings.SfxVolume : Settings.SmashRingOpacity;

        // Whole row (for hover) and its < / > buttons
        private static Rectangle RowRectangle(int row) => new Rectangle(210, RowY + row * RowSpacing - 6, 380, 44);
        private static Rectangle ArrowRectangle(int row, int direction) => new Rectangle(direction < 0 ? 420 : 540, RowY + row * RowSpacing - 4, 40, 40);

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var mouse = Mouse.GetState().Position;

            spriteBatch.Begin();

            // Darken the title and put a panel behind the menu
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(190, 90, 420, 320), new Color(10, 20, 30) * 0.9f * TransitionAlpha);
            DrawText("Settings", new Vector2(400 - font.MeasureString("Settings").X * 0.5f, 100), Color.Yellow, 1f);

            for (int i = 0; i < Labels.Length; i++)
            {
                bool selected = i == _selected;
                if (selected) spriteBatch.Draw(_pixel, RowRectangle(i), new Color(30, 90, 60) * 0.85f * TransitionAlpha);
                DrawText(Labels[i], new Vector2(225, RowY + i * RowSpacing), selected ? Color.Yellow : Color.White, 0.6f);

                // < value >
                float value = Value(i);
                string text = i == 2 && value == 0 ? "Off" : $"{value * 100:0}%";
                foreach (int direction in new[] { -1, 1 })
                {
                    var arrow = ArrowRectangle(i, direction);
                    spriteBatch.Draw(_pixel, arrow, (arrow.Contains(mouse) ? new Color(60, 140, 90) : new Color(20, 40, 55)) * TransitionAlpha);
                    DrawText(direction < 0 ? "<" : ">", new Vector2(arrow.X + 12, arrow.Y + 4), Color.White, 0.6f);
                }
                DrawText(text, new Vector2(500 - font.MeasureString(text).X * 0.3f, RowY + i * RowSpacing), Color.Cyan, 0.6f);
            }

            // Back button
            bool backSelected = _selected == Labels.Length;
            spriteBatch.Draw(_pixel, BackButton, (backSelected ? new Color(30, 90, 60) : new Color(20, 40, 55)) * TransitionAlpha);
            DrawText("Back", new Vector2(BackButton.Center.X - font.MeasureString("Back").X * 0.3f, BackButton.Y + 8), backSelected ? Color.Yellow : Color.White, 0.6f);

            spriteBatch.End();
        }

        // Text with a shadow
        private void DrawText(string text, Vector2 position, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
