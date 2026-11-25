using UnityEngine;

namespace Siege.League
{
    /// <summary>Result of a league lookup for a trophy count.</summary>
    public struct LeagueInfo
    {
        public string league;      // "Silver"
        public int division;       // 1, 2 or 3 (0 = no divisions, e.g. Kings)
        public string label;       // "Silver II", "Kings"
        public int minTrophies;    // first trophy count of this division
        public int maxTrophies;    // last trophy count of this division (int.MaxValue for open-ended)
        public int leagueIndex;    // 0 = Wood ... 8 = Champions, 9 = Kings
    }

    /// <summary>
    /// League / division table.
    ///   Wood: I 0-100, II 101-200, III 201-300
    ///   Silver: I 301-400, II 401-500, III 501-600
    ///   ... every league has 3 divisions of 100 trophies.
    ///   Champions: I 2401-2500, II 2501-2600, III 2601+
    ///   Kings: global top 100 only, no divisions (needs a leaderboard rank, see isTop100).
    /// </summary>
    public static class LeagueSystem
    {
        public const int DIVISION_SIZE = 100;
        public const int DIVISIONS_PER_LEAGUE = 3;

        public static readonly string[] LeagueNames =
        {
            "Wood", "Silver", "Gold", "Platinum", "Tiel", "Epic", "Super", "Legend", "Champions", "Kings"
        };

        private static readonly string[] Roman = { "I", "II", "III" };

        /// <summary>Accent colours per league (top, bottom, rim) for the rank banner.</summary>
        private static readonly Color[,] Palette =
        {
            { new Color(0.93f, 0.62f, 0.34f), new Color(0.55f, 0.29f, 0.12f), new Color(0.35f, 0.17f, 0.05f) }, // Wood
            { new Color(0.88f, 0.92f, 0.98f), new Color(0.47f, 0.54f, 0.66f), new Color(0.24f, 0.29f, 0.40f) }, // Silver
            { new Color(1.00f, 0.86f, 0.30f), new Color(0.88f, 0.55f, 0.06f), new Color(0.50f, 0.28f, 0.02f) }, // Gold
            { new Color(0.55f, 0.95f, 0.90f), new Color(0.12f, 0.62f, 0.66f), new Color(0.04f, 0.32f, 0.36f) }, // Platinum
            { new Color(0.55f, 0.82f, 1.00f), new Color(0.16f, 0.42f, 0.86f), new Color(0.05f, 0.16f, 0.46f) }, // Tiel
            { new Color(0.85f, 0.62f, 1.00f), new Color(0.46f, 0.20f, 0.80f), new Color(0.22f, 0.06f, 0.44f) }, // Epic
            { new Color(1.00f, 0.60f, 0.70f), new Color(0.86f, 0.20f, 0.36f), new Color(0.44f, 0.06f, 0.16f) }, // Super
            { new Color(1.00f, 0.78f, 0.45f), new Color(0.92f, 0.36f, 0.10f), new Color(0.46f, 0.14f, 0.02f) }, // Legend
            { new Color(0.95f, 0.50f, 1.00f), new Color(0.58f, 0.16f, 0.82f), new Color(0.28f, 0.05f, 0.45f) }, // Champions
            { new Color(1.00f, 0.95f, 0.55f), new Color(0.95f, 0.70f, 0.10f), new Color(0.55f, 0.35f, 0.02f) }, // Kings
        };

        public const int KINGS_TOP_N = 100;

        /// <summary>
        /// League + division for a trophy count. Pass isTop100 = true when the player is in the global top 100
        /// (Kings league, no divisions) and the trophy count is at least the Champions threshold.
        /// </summary>
        public static LeagueInfo Get(int trophies, bool isTop100 = false)
        {
            trophies = Mathf.Max(0, trophies);

            // Global division index: 0 = Wood I ... 26 = Champions III (9 leagues x 3 divisions).
            const int LAST_DIVISION = 8 * DIVISIONS_PER_LEAGUE + DIVISIONS_PER_LEAGUE - 1; // 26
            int g = trophies <= 0 ? 0 : Mathf.Min((trophies - 1) / DIVISION_SIZE, LAST_DIVISION);

            int leagueIdx = g / DIVISIONS_PER_LEAGUE;          // 0..8, never Kings here
            int div = g % DIVISIONS_PER_LEAGUE + 1;            // 1..3

            // Division starts: Wood I begins at 0, every other division at 100*g + 1.
            int min = g == 0 ? 0 : DIVISION_SIZE * g + 1;
            int max = g == 0 ? DIVISION_SIZE : min + DIVISION_SIZE - 1;
            if (g == LAST_DIVISION) max = int.MaxValue;        // Champions III is open-ended

            var info = new LeagueInfo
            {
                league = LeagueNames[leagueIdx],
                division = div,
                label = LeagueNames[leagueIdx] + " " + Roman[div - 1],
                minTrophies = min,
                maxTrophies = max,
                leagueIndex = leagueIdx,
            };

            if (isTop100 && trophies >= 2401)
            {
                info.league = LeagueNames[LeagueNames.Length - 1];
                info.division = 0;
                info.label = info.league;
                info.leagueIndex = LeagueNames.Length - 1;
                info.minTrophies = 2401;
                info.maxTrophies = int.MaxValue;
            }
            return info;
        }

        /// <summary>Banner colours for a league index (0..9).</summary>
        public static void GetColors(int leagueIndex, out Color top, out Color bottom, out Color rim)
        {
            int i = Mathf.Clamp(leagueIndex, 0, Palette.GetLength(0) - 1);
            top = Palette[i, 0];
            bottom = Palette[i, 1];
            rim = Palette[i, 2];
        }
    }
}
