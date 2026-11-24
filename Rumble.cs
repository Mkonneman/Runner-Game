using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Runner_Game
{
    /// <summary>
    /// Controller rumble. Low = the heavy left motor (thuds), high = the buzzy right one (ticks and zaps)
    /// Each pulse fades out over its time, a stronger pulse takes over a weaker one. The Rumble setting scales all of it (0 = off)
    /// </summary>
    public static class Rumble
    {
        private static float _low, _high, _left, _length;
        private static bool _on;

        public static void Pulse(float low, float high, float seconds)
        {
            if (Settings.Rumble <= 0) return;
            float now = _left > 0 ? (_low + _high) * _left / _length : 0;
            if (low + high < now) return;  // something bigger is already shaking
            _low = low; _high = high; _left = _length = seconds;
        }

        public static void Update(float dt)
        {
            if (_left > 0)
            {
                _left -= dt;
                float k = MathF.Max(0, _left / _length) * Settings.Rumble;
                GamePad.SetVibration(PlayerIndex.One, _low * k, _high * k);
                _on = true;
            }
            else if (_on) Stop();
        }

        public static void Stop()
        {
            _left = 0; _on = false;
            GamePad.SetVibration(PlayerIndex.One, 0, 0);
        }

        // The set, smallest to biggest
        public static void MenuMove() => Pulse(0f, 0.12f, 0.05f);
        public static void MenuPick() => Pulse(0.2f, 0.3f, 0.08f);
        public static void Swing() => Pulse(0.1f, 0.25f, 0.07f);
        public static void Pickup() => Pulse(0f, 0.25f, 0.07f);
        public static void Warning() => Pulse(0.25f, 0f, 0.2f);
        public static void RockBreak() => Pulse(0.35f, 0.3f, 0.12f);
        public static void WallBreak() => Pulse(0.55f, 0.45f, 0.18f);
        public static void Laser() => Pulse(0.3f, 0.6f, 0.3f);
        public static void Hurt() => Pulse(0.75f, 0.55f, 0.28f);
        public static void Slam() => Pulse(1f, 0.3f, 0.4f);
        public static void Death() => Pulse(1f, 1f, 0.6f);
    }
}
