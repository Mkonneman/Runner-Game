using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Little particle flair for each skin, scaled by the "Skin effects" setting (0 = off)
    /// Explorer: boot dust, Traveller: red leaves, Thief: purple smoke, Soldier: shell casings on smash/land,
    /// Ninja: afterimages while dashing/jumping, Hazmat: toxic drips, Spacesuit: star twinkles
    /// </summary>
    public class SkinFx
    {
        private struct Bit { public Vector2 Pos, Vel; public float Age, Life, Size, Gravity, Spin; public Color Color; public bool Bounce, Star; }
        private readonly List<Bit> _bits = new();
        private readonly List<(Vector2 Pos, float Age)> _ghosts = new();  // ninja afterimages
        private readonly ItemId _skin;
        private float _emit;              // carries over fractional particles between frames
        private bool _wasOnGround = true;
        private float _ghostTimer;

        public SkinFx() => _skin = Shop.Equipped(Slot.Skin) ?? ItemId.SkinExplorer;

        private static float Amount => Settings.SkinFxAmount;

        private void Add(Vector2 pos, Vector2 vel, float life, float size, Color color, float gravity = 0, bool bounce = false, bool star = false)
            => _bits.Add(new Bit { Pos = pos, Vel = vel, Life = life, Size = size, Color = color, Gravity = gravity, Bounce = bounce, Star = star, Spin = RandomHelper.NextFloat(0, 6) });

        // Spawns this many per second (times the setting), keeps the leftovers so low rates still come out
        private int Rate(float perSecond, float dt)
        {
            _emit += perSecond * Amount * dt;
            int n = (int)_emit; _emit -= n; return n;
        }

        /// <summary>
        /// Call once a frame. floorY = where casings and drips bounce/splat, smashed = he swung this frame
        /// </summary>
        public void Update(float dt, Runner runner, float floorY, bool smashed)
        {
            var b = runner.Bounds;
            var feet = new Vector2(runner.Position.X, b.Bottom);
            var back = new Vector2(b.X + 4, b.Y + b.Height * 0.4f);
            bool onGround = runner.IsOnGround, landed = onGround && !_wasOnGround, jumped = !onGround && _wasOnGround;
            bool dashing = runner.Velocity != Vector2.Zero, running = runner.IsMoving;
            _wasOnGround = onGround;

            if (Amount > 0)
                switch (_skin)
                {
                    case ItemId.SkinExplorer:
                        if (onGround && running) for (int i = Rate(10, dt); i > 0; i--) Add(feet + new Vector2(RandomHelper.NextFloat(-10, 6), 0), new Vector2(RandomHelper.NextFloat(-80, -30), RandomHelper.NextFloat(-30, -5)), 0.5f, 3, new Color(150, 130, 100));
                        if (landed) for (int i = 0; i < 6 * Amount; i++) Add(feet, new Vector2(RandomHelper.NextFloat(-90, 90), RandomHelper.NextFloat(-40, -10)), 0.5f, 3, new Color(150, 130, 100));
                        break;
                    case ItemId.SkinTraveller:
                        if (running) for (int i = Rate(5, dt); i > 0; i--) Add(back, new Vector2(RandomHelper.NextFloat(-120, -60), RandomHelper.NextFloat(-20, 20)), 1.2f, 4, RandomHelper.Next(0, 1) == 0 ? new Color(200, 60, 40) : new Color(230, 120, 40), gravity: 40);
                        break;
                    case ItemId.SkinThief:
                        if (running) for (int i = Rate(14, dt); i > 0; i--) Add(back + new Vector2(0, RandomHelper.NextFloat(-20, 30)), new Vector2(RandomHelper.NextFloat(-70, -30), RandomHelper.NextFloat(-25, -5)), 0.45f, 6, new Color(110, 60, 160));
                        break;
                    case ItemId.SkinSoldier:
                        if (smashed || landed)
                            for (int i = 0; i < 3 * Amount + 0.5f; i++) Add(new Vector2(b.X + b.Width / 2, b.Y + 40), new Vector2(RandomHelper.NextFloat(-160, -40), RandomHelper.NextFloat(-260, -160)), 1.4f, 3, new Color(230, 180, 70), gravity: 900, bounce: true);
                        break;
                    case ItemId.SkinNinja:
                        if ((dashing || !onGround) && (_ghostTimer -= dt) <= 0 && Amount > 0)
                        {
                            _ghosts.Add((runner.Position, 0));
                            _ghostTimer = 0.05f / Amount;
                        }
                        break;
                    case ItemId.SkinHazmat:
                        for (int i = Rate(6, dt); i > 0; i--) Add(new Vector2(RandomHelper.NextFloat(b.X, b.Right), b.Y + RandomHelper.NextFloat(30, b.Height - 10)), new Vector2(-30, 0), 1.2f, 3, new Color(120, 255, 90), gravity: 500, bounce: true);
                        break;
                    case ItemId.SkinSpacesuit:
                        for (int i = Rate(4, dt); i > 0; i--) Add(new Vector2(RandomHelper.NextFloat(b.X - 20, b.Right + 20), RandomHelper.NextFloat(b.Y - 20, b.Bottom)), new Vector2(-60, 0), 0.6f, 5, new Color(200, 230, 255), star: true);
                        break;
                }

            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var p = _bits[i];
                p.Age += dt;
                p.Vel.Y += p.Gravity * dt;
                p.Pos += p.Vel * dt;
                if (p.Bounce && p.Pos.Y > floorY) { p.Pos.Y = floorY; p.Vel.Y *= -0.35f; p.Vel.X *= 0.6f; }  // casings clink, drips splat
                if (p.Age > p.Life) _bits.RemoveAt(i); else _bits[i] = p;
            }
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                _ghosts[i] = (_ghosts[i].Pos - new Vector2(150 * dt, 0), _ghosts[i].Age + dt);  // trail behind him
                if (_ghosts[i].Age > 0.25f) _ghosts.RemoveAt(i);
            }
        }

        /// <summary>
        /// Ninja afterimages, drawn before him so they sit behind
        /// </summary>
        public void DrawBehind(SpriteBatch spriteBatch, Runner runner, float alpha)
        {
            if (_ghosts.Count == 0) return;
            var keepPos = runner.Position; var keepColor = runner.Color;
            foreach (var (pos, age) in _ghosts)
            {
                runner.Position = pos;
                runner.Color = new Color(40, 40, 60) * (0.5f * (1 - age / 0.25f) * Amount * alpha);
                runner.Draw(new GameTime(), spriteBatch);  // zero time so his animation doesn't skip ahead
            }
            runner.Position = keepPos; runner.Color = keepColor;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, float alpha)
        {
            foreach (var p in _bits)
            {
                float fade = (1 - p.Age / p.Life) * alpha * MathHelper.Clamp(Amount * 1.5f, 0, 1);
                int s = (int)p.Size;
                if (p.Star)  // little 4 point twinkle that pulses
                {
                    int arm = (int)(s * MathF.Sin(p.Age / p.Life * MathF.PI)) + 1;
                    spriteBatch.Draw(pixel, new Rectangle((int)p.Pos.X - arm, (int)p.Pos.Y, arm * 2 + 1, 1), p.Color * fade);
                    spriteBatch.Draw(pixel, new Rectangle((int)p.Pos.X, (int)p.Pos.Y - arm, 1, arm * 2 + 1), p.Color * fade);
                }
                else spriteBatch.Draw(pixel, new Rectangle((int)p.Pos.X - s / 2, (int)p.Pos.Y - s / 2, s, s), p.Color * fade);
            }
        }
    }
}
