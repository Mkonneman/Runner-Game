using System;
using System.IO;
using System.Text.Json;

namespace Runner_Game
{
    /// <summary>
    /// Everything that gets saved (gems, upgrades and settings)
    /// </summary>
    public class SaveData
    {
        public int Gems { get; set; }
        public int[] UpgradeLevels { get; set; } = Array.Empty<int>();
        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public float SmashRingOpacity { get; set; } = 0.25f;
    }

    /// <summary>
    /// Saves and loads the game to a JSON file in AppData (so rebuilding the game doesnt wipe it)
    /// </summary>
    public static class SaveSystem
    {
        private static readonly string SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RockRush", "save.json");

        /// <summary>
        /// Writes the current gems, upgrades and settings to the save file
        /// </summary>
        public static void Save()
        {
            var data = new SaveData
            {
                Gems = Shop.Gems,
                UpgradeLevels = Shop.Levels(),
                MusicVolume = Settings.MusicVolume,
                SfxVolume = Settings.SfxVolume,
                SmashRingOpacity = Settings.SmashRingOpacity,
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { }  // Couldnt save (file locked or something), the game keeps going anyway
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Loads the save file if there is one, a missing or broken file just starts fresh
        /// </summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return;
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return;

                Shop.Load(data.Gems, data.UpgradeLevels);
                Settings.MusicVolume = Math.Clamp(data.MusicVolume, 0, 1);
                Settings.SfxVolume = Math.Clamp(data.SfxVolume, 0, 1);
                Settings.SmashRingOpacity = Math.Clamp(data.SmashRingOpacity, 0, 1);
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Wipes the gems and upgrades (settings stay how they are) and saves right away
        /// </summary>
        public static void ResetPlayerData()
        {
            Shop.Load(0, Array.Empty<int>());
            Save();
        }
    }
}
