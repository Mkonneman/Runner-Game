using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Two button question over whatever is underneath (the end door, equipping after a buy)
    /// </summary>
    public class PromptScreen : GameScreen
    {
        private readonly string _title, _line;
        private readonly string[] _labels;
        private readonly Action _yes, _no;     // _no null = second button opens the shop and this stays up (the door)
        private readonly bool _runMusic;
        private Texture2D _pixel, _button;
        private Song _music;
        private int _choice;
        private MouseState _lastMouse;
        private static Rectangle Button(int b) => new Rectangle(b == 0 ? 252 : 447, 250, 155, 48);

        public PromptScreen(string title, string line, string yesLabel, string noLabel, Action yes, Action no = null, bool runMusic = false)
        {
            (_title, _line, _labels, _yes, _no, _runMusic) = (title, line, new[] { yesLabel, noLabel }, yes, no, runMusic);
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.3);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");
            if (_runMusic) _music = ScreenManager.Game.Content.Load<Song>("Audio/Music/court_death");
            _lastMouse = VirtualMouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            // Back from the shop, the shop stopped the music so start the run music again
            if (_runMusic && !coveredByOtherScreen && !IsExiting && MediaPlayer.State != MediaState.Playing)
            {
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Volume = Settings.MusicVolume;
                MediaPlayer.Play(_music);
            }
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            if (input.IsNewKeyPress(Keys.Left)) _choice = 0;
            if (input.IsNewKeyPress(Keys.Right)) _choice = 1;
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool moved = mouse.Position != _lastMouse.Position;
            _lastMouse = mouse;
            bool choose = input.IsNewKeyPress(Keys.Enter);
            for (int b = 0; b < 2; b++)
                if (Button(b).Contains(mouse.Position)) { if (moved || click) _choice = b; if (click) choose = true; }
            if (!choose) return;

            if (_choice == 1 && _no == null) { ScreenManager.AddScreen(new ShopScreen()); return; }  // stays so you come back to the question
            ExitScreen();
            (_choice == 0 ? _yes : _no)();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f * TransitionAlpha);
            DrawCentered(_title, 105, Color.OrangeRed, 0.5f, ScreenManager.BigFont);
            DrawCentered(_line, 180, Color.White, 0.55f, ScreenManager.MenuFont);
            for (int b = 0; b < 2; b++)
            {
                var rect = Button(b);
                MenuButton.Draw(spriteBatch, _button, rect, MenuButton.Tint(_choice == b) * TransitionAlpha);
                var size = ScreenManager.MenuFont.MeasureString(_labels[b]) * 0.5f;
                Shadowed(_labels[b], new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2), _choice == b ? Color.Yellow : Color.White, 0.5f, ScreenManager.MenuFont);
            }
            spriteBatch.End();
        }

        private void DrawCentered(string text, float y, Color color, float scale, SpriteFont font)
        {
            float scaleFit = MathF.Min(scale, (ScreenManager.GraphicsDevice.Viewport.Width - 60) / font.MeasureString(text).X);
            var size = font.MeasureString(text) * scaleFit;
            Shadowed(text, new Vector2(ScreenManager.GraphicsDevice.Viewport.Width / 2 - size.X / 2, y), color, scaleFit, font);
        }

        private void Shadowed(string text, Vector2 position, Color color, float scale, SpriteFont font)
        {
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
