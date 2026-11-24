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
        private static readonly Rectangle BackButton = new Rectangle(287, 424, 120, 44);
        private static readonly Rectangle StatsButton = new Rectangle(427, 424, 140, 44);  // flips between trophies and lifetime stats
        private bool _stats;
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
            if (mouse.Position != _lastMouse.Position) _pick = BackButton.Contains(mouse.Position) ? 0 : StatsButton.Contains(mouse.Position) ? 1 : _pick;
            _lastMouse = mouse;
            // Left / Right (d-pad, stick) picks Back or Stats, Enter / A presses the picked one
            int was = _pick;
            if (input.IsNewKeyPress(Keys.Left)) _pick = 0;
            if (input.IsNewKeyPress(Keys.Right)) _pick = 1;
            if (_pick != was) Sfx.Play("menu_move", 0.4f, 0f);
            bool press = input.IsNewKeyPress(Keys.Enter);
            if (input.IsNewKeyPress(Keys.Tab) || input.IsNewKeyPress(Keys.Q) || input.IsNewKeyPress(Keys.E) || input.IsNewButtonPress(Buttons.LeftShoulder) || input.IsNewButtonPress(Buttons.RightShoulder) || (click && StatsButton.Contains(mouse.Position)) || (press && _pick == 1))
            { _stats = !_stats; Sfx.UiPick(); }
            else if (input.IsNewKeyPress(Keys.Escape) || input.IsNewButtonPress(Buttons.Back) || (click && BackButton.Contains(mouse.Position)) || (press && _pick == 0))
                { Sfx.UiBack(); ExitScreen(); }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            float a = TransitionAlpha;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, 854, 480), Color.Black * 0.5f * a);
            spriteBatch.Draw(_pixel, new Rectangle(117, 12, 620, 462), new Color(10, 20, 30) * 0.94f * a);
            string title = _stats ? "Lifetime Stats" : $"Achievements  {Achievements.Count}/{Achievements.All.Length}";
            Text(title, new Vector2(427 - ScreenManager.TitleFont.MeasureString(title).X * 0.3f, 20), Color.Yellow, 0.6f, ScreenManager.TitleFont);

            if (_stats) DrawStats(spriteBatch, a);
            else for (int i = 0; i < Achievements.All.Length; i++)
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

            Button(BackButton, "Back", _pick == 0);
            Button(StatsButton, _stats ? "Trophies" : "Stats", _pick == 1);
            spriteBatch.End();
        }

        private int _pick;  // 0 = Back, 1 = Stats
        private void Button(Rectangle rect, string label, bool picked)
        {
            bool hover = rect.Contains(VirtualMouse.GetState().Position) || picked;
            MenuButton.Draw(ScreenManager.SpriteBatch, _button, rect, MenuButton.Tint(hover) * TransitionAlpha);
            var size = ScreenManager.MenuFont.MeasureString(label) * 0.6f;
            Text(label, new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 2), hover ? Color.Yellow : Color.White, 0.6f);
        }

        // Lifetime totals in crystal tiles, 4 across 2 down
        private void DrawStats(SpriteBatch spriteBatch, float a)
        {
            int seconds = Achievements.Stat("seconds");
            (string Value, string Label, Color Color)[] tiles =
            {
                (Achievements.Stat("runs").ToString(), "runs", Color.White),
                ($"{Achievements.Stat("meters")}m", "meters run", new Color(120, 200, 255)),
                ($"{seconds / 3600}:{seconds / 60 % 60:00}", "time played", new Color(200, 200, 220)),
                (Achievements.Stat("golems").ToString(), "golems slain", new Color(170, 90, 255)),
                (Achievements.Stat("walls").ToString(), "walls smashed", new Color(130, 150, 230)),
                (Achievements.Stat("rocks").ToString(), "rocks smashed", new Color(210, 180, 140)),
                (Achievements.Stat("diamonds").ToString(), "diamonds", new Color(120, 240, 230)),
                (Achievements.Stat("gems_earned").ToString(), "gems earned", Color.Cyan),
            };
            const int TileW = 136, TileH = 96, Gap = 12;
            int startX = 427 - (4 * TileW + 3 * Gap) / 2;
            var big = ScreenManager.BigFont; var font = ScreenManager.MenuFont;
            for (int i = 0; i < tiles.Length; i++)
            {
                var (value, label, color) = tiles[i];
                var tile = new Rectangle(startX + i % 4 * (TileW + Gap), 110 + i / 4 * (TileH + 30), TileW, TileH);
                Hud.Frame(spriteBatch, tile, 14, Color.White * a);
                Hud.Glow(spriteBatch, new Vector2(tile.Center.X, tile.Y + 38), new Vector2(TileW - 30, 40), color * 0.25f * a);
                float valueScale = MathF.Min(0.7f, (TileW - 40) / big.MeasureString(value).X);
                var valueSize = big.MeasureString(value) * valueScale;
                var at = new Vector2(tile.Center.X - valueSize.X / 2, tile.Y + 40 - valueSize.Y / 2);
                spriteBatch.DrawString(big, value, at + new Vector2(2), Color.Black * a, 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(big, value, at, color * a, 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);
                float labelScale = MathF.Min(0.3f, (TileW - 30) / font.MeasureString(label).X);
                var labelSize = font.MeasureString(label) * labelScale;
                spriteBatch.DrawString(font, label, new Vector2(tile.Center.X - labelSize.X / 2, tile.Bottom - labelSize.Y - 16), Color.LightGray * a, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
            }
            string hint = "Tab / LB / RB to flip";
            var hintSize = font.MeasureString(hint) * 0.3f;
            spriteBatch.DrawString(font, hint, new Vector2(427 - hintSize.X / 2, 380), Color.Gray * a, 0f, Vector2.Zero, 0.3f, SpriteEffects.None, 0f);
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
