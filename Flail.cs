using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Boomerang Flail, the boomerang flies out on a chain from his hand in a big loop
    /// (high going out, low coming back), smashes everything it passes through. Maxed out the chain cuts rocks too
    /// </summary>
    public class Flail
    {
        public const float FlightTime = 0.85f;
        private const float Up = 220f, Down = 64f;  // how far the loop goes above / below his hand (down skims the floor)
        public static float Reach => 190f + 45f * Math.Max(0, Shop.Level(ItemId.BoomerangFlail) - 1);  // goes farther each level
        public static bool ChainCuts => Shop.Level(ItemId.BoomerangFlail) >= 3;

        private Texture2D _boomerang;
        private float _t = FlightTime, _spin;
        private int _dir = 1;
        public bool Out => _t < FlightTime;
        public Vector2 Head { get; private set; }
        public Vector2 Hand { get; private set; }
        public bool HitBoss;  // the golem only takes one hit per throw

        public void Load(ContentManager content)
        {
            _boomerang = content.Load<Texture2D>("Images/Gameplay/flail");
        }

        public void Throw(int dir) { _t = 0; _dir = dir; HitBoss = false; }

        // Follows his hand so it always comes back to him, even mid jump
        public void Update(float dt, Vector2 hand)
        {
            if (!Out) return;
            _t += dt;
            _spin += dt * 22f;
            Hand = hand;
            float a = MathHelper.TwoPi * MathF.Min(1, _t / FlightTime);
            Head = hand + new Vector2(_dir * Reach / 2 * (1 - MathF.Cos(a)), -MathF.Sin(a) * (a < MathHelper.Pi ? Up : Down));
        }

        // How far a point is from the chain (hand to boomerang), for the maxed chain cutting rocks
        public float ChainDistance(Vector2 point)
        {
            var line = Head - Hand;
            float along = MathHelper.Clamp(Vector2.Dot(point - Hand, line) / MathF.Max(1, line.LengthSquared()), 0, 1);
            return Vector2.Distance(point, Hand + line * along);
        }

        // Chain from his hand out to the spinning boomerang (cyan links once it cuts)
        public void Draw(SpriteBatch sb, Texture2D pixel, float alpha)
        {
            if (!Out) return;
            float length = Vector2.Distance(Hand, Head);
            var shine = ChainCuts ? new Color(120, 240, 255) : new Color(160, 170, 190);
            for (float d = 4; d < length - 12; d += 7)
            {
                var link = Vector2.Lerp(Hand, Head, d / length);
                sb.Draw(pixel, new Rectangle((int)link.X - 2, (int)link.Y - 2, 4, 4), new Color(60, 64, 80) * alpha);
                sb.Draw(pixel, new Rectangle((int)link.X - 1, (int)link.Y - 1, 2, 2), shine * alpha);
            }
            sb.Draw(_boomerang, Head, null, Color.White * alpha, _spin * _dir, new Vector2(_boomerang.Width / 2f, _boomerang.Height / 2f), 0.75f, SpriteEffects.None, 0f);
        }
    }
}
