using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Pause popup over the frozen run (Esc / Start / P), Resume gives a 3 2 1 before it goes again
    /// </summary>
    public class PauseScreen : GameScreen
    {
        private static readonly string[] Options = { "Resume", "Settings", "Main Menu" };
        private readonly GameScreen _run;
        private Texture2D _pixel, _button;
        private int _selected;
        private MouseState _lastMouse;

        public PauseScreen(GameScreen run)
        {
            _run = run;
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.15);
            TransitionOffTime = TimeSpan.FromSeconds(0.15);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");
            _lastMouse = VirtualMouse.GetState();
            Rumble.Stop();
            MediaPlayer.Pause();
        }

        private static Rectangle OptionRectangle(int i) => new Rectangle(Game1.VirtualWidth / 2 - 95, 190 + i * 58, 190, 48);

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            int was = _selected;
            if (input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W)) _selected = Math.Max(0, _selected - 1);
            if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _selected = Math.Min(Options.Length - 1, _selected + 1);
            bool choose = input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space);
            bool resume = input.IsNewKeyPress(Keys.Escape) || input.IsNewKeyPress(Keys.P) || input.IsNewButtonPress(Buttons.Start) || input.IsNewButtonPress(Buttons.B);

            var mouse = VirtualMouse.GetState();
            for (int i = 0; i < Options.Length; i++)
            {
                if (!OptionRectangle(i).Contains(mouse.Position)) continue;
                if (mouse.Position != _lastMouse.Position) _selected = i;
                if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released) { _selected = i; choose = true; }
            }
            _lastMouse = mouse;
            if (_selected != was) Sfx.Play("menu_move", 0.4f, 0f);

            if (resume || (choose && _selected == 0)) { Resume(); return; }
            if (!choose) return;
            Sfx.Play("menu_pick", 0.5f, 0f);
            if (_selected == 1) ScreenManager.AddScreen(new SettingsScreen());
            if (_selected == 2)  // bail on the run, no gems for a quit
            {
                MediaPlayer.Stop();
                ExitScreen();
                _run.ExitScreen();
            }
        }

        private void Resume()
        {
            Sfx.Play("menu_pick", 0.5f, 0f);
            RunStats.Countdown = 3f;
            MediaPlayer.Resume();
            ExitScreen();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.TitleFont;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.75f * TransitionAlpha);  // dims the run way down so the menu reads
            DrawCentered("Paused", 110, Color.Yellow, 1.1f, ScreenManager.BigFont);
            for (int i = 0; i < Options.Length; i++)
            {
                var rect = OptionRectangle(i);
                bool selected = i == _selected;
                MenuButton.Draw(spriteBatch, _button, rect, MenuButton.Tint(selected) * TransitionAlpha);
                float scale = MathF.Min(0.65f, (rect.Width - 28) / font.MeasureString(Options[i]).X);
                var size = font.MeasureString(Options[i]) * scale;
                var pos = new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2);
                spriteBatch.DrawString(font, Options[i], MenuButton.TextPos(pos) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(pos, scale), SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, Options[i], MenuButton.TextPos(pos), (selected ? Color.Yellow : Color.White) * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(pos, scale), SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        private void DrawCentered(string text, float y, Color color, float scale, SpriteFont font)
        {
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2((Game1.VirtualWidth - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, pos + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, pos, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
