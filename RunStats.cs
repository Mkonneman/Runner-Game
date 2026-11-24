using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Everything about the run going on right now: the stats for the summary, the combo, the "+points" pop by the score and the 3 2 1 countdown
    /// Caves and the arena share it so a run that walks through the door keeps its combo and stats
    /// </summary>
    public static class RunStats
    {
        // Summary on the game over screen
        public static float Distance, Time;   // pixels scrolled, seconds played
        public static int Walls, Rocks, Gems;  // stuff smashed this run
        public static float BestMultiplier = 1f;

        // Best score ever and the top 5 runs by score and by distance in meters (saved)
        public static int Best = SeedScore;
        public static int[] TopScores = { SeedScore }, TopMeters = { SeedMeters };
        public const int SeedScore = 33765, SeedMeters = 2042;  // the dev's run, what a new save has to beat

        // Drops a finished run into both top 5 lists
        public static void Record(int score, int meters)
        {
            TopScores = Top5(TopScores, score);
            TopMeters = Top5(TopMeters, meters);
        }
        private static int[] Top5(int[] list, int value) => value <= 0 ? list : System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(System.Linq.Enumerable.Append(list, value), v => v), 5));

        // Combo: every 4 breaks in a row adds x0.5, tops out at x3. Getting hurt or going 6s without breaking anything drops it
        private const int BreaksPerStep = 4;
        private const float MaxMultiplier = 3f, ChainTime = 6f;
        private static int _chain;
        private static float _chainLeft, _bounceT = 10f;
        public static float Multiplier => MathF.Min(MaxMultiplier, 1f + 0.5f * (_chain / BreaksPerStep));

        // Only a quarter of the combo's extra points turn into gems, so combos feel great on the score without flooding the shop
        public static int ComboBonus;
        public static int GemsFor(int score) => Shop.GemsFromScore(Math.Max(0, score - (int)(ComboBonus * 0.75f)));

        // "+points" next to the score, adds up while points keep coming and fades 2s after the last ones
        private static int _lastScore, _pop;
        private static float _popLeft, _popBounceT = 10f;

        // 3 2 1 GO after unpausing
        public static float Countdown;
        private static float _goLeft;

        public static void Reset()
        {
            Distance = Time = 0; Walls = Rocks = Gems = 0; BestMultiplier = 1f;
            _chain = 0; _chainLeft = 0; ComboBonus = 0; _bounceT = 10f;
            _lastScore = 0; _pop = 0; _popLeft = 0;
            Countdown = 0; _goLeft = 0;  // only unpausing counts down
        }

        /// <summary>
        /// Points for breaking something, the combo multiplies them and goes up a notch every 5 breaks
        /// </summary>
        public static int Points(int points)
        {
            float before = Multiplier;
            _chain++;
            _chainLeft = ChainTime;
            if (Multiplier > before) { _bounceT = 0; Sfx.Play("diamond", 0.35f, 0f); Rumble.Play("combo"); }  // stepped up, bounce it
            BestMultiplier = MathF.Max(BestMultiplier, Multiplier);
            int bonus = (int)MathF.Round(points * (Multiplier - 1));
            ComboBonus += bonus;
            HitStop.Freeze(0.035f);
            return points + bonus;
        }

        // Got hit, combo's gone
        public static void Hurt() { _chain = 0; _chainLeft = 0; }

        // True while the 3 2 1 is still counting (the run is frozen till it's done)
        public static bool CountingDown(float dt)
        {
            if (Countdown <= 0) { _goLeft -= dt; return false; }
            int before = Countdown >= 3f ? 4 : (int)MathF.Ceiling(Countdown);  // so the 3 gets its beep too
            Countdown -= dt;
            int after = (int)MathF.Ceiling(Countdown);
            if (Countdown <= 0) { _goLeft = 0.6f; Sfx.Play("menu_pick", 0.5f, 0f); }
            else if (after != before) Sfx.Play("menu_move", 0.5f, 0f);
            return true;
        }

        public static void Update(float dt, int score)
        {
            Time += dt;
            if (_chainLeft > 0 && (_chainLeft -= dt) <= 0) _chain = 0;
            _bounceT += dt;
            _popBounceT += dt;
            if (score > _lastScore) { _pop += score - _lastScore; _popLeft = 2f; _popBounceT = 0; }
            _lastScore = score;
            if (_popLeft > 0 && (_popLeft -= dt) <= 0) _pop = 0;
        }

        // The score text kicks a little when points land (same timing as the "+points" pop)
        public static float ScoreScale => Bounce(_popBounceT, 0.12f);

        // Springy scale, big right after it fires then wobbles back to 1
        private static float Bounce(float t, float amount) => 1f + amount * MathF.Exp(-8f * t) * MathF.Cos(t * 25f);

        /// <summary>
        /// "+120" just right of the score text, at = (left edge, bottom of the score text) so they share a baseline
        /// </summary>
        public static void DrawScorePop(SpriteBatch spriteBatch, SpriteFont font, Vector2 at, float alpha)
        {
            if (_pop <= 0) return;
            string text = $"+{_pop}";
            float scale = 0.55f * Bounce(_popBounceT, 0.35f), fade = MathF.Min(1, _popLeft / 0.5f) * alpha;
            var size = font.MeasureString(text) * scale;
            var pos = at - new Vector2(0, size.Y);
            spriteBatch.DrawString(font, text, pos + new Vector2(2), Color.Black * fade, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, Color.White * fade, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Combo in the top right, right edge at rightX. Hotter color the higher it goes, a bar for time left and pips for the next step
        /// </summary>
        public static void DrawCombo(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, float rightX, float y, float alpha)
        {
            if (_chain <= 0) return;
            float m = Multiplier;
            var color = m >= 3 ? Color.Lerp(Color.Red, Color.White, 0.5f + 0.5f * MathF.Sin(Time * 18)) : m >= 2.5f ? Color.OrangeRed : m >= 2 ? Color.Orange : m >= 1.5f ? Color.Gold : Color.White;
            string text = $"x{m:0.0}";
            float scale = 0.7f * Bounce(_bounceT, 0.6f);
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(rightX - size.X, y + 14 - size.Y / 2);
            spriteBatch.DrawString(font, text, pos + new Vector2(2), Color.Black * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, color * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, "COMBO", new Vector2(rightX - size.X - 52, y + 8), Color.White * 0.8f * alpha, 0f, Vector2.Zero, 0.3f, SpriteEffects.None, 0f);

            // Time left bar
            spriteBatch.Draw(pixel, new Rectangle((int)rightX - 90, (int)y + 32, 90, 4), Color.Black * 0.6f * alpha);
            spriteBatch.Draw(pixel, new Rectangle((int)rightX - 90, (int)y + 32, (int)(90 * _chainLeft / ChainTime), 4), color * alpha);

            // Pips toward the next step (all lit at max)
            int lit = m >= MaxMultiplier ? BreaksPerStep : _chain % BreaksPerStep;
            for (int i = 0; i < BreaksPerStep; i++)
                spriteBatch.Draw(pixel, new Rectangle((int)rightX - 90 + i * 24, (int)y + 39, 18, 3), (i < lit ? color : Color.Gray * 0.4f) * alpha);
        }

        /// <summary>
        /// Big 3 2 1 in the middle (each number pops in), then GO!
        /// </summary>
        public static void DrawCountdown(SpriteBatch spriteBatch, SpriteFont font, float alpha)
        {
            string text; float t;
            if (Countdown > 0) { text = ((int)MathF.Ceiling(Countdown)).ToString(); t = MathF.Ceiling(Countdown) - Countdown; }  // time since this number showed up
            else if (_goLeft > 0) { text = "GO!"; t = 0.6f - _goLeft; alpha *= MathF.Min(1, _goLeft / 0.3f); }
            else return;
            float scale = 1.6f * Bounce(t, 0.5f);
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(Game1.VirtualWidth / 2f - size.X / 2, Game1.VirtualHeight / 2f - size.Y / 2 - 30);
            spriteBatch.DrawString(font, text, pos + new Vector2(4), Color.Black * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, (text == "GO!" ? Color.LimeGreen : Color.Yellow) * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// Hit stop, the whole run freezes for a couple frames on a big hit so it feels heavier (Settings can turn it off)
    /// </summary>
    public static class HitStop
    {
        private static float _left;
        public static void Freeze(float seconds) { if (Settings.HitStop) _left = MathF.Max(_left, seconds); }
        public static bool Frozen(float dt) { if (_left <= 0) return false; _left -= dt; return true; }
    }

    /// <summary>
    /// Game sound effects go through the Sound effects slider (UI clicks have their own slider in Sfx)
    /// </summary>
    public static class SoundExtensions
    {
        public static void PlaySfx(this SoundEffect sound, float volume = 1f, float pitch = 0f, float pan = 0f) => sound.Play(MathHelper.Clamp(volume * Settings.SfxVolume, 0, 1), pitch, pan);
    }
}
