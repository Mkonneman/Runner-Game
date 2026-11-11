using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Item rarity, Grey -> Blue -> Purple -> Yellow -> Red. Base tier comes from the price, maxing an item bumps it up one
    /// Also draws the soft glow + floating sparkles behind items and names in that color
    /// </summary>
    public static class Rarity
    {
        public static readonly Color[] Colors = { new(170, 170, 175), new(70, 150, 255), new(175, 90, 255), new(255, 215, 60), new(255, 60, 50) };

        public static int Tier(ItemId id) => TierAt(id, Shop.Level(id));

        // Tier at a given level (0 = not owned shows the base), the last level goes up a color (Rocket Launcher yellow -> red)
        public static int TierAt(ItemId id, int level)
        {
            var item = Shop.Get(id);
            int tier = id == ItemId.MirrorHelm ? 3  // hand picked, maxed it hits red
                : item.Price == 0 ? 0 : item.Price <= 60 ? 1 : item.Price <= 250 ? 2 : 3;  // starter / cheap / mid / top shelf
            if (item.MaxLevel > 1 && level >= item.MaxLevel) tier++;
            return Math.Min(tier, Colors.Length - 1);
        }

        public static Color Of(ItemId id) => Colors[Tier(id)];

        private static Texture2D _glow, _dot;
        private static float Time => Environment.TickCount64 / 1000f;

        // Soft round glow behind an item, pulses a little
        public static void DrawGlow(SpriteBatch spriteBatch, Rectangle area, Color color, float alpha)
        {
            _glow ??= MakeGlow(spriteBatch.GraphicsDevice);
            float pulse = 0.75f + 0.25f * MathF.Sin(Time * 3f + area.X * 0.05f);
            var grow = (int)(area.Width * 0.35f);
            spriteBatch.Draw(_glow, new Rectangle(area.X - grow, area.Y - grow, area.Width + grow * 2, area.Height + grow * 2), color * 0.7f * pulse * alpha);
        }

        // Little sparkles drifting up through an area (no state, positions come from the clock so any screen can just call it)
        public static void DrawSparks(SpriteBatch spriteBatch, Rectangle area, Color color, float alpha, int count = 6)
        {
            _dot ??= MakeDot(spriteBatch.GraphicsDevice);
            for (int k = 0; k < count; k++)
            {
                float seed = k * 12.9898f + area.X * 0.017f + area.Y * 0.031f;
                float rand = Frac(MathF.Sin(seed) * 43758.55f);
                float phase = Frac(Time * (0.45f + rand * 0.4f) + rand * 7f);  // 0 -> 1 as it rises
                float x = area.X + Frac(rand * 9.1f + MathF.Floor(Time * 0.45f + rand * 7f) * 0.37f) * area.Width;  // new x each loop
                float y = area.Bottom - phase * (area.Height + 10);
                int size = rand > 0.6f ? 3 : 2;
                spriteBatch.Draw(_dot, new Rectangle((int)x, (int)y, size, size), Color.Lerp(color, Color.White, 0.4f) * MathF.Sin(phase * MathF.PI) * alpha);
            }
        }

        private static float Frac(float v) => v - MathF.Floor(v);

        private static Texture2D MakeGlow(GraphicsDevice device)
        {
            const int N = 32;
            var data = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f)) / (N / 2f);
                    float a = MathF.Pow(MathHelper.Clamp(1 - d, 0, 1), 2);
                    data[y * N + x] = Color.White * a;  // premultiplied
                }
            var texture = new Texture2D(device, N, N);
            texture.SetData(data);
            return texture;
        }

        private static Texture2D MakeDot(GraphicsDevice device)
        {
            var texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { Color.White });
            return texture;
        }
    }
}
