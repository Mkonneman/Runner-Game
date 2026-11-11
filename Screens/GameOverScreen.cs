using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// What ended the run, picks which pun list the death screen uses
    /// </summary>
    public enum DeathCause { Rock, Spike, Wall, Laser, Hole, Door }  // Door = made it to the end (not really a death lol)

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
        private static readonly string[] Options = { "Play Again", "Shop", "Main Menu" };
        private Texture2D _button;
        private Texture2D _shopIcon;
        private int _selected;

        // Rock puns for rocks (and anything without its own list), random every death (Might add more later lol)
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

        // Walls get the most since you'll be seeing these a lot (oooh the wall roasts)
        private static readonly string[] WallPuns =
        {
            "You hit a wall. Literally.",
            "Brick-ed it.",
            "The wall wins this round.",
            "Wall: {walls}, You: 0",  // {walls} = how many times a wall got you
            "Pro tip: walls don't move. You do.",
            "You stone-walled yourself.",
            "That wall had your name on it. Now it has your face.",
            "Mortar-fied.",
            "Another one bites the wall.",
            "Hey, at least the wall is okay.",
            "The smash button misses you.",
            "You and the wall are on a first face basis now.",
            "Bold strategy, running face first.",
            "That wall is going to tell its friends about you.",
        };

        private static readonly string[] LaserPuns =
        {
            "You got zapped.",
            "Laser focused... on you.",
            "Shocking, really.",
            "That red line was a hint.",
            "Well, that was a beam-er.",
        };

        private static readonly string[] DoorPuns =
        {
            "Something big is waiting behind that door...",
            "The boss is still getting ready. Come back soon!",
            "You rocked it.",
        };

        private static readonly string[] NoWeaponPuns =
        {
            "Forgot your weapon? That's a rock-ie mistake.",
            "Bare hands vs a cave. The cave won.",
            "Next time pack something heavier than hope.",
            "No weapon equipped. Bold. Very bold.",
        };

        private static readonly string[] HolePuns =
        {
            "You fell for it.",
            "Hole-y moly.",
            "Mind the gap.",
            "That's one deep dive.",
            "Gone, but not floor-gotten.",
        };

        private static readonly string[] SpikePuns =
        {
            "Get the point?",
            "Crystal clear mistake.",
            "That was a sharp decision.",
        };
        public static int WallDeaths;     // every death to a wall ever (saved)
        private static string _lastPun;  // so the same pun never shows twice in a row
        private readonly string _pun;
        private MouseState _lastMouse;

        private readonly bool _cleared;

        public GameOverScreen(GameScreen gameplay, int score, DeathCause cause)
        {
            _cleared = cause == DeathCause.Door;
            _gameplay = gameplay;
            _score = score;

            // Random pun picked from whatever killed you (rerolls if it's the one you just saw)
            var puns = cause != DeathCause.Door && Shop.Equipped(Slot.Weapon) == null ? NoWeaponPuns : cause switch
            {
                DeathCause.Wall => WallPuns,
                DeathCause.Laser => LaserPuns,
                DeathCause.Hole => HolePuns,
                DeathCause.Spike => SpikePuns,
                DeathCause.Door => DoorPuns,
                _ => Puns,
            };
            string pick;
            do pick = puns[RandomHelper.Next(0, puns.Length - 1)]; while (pick == _lastPun);
            _lastPun = pick;
            if (cause == DeathCause.Wall) { WallDeaths++; SaveSystem.Save(); }
            Achievements.Unlock("first_run");
            if (WallDeaths >= 10) Achievements.Unlock("walls_died_10");
            _pun = pick.Replace("{walls}", WallDeaths.ToString());
            if (score == 67 || Shop.GemsFromScore(score) == 67) _pun = "6... 7!";  // the meme beats any other pun

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
            _shopIcon = ScreenManager.Game.Content.Load<Texture2D>("Images/Menu/shop_icon");
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            // Arrows or WASD move between the buttons
            if (input.IsNewKeyPress(Keys.Left) || input.IsNewKeyPress(Keys.A)) _selected = Math.Max(_selected - 1, 0);
            if (input.IsNewKeyPress(Keys.Right) || input.IsNewKeyPress(Keys.D)) _selected = Math.Min(_selected + 1, Options.Length - 1);
            bool choose = input.IsNewKeyPress(Keys.Enter) || input.IsNewKeyPress(Keys.Space) || input.IsNewButtonPress(Buttons.Start);

            // Hovering a button selects it, clicking it chooses it
            var mouse = VirtualMouse.GetState();
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
            if (_selected == 0) ScreenManager.AddScreen(TitleScreen.NewRun());
            if (_selected == 1) ScreenManager.AddScreen(new ShopScreen());  // spend those gems right away, Back drops you on the title
        }

        // Clickable area of a button, both are centered on the screen
        private Rectangle OptionRectangle(int i) => new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width / 2 - 255 + i * 175, 320, 160, 48);  // 3 buttons centered

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Darken the game behind so the text stands out
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f * TransitionAlpha);

            if (_cleared) DrawCentered("Cave Cleared!", 80, Color.LimeGreen, 1.3f, ScreenManager.BigFont);
            else DrawCentered("You Died", 80, Color.Red, 1.6f, ScreenManager.BigFont);

            // This deaths pun (shrinks if its too wide for the screen)
            var font = ScreenManager.MenuFont;
            DrawCentered(_pun, 165, Color.White, MathF.Min(0.55f, (viewport.Width - 60) / font.MeasureString(_pun).X));

            DrawCentered($"Score: {_score}", 210, Color.Yellow, 0.9f, ScreenManager.BigFont);
            DrawCentered($"+{Shop.GemsFromScore(_score)} gems", 258, Color.Cyan, 0.6f);

            // Same stone buttons as the title, the picked one lights up with yellow text
            for (int i = 0; i < Options.Length; i++)
            {
                var rect = OptionRectangle(i);
                var buttonFont = ScreenManager.TitleFont;
                bool selected = i == _selected;
                spriteBatch.Draw(_button, MenuButton.Squash(rect, selected), (selected ? Color.White : new Color(170, 180, 200)) * TransitionAlpha);
                float scale = MathF.Min(0.65f, (rect.Width - 28) / buttonFont.MeasureString(Options[i]).X);
                Vector2 size = buttonFont.MeasureString(Options[i]) * scale;
                // Shop gets the gem pouch on the left and its text nudged over to make room
                bool hasIcon = Options[i] == "Shop";
                if (hasIcon) ScreenManager.SpriteBatch.Draw(_shopIcon, MenuButton.Inside(new Rectangle(rect.X + 14, rect.Center.Y - _shopIcon.Height / 2, _shopIcon.Width, _shopIcon.Height)), Color.White * TransitionAlpha);
                var position = new Vector2(rect.Center.X - size.X / 2 + (hasIcon ? 20 : 0), rect.Center.Y - size.Y / 2);
                spriteBatch.DrawString(buttonFont, Options[i], MenuButton.TextPos(position) + new Vector2(2), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
                spriteBatch.DrawString(buttonFont, Options[i], MenuButton.TextPos(position), (selected ? Color.Yellow : Color.White) * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        // Draws text centered left to right on the screen (with a shadow so it stands out)
        private void DrawCentered(string text, float y, Color color, float scale, SpriteFont font = null)
        {
            font ??= ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            Vector2 size = font.MeasureString(text) * scale;
            Vector2 position = new Vector2((viewport.Width - size.X) / 2, y);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position) + new Vector2(3), Color.Black * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
            ScreenManager.SpriteBatch.DrawString(font, text, MenuButton.TextPos(position), color * TransitionAlpha, 0f, Vector2.Zero, MenuButton.TextScale(position, scale), SpriteEffects.None, 0f);
        }
    }
}
