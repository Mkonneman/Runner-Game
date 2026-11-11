using System;
using System.Linq;

namespace Runner_Game
{
    /// <summary>
    /// The 3 gear slots, one item equipped in each
    /// </summary>
    public enum Slot { Weapon, Ability, Armor, Skin }  // Skin is just looks, picked in the shop (not in Gear)

    /// <summary>
    /// Every item in the game (append new ones at the end so old saves still line up!)
    /// </summary>
    public enum ItemId
    {
        Hammer, Pickaxe, QuakeHammer, RocketLauncher,
        Dash, PatchUp, BubbleShield, TimeSlow,
        HazardHat, MinersVest, MirrorHelm, SpringBoots, CrystalPlate, HoverBoots, LuckyCharm, PhoenixHeart,
        Drill,
        SkinExplorer, SkinTraveller, SkinThief, SkinSoldier, SkinNinja, SkinHazmat, SkinSpacesuit
    }

    /// <summary>
    /// One item the merchant can sell
    /// </summary>
    public class Item
    {
        public ItemId Id;
        public string Name;
        public Slot Slot;
        public string Icon;           // texture path, swap these for your own art
        public int Price;             // gems to buy it (0 = you start with it)
        public int UpgradePrice;      // each level costs this times the level you're on
        public float[] Values;        // the stat at level 1, 2, 3... (one value = can't be upgraded)
        public Func<float, string> Show;
        public bool InShop = true;    // flip this to hide it from the shop (for unlocking stuff later on)
        public int MaxLevel => Values.Length;
        public bool StartsOwned => Price == 0;
    }

    /// <summary>
    /// Gems, what you own, what level it's at, and what you have equipped
    /// </summary>
    public static class Shop
    {
        // Grouped by slot, order here does not matter (might rebalance prices later)
        public static readonly Item[] Items = new Item[]
        {
            // Weapons (what Smash does)
            new() { Id = ItemId.Hammer, Name = "Hammer", Slot = Slot.Weapon, Icon = "Images/Items/hammer", Price = 0, UpgradePrice = 15,
                    Values = new[] { 3f, 2.5f, 2.1f, 1.7f }, Show = v => $"Smash walls and rocks, {v:0.#}s cooldown" },
            new() { Id = ItemId.Pickaxe, Name = "Pickaxe", Slot = Slot.Weapon, Icon = "Images/Items/pickaxe", Price = 50, UpgradePrice = 40,
                    Values = new[] { 1.4f, 1.15f, 0.9f }, Show = v => $"Quick, breaks spikes too, reach grows each level, {v:0.##}s cooldown" },
            new() { Id = ItemId.QuakeHammer, Name = "Quake Hammer", Slot = Slot.Weapon, Icon = "Images/Items/quake_hammer", Price = 200, UpgradePrice = 100,
                    Values = new[] { 6f, 5f, 3f }, Show = v => $"Huge swing, shockwave on the ground, {v:0.#}s" },

            // Abilities (on the Ability key)
            new() { Id = ItemId.Dash, Name = "Dash", Slot = Slot.Ability, Icon = "Images/Items/dash", Price = 0, UpgradePrice = 30,
                    Values = new[] { 2f, 1.7f, 1.45f, 1.2f }, Show = v => $"Quick burst sideways, {v:0.##}s cooldown" },
            new() { Id = ItemId.PatchUp, Name = "Heal Locket", Slot = Slot.Ability, Icon = "Images/Items/heal_locket", Price = 50, UpgradePrice = 50,
                    Values = new[] { 0.25f, 0.35f, 0.5f }, Show = v => $"Heals {v * 100:0}% health, 20s cooldown" },
            new() { Id = ItemId.BubbleShield, Name = "Bubble Shield", Slot = Slot.Ability, Icon = "Images/Items/bubble_shield", Price = 150, UpgradePrice = 70,
                    Values = new[] { 3f, 4f, 5f }, Show = v => $"Blocks the next hit for {v:0}s, 15s cooldown" },
            new() { Id = ItemId.TimeSlow, Name = "Time Slow", Slot = Slot.Ability, Icon = "Images/Items/time_slow", Price = 250, UpgradePrice = 100,
                    Values = new[] { 2.5f, 3.5f, 4.5f }, Show = v => $"Half speed world for {v:0.#}s, 20s cooldown" },
            // negative cooldown = homing rockets (the last upgrade)
            new() { Id = ItemId.RocketLauncher, Name = "Rocket Launcher", Slot = Slot.Ability, Icon = "Images/Items/rocket_launcher", Price = 300, UpgradePrice = 130,
                    Values = new[] { 8f, 6.5f, 5f, -5f }, Show = v => v < 0 ? $"Homing rockets, {-v:0.#}s cooldown" : $"Rocket blows up the next wall or rock, {v:0.#}s" },

            // Armor (always on)
            new() { Id = ItemId.HazardHat, Name = "Hazard Hat", Slot = Slot.Armor, Icon = "Images/Items/hazard_hat", Price = 0, UpgradePrice = 30,
                    Values = new[] { 0.2f, 0.15f, 0.1f }, Show = v => $"Rocks only take {v * 100:0}% health" },
            new() { Id = ItemId.MinersVest, Name = "Miner's Vest", Slot = Slot.Armor, Icon = "Images/Items/miners_vest", Price = 50, UpgradePrice = 50,
                    Values = new[] { 1.25f, 1.5f, 1.75f }, Show = v => $"+{(v - 1) * 100:0}% max health" },
            new() { Id = ItemId.MirrorHelm, Name = "Mirror Helm", Slot = Slot.Armor, Icon = "Images/Items/mirror_helm", Price = 200, UpgradePrice = 150,
                    Values = new[] { 0.35f, 0.6f, 1f }, Show = v => $"Lasers bounce off you and break stuff ahead, beam lasts {v:0.##}s" + (v >= 1 ? ", hold Left to tilt it up" : "") },
            new() { Id = ItemId.SpringBoots, Name = "Spring Boots", Slot = Slot.Armor, Icon = "Images/Items/spring_boots", Price = 50, UpgradePrice = 40,
                    Values = new[] { 1.15f, 1.3f, 1.45f }, Show = v => $"Hold jump to go {(v - 1) * 100:0}% higher, tap for a hop" },
            new() { Id = ItemId.CrystalPlate, Name = "Crystal Plate", Slot = Slot.Armor, Icon = "Images/Items/crystal_plate", Price = 150, UpgradePrice = 70,
                    Values = new[] { 0.3f, 0.15f, 0f }, Show = v => v == 0 ? "Spikes do nothing to you" : $"Spikes only take {v * 100:0}% health" },
            new() { Id = ItemId.HoverBoots, Name = "Hover Boots", Slot = Slot.Armor, Icon = "Images/Items/hover_boots", Price = 60, UpgradePrice = 60,
                    Values = new[] { 1f, 2f, 3f }, Show = v => v switch { 1 => "Float right over holes in the floor", 2 => "Float over holes, jump again in the air", _ => "Floats over holes, double jump or hold jump to hover (swap in Gear)" } },
            new() { Id = ItemId.LuckyCharm, Name = "Lucky Charm", Slot = Slot.Armor, Icon = "Images/Items/lucky_charm", Price = 50, UpgradePrice = 70,
                    Values = new[] { 1.25f, 1.5f, 2f }, Show = v => $"+{(v - 1) * 100:0}% gems from runs" },
            new() { Id = ItemId.PhoenixHeart, Name = "Phoenix Heart", Slot = Slot.Armor, Icon = "Images/Items/phoenix_heart", Price = 250, UpgradePrice = 250,
                    Values = new[] { 0.25f, 0.5f }, Show = v => $"Come back once per run with {v * 100:0}% health" },
            // Drill is NOT a playable concept yet (fuel + cracking needs more design), hidden from the shop and gear till it's figured out
            // All its code is still in GameplayScreen (UpdateDrill, DrillTarget, DrawDrillCracks), flip InShop back to true to test it
            new() { Id = ItemId.Drill, Name = "Drill", Slot = Slot.Weapon, Icon = "Images/Items/drill", Price = 250, UpgradePrice = 100, InShop = false,
                    Values = new[] { 0.8f, 1.1f, 1.4f, 2.2f }, Show = v => v > 2 ? $"Drills twice as fast, {v:0.#}s of fuel" : $"Hold to drill through stuff, {v:0.#}s of fuel" },

            // Skins (cosmetic, from the Platformer Hero Pack by Drasnus), the explorer is the one you start as
            Skin(ItemId.SkinExplorer, "Explorer", "explorer", 0),
            Skin(ItemId.SkinTraveller, "Traveller", "traveller", 150),
            Skin(ItemId.SkinThief, "Thief", "thief", 200),
            Skin(ItemId.SkinSoldier, "Soldier", "soldier", 250),
            Skin(ItemId.SkinNinja, "Ninja", "ninja", 300),
            Skin(ItemId.SkinHazmat, "Hazmat", "hazmat", 350),
            Skin(ItemId.SkinSpacesuit, "Spacesuit", "spacesuit", 500),
        }.OrderBy(i => i.Id).ToArray();

        private static Item Skin(ItemId id, string name, string key, int price) => new()
        {
            Id = id, Name = name, Slot = Slot.Skin, Icon = "Images/Items/skin_" + key, Price = price, UpgradePrice = 0,
            Values = new[] { 1f }, Show = _ => "Cosmetic, just changes how you look"
        };

        // Folder under Images/Skins for the equipped skin's run + moves sheets
        public static string SkinFolder(ItemId? id) => "Images/Skins/" + (id ?? ItemId.SkinExplorer).ToString().Substring(4).ToLower();  // sorted so Items[(int)id] always works no matter how the list above is grouped

        public static Item Get(ItemId id) => Items[(int)id];

        private static int _gems;
        public static int Gems { get => Settings.InfiniteMoney ? 999999 : _gems; private set => _gems = value; }
        public static int RealGems => _gems;  // what actually gets saved
        private static readonly int[] _levels = new int[Items.Length];      // 0 = not owned yet
        private static readonly ItemId?[] _equipped = { ItemId.Hammer, ItemId.Dash, ItemId.HazardHat, ItemId.SkinExplorer };  // null = empty slot (put away in Gear)

        public static int Level(ItemId id) => _levels[(int)id];
        public static bool Owns(ItemId id) => Level(id) > 0;
        public static bool IsMaxed(ItemId id) => Level(id) >= Get(id).MaxLevel;
        public static ItemId? Equipped(Slot slot) => _equipped[(int)slot];
        public static bool IsEquipped(ItemId id) => Equipped(Get(id).Slot) == id;

        // The stat at the current level (level 1 if you don't own it, so the shop can preview it)
        public static float Value(ItemId id) => Get(id).Values[Math.Max(0, Level(id) - 1)];

        // Gems to buy it, or to upgrade it once you own it
        // Pickaxe reach, longer every level
        public static float PickReach => 68f + 22f * Math.Max(0, Level(ItemId.Pickaxe) - 1);

        // Sets up the Hover Boots air move on the runner (level 2 = double jump, level 3 = pick double jump or hover in Gear)
        public static void ApplyBoots(Runner runner)
        {
            if (!IsEquipped(ItemId.HoverBoots) || Level(ItemId.HoverBoots) < 2) return;
            bool hover = Level(ItemId.HoverBoots) >= 3 && Settings.AirHover;
            runner.DoubleJump = !hover;
            runner.AirHover = hover;
        }

        public static int Price(ItemId id) => Owns(id) ? Get(id).UpgradePrice * Level(id) : Get(id).Price;

        // 1 gem for every 10 points scored in a run (more with the Lucky Charm on)
        public static int GemsFromScore(int score) => (int)(score / 10 * (IsEquipped(ItemId.LuckyCharm) ? Value(ItemId.LuckyCharm) : 1f));

        public static void AddGems(int gems)
        {
            _gems += gems;
            SaveSystem.Save();
        }

        /// <summary>
        /// Buys it if you don't have it, otherwise buys the next level (false if maxed or too poor)
        /// </summary>
        public static bool Buy(ItemId id)
        {
            if (IsMaxed(id) || Gems < Price(id)) return false;
            if (!Settings.InfiniteMoney) Gems -= Price(id);
            _levels[(int)id]++;
            SaveSystem.Save();
            Achievements.Add("buys");
            if (IsMaxed(id) && Get(id).MaxLevel > 1) Achievements.Unlock("maxed");
            return true;
        }

        public static void Unequip(Slot slot)
        {
            _equipped[(int)slot] = null;
            SaveSystem.Save();
        }

        // Achievement rewards hand out items for free
        public static void Give(ItemId id) { if (!Owns(id)) { _levels[(int)id] = 1; SaveSystem.Save(); } }

        public static void Equip(ItemId id)
        {
            if (!Owns(id)) return;
            if (Get(id).Slot == Slot.Skin && id != ItemId.SkinExplorer) Achievements.Unlock("skin");
            _equipped[(int)Get(id).Slot] = id;
            SaveSystem.Save();
        }

        // For the save file
        public static int[] Levels() => (int[])_levels.Clone();
        public static string[] EquippedNames() => Array.ConvertAll(_equipped, e => e?.ToString() ?? "None");  // one per slot, in slot order

        /// <summary>
        /// Loads gems, levels and equipped gear from a save (anything weird gets fixed so you always have your starter gear)
        /// </summary>
        public static void Load(int gems, int[] levels, string[] equipped)
        {
            Gems = Math.Max(0, gems);
            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = levels != null && i < levels.Length ? Math.Clamp(levels[i], 0, Items[i].MaxLevel) : 0;
                if (Items[i].StartsOwned) _levels[i] = Math.Max(1, _levels[i]);
            }

            _equipped[0] = ItemId.Hammer; _equipped[1] = ItemId.Dash; _equipped[2] = ItemId.HazardHat; _equipped[3] = ItemId.SkinExplorer;
            if (equipped == null) return;
            for (int slot = 0; slot < Math.Min(_equipped.Length, equipped.Length); slot++)
            {
                if (equipped[slot] == "None") _equipped[slot] = null;
                else if (Enum.TryParse(equipped[slot], out ItemId id) && Owns(id) && Get(id).InShop && (int)Get(id).Slot == slot) _equipped[slot] = id;
            }
        }

        /// <summary>
        /// Turns a save from the old 6 upgrade shop into the new gear (keeps what lines up)
        /// </summary>
        public static int[] FromOldUpgrades(int[] old)
        {
            // old order was Phase Dash, Quick Dash, Tough Heart, Heavy Hammer, Healing, Lucky Gem (level 0 = default)
            int Old(int i) => old != null && i < old.Length ? old[i] : 0;
            var levels = new int[Items.Length];
            levels[(int)ItemId.Dash] = 1 + Old(1);
            levels[(int)ItemId.Hammer] = 1 + Old(3);
            levels[(int)ItemId.HazardHat] = 1;
            levels[(int)ItemId.MinersVest] = Old(2);
            levels[(int)ItemId.PatchUp] = Old(4);
            levels[(int)ItemId.LuckyCharm] = Old(5);
            return levels;
        }

        // Quick self check that the table lines up with the enum (runs once at startup in debug)
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Check()
        {
            for (int i = 0; i < Items.Length; i++)
                System.Diagnostics.Debug.Assert((int)Items[i].Id == i, $"Shop item {Items[i].Name} is out of order");
            System.Diagnostics.Debug.Assert(Items.Length == Enum.GetValues<ItemId>().Length, "every ItemId needs a shop entry");
            System.Diagnostics.Debug.Assert(Items.Where(i => i.StartsOwned).Select(i => i.Slot).Distinct().Count() == 4, "one starter item per slot");
        }
    }
}
