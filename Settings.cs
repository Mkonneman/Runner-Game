using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Runner_Game
{
    /// <summary>
    /// Player settings from the settings menu (saved with SaveSystem when the menu closes)
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

        // Sound effect volume from 0 to 1 (crash, smash, etc), every game sound multiplies by it (PlaySfx / Sfx.Play)
        public static float SfxVolume { get; set; } = 1f;

        // Menu clicks have their own slider
        public static float UiVolume { get; set; } = 1f;

        // How hard the screen shakes (0 = none, for anyone it makes queasy)
        public static float ShakeAmount { get; set; } = 1f;

        // Controller rumble strength (0 = off)
        public static float Rumble { get; set; } = 1f;

        // Couple frame freeze on big hits
        public static bool HitStop { get; set; } = true;

        // How visible the smash range ring is (0 turns it off)
        public static float SmashRingOpacity { get; set; } = 0.25f;

        // How much of the skin particle flair shows (0 turns it off)
        public static float SkinFxAmount { get; set; } = 1f;

        // Testing toggle, shop shows a pile of gems and buying is free
        public static bool InfiniteMoney { get; set; }

        // Hover Boots level 3: true = hold jump to hover, false = double jump (swapped in Gear)
        public static bool AirHover { get; set; }

        // "Equip it now?" popup after buying gear (its Don't show again button turns this off, Settings turns it back on)
        public static bool EquipPrompt { get; set; } = true;

        // Testing: where Play drops you (not saved, always back to the blue cave on launch)
        public static readonly string[] StartNames = { "Blue", "Orange", "Green", "Door", "Hall", "Boss" };
        public static int TestStart;
        public static bool ShowHitboxes;  // testing: outlines on the boss fight hitboxes (F3 flips it in the arena)
    }
}
