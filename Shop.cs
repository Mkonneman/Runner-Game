using System;

namespace Runner_Game
{
    /// <summary>
    /// The upgrades the merchant sells
    /// </summary>
    public enum Upgrade { PowerDash, Dash, Heart, Hammer, Heal, Points }

    /// <summary>
    /// Gems and upgrade levels (kept in memory only, so closing the game resets the shop)
    /// </summary>
    public static class Shop
    {
        // Name, price of level 1 (each level costs that much more), the stat at each level, and how to show it (Same order as the enum!)
        public static readonly (string Name, int BasePrice, float[] Values, Func<float, string> Show)[] Items =
        {
            ("Power Dash", 150, new[] { 0f, 1f, 2f }, v => v == 0 ? "plain dash" : v == 1 ? "Smash Dash: 2x smash reach + points after a dash" : "Phase Dash: can't be hurt while dashing"),
            ("Quick Dash", 70, new[] { 2f, 1.7f, 1.45f, 1.2f }, v => $"{v:0.##}s dash cooldown"),
            ("Tough Heart", 100, new[] { 1f, 1.2f, 1.35f, 1.5f }, v => $"+{(v - 1) * 100:0}% health"),
            ("Heavy Hammer", 80, new[] { 3f, 2.6f, 2.3f, 2f }, v => $"{v:0.##}s smash cooldown"),
            ("Healing", 90, new[] { 0f, 0.15f, 0.2f, 0.25f }, v => v == 0 ? "no healing" : $"H heals {v * 100:0}% (150 score)"),
            ("Lucky Gem", 120, new[] { 1f, 1.15f, 1.3f, 1.5f }, v => $"+{(v - 1) * 100:0}% points"),
        };

        // 1 gem for every 10 points scored in a run
        public static int GemsFromScore(int score) => score / 10;

        public static int Gems { get; private set; }
        private static readonly int[] _levels = new int[Items.Length];

        public static int Level(Upgrade upgrade) => _levels[(int)upgrade];
        public static int MaxLevel(Upgrade upgrade) => Items[(int)upgrade].Values.Length - 1;
        public static bool Has(Upgrade upgrade) => Level(upgrade) > 0;

        // The stat this upgrade gives at its current level
        public static float Value(Upgrade upgrade) => Items[(int)upgrade].Values[Level(upgrade)];

        // Cost of the next level (level 2 is double, level 3 is triple and so on)
        public static int Price(Upgrade upgrade) => Items[(int)upgrade].BasePrice * (Level(upgrade) + 1);

        public static void AddGems(int gems) => Gems += gems;

        /// <summary>
        /// Buys the next level if it isn't maxed and there are enough gems
        /// </summary>
        public static bool Buy(Upgrade upgrade)
        {
            if (Level(upgrade) == MaxLevel(upgrade) || Gems < Price(upgrade)) return false;
            Gems -= Price(upgrade);
            _levels[(int)upgrade]++;
            return true;
        }
    }
}
