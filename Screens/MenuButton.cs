using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Draws the stone button art at any size, the corners stay the same and only the middle stretches (9 slice)
    /// </summary>
    public static class MenuButton
    {
        private const int Border = 10;  // how many pixels of the art are the stone frame

        // Same look the title buttons use, bright when picked and a lil dim otherwise
        public static Color Tint(bool selected) => selected ? Color.White : new Color(170, 180, 200);

        // Press squash, a button that gets clicked (or picked with Enter/Space/A) squashes to 118% x 82% then springs back in 0.3s
        private const float SquashTime = 0.3f;
        private static readonly Dictionary<Point, float> _squashes = new();  // button center -> seconds since pressed
        private static readonly Dictionary<Point, Rectangle> _squashRects = new();  // full size rect, so labels inside it squash too
        private static bool _clicked, _confirmed;
        private static MouseState _lastMouse;
        private static KeyboardState _lastKeys;
        private static GamePadState _lastPad;

        // Once a frame (InputState calls it), finds this frame's click / confirm press and ages the squashes
        public static void Update(float dt)
        {
            var mouse = VirtualMouse.GetState(); var keys = Keyboard.GetState(); var pad = GamePad.GetState(PlayerIndex.One);
            _clicked = mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released;
            _confirmed = (keys.IsKeyDown(Keys.Enter) && !_lastKeys.IsKeyDown(Keys.Enter)) || (keys.IsKeyDown(Keys.Space) && !_lastKeys.IsKeyDown(Keys.Space))
                || (pad.IsButtonDown(Buttons.A) && !_lastPad.IsButtonDown(Buttons.A));
            _lastMouse = mouse; _lastKeys = keys; _lastPad = pad;
            foreach (var key in new List<Point>(_squashes.Keys))
                if ((_squashes[key] += dt) > SquashTime) { _squashes.Remove(key); _squashRects.Remove(key); }
        }

        /// <summary>
        /// The rect to draw a button at, squashed if it just got pressed (selected = it's the one Enter would pick)
        /// </summary>
        // Squash a button from code (the HUD cooldown icons when they get used)
        public static void Trigger(Rectangle dest) { _squashes[dest.Center] = 0; _squashRects[dest.Center] = dest; }

        public static Rectangle Squash(Rectangle dest, bool selected)
        {
            if ((_clicked && dest.Contains(VirtualMouse.GetState().Position)) || (_confirmed && selected)) { _squashes[dest.Center] = 0; _squashRects[dest.Center] = dest; }
            if (!_squashes.TryGetValue(dest.Center, out float t)) return dest;
            var (sx, sy) = Amount(t);
            int w = (int)(dest.Width * sx), h = (int)(dest.Height * sy);
            return new Rectangle(dest.Center.X - w / 2, dest.Center.Y - h / 2, w, h);
        }

        private static (float X, float Y) Amount(float t)
        {
            float spring = MathF.Exp(-t * 9f) * MathF.Cos(t / SquashTime * MathHelper.TwoPi * 1.25f);  // starts at full squash, wobbles back to 1
            return (1 + 0.18f * spring, 1 - 0.18f * spring);
        }

        // Labels: if the text starts inside a squashing button it squashes around that button's middle too
        private static bool Find(Vector2 position, out Rectangle rect, out float t)
        {
            foreach (var (key, r) in _squashRects)
                if (r.Contains(position)) { rect = r; t = _squashes[key]; return true; }
            rect = default; t = 0; return false;
        }
        public static Vector2 TextPos(Vector2 position)
        {
            if (!Find(position, out var r, out float t)) return position;
            var (sx, sy) = Amount(t);
            var c = r.Center.ToVector2();
            return c + (position - c) * new Vector2(sx, sy);
        }
        public static Vector2 TextScale(Vector2 position, float scale)
        {
            if (!Find(position, out _, out float t)) return new Vector2(scale);
            var (sx, sy) = Amount(t);
            return new Vector2(scale * sx, scale * sy);
        }

        // Icons and pictures inside a button squash with it, same as the labels
        public static Rectangle Inside(Rectangle r)
        {
            var p = TextPos(r.Location.ToVector2()); var s = TextScale(r.Location.ToVector2(), 1f);
            return new Rectangle((int)p.X, (int)p.Y, (int)(r.Width * s.X), (int)(r.Height * s.Y));
        }

        public static void Draw(SpriteBatch spriteBatch, Texture2D texture, Rectangle dest, Color color)
        {
            dest = Squash(dest, color.R >= 250 && color.G >= 250 && color.B >= 250);  // Tint(true) is white, that's the picked one
            int b = Border, w = texture.Width, h = texture.Height;
            int bx = System.Math.Min(b, dest.Width / 2), by = System.Math.Min(b, dest.Height / 2);  // tiny buttons just squish the corners
            int[] sx = { 0, b, w - b, w }, dx = { dest.Left, dest.Left + bx, dest.Right - bx, dest.Right };
            int[] sy = { 0, b, h - b, h }, dy = { dest.Top, dest.Top + by, dest.Bottom - by, dest.Bottom };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    spriteBatch.Draw(texture,
                        new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]),
                        new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]),
                        color);
        }
    }
}
