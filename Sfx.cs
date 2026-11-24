using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace Runner_Game
{
    /// <summary>
    /// The little sound pack (jumps, pickups, hits, booms, menu clicks), loaded once and played from anywhere
    /// </summary>
    public static class Sfx
    {
        private static readonly Dictionary<string, SoundEffect> _sounds = new();

        public static void Load(ContentManager content)
        {
            foreach (var name in new[] { "jump", "double_jump", "gem", "diamond", "hurt", "golem_hit", "shield_bonk", "slam", "golem_die", "rocket", "rocket_boom", "beam", "heal", "achievement", "menu_move", "menu_pick" })
                _sounds[name] = content.Load<SoundEffect>("Audio/Sfx/Pack/" + name);
        }

        // Rumble that goes with each sound (low motor, high motor, seconds), so a sound and its buzz always match up
        private static readonly Dictionary<string, (float Low, float High, float Time)> _rumble = new()
        {
            ["menu_move"] = (0f, 0.12f, 0.05f), ["menu_pick"] = (0.2f, 0.3f, 0.08f),
            ["jump"] = (0f, 0.1f, 0.06f), ["double_jump"] = (0f, 0.18f, 0.08f),
            ["gem"] = (0f, 0.2f, 0.06f), ["diamond"] = (0.05f, 0.25f, 0.08f), ["heal"] = (0.15f, 0.15f, 0.2f),
            ["hurt"] = (0.75f, 0.55f, 0.28f), ["shield_bonk"] = (0.2f, 0.4f, 0.08f), ["golem_hit"] = (0.4f, 0.5f, 0.12f),
            ["slam"] = (1f, 0.3f, 0.4f), ["golem_die"] = (1f, 1f, 0.8f), ["beam"] = (0.1f, 0.3f, 0.1f),
            ["rocket"] = (0.2f, 0.3f, 0.1f), ["rocket_boom"] = (0.7f, 0.6f, 0.25f), ["achievement"] = (0.25f, 0.35f, 0.2f),
        };

        // Tiny pitch wobble each time so repeats don't get annoying. Menu clicks use the UI slider, everything else the Sound effects one
        // Menu sounds everywhere: high blip to pick / open, low blip to go back, quiet low tick when the selection moves
        public static void UiPick() => Play("menu_pick", 0.5f, 0f);
        public static void UiBack() => Play("menu_move", 0.6f, 0f);
        public static void UiMove() => Play("menu_move", 0.35f, 0f);

        public static void Play(string name, float volume = 0.6f, float wobble = 0.1f)
        {
            float slider = name.StartsWith("menu") ? Settings.UiVolume : Settings.SfxVolume;
            if (_sounds.TryGetValue(name, out var s)) s.Play(MathHelper.Clamp(volume * slider, 0, 1), RandomHelper.NextFloat(-wobble, wobble), 0f);
            if (_rumble.TryGetValue(name, out var r)) Rumble.Pulse(r.Low, r.High, r.Time);
        }
    }
}
