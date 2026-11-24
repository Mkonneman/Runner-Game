using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Runner_Game
{
    /// <summary>
    /// Controller rumble. Low = the heavy left motor (thuds), high = the buzzy right one (ticks and zaps)
    /// Every event has its own little pattern of beats (a crack then rubble, a heartbeat after dying, a boing for mushrooms)
    /// Buzz makes a beat flutter instead of fading smooth. A stronger beat takes over a weaker one. The Rumble setting scales all of it (0 = off)
    /// </summary>
    public static class Rumble
    {
        private static float _low, _high, _left, _length, _buzz, _clock;
        private static bool _on;
        private static readonly List<(float At, float Low, float High, float Time, float Buzz)> _queue = new();  // beats waiting to start

        // (start, low, high, seconds, buzz per second)
        private static readonly Dictionary<string, (float At, float Low, float High, float Time, float Buzz)[]> Patterns = new()
        {
            // Menus and little stuff, quick ticks on the buzzy motor
            ["menu_move"] = new[] { (0f, 0f, 0.12f, 0.04f, 0f) },
            ["menu_pick"] = new[] { (0f, 0.2f, 0.3f, 0.05f, 0f), (0.07f, 0f, 0.3f, 0.05f, 0f) },  // tap tap
            ["jump"] = new[] { (0f, 0f, 0.12f, 0.06f, 0f) },
            ["double_jump"] = new[] { (0f, 0f, 0.15f, 0.04f, 0f), (0.05f, 0f, 0.22f, 0.05f, 0f) },  // hop hop
            ["gem"] = new[] { (0f, 0f, 0.2f, 0.03f, 0f), (0.05f, 0f, 0.28f, 0.04f, 0f) },  // sparkle
            ["diamond"] = new[] { (0f, 0f, 0.15f, 0.03f, 0f), (0.04f, 0f, 0.22f, 0.03f, 0f), (0.08f, 0.05f, 0.3f, 0.05f, 0f) },  // rising chime
            ["combo"] = new[] { (0f, 0.1f, 0.3f, 0.05f, 0f), (0.06f, 0.2f, 0.45f, 0.08f, 0f) },
            ["swing"] = new[] { (0f, 0.05f, 0.15f, 0.04f, 0f), (0.04f, 0.15f, 0.3f, 0.06f, 0f) },  // whoosh
            ["flail"] = new[] { (0f, 0.1f, 0.3f, 0.35f, 20f) },  // whirring out on the chain
            ["icicle"] = new[] { (0f, 0f, 0.35f, 0.03f, 0f), (0.05f, 0f, 0.25f, 0.03f, 0f), (0.1f, 0f, 0.15f, 0.03f, 0f) },  // tinkle
            ["mushroom"] = new[] { (0f, 0.45f, 0f, 0.06f, 0f), (0.06f, 0.15f, 0.35f, 0.08f, 0f), (0.14f, 0f, 0.2f, 0.06f, 0f) },  // boing, squish then spring
            ["heal"] = new[] { (0f, 0.1f, 0.1f, 0.15f, 0f), (0.12f, 0.18f, 0.18f, 0.3f, 8f) },  // warm swell
            ["achievement"] = new[] { (0f, 0.15f, 0.2f, 0.06f, 0f), (0.1f, 0.2f, 0.3f, 0.06f, 0f), (0.2f, 0.35f, 0.45f, 0.15f, 0f) },  // da da DA

            // Hits and breaks, the heavy motor joins in
            ["warning"] = new[] { (0f, 0.35f, 0f, 0.07f, 0f), (0.14f, 0.35f, 0f, 0.07f, 0f) },  // thump thump
            ["rock_break"] = new[] { (0f, 0.45f, 0.35f, 0.07f, 0f), (0.07f, 0.2f, 0.3f, 0.07f, 0f) },  // crunch
            ["wall_break"] = new[] { (0f, 0.75f, 0.5f, 0.1f, 0f), (0.1f, 0.35f, 0.25f, 0.25f, 25f) },  // crack, then rubble
            ["quake"] = new[] { (0f, 0.6f, 0.15f, 0.15f, 0f), (0.12f, 0.4f, 0.1f, 0.25f, 10f) },  // rolling shockwave
            ["shield_bonk"] = new[] { (0f, 0.1f, 0.45f, 0.05f, 0f), (0.06f, 0.25f, 0f, 0.08f, 0f) },  // hollow clonk
            ["beam"] = new[] { (0f, 0.1f, 0.35f, 0.15f, 35f) },
            ["laser"] = new[] { (0f, 0.25f, 0.7f, 0.35f, 40f) },  // zzzzap
            ["rocket"] = new[] { (0f, 0.15f, 0.25f, 0.08f, 0f), (0.08f, 0.05f, 0.3f, 0.1f, 0f) },
            ["rocket_boom"] = new[] { (0f, 0.8f, 0.7f, 0.15f, 0f), (0.15f, 0.4f, 0.2f, 0.2f, 18f) },
            ["vine_grab"] = new[] { (0f, 0.5f, 0.2f, 0.45f, 15f) },  // squeezing tremble
            ["hurt"] = new[] { (0f, 0.8f, 0.6f, 0.1f, 0f), (0.1f, 0.35f, 0.2f, 0.25f, 22f) },  // jolt then shaky
            ["golem_hit"] = new[] { (0f, 0.5f, 0.3f, 0.08f, 0f), (0.08f, 0.1f, 0.45f, 0.06f, 0f) },  // thud and chip
            ["slam"] = new[] { (0f, 1f, 0.3f, 0.25f, 0f), (0.28f, 0.55f, 0.1f, 0.2f, 0f), (0.5f, 0.3f, 0f, 0.2f, 0f) },  // boom and aftershocks
            ["golem_die"] = new[] { (0f, 1f, 1f, 0.3f, 0f), (0.3f, 0.8f, 0.4f, 0.2f, 0f), (0.55f, 0.6f, 0.2f, 0.2f, 0f), (0.8f, 0.45f, 0.1f, 0.25f, 0f), (1.1f, 0.3f, 0f, 0.4f, 12f) },  // crumbling apart
            ["death"] = new[] { (0f, 1f, 1f, 0.45f, 0f), (0.6f, 0.5f, 0f, 0.1f, 0f), (0.82f, 0.5f, 0f, 0.1f, 0f), (1.3f, 0.25f, 0f, 0.1f, 0f), (1.52f, 0.25f, 0f, 0.1f, 0f) },  // big hit, then a fading heartbeat
            ["test"] = new[] { (0f, 0.6f, 0.6f, 0.25f, 0f) },  // settings slider
        };

        // Plays a named pattern, false if there isn't one
        public static bool Play(string name)
        {
            if (Settings.Rumble <= 0 || !Patterns.TryGetValue(name, out var steps)) return false;
            foreach (var s in steps) _queue.Add((_clock + s.At, s.Low, s.High, s.Time, s.Buzz));
            return true;
        }

        public static void Pulse(float low, float high, float seconds, float buzz = 0)
        {
            if (Settings.Rumble <= 0) return;
            float now = _left > 0 ? (_low + _high) * _left / _length : 0;
            if (low + high < now) return;  // something bigger is already shaking
            _low = low; _high = high; _left = _length = seconds; _buzz = buzz;
        }

        public static void Update(float dt)
        {
            _clock += dt;
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (_queue[i].At <= _clock) { var s = _queue[i]; _queue.RemoveAt(i); Pulse(s.Low, s.High, s.Time, s.Buzz); }
            if (_left > 0)
            {
                _left -= dt;
                float k = MathF.Max(0, _left / _length) * Settings.Rumble;
                if (_buzz > 0) k *= 0.55f + 0.45f * MathF.Sin(_clock * _buzz * MathHelper.TwoPi);  // flutter
                GamePad.SetVibration(PlayerIndex.One, _low * k, _high * k);
                _on = true;
            }
            else if (_on) Stop();
        }

        public static void Stop()
        {
            _left = 0; _on = false;
            _queue.Clear();
            GamePad.SetVibration(PlayerIndex.One, 0, 0);
        }

        // The set the game calls by name
        public static void MenuMove() => Play("menu_move");
        public static void MenuPick() => Play("menu_pick");
        public static void Swing() => Play("swing");
        public static void Pickup() => Play("gem");
        public static void Warning() => Play("warning");
        public static void RockBreak() => Play("rock_break");
        public static void WallBreak() => Play("wall_break");
        public static void Laser() => Play("laser");
        public static void Hurt() => Play("hurt");
        public static void Slam() => Play("slam");
        public static void Death() { _queue.Clear(); Play("death"); }
    }
}
