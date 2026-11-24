using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace Runner_Game
{
    /// <summary>
    /// Everything that gets saved (gems, gear and settings)
    /// </summary>
    public class SaveData
    {
        public int Gems { get; set; }
        public int[] UpgradeLevels { get; set; }   // the old shop, only read to convert old saves
        public int[] ItemLevels { get; set; }
        public string[] Equipped { get; set; }
        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public float UiVolume { get; set; } = 1f;
        public float ShakeAmount { get; set; } = 1f;
        public float Rumble { get; set; } = 1f;
        public bool HitStop { get; set; } = true;
        public int BestScore { get; set; }
        public int[] TopScores { get; set; }
        public int[] TopMeters { get; set; }
        public float SmashRingOpacity { get; set; } = 0.25f;
        public float SkinFxAmount { get; set; } = 1f;
        public bool InfiniteMoney { get; set; }
        public bool AirHover { get; set; }
        public bool EquipPrompt { get; set; } = true;
        public string[] KeyBindings { get; set; }
        public int WallDeaths { get; set; }       // for the "Wall: N, You: 0" pun
        public int Checkpoint { get; set; }       // checkpoints were removed, kept so old saves still load
        public string[] Achievements { get; set; }
        public Dictionary<string, int> Stats { get; set; }       // furthest cave reached (0 blue, 1 orange, 2 green)
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
                Gems = Shop.RealGems,
                ItemLevels = Shop.Levels(),
                Equipped = Shop.EquippedNames(),
                MusicVolume = Settings.MusicVolume,
                SfxVolume = Settings.SfxVolume,
                UiVolume = Settings.UiVolume,
                ShakeAmount = Settings.ShakeAmount,
                Rumble = Settings.Rumble,
                HitStop = Settings.HitStop,
                BestScore = RunStats.Best,
                TopScores = RunStats.TopScores,
                TopMeters = RunStats.TopMeters,
                SmashRingOpacity = Settings.SmashRingOpacity,
                SkinFxAmount = Settings.SkinFxAmount,
                InfiniteMoney = Settings.InfiniteMoney,
                AirHover = Settings.AirHover,
                EquipPrompt = Settings.EquipPrompt,
                KeyBindings = Controls.Save(),
                Checkpoint = CaveZones.Checkpoint,
                WallDeaths = Screens.GameOverScreen.WallDeaths,
                Achievements = Runner_Game.Achievements.SaveUnlocked(),
                Stats = Runner_Game.Achievements.SaveStats(),
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

                // Saves from before the gear update get their old upgrades turned into gear
                Shop.Load(data.Gems, data.ItemLevels ?? Shop.FromOldUpgrades(data.UpgradeLevels), data.Equipped);
                Settings.MusicVolume = Math.Clamp(data.MusicVolume, 0, 1);
                Settings.SfxVolume = Math.Clamp(data.SfxVolume, 0, 1);
                Settings.UiVolume = Math.Clamp(data.UiVolume, 0, 1);
                Settings.ShakeAmount = Math.Clamp(data.ShakeAmount, 0, 1);
                Settings.Rumble = Math.Clamp(data.Rumble, 0, 1);
                Settings.HitStop = data.HitStop;
                RunStats.Best = Math.Max(0, data.BestScore);
                RunStats.TopScores = data.TopScores ?? (RunStats.Best > 0 ? new[] { RunStats.Best } : Array.Empty<int>());  // older saves only had the best
                RunStats.TopMeters = data.TopMeters ?? Array.Empty<int>();
                Settings.SmashRingOpacity = Math.Clamp(data.SmashRingOpacity, 0, 1);
                Settings.SkinFxAmount = Math.Clamp(data.SkinFxAmount, 0, 1);
                Settings.InfiniteMoney = data.InfiniteMoney;
                Settings.AirHover = data.AirHover;
                Settings.EquipPrompt = data.EquipPrompt;
                Controls.Load(data.KeyBindings);
                CaveZones.Checkpoint = Math.Clamp(data.Checkpoint, 0, CaveZones.Count - 1);
                Screens.GameOverScreen.WallDeaths = Math.Max(0, data.WallDeaths);
                Runner_Game.Achievements.Load(data.Achievements, data.Stats);
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Wipes the gems and upgrades (settings stay how they are) and saves right away
        /// </summary>
        public static void ResetPlayerData()
        {
            Shop.Load(0, null, null);
            CaveZones.Checkpoint = 0;
            Screens.GameOverScreen.WallDeaths = 0;
            RunStats.Best = RunStats.SeedScore;  // back to the dev's run to beat
            RunStats.TopScores = new[] { RunStats.SeedScore }; RunStats.TopMeters = new[] { RunStats.SeedMeters };
            Runner_Game.Achievements.Load(null, null);
            Save();
        }
    }
}
