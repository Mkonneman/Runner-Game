using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Controls popup from Settings, pick an action and press a new key for it
    /// </summary>
    public class KeybindsScreen : GameScreen
    {
        private Texture2D _pixel;
        private Texture2D _button;
        private MouseState _lastMouse;

        private static readonly string[] Names = { "Move left", "Move right", "Jump", "Ability", "Smash" };  // Heal moved to the Heal Locket ability
        private static readonly string[] PadHints = { "D-pad / stick", "D-pad / stick", "A", "X / LT", "B / RT" };
        private const int RowY = 130;
        private const int RowSpacing = 38;
        private static readonly Rectangle DefaultsButton = new Rectangle(257, 380, 160, 40);
        private static readonly Rectangle BackButton = new Rectangle(437, 380, 160, 40);
        private int DefaultsRow => Names.Length;
        private int BackRow => Names.Length + 1;
        private int _selected;
        private bool _waiting;   // true while it's waiting for you to press the new key

        public KeybindsScreen()
        {
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.2);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");  // same stone button as the title
            _lastMouse = VirtualMouse.GetState();
        }

        private static Rectangle RowRectangle(int row) => new Rectangle(197, RowY + row * RowSpacing - 4, 460, 34);

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            bool moved = mouse.Position != _lastMouse.Position;
            _lastMouse = mouse;

            // Waiting for a key, the first new key pressed becomes the bind (Esc cancels)
            if (_waiting)
            {
                var pressed = input.CurrentKeyboardState.GetPressedKeys().FirstOrDefault(k => input.LastKeyboardState.IsKeyUp(k));
                if (input.IsNewKeyPress(Keys.Escape) || click) _waiting = false;
                else if (pressed != Keys.None && pressed != Keys.F11)
                {
                    Controls.Bind((PlayerAction)_selected, pressed);
                    _waiting = false;
                }
                return;
            }

            // Defaults and Back sit side by side, so Left/Right hops between them and Up goes back to the list
            bool bottom = _selected >= DefaultsRow;
            if (input.IsNewKeyPress(Keys.Up)) _selected = bottom ? Names.Length - 1 : Math.Max(_selected - 1, 0);
            else if (input.IsNewKeyPress(Keys.Down) && !bottom) _selected++;
            if (bottom && input.IsNewKeyPress(Keys.Left)) _selected = DefaultsRow;
            if (bottom && input.IsNewKeyPress(Keys.Right)) _selected = BackRow;
            bool choose = input.IsNewKeyPress(Keys.Enter);
            bool back = input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back);

            for (int i = 0; i < Names.Length; i++)
            {
                if (!RowRectangle(i).Contains(mouse.Position)) continue;
                if (moved) _selected = i;
                if (click) { _selected = i; choose = true; }
            }
            if (DefaultsButton.Contains(mouse.Position)) { if (moved) _selected = DefaultsRow; if (click) { _selected = DefaultsRow; choose = true; } }
            if (BackButton.Contains(mouse.Position)) { if (moved) _selected = BackRow; if (click) { _selected = BackRow; choose = true; } }

            if (choose)
            {
                if (_selected < Names.Length) _waiting = true;
                else if (_selected == DefaultsRow) Controls.ResetToDefaults();
                else back = true;
            }

            if (back)
            {
                SaveSystem.Save();  // binds stick around next time
                ExitScreen();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(177, 30, 500, 410), new Color(10, 20, 30) * 0.95f * TransitionAlpha);
            DrawText("Controls", new Vector2(427 - ScreenManager.TitleFont.MeasureString("Controls").X * 0.4f, 40), Color.Yellow, 0.8f, ScreenManager.TitleFont);
            DrawText("Key", new Vector2(390, RowY - 30), Color.Gray, 0.4f);
            DrawText("Controller", new Vector2(520, RowY - 30), Color.Gray, 0.4f);

            for (int i = 0; i < Names.Length; i++)
            {
                bool selected = i == _selected;
                if (selected) spriteBatch.Draw(_pixel, RowRectangle(i), (_waiting ? new Color(110, 80, 20) : new Color(30, 90, 60)) * 0.85f * TransitionAlpha);
                float y = RowY + i * RowSpacing;
                DrawText(Names[i], new Vector2(210, y), selected ? Color.Yellow : Color.White, 0.5f);
                string key = selected && _waiting ? "Press a key..." : Controls.KeyName(Controls.Bindings[i]);
                DrawText(key, new Vector2(390, y), Color.Cyan, 0.5f);
                DrawText(PadHints[i], new Vector2(520, y), Color.LightGray, 0.4f);
            }

            DrawButton(DefaultsButton, "Defaults", _selected == DefaultsRow);
            DrawButton(BackButton, "Back", _selected == BackRow);
            spriteBatch.End();
        }

        private void DrawButton(Rectangle button, string label, bool selected)
        {
            MenuButton.Draw(ScreenManager.SpriteBatch, _button, button, MenuButton.Tint(selected) * TransitionAlpha);
            var size = ScreenManager.MenuFont.MeasureString(label) * 0.5f;
            DrawText(label, new Vector2(button.Center.X - size.X / 2, button.Center.Y - size.Y / 2), selected ? Color.Yellow : Color.White, 0.5f);
        }

        // Text with a shadow
        private void DrawText(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            ScreenManager.SpriteBatch.DrawString(font, text, position + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, position, color * TransitionAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
