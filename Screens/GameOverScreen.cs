using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The game over screen (a popup over the frozen game)
    /// </summary>
    public class GameOverScreen : GameScreen
    {
        // The run that just ended (closed along with this screen) and its score
        private readonly GameScreen _gameplay;
        private readonly int _score;

        // 1x1 white pixel for the dark overlay
        private Texture2D _pixel;

        // The two stone buttons side by side (click one or use the arrows and Enter)
        private static readonly string[] Options = { "Play Again", "Main Menu" };
        private Texture2D _button;
        private int _selected;

        // Rock puns for when you die, these go in order so they dont repeat till the list runs out (Might add more later lol)
        private static readonly string[] Puns =
        {
            "You've hit rock bottom.",
            "That run was a little rocky.",
            "Looks like you took that for granite.",
            "Don't take it for granite, try again!",
            "You got stoned. By a rock.",
            "Ouch. That one really rocked you.",
            "Pebble-y should have dodged that.",
            "Sedimentary, my dear runner.",
            "You and that rock just didn't click... it clicked you.",
            "Whole lotta boulder than you expected.",
            "That was a slate-ly bad dodge.",
            "Shale we try that again?",
            "You're making me quarry about you.",
            "Rock and roll? More like rock and fall.",
            "Igneous-ignorance is not bliss.",
        };
        private static int _nextPun;
        private readonly string _pun;
        private MouseState _lastMouse;

        public GameOverScreen(GameScreen gameplay, int score)
        {
            _gameplay = gameplay;
            _score = score;
            _pun = Puns[_nextPun];
            _nextPun = (_nextPun + 1) % Puns.Length;

            // Popup so the crashed game stays visible behind it
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.3);
            TransitionOffTime = TimeSpan.FromSeconds(0.3);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Arrows or WASD move between the buttons
            if (input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A) || input.IsNewKeyPress(Keys.Up) || input.IsNewKeyPress(Keys.W)) _selected = 0;
            if (input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D) || input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _selected = 1;
            bool choose = input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space) || input.IsNewButtonPress(Buttons.Start);

            // Hovering a button selects it, clicking it chooses it
            var mouse = Mouse.GetState();
            for (int i = 0; i < Options.Length; i++)
            {
                if (OptionRectangle(i).Contains(mouse.Position))
                {
                    if (mouse.Position != _lastMouse.Position) _selected = i;
                    if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released)
                    {
                        _selected = i;
                        choose = true;
                    }
                }
            }
            _lastMouse = mouse;

            if (!choose) return;

            // Both close this screen and the run under it (the title is still underneath)
            ExitScreen();
            _gameplay.ExitScreen();
            if (_selected == 0) ScreenManager.AddScreen(new GameplayScreen());
        }

        // Clickable area of a button, both are centered on the screen
        private Rectangle OptionRectangle(int i) => new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width / 2 - 175 + i * 190, 320, 160, 48);

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Darken the game behind so the text stands out
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f * TransitionAlpha);

            DrawCentered("You Died", 80, Color.Red, 1.6f);

            // This deaths pun (shrinks if its too wide for the screen)
            var font = ScreenManager.MenuFont;
            DrawCentered(_pun, 165, Color.White, MathF.Min(0.55f, (viewport.Width - 60) / font.MeasureString(_pun).X));

            DrawCentered($"Score: {_score}", 210, Color.Yellow, 1f);
            DrawCentered($"+{Shop.GemsFromScore(_score)} gems", 258, Color.Cyan, 0.6f);

            // Same stone buttons as the title, the picked one lights up with yellow text
            for (int i = 0; i < Options.Length; i++)
            {
                var rect = OptionRectangle(i);
                bool selected = i == _selected;
                spriteBatch.Draw(_button, rect, (selected ? Color.White : new Color(170, 180, 200)) * TransitionAlpha);
                float scale = MathF.Min(0.65f, (rect.Width - 28) / font.MeasureString(Options[i]).X);
                Vector2 size = font.MeasureString(Options[i]) * scale;
                var position = new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2);
                spriteBatch.DrawString(font, Options[i], position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, Options[i], position, (selected ? Color.Yellow : Color.White) * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        // Draws text centered left to right on the screen (with a shadow so it stands out)
        private void DrawCentered(string text, float y, Color color, float scale)
        {
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
