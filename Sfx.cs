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
        // Rumble patterns live in Rumble.cs under the same names as the sounds

        // Tiny pitch wobble each time so repeats don't get annoying. Menu clicks use the UI slider, everything else the Sound effects one
        // Menu sounds everywhere: high blip to pick / open, low blip to go back, quiet low tick when the selection moves
        public static void UiPick() => Play("menu_pick", 0.5f, 0f);
        public static void UiBack() => Play("menu_move", 0.6f, 0f);
        public static void UiMove() => Play("menu_move", 0.35f, 0f);

        public static void Play(string name, float volume = 0.6f, float wobble = 0.1f)
        {
            float slider = name.StartsWith("menu") ? Settings.UiVolume : Settings.SfxVolume;
            if (_sounds.TryGetValue(name, out var s)) s.Play(MathHelper.Clamp(volume * slider, 0, 1), RandomHelper.NextFloat(-wobble, wobble), 0f);
            Rumble.Play(name);
        }
    }
}
