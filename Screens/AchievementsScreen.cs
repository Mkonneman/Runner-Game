using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Trophy list popup over the title, unlocked ones light up gold with what they paid out
    /// </summary>
    public class AchievementsScreen : GameScreen
    {
        private Texture2D _pixel, _button;
        private MouseState _lastMouse;
        private static readonly Rectangle BackButton = new Rectangle(367, 424, 120, 44);
        private const int RowY = 78, RowH = 26;

        public AchievementsScreen()
        {
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.2);
            TransitionOffTime = TimeSpan.FromSeconds(0.2);
        }

        public override void Activate()
        {
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _button = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/button");
            _lastMouse = VirtualMouse.GetState();
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            var mouse = VirtualMouse.GetState();
            bool click = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            _lastMouse = mouse;
            if (input.IsNewKeyPress(Keys.Escape) || input.IsNewKeyPress(Keys.Enter) || input.IsNewButtonPress(Buttons.Back) || (click && BackButton.Contains(mouse.Position)))
                ExitScreen();
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            float a = TransitionAlpha;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, 854, 480), Color.Black * 0.5f * a);
            spriteBatch.Draw(_pixel, new Rectangle(117, 12, 620, 462), new Color(10, 20, 30) * 0.94f * a);
            string title = $"Achievements  {Achievements.Count}/{Achievements.All.Length}";
            Text(title, new Vector2(427 - ScreenManager.TitleFont.MeasureString(title).X * 0.3f, 20), Color.Yellow, 0.6f, ScreenManager.TitleFont);

            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var award = Achievements.All[i];
                bool got = Achievements.Has(award.Id);
                int y = RowY + i * RowH;
                if (i % 2 == 0) spriteBatch.Draw(_pixel, new Rectangle(127, y - 2, 600, RowH), Color.White * 0.04f * a);
                Achievements.DrawTrophy(spriteBatch, _pixel, new Vector2(138, y), a, got, 0.7f);  // gold or grey
                Text(award.Name, new Vector2(178, y + 2), got ? new Color(255, 210, 70) : Color.Gray, 0.36f);
                Text(award.Desc, new Vector2(345, y + 4), got ? Color.White : Color.Gray * 0.8f, 0.3f);
                string reward = (got ? "Got " : "") + $"{award.Gems}g" + (award.Skin is ItemId s ? $" + {Shop.Get(s).Name}" : "");
                Text(reward, new Vector2(717 - font.MeasureString(reward).X * 0.3f, y + 4), got ? Color.LimeGreen : Color.Cyan * 0.7f, 0.3f);
            }

            bool hover = BackButton.Contains(VirtualMouse.GetState().Position);
            MenuButton.Draw(spriteBatch, _button, BackButton, MenuButton.Tint(hover) * a);
            var size = font.MeasureString("Back") * 0.6f;
            Text("Back", new Vector2(BackButton.Center.X - size.X / 2, BackButton.Center.Y - size.Y / 2), hover ? Color.Yellow : Color.White, 0.6f);
            spriteBatch.End();
        }

        private void Text(string text, Vector2 position, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            var sb = ScreenManager.SpriteBatch;
            sb.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            sb.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
