using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// Area name banner that drops in from the top when you reach a new cave, bounces a little, floats, then drifts up and fades
    /// </summary>
    public static class Banner
    {
        // In order: the 3 caves, then the golem's lair (shown when the hallway starts)
        public const int Lair = 3;
        private static readonly string[] Files = { "banner_blue", "banner_ember", "banner_moss", "banner_lair" };
        private static Texture2D[] _art;

        private const float DropTime = 0.5f, HoldTime = 1.1f, FadeTime = 0.45f, RestY = 62f, Scale = 0.7f;
        private static Texture2D _showing;
        private static float _t = 99f;

        public static void Load(ContentManager content) => _art = Array.ConvertAll(Files, f => content.Load<Texture2D>("Images/HUD/" + f));

        public static void Show(int which, float delay = 0) { _showing = _art[which]; _t = -delay; }  // negative time = still waiting to drop
        public static void Update(float dt) => _t += dt;

        public static void Draw(SpriteBatch spriteBatch, float alpha)
        {
            if (_showing == null || _t < 0 || _t > DropTime + HoldTime + FadeTime) return;
            float y = RestY - 180 * MathF.Exp(-7f * _t) * MathF.Cos(_t * MathHelper.TwoPi * 1.4f);  // springs down from off screen
            y += 4 * MathF.Sin(MathF.Max(0, _t - DropTime) * 3f);  // bobs gently while it hangs there
            float gone = MathHelper.Clamp((_t - DropTime - HoldTime) / FadeTime, 0, 1);
            y -= 24 * gone * gone;  // floats up as it fades
            float fade = alpha * (1 - gone);
            var size = new Vector2(_showing.Width, _showing.Height) * Scale;
            spriteBatch.Draw(_showing, new Vector2(Game1.VirtualWidth / 2f - size.X / 2, y), null, Color.White * fade, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
        }
    }
}
