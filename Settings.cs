using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Runner_Game
{
    /// <summary>
    /// Player settings from the settings menu (only kept in memory like the shop, might save these to a file later)
    /// </summary>
    public static class Settings
    {
        // Music volume from 0 to 1 (the title and shop fades stop at this instead of full)
        private static float _musicVolume = 1f;
        public static float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = value;
                MediaPlayer.Volume = value;
            }
        }

        // Sound effect volume from 0 to 1 (crash, smash, etc)
        public static float SfxVolume
        {
            get => SoundEffect.MasterVolume;
            set => SoundEffect.MasterVolume = value;
        }

        // How visible the smash range ring is (0 turns it off)
        public static float SmashRingOpacity { get; set; } = 0.25f;
    }
}
