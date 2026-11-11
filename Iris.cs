using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Black screen with a circle hole that grows out from the player until it's off screen (plays when a run starts)
    /// </summary>
    public static class Iris
    {
        private const float Hold = 0.25f, Open = 0.9f;  // all black for a beat, then the circle opens
        private static float _t = -1;                    // -1 = not playing
        public static Vector2 Center = new(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f);  // the run screens point this at the player

        public static bool Active => _t >= 0;
        public static void Start() => _t = 0;

        public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, float dt)
        {
            if (!Active) return;
            _t += dt;
            float p = MathHelper.Clamp((_t - Hold) / Open, 0, 1);
            if (p >= 1) { _t = -1; return; }
            float r = p * p * 1000f;  // eases in, 1000 is past the far corner from anywhere on screen

            // Scanline strips: black left and right of the circle on each row
            for (int y = 0; y < Game1.VirtualHeight; y += 2)
            {
                float dy = y + 1 - Center.Y;
                if (MathF.Abs(dy) >= r) { spriteBatch.Draw(pixel, new Rectangle(0, y, Game1.VirtualWidth, 2), Color.Black); continue; }
                float half = MathF.Sqrt(r * r - dy * dy);
                int left = (int)(Center.X - half), right = (int)(Center.X + half);
                if (left > 0) spriteBatch.Draw(pixel, new Rectangle(0, y, left, 2), Color.Black);
                if (right < Game1.VirtualWidth) spriteBatch.Draw(pixel, new Rectangle(right, y, Game1.VirtualWidth - right, 2), Color.Black);
            }
        }
    }
}
