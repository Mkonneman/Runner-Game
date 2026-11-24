using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// What ended the run, picks which pun list the death screen uses
    /// </summary>
    public enum DeathCause { Rock, Spike, Wall, Laser, Hole, Door, Crystal, Icicle, Vine, Mushroom, Golem }  // Door = made it to the end (not really a death lol)

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

        // Rock puns for rocks (and anything without its own list), random every death
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
            "Just a little mineral damage.",
            "Ore you okay?",
            "Gneiss try. Really.",
            "Rock, paper, scissors. Rock won.",
            "Stone cold defeat.",
            "Pumice-ed yourself you'd dodge that one.",
            "You're on the rocks now.",
            "Rocks: 1, Skull: 0.",
            "The cave rocked your world.",
            "Pebble-ieve in yourself next time.",
            "Boulder-dash? More like boulder-crash.",
        };

        // Walls get the most since you'll be seeing these a lot (oooh the wall roasts)
        private static readonly string[] WallPuns =
        {
            "You hit a wall. Literally.",
            "Brick-ed it.",
            "The wall wins this round.",
            "Wall: {walls}, You: 0",
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
            "Wall-come back anytime.",
            "Didn't see that coming? It was a wall.",
            "You're up against the wall now.",
            "Running into problems. Literally.",
            "Splat. A classic.",
            "That wall was load-bearing. So was your face.",
            "Your face just got a new wall-paper.",
            "Wall of shame: you're on it.",
        };

        // Lasers in the caves and the golem's eye beam
        private static readonly string[] LaserPuns =
        {
            "You got zapped.",
            "Laser focused... on you.",
            "Shocking, really.",
            "That red line was a hint.",
            "Well, that was a beam-er.",
            "Fried and true.",
            "You got lit up.",
            "Photon finish.",
            "Red means stop.",
            "Light work. For the laser.",
            "Tanning bed: maximum setting.",
            "That's what you get for stealing the spotlight.",
            "Ray-ted zero out of ten.",
            "Beam me up... oh, it did.",
        };

        // Beat the golem (not really a death lol)
        private static readonly string[] DoorPuns =
        {
            "You rocked it.",
            "That golem is gravel now.",
            "Stone cold victory.",
            "You crushed it. And him.",
            "Absolutely boulder.",
            "Ore-some work.",
            "Rock solid run.",
            "He's not a golem anymore. He's a pile.",
            "Cave cleared. Rock on.",
            "Golem? More like go-lame.",
            "Gem-erous amounts of skill.",
            "You made that boss look like a pebble.",
        };

        // No weapon on beats every other list
        private static readonly string[] NoWeaponPuns =
        {
            "Forgot your weapon? That's a rock-ie mistake.",
            "Bare hands vs a cave. The cave won.",
            "Next time pack something heavier than hope.",
            "No weapon equipped. Bold. Very bold.",
            "You brought a runner to a rock fight.",
            "Weapon slot: empty. Like your health bar.",
            "The shop sells weapons, just saying.",
            "Swing by the shop. Then swing something.",
            "Fists are a weapon. Not a good one.",
        };

        // Fell in a gap
        private static readonly string[] HolePuns =
        {
            "You fell for it.",
            "Hole-y moly.",
            "Mind the gap.",
            "That's one deep dive.",
            "Gone, but not floor-gotten.",
            "Floor not found.",
            "Gravity: still undefeated.",
            "That's the pits.",
            "Down the rabbit hole.",
            "Into the abyss. It says hi.",
            "Bottomless pit. Bottomless regret.",
            "Gap-ped out.",
            "Well well well. Mostly well.",
            "You've fallen and you can't get up.",
            "Ground control to runner...",
            "Jumped to conclusions. Not across the gap.",
        };

        // Floor spikes
        private static readonly string[] SpikePuns =
        {
            "Get the point?",
            "Crystal clear mistake.",
            "That was a sharp decision.",
            "That's a pointed reminder.",
            "Spiked your chances.",
            "Stuck the landing. On a spike.",
            "On the cutting edge of failure.",
            "Pointless. Well, one point.",
            "You've been sharp-ened.",
            "Spikes 101: don't.",
            "Looks like you took that spike personally.",
        };

        // Gem spikes hanging off the ceiling
        private static readonly string[] CrystalPuns =
        {
            "Crystal clear: look up.",
            "Geode-bye.",
            "Shard luck.",
            "That gem was a cut above.",
            "Hard as diamonds. You aren't.",
            "What an ame-thyst-ake.",
            "Quartz of a second too slow.",
            "Facet-ious way to go.",
            "Some gems are better left un-touched.",
            "Hanging around is their whole thing. Not yours.",
            "You got gem-med up.",
            "Precious stone. Precious little health left.",
        };

        // Blue cave icicles
        private static readonly string[] IciclePuns =
        {
            "Ice to meet you.",
            "Chill out.",
            "Frozen in your tracks.",
            "Snow way you saw that coming.",
            "You got iced.",
            "That's an ice-ident.",
            "Icy what happened there.",
            "Freeze! ...too late.",
            "Hail no.",
            "Water you doing under there?",
            "The ceiling dropped a cold one on you.",
            "Should've looked up. Icicle you later.",
            "Cold. Ice cold.",
            "Brrr-illiant timing.",
        };

        // Died in a vine's grip (or just after it let go)
        private static readonly string[] VinePuns =
        {
            "Tangled up.",
            "You got a little too attached.",
            "Clingy much?",
            "Leaf me alone!",
            "Rooted to the spot.",
            "That's your vine-al answer?",
            "Stuck in a toxic relationship. With a plant.",
            "Hanging by a thread. Well, a vine.",
            "Plant-ly put, you're done.",
            "The grape escape didn't work out.",
            "The plant won. Let that sink in.",
            "Caught in the moss-t awkward spot.",
        };

        // Died mid mushroom launch
        private static readonly string[] MushroomPuns =
        {
            "Fun-gi, terrible landing.",
            "There wasn't mush-room for error.",
            "Lots of mushroom for improvement.",
            "Don't be a spore loser.",
            "Bounced right out of the run.",
            "Morel of the story: watch where you land.",
            "That launch was mush too much.",
            "Shroom with a view. Briefly.",
            "High hopes, low landing.",
            "Cap-tain, we're going down.",
            "Spore-adic jumping.",
        };

        // Golem punches, stomps and thrown rocks
        private static readonly string[] GolemPuns =
        {
            "Golem-ed over.",
            "Big rock, bigger problem.",
            "That boss was hard. Like, granite hard.",
            "Crushed it. You were the 'it'.",
            "Pro tip: he punches back.",
            "Bossed around.",
            "You got stomped.",
            "He'll be here when you get back. He's a rock.",
            "The golem says gg.",
            "A fist-ful of regret.",
            "That's a knockout.",
            "Rock solid beatdown.",
            "The bigger they are, the harder they punch.",
            "Hit the ground running? You hit the ground.",
        };

        // Every so often the pun is about the weapon you had on instead
        private static readonly Dictionary<ItemId, string[]> WeaponPuns = new()
        {
            [ItemId.Hammer] = new[] { "Hammer time is over.", "You nailed it. It was you.", "Should've hammered that home.", "Hit the nail on the head. Your head." },
            [ItemId.Pickaxe] = new[] { "You should've picked a better line.", "Pick yourself up and try again.", "Picky, picky. Just not quick enough.", "Mining your own business didn't work." },
            [ItemId.QuakeHammer] = new[] { "You shook the cave. The cave shook back.", "That's what I call a ground-breaking failure.", "Rumble? More like crumble.", "Your run was on shaky ground." },
            [ItemId.BoomerangFlail] = new[] { "What goes around comes around.", "It came back. You didn't.", "Flail-ure is part of the process.", "Threw it all away. Then it came back." },
            [ItemId.Drill] = new[] { "Drill-ed it. Not in a good way.", "You know the drill. Try again.", "Bore-ing ending.", "That run went down the drain. Er, drill." },
        };
        public static int WallDeaths;     // every death to a wall ever (saved)
        private static string _lastPun;  // so the same pun never shows twice in a row
        private readonly string _pun;
        private MouseState _lastMouse;

        private readonly bool _cleared;

        // Best score and the gem count that ticks up
        private readonly bool _newBest;
        private readonly int _gems;
        private float _age, _tickWait;
        private int _gemsShown;
        private readonly (string Value, string Label, Color Color)[] _stats;

        public GameOverScreen(GameScreen gameplay, int score, DeathCause cause)
        {
            _cleared = cause == DeathCause.Door;
            _gameplay = gameplay;
            _score = score;
            _gems = RunStats.GemsFor(score);
            _newBest = score > RunStats.Best;
            if (_newBest) RunStats.Best = score;
            RunStats.Record(score, (int)(RunStats.Distance / 32));
            SaveSystem.Save();
            int seconds = (int)RunStats.Time;
            _stats = new[]
            {
                ($"{RunStats.Distance / 32:0}m", "distance", new Color(120, 200, 255)),
                ($"{seconds / 60}:{seconds % 60:00}", "time", Color.White),
                (RunStats.Walls.ToString(), "walls", new Color(110, 140, 220)),
                (RunStats.Rocks.ToString(), "rocks", new Color(200, 170, 130)),
                (RunStats.Gems.ToString(), "gems smashed", new Color(110, 240, 230)),
                ($"x{RunStats.BestMultiplier:0.0}", "best combo", Color.Orange),
            };

            // Random pun picked from whatever killed you (rerolls if it's the one you just saw)
            var puns = cause != DeathCause.Door && Shop.Equipped(Slot.Weapon) == null ? NoWeaponPuns : cause switch
            {
                DeathCause.Wall => WallPuns,
                DeathCause.Laser => LaserPuns,
                DeathCause.Hole => HolePuns,
                DeathCause.Spike => SpikePuns,
                DeathCause.Door => DoorPuns,
                DeathCause.Crystal => CrystalPuns,
                DeathCause.Icicle => IciclePuns,
                DeathCause.Vine => VinePuns,
                DeathCause.Mushroom => MushroomPuns,
                DeathCause.Golem => GolemPuns,
                _ => Puns,
            };
            if (cause != DeathCause.Door && Shop.Equipped(Slot.Weapon) is ItemId weapon && WeaponPuns.TryGetValue(weapon, out var weaponPuns) && RandomHelper.Next(0, 3) == 0) puns = weaponPuns;
            string pick;
            do pick = puns[RandomHelper.Next(0, puns.Length - 1)]; while (pick == _lastPun);
            _lastPun = pick;
            if (cause == DeathCause.Wall) { WallDeaths++; SaveSystem.Save(); }
            Achievements.Unlock("first_run");
            Achievements.Add("runs"); Achievements.Add("meters", (int)(RunStats.Distance / 32)); Achievements.Add("seconds", (int)RunStats.Time); Achievements.Add("gems_earned", _gems);  // lifetime stats
            if (WallDeaths >= 10) Achievements.Unlock("walls_died_10");
            _pun = pick.Replace("{walls}", WallDeaths.ToString());
            if (score == 67 || _gems == 67) _pun = "6... 7!";  // the meme beats any other pun

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

        // Gems count up from 0 with little ticks
        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _age += dt;
            int target = (int)(_gems * MathHelper.Clamp((_age - 0.5f) / 1.2f, 0, 1));
            if (target > _gemsShown && (_tickWait -= dt) <= 0)
            {
                _tickWait = 0.05f;
                Sfx.Play("gem", 0.15f, 0.05f);
            }
            _gemsShown = target;
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            int was = _selected;
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
            if (_selected != was) Sfx.Play("menu_move", 0.4f, 0f);

            if (!choose) return;
            Sfx.Play("menu_pick", 0.5f, 0f);

            // Both close this screen and the run under it (the title is still underneath)
            ExitScreen();
            _gameplay.ExitScreen();
            if (_selected == 0) { ScreenManager.AddScreen(TitleScreen.NewRun()); Iris.Start(); }  // same circle opening as starting from the title
            if (_selected == 1) ScreenManager.AddScreen(new ShopScreen());  // spend those gems right away, Back drops you on the title
        }

        // Clickable area of a button, both are centered on the screen
        private Rectangle OptionRectangle(int i) => new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width / 2 - 255 + i * 175, 368, 160, 48);  // 3 buttons centered

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            spriteBatch.Begin();

            // Darken the game behind so the text stands out
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f * TransitionAlpha);

            if (_cleared) DrawCentered("Victory!", 70, Color.Gold, 1.6f, ScreenManager.BigFont);  // beat the golem
            else DrawCentered("You Died", 70, Color.Red, 1.6f, ScreenManager.BigFont);

            // This deaths pun (shrinks if its too wide for the screen)
            var font = ScreenManager.MenuFont;
            DrawCentered(_pun, 143, Color.White, MathF.Min(0.55f, (viewport.Width - 60) / font.MeasureString(_pun).X));

            DrawCentered($"Score: {_score}", 180, Color.Yellow, 0.9f, ScreenManager.BigFont);
            if (_newBest) DrawCentered("NEW BEST!", 219, Color.Lerp(Color.Gold, Color.White, 0.5f + 0.5f * MathF.Sin(_age * 8)), 0.5f);
            else DrawCentered($"Best: {RunStats.Best}", 219, Color.Gold, 0.45f);
            float pop = _gemsShown == _gems && _gems > 0 ? 1f + 0.25f * MathF.Exp(-8f * (_age - 1.7f)) * MathF.Cos((_age - 1.7f) * 25f) : 1f;  // bounces when it lands
            DrawCentered($"+{_gemsShown} gems", 243, Color.Cyan, 0.6f * MathF.Max(1f, pop));

            // Run stats as a row of little tiles, each one slides up and fades in a beat after the last
            const int TileW = 112, TileH = 62, Gap = 8;
            int startX = (viewport.Width - (_stats.Length * TileW + (_stats.Length - 1) * Gap)) / 2;
            for (int i = 0; i < _stats.Length; i++)
            {
                float t = MathHelper.Clamp((_age - 0.3f - i * 0.12f) / 0.3f, 0, 1), ease = 1 - (1 - t) * (1 - t);
                if (t <= 0) continue;
                var (value, label, color) = _stats[i];
                var tile = new Rectangle(startX + i * (TileW + Gap), 290 + (int)((1 - ease) * 12), TileW, TileH);
                Hud.Frame(spriteBatch, tile, 11, Color.White * ease * TransitionAlpha);  // same crystal frame as the shop card
                Hud.Glow(spriteBatch, new Vector2(tile.Center.X, tile.Y + 22), new Vector2(TileW - 20, 30), color * 0.25f * ease * TransitionAlpha);  // the stat's color glows behind its number
                var big = ScreenManager.BigFont;
                float valueScale = MathF.Min(0.5f, (TileW - 30) / big.MeasureString(value).X);
                var valueSize = big.MeasureString(value) * valueScale;
                spriteBatch.DrawString(big, value, new Vector2(tile.Center.X - valueSize.X / 2 + 2, tile.Y + 12 + 2), Color.Black * ease * TransitionAlpha, 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(big, value, new Vector2(tile.Center.X - valueSize.X / 2, tile.Y + 12), color * ease * TransitionAlpha, 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);
                float labelScale = MathF.Min(0.26f, (TileW - 26) / font.MeasureString(label).X);
                var labelSize = font.MeasureString(label) * labelScale;
                spriteBatch.DrawString(font, label, new Vector2(tile.Center.X - labelSize.X / 2, tile.Bottom - labelSize.Y - 11), Color.LightGray * ease * TransitionAlpha, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
            }

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
