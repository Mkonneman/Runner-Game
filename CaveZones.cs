using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// The cave changes color the deeper he runs (blue -> orange -> green) and stays on the last one
    /// Each zone is where a boss could go later (Might add those later lol)
    /// </summary>
    public static class CaveZones
    {
        public const float ZoneLength = 90f;  // seconds in each zone before it starts fading

        // Furthest cave reached, runs start at the beginning of it (saved)
        public static int Checkpoint;
        public const float FadeTime = 6f;     // how long the fade into the next color takes

        // Hue shift in degrees, the color it gets pulled toward, and how hard it gets pulled
        private static readonly (float Shift, Color Target, float Mix)[] Zones =
        {
            (0f, Color.White, 0f),                    // blue (the art as is)
            (175f, new Color(255, 140, 40), 0.5f),    // orange
            (-95f, new Color(110, 255, 120), 0.4f),   // green
        };

        public static int Count => Zones.Length;

        // For the progress bar at the top
        public static readonly Color[] BarColors = { new(80, 140, 255), new(255, 140, 40), new(110, 230, 120) };
        public static readonly string[] Names = { "Blue Cave", "Ember Cave", "Moss Cave" };

        /// <summary>
        /// Which zone you're in, which one it's fading to, and how far along the fade is (0 to 1)
        /// </summary>
        public static (int From, int To, float Blend) At(float time)
        {
            int zone = (int)(time / ZoneLength);
            if (zone >= Zones.Length - 1) return (Zones.Length - 1, Zones.Length - 1, 0f);  // last color sticks
            float blend = MathHelper.Clamp((time - zone * ZoneLength - (ZoneLength - FadeTime)) / FadeTime, 0f, 1f);
            return (zone, zone + 1, blend);
        }

        // Recolored copies get made once and reused every run
        private static readonly Dictionary<(Texture2D, Color), Texture2D[]> Cache = new();

        /// <summary>
        /// One copy of the texture per zone (tint multiplies the art first, like the blue on the spikes)
        /// </summary>
        public static Texture2D[] Recolor(Texture2D texture, Color? tint = null)
        {
            var key = (texture, tint ?? Color.White);
            if (Cache.TryGetValue(key, out var done)) return done;

            var source = new Color[texture.Width * texture.Height];
            texture.GetData(source);
            var versions = new Texture2D[Zones.Length];
            for (int z = 0; z < Zones.Length; z++)
            {
                var pixels = new Color[source.Length];
                for (int i = 0; i < source.Length; i++) pixels[i] = Shade(source[i], tint ?? Color.White, Zones[z]);
                versions[z] = new Texture2D(texture.GraphicsDevice, texture.Width, texture.Height);
                versions[z].SetData(pixels);
            }
            Cache[key] = versions;
            return versions;
        }

        // The pixels are premultiplied, so undo that, shift the hue, pull toward the zone color, then premultiply again
        private static Color Shade(Color c, Color tint, (float Shift, Color Target, float Mix) zone)
        {
            if (c.A == 0) return Color.Transparent;
            float a = c.A / 255f;
            float r = c.R / 255f / a * tint.R / 255f, g = c.G / 255f / a * tint.G / 255f, b = c.B / 255f / a * tint.B / 255f;

            if (zone.Mix > 0)
            {
                float lum = 0.3f * r + 0.59f * g + 0.11f * b;
                (r, g, b) = ShiftHue(r, g, b, zone.Shift);
                r += (MathF.Min(1, lum * 2.2f * zone.Target.R / 255f) - r) * zone.Mix;
                g += (MathF.Min(1, lum * 2.2f * zone.Target.G / 255f) - g) * zone.Mix;
                b += (MathF.Min(1, lum * 2.2f * zone.Target.B / 255f) - b) * zone.Mix;
            }
            return new Color(r * a, g * a, b * a, a);
        }

        // Plain rgb -> hsv -> rgb with the hue spun around
        private static (float, float, float) ShiftHue(float r, float g, float b, float degrees)
        {
            float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b)), delta = max - min;
            if (delta < 0.0001f) return (r, g, b);  // grays have no hue to shift
            float h = max == r ? (g - b) / delta % 6 : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4;
            h = ((h * 60 + degrees) % 360 + 360) % 360 / 60;
            float s = delta / max, v = max;
            float f = h - MathF.Floor(h), p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            return ((int)h % 6) switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q),
            };
        }
    }
}
