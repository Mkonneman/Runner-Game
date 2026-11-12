using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Achievements, lifetime stats that unlock them, the rewards they pay out, and the drop-bounce banner when one pops
    /// </summary>
    public static class Achievements
    {
        public record Award(string Id, string Name, string Desc, int Gems, ItemId? Skin = null);

        // The list, rewards are gems and a few give skins
        public static readonly Award[] All =
        {
            new("first_run", "First Steps", "Finish your first run", 50),
            new("rocks_25", "Rock Breaker", "Smash 25 falling rocks", 100),
            new("walls_25", "Wall Wrecker", "Smash 25 walls", 150),
            new("diamonds_100", "Shiny!", "Grab 100 diamonds", 150),
            new("orange", "Getting Warm", "Reach the orange cave", 100),
            new("green", "Deep Down", "Reach the green cave", 200, ItemId.SkinTraveller),
            new("arena", "Door Opener", "Make it to the arena", 300),
            new("golem", "Golem Slayer", "Beat the Stone Golem", 500, ItemId.SkinNinja),
            new("flawless", "Untouchable", "Beat the Golem without getting hit", 1000, ItemId.SkinSpacesuit),
            new("buy_5", "Big Spender", "Buy 5 things in the shop", 100),
            new("maxed", "Maxed Out", "Fully upgrade an item", 200),
            new("skin", "Fresh Fit", "Wear a new skin", 50),
            new("walls_died_10", "Wall: 10, You: 0", "Die to a wall 10 times", 75),
        };

        private static readonly HashSet<string> _unlocked = new();
        private static readonly Dictionary<string, int> _stats = new();
        public static bool Has(string id) => _unlocked.Contains(id);
        public static int Count => _unlocked.Count;

        // Bumps a lifetime stat and unlocks whatever hits its goal
        public static void Add(string stat, int amount = 1)
        {
            _stats[stat] = _stats.GetValueOrDefault(stat) + amount;
            int v = _stats[stat];
            if (stat == "rocks" && v >= 25) Unlock("rocks_25");
            if (stat == "walls" && v >= 25) Unlock("walls_25");
            if (stat == "diamonds" && v >= 100) Unlock("diamonds_100");
            if (stat == "buys" && v >= 5) Unlock("buy_5");
        }

        /// <summary>
        /// Unlocks it once, pays the reward and queues the banner
        /// </summary>
        public static void Unlock(string id)
        {
            var award = All.FirstOrDefault(a => a.Id == id);
            if (award == null || !_unlocked.Add(id)) return;
            Shop.AddGems(award.Gems);
            if (award.Skin is ItemId skin) Shop.Give(skin);
            _queue.Enqueue(award);
            SaveSystem.Save();
        }

        // For the save file
        public static string[] SaveUnlocked() => _unlocked.ToArray();
        public static Dictionary<string, int> SaveStats() => new(_stats);
        public static void Load(string[] unlocked, Dictionary<string, int> stats)
        {
            _unlocked.Clear(); _stats.Clear();
            foreach (var id in unlocked ?? Array.Empty<string>()) _unlocked.Add(id);
            foreach (var (k, v) in stats ?? new()) _stats[k] = v;
        }

        // ---- Banner: drops 96px from above on a bouncy spring, sits, then lifts away over 0.7s ----
        private static readonly Queue<Award> _queue = new();
        private static Award _showing;
        private static float _t;
        private const float DropFrom = 96f, HoldTime = 2.6f, LiftTime = 0.7f, RestY = 10f;

        public static void Update(float dt)
        {
            if (_showing == null && _queue.Count > 0) { _showing = _queue.Dequeue(); _t = 0; Sfx.Play("achievement", 0.6f, 0f); }
            if (_showing == null) return;
            if ((_t += dt) > HoldTime + LiftTime) _showing = null;
        }

        public static void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
        {
            if (_showing == null) return;
            // Spring in (overshoots a little and wobbles), then ease up and out
            float y = RestY - DropFrom * MathF.Exp(-7f * _t) * MathF.Cos(_t * MathHelper.TwoPi * 1.6f);
            if (_t > HoldTime) { float l = (_t - HoldTime) / LiftTime; y = RestY - (DropFrom + 70) * l * l; }

            var box = new Rectangle(427 - 170, (int)y, 340, 62);
            spriteBatch.Draw(pixel, new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6), Color.Black);
            spriteBatch.Draw(pixel, box, new Color(40, 30, 10));
            spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, 3), new Color(255, 200, 60));  // gold trim
            spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - 3, box.Width, 3), new Color(255, 200, 60));
            DrawTrophy(spriteBatch, pixel, new Vector2(box.X + 14, box.Y + 14));

            string reward = $"+{_showing.Gems} gems" + (_showing.Skin is ItemId s ? $" + {Shop.Get(s).Name} skin" : "");
            Text(spriteBatch, font, "Achievement: " + _showing.Name, new Vector2(box.X + 58, box.Y + 8), new Color(255, 210, 70), 0.42f);
            Text(spriteBatch, font, _showing.Desc, new Vector2(box.X + 58, box.Y + 26), Color.White, 0.32f);
            Text(spriteBatch, font, reward, new Vector2(box.X + 58, box.Y + 42), Color.Cyan, 0.3f);
        }

        // The gold cup (grey when it's still locked), about 32px tall at scale 1
        public static Texture2D Cup;
        public static void DrawTrophy(SpriteBatch spriteBatch, Texture2D pixel, Vector2 at, float alpha = 1f, bool gold = true, float scale = 1f)
        {
            if (Cup == null) return;
            float h = 32 * scale, w = h * Cup.Width / Cup.Height;
            spriteBatch.Draw(Cup, new Rectangle((int)(at.X + (32 * scale - w) / 2), (int)at.Y, (int)w, (int)h), (gold ? Color.White : new Color(70, 70, 80)) * alpha);
        }

        private static void Text(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 pos, Color color, float scale)
        {
            spriteBatch.DrawString(font, text, pos + new Vector2(2), Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
