using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// How to play popup over the title, shows the controls and rules
    /// </summary>
    public class HowToPlayScreen : GameScreen
    {
        private Texture2D _pixel;
        private MouseState _lastMouse;

        // Key on the left, what it does on the right
        private static readonly (string Key, string Action)[] Lines =
        {
            ("A / D  or  Left / Right", "Move"),
            ("Space / W / Up", "Jump over spikes"),
            ("Shift", "Dash"),
            ("S / Down", "Smash walls and rocks"),
            ("H", "Heal (with the Healing upgrade)"),
            ("", ""),
            ("Walls", "Kill you, smash them!"),
            ("Spikes / Rocks", "Hurt you"),
            ("Smashing", "Points, which turn into gems"),
            ("Gems", "Buy upgrades in the Shop"),
        };

        private static readonly Rectangle BackButton = new Rectangle(340, 400, 120, 44);

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

            // So the click that opened it doesn't also press Back
            _lastMouse = Mouse.GetState();
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Esc, Enter or clicking Back closes it
            var mouse = Mouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            _lastMouse = mouse;
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewKeyPress(Keys.Enter) || input.IsNewButtonPress(Buttons.Back) || (click && BackButton.Contains(mouse.Position)))
            {
                ExitScreen();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Darken the title and put a panel behind the text
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(120, 30, 560, 430), new Color(10, 20, 30) * 0.92f * TransitionAlpha);
            DrawText("How to Play", new Vector2(400 - font.MeasureString("How to Play").X * 0.45f, 40), Color.Yellow, 0.9f);

            for (int i = 0; i < Lines.Length; i++)
            {
                float y = 100 + i * 29;
                DrawText(Lines[i].Key, new Vector2(150, y), Color.Cyan, 0.45f);
                DrawText(Lines[i].Action, new Vector2(390, y), Color.White, 0.45f);
            }

            // Back button
            bool hover = BackButton.Contains(Mouse.GetState().Position);
            spriteBatch.Draw(_pixel, BackButton, (hover ? new Color(30, 90, 60) : new Color(20, 40, 55)) * TransitionAlpha);
            DrawText("Back", new Vector2(BackButton.Center.X - font.MeasureString("Back").X * 0.3f, BackButton.Y + 8), hover ? Color.Yellow : Color.White, 0.6f);

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
