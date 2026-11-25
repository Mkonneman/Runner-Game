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
        private readonly Action _third;        // optional small button under the two (Don't show again)
        private readonly bool _runMusic;
        private Texture2D _pixel, _button;
        private Song _music;
        private int _choice;
        private MouseState _lastMouse;
        private bool Door => _no == null;  // the end door: Shop first and Gear open screens and come back here
        private Rectangle Button(int b) => b == 2 ? (Door ? new Rectangle(350, 316, 155, 48) : new Rectangle(327, 316, 200, 36)) : new Rectangle(b == 0 ? 252 : 447, 250, 155, 48);
        private int ButtonCount => _third != null ? 3 : 2;

        public PromptScreen(string title, string line, string yesLabel, string noLabel, Action yes, Action no = null, bool runMusic = false, string thirdLabel = null, Action third = null)
        {
            (_title, _line, _labels, _yes, _no, _runMusic, _third) = (title, line, new[] { yesLabel, noLabel, thirdLabel }, yes, no, runMusic, third);
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.3);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");
            if (_runMusic) _music = ScreenManager.Game.Content.Load<Song>(GameplayScreen.CaveSong(CaveZones.Count - 1));  // the door is at the end of the last cave
            _lastMouse = VirtualMouse.GetState();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            // Back from the shop, the shop stopped the music so start the run music again
            if (_runMusic && !coveredByOtherScreen && !IsExiting) Music.Play(_music, 1.5f);
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            int was = _choice;
            if (input.IsNewKeyPress(Keys.Left)) _choice = 0;
            if (input.IsNewKeyPress(Keys.Right)) _choice = 1;
            if (input.IsNewKeyPress(Keys.Down) && _third != null) _choice = 2;
            if (input.IsNewKeyPress(Keys.Up) && _choice == 2) _choice = 0;
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool moved = mouse.Position != _lastMouse.Position;
            _lastMouse = mouse;
            bool choose = input.IsNewKeyPress(Keys.Enter);
            for (int b = 0; b < ButtonCount; b++)
                if (Button(b).Contains(mouse.Position)) { if (moved || click) _choice = b; if (click) choose = true; }
            if (_choice != was) Sfx.UiMove();
            if (!choose) return;

            if (_choice == 0 || _no == null) Sfx.UiPick(); else Sfx.UiBack();
            if (_choice >= 1 && Door) { ScreenManager.AddScreen(_choice == 1 ? new ShopScreen() : new GearScreen()); return; }  // stays so you come back to the question
            ExitScreen();
            (_choice == 0 ? _yes : _choice == 1 ? _no : _third)();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f * TransitionAlpha);
            DrawCentered(_title, 105, Color.OrangeRed, 0.5f, ScreenManager.BigFont);
            DrawCentered(_line, 180, Color.White, 0.55f, ScreenManager.MenuFont);
            for (int b = 0; b < ButtonCount; b++)
            {
                var rect = Button(b);
                bool quiet = b == 2 && !Door;  // the third one is small and quiet (except the door's Gear)
                float scale = quiet ? 0.38f : 0.5f;
                MenuButton.Draw(spriteBatch, _button, rect, MenuButton.Tint(_choice == b) * (quiet && _choice != 2 ? 0.7f : 1f) * TransitionAlpha);
                var size = ScreenManager.MenuFont.MeasureString(_labels[b]) * scale;
                Shadowed(_labels[b], new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2), _choice == b ? Color.Yellow : quiet ? Color.LightGray : Color.White, scale, ScreenManager.MenuFont);
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
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
