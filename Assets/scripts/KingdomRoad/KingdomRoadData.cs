using System.Collections.Generic;
using Siege.League;

namespace Siege.KingdomRoad
{
    public enum RewardKind { Coins, Cards, SilverChest, GoldChest, Badge }

    /// <summary>One trophy milestone reward on the Kingdom Road.</summary>
    public struct Milestone
    {
        public int threshold;
        public RewardKind kind;
        public string title;
        public int amount;
    }

    /// <summary>
    /// Predefined reward table. Used only by the editor build tool to create the prebuilt rows,
    /// and the values are then stored on each row (editable in the Inspector).
    ///
    /// Every division (27 = 9 leagues x 3) has 4 rewards at +25, +50, +75, +100 trophies from its base:
    ///   Wood I  : 25 / 50 / 75 / 100   (100 coins, Fire Arrows, 200 coins, badge)
    ///   Wood II : 125 / 150 / 175 / 200 (300 coins, Archer Cards x10, 25 Archer Cards, Silver Chest)
    ///   Wood III: 225 / 250 / 275 / 300 (300 coins, card pack, 500 coins, Gold Chest)
    ///   Later divisions follow a generated pattern (see Build), so rewards run 25 .. 2600 and Champions III is open-ended.
    /// Kings is top-100 only and has no trophy milestones.
    /// </summary>
    public static class KingdomRoadData
    {
        public const int DIVISION_COUNT = 27;   // 9 leagues x 3 divisions (Wood I .. Champions III)
        private static readonly string[] Roman = { "I", "II", "III" };

        public static int DivisionStart(int n) => n == 0 ? 0 : LeagueSystem.DIVISION_SIZE * n + 1;

        public static string DivisionName(int n) =>
            LeagueSystem.LeagueNames[n / LeagueSystem.DIVISIONS_PER_LEAGUE].ToUpperInvariant() + " " + Roman[n % LeagueSystem.DIVISIONS_PER_LEAGUE];

        public static List<Milestone> Build()
        {
            var list = new List<Milestone>();
            for (int n = 0; n < DIVISION_COUNT; n++)
            {
                int b = LeagueSystem.DIVISION_SIZE * n;
                if (n == 0)
                {
                    list.Add(M(b + 25, RewardKind.Coins, "100 Coins", 100));
                    list.Add(M(b + 50, RewardKind.Cards, "Fire Arrows", 1));
                    list.Add(M(b + 75, RewardKind.Coins, "200 Coins", 200));
                    list.Add(M(b + 100, RewardKind.Badge, "Division Badge", 1));
                }
                else if (n == 1)
                {
                    list.Add(M(b + 25, RewardKind.Coins, "300 Coins", 300));
                    list.Add(M(b + 50, RewardKind.Cards, "10 Archer Cards", 10));
                    list.Add(M(b + 75, RewardKind.Cards, "25 Archer Cards", 25));
                    list.Add(M(b + 100, RewardKind.SilverChest, "Silver Chest", 1));
                }
                else if (n == 2)
                {
                    list.Add(M(b + 25, RewardKind.Coins, "300 Coins", 300));
                    list.Add(M(b + 50, RewardKind.Cards, "Card Pack", 1));
                    list.Add(M(b + 75, RewardKind.Coins, "500 Coins", 500));
                    list.Add(M(b + 100, RewardKind.GoldChest, "Gold Chest", 1));
                }
                else
                {
                    bool lastDivision = n % LeagueSystem.DIVISIONS_PER_LEAGUE == LeagueSystem.DIVISIONS_PER_LEAGUE - 1;
                    list.Add(M(b + 25, RewardKind.Coins, (100 * (n + 1)) + " Coins", 100 * (n + 1)));
                    list.Add(M(b + 50, RewardKind.Cards, (5 + n) + " Cards", 5 + n));
                    list.Add(M(b + 75, RewardKind.Coins, (200 * (n + 1)) + " Coins", 200 * (n + 1)));
                    list.Add(lastDivision
                        ? M(b + 100, RewardKind.GoldChest, "Gold Chest", 1)
                        : M(b + 100, RewardKind.SilverChest, "Silver Chest", 1));
                }
            }
            return list;
        }

        private static Milestone M(int threshold, RewardKind kind, string title, int amount) =>
            new Milestone { threshold = threshold, kind = kind, title = title, amount = amount };
    }
}
