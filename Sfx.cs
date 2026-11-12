using System.Collections.Generic;
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

        // Tiny pitch wobble each time so repeats don't get annoying
        public static void Play(string name, float volume = 0.6f, float wobble = 0.1f)
        {
            if (_sounds.TryGetValue(name, out var s)) s.Play(volume, RandomHelper.NextFloat(-wobble, wobble), 0f);
        }
    }
}
