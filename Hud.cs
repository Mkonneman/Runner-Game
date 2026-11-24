using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Rounded bars for the HUD (pill shapes with a soft glow), made from a circle and a soft dot built on first use
    /// </summary>
    public static class Hud
    {
        private static Texture2D _circle, _soft;
        private static Texture2D _panel;  // crystal frame art

        public static void Load(Microsoft.Xna.Framework.Content.ContentManager content) => _panel = content.Load<Texture2D>("Images/Menu/upgrade_panel");

        /// <summary>
        /// The crystal frame stretched to any size with its corners kept whole (9 slice), border is how thick the edge draws
        /// </summary>
        public static void Frame(SpriteBatch spriteBatch, Rectangle box, int border, Color color) => Frame(spriteBatch, _panel, 18, box, border, color);

        // Same 9 slice for any frame art, B = how thick its edge is in the art
        public static void Frame(SpriteBatch spriteBatch, Texture2D art, int B, Rectangle box, int border, Color color)
        {
            var _panel = art;
            int w = _panel.Width, h = _panel.Height;
            int[] sx = { 0, B, w - B, w }, dx = { box.Left, box.Left + border, box.Right - border, box.Right };
            int[] sy = { 0, B, h - B, h }, dy = { box.Top, box.Top + border, box.Bottom - border, box.Bottom };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    spriteBatch.Draw(_panel, new Rectangle(dx[c], dy[r], dx[c + 1] - dx[c], dy[r + 1] - dy[r]), new Rectangle(sx[c], sy[r], sx[c + 1] - sx[c], sy[r + 1] - sy[r]), color);
        }

        /// <summary>
        /// Round dot with a black outline (the shop's "equipped" marker)
        /// </summary>
        // Small dark pill with a controller button name in it, like "RT"
        public static void PadHint(SpriteBatch spriteBatch, SpriteFont font, string label, Vector2 center, float alpha)
        {
            var size = font.MeasureString(label) * 0.3f;
            var box = new Rectangle((int)(center.X - size.X / 2 - 6), (int)(center.Y - size.Y / 2 - 2), (int)size.X + 12, (int)size.Y + 4);
            Pill(spriteBatch, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2), Color.White * 0.8f * alpha);
            Pill(spriteBatch, box, new Color(20, 24, 36) * alpha);
            spriteBatch.DrawString(font, label, new Vector2(box.X + 6, box.Y + 2), Color.White * alpha, 0f, Vector2.Zero, 0.3f, SpriteEffects.None, 0f);
        }

        public static void Dot(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
        {
            Make(spriteBatch.GraphicsDevice);
            int d = (int)size;
            Pill(spriteBatch, new Rectangle((int)center.X - d / 2 - 2, (int)center.Y - d / 2 - 2, d + 4, d + 4), Color.Black * (color.A / 255f));
            Pill(spriteBatch, new Rectangle((int)center.X - d / 2, (int)center.Y - d / 2, d, d), color);
        }

        private static void Make(GraphicsDevice device)
        {
            if (_circle != null) return;
            const int D = 32;
            var hard = new Color[D * D]; var soft = new Color[D * D];
            for (int i = 0; i < hard.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(i % D + 0.5f, i / D + 0.5f), new Vector2(D / 2f)) / (D / 2f);
                hard[i] = Color.White * MathHelper.Clamp((1 - d) * D / 2f, 0, 1);  // crisp edge, 1px of smoothing
                soft[i] = Color.White * MathF.Pow(MathHelper.Clamp(1 - d, 0, 1), 2);
            }
            _circle = new Texture2D(device, D, D); _circle.SetData(hard);
            _soft = new Texture2D(device, D, D); _soft.SetData(soft);
        }

        /// <summary>
        /// Thin vertical scroll bar, dark track and a thumb sized to how much fits (draws nothing when it all fits)
        /// </summary>
        public static void ScrollBar(SpriteBatch spriteBatch, Rectangle track, int first, int visible, int total, Color color, float alpha)
        {
            if (total <= visible) return;
            Make(spriteBatch.GraphicsDevice);
            int thumb = Math.Max(track.Width * 2, track.Height * visible / total);
            int y = track.Y + (track.Height - thumb) * first / (total - visible);
            UpPill(spriteBatch, track, Color.Black * 0.6f * alpha);
            UpPill(spriteBatch, new Rectangle(track.X, y, track.Width, thumb), color * alpha);
        }

        // Pill standing up (round top and bottom)
        private static void UpPill(SpriteBatch spriteBatch, Rectangle r, Color color)
        {
            int half = r.Width / 2;
            spriteBatch.Draw(_circle, new Rectangle(r.X, r.Y, r.Width, half), new Rectangle(0, 0, 32, 16), color);
            spriteBatch.Draw(_circle, new Rectangle(r.X, r.Bottom - half, r.Width, half), new Rectangle(0, 16, 32, 16), color);
            spriteBatch.Draw(_circle, new Rectangle(r.X, r.Y + half, r.Width, r.Height - half * 2), new Rectangle(0, 15, 32, 2), color);
        }

        /// <summary>
        /// A pill (rectangle with round ends) filling r
        /// </summary>
        public static void Pill(SpriteBatch spriteBatch, Rectangle r, Color color)
        {
            Make(spriteBatch.GraphicsDevice);
            if (r.Width <= 0) return;
            int d = r.Height;
            if (r.Width <= d) { spriteBatch.Draw(_circle, new Rectangle(r.X, r.Y, r.Width, d), color); return; }  // too short for a middle, squished dot
            int half = d / 2;
            spriteBatch.Draw(_circle, new Rectangle(r.X, r.Y, half, d), new Rectangle(0, 0, 16, 32), color);                 // left half circle
            spriteBatch.Draw(_circle, new Rectangle(r.Right - half, r.Y, half, d), new Rectangle(16, 0, 16, 32), color);     // right half
            spriteBatch.Draw(_circle, new Rectangle(r.X + half, r.Y, r.Width - half * 2, d), new Rectangle(15, 0, 2, 32), color);  // middle
        }

        /// <summary>
        /// Glowing rounded bar: dark outline, dim track, glow behind the fill, shine line, pulsing head at the front while it's not full
        /// </summary>
        public static void Bar(SpriteBatch spriteBatch, Rectangle back, float fill, Color color, float time, float alpha)
        {
            Pill(spriteBatch, new Rectangle(back.X - 2, back.Y - 2, back.Width + 4, back.Height + 4), Color.Black * 0.7f * alpha);  // outline
            Pill(spriteBatch, back, color * 0.22f * alpha);
            if (fill <= 0) return;
            int filled = Math.Max(back.Height, (int)(back.Width * MathHelper.Clamp(fill, 0, 1)));
            Glow(spriteBatch, new Vector2(back.X + filled / 2f, back.Center.Y), new Vector2(filled + 18, back.Height * 3), color * 0.35f * alpha);  // soft light behind the fill
            Pill(spriteBatch, new Rectangle(back.X, back.Y, filled, back.Height), color * alpha);
            Pill(spriteBatch, new Rectangle(back.X + 3, back.Y + 2, Math.Max(0, filled - 6), 2), Color.White * 0.35f * alpha);  // shine line
            if (fill < 1) Glow(spriteBatch, new Vector2(back.X + filled - 3, back.Center.Y), new Vector2(26, 26), Color.Lerp(color, Color.White, 0.5f) * (0.7f + 0.3f * MathF.Sin(time * 6)) * alpha);  // bright head
        }

        /// <summary>
        /// HUD number that pops: soft glow in its color behind, drop shadow, black outline, then the text. Returns its size
        /// </summary>
        public static Vector2 PopText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale, float alpha)
        {
            var size = font.MeasureString(text) * scale;
            Glow(spriteBatch, position + size / 2, size + new Vector2(28, 14), color * 0.3f * alpha);
            spriteBatch.DrawString(font, text, position + new Vector2(2, 3), Color.Black * 0.6f * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) spriteBatch.DrawString(font, text, position + new Vector2(ox, oy) * 1.5f, Color.Black * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return size;
        }

        /// <summary>
        /// Soft glow blob centered on a spot
        /// </summary>
        public static void Glow(SpriteBatch spriteBatch, Vector2 center, Vector2 size, Color color)
        {
            Make(spriteBatch.GraphicsDevice);
            spriteBatch.Draw(_soft, center, null, color, 0f, new Vector2(16), size / 32f, SpriteEffects.None, 0f);
        }
    }
}
