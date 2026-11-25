using System;
using System.Collections.Generic;
using Siege.Profile;
using UnityEngine;

namespace Siege.Rankings
{
    /// <summary>Which leaderboard to show.</summary>
    public enum RankingBoard { Global, Local, Clans }

    /// <summary>One row on a leaderboard: a player, or a clan when isClan is true.</summary>
    [Serializable]
    public class RankedEntry
    {
        public string id;
        public string name;
        public string subtitle;      // clan name for players, "42/50 members" for clans
        public int trophies;
        public int rank;             // 1-based
        public int avatarIndex;      // index into the avatar list (same order as the Profile page)
        public int level;            // king level (players only)
        public int flagIndex = -1;   // index into RankingsArt.flags, -1 = none
        public int clanBadge;        // colour index of the clan shield (clans only)
        public int memberCount;      // clans only
        public bool isClan;
        public bool isLocal;         // the local player / the local player's clan
    }

    /// <summary>Everything one tab of the Rankings page needs.</summary>
    public class LeaderboardResult
    {
        public RankingBoard board;
        public List<RankedEntry> entries = new List<RankedEntry>();
        /// <summary>The local player (or clan) with their real rank, shown pinned at the bottom. Null = not ranked.</summary>
        public RankedEntry self;
        public int seasonNumber;
        public DateTime seasonEndsUtc;
        public string regionName;    // Local board only
    }

    /// <summary>
    /// The Rankings page reads all of its data through this interface.
    /// To use a real backend: RankingsController.Instance.Provider = new MyServerRankingsProvider();
    /// The callback may run later (async); the page ignores answers that arrive after the user switched tabs.
    /// </summary>
    public interface IRankingsDataProvider
    {
        void GetLeaderboard(RankingBoard board, int count, Action<LeaderboardResult> onComplete);
    }

    /// <summary>
    /// Offline mock with believable random players. Boards are generated from fixed seeds, so the same
    /// players appear every time. The local player is placed by their real trophy count.
    /// </summary>
    public class MockRankingsDataProvider : IRankingsDataProvider
    {
        public const int AVATAR_COUNT = 15;   // matches ProfileSetupTool's avatar list
        public const int FLAG_COUNT = 16;     // matches RankingsSetupTool's flag list
        public const int CLAN_BADGE_COUNT = 8;
        private const int SEASON_DAYS = 28;

        private static readonly string[] Prefixes =
        {
            "Sir", "Lady", "Lord", "King", "Queen", "Baron", "Duke", "Iron", "Storm", "Shadow",
            "Golden", "Red", "Grim", "Wild", "Brave", "Dark", "Swift", "Royal"
        };

        private static readonly string[] Cores =
        {
            "Arthur", "Brienne", "Cedric", "Draven", "Elara", "Fenwick", "Gareth", "Helga", "Ivar", "Jorah",
            "Kael", "Lyra", "Magnus", "Nyx", "Osric", "Percival", "Quinn", "Rowan", "Sigrid", "Tristan",
            "Ulric", "Vera", "Wulf", "Ysolde", "Zane", "Bjorn", "Leif", "Mara", "Thane", "Edric",
            "Rook", "Falk", "Odo", "Hilda", "Bran", "Sable", "Torin", "Wren", "Aldric", "Merek"
        };

        private static readonly string[] Suffixes =
        {
            "99", "X", "07", "TheBold", "Rex", "II", "Pro", "88", "_YT", "777", "Prime", "21"
        };

        private static readonly string[] ClanWordA =
        {
            "Iron", "Royal", "Crimson", "Silver", "Storm", "Golden", "Shadow", "Dragon", "Lion", "Raven", "Frost", "Ember"
        };

        private static readonly string[] ClanWordB =
        {
            "Crown", "Legion", "Wardens", "Vanguard", "Order", "Knights", "Banner", "Keep", "Guard", "Brotherhood", "Siege", "Throne"
        };

        public void GetLeaderboard(RankingBoard board, int count, Action<LeaderboardResult> onComplete)
        {
            count = Mathf.Clamp(count, 3, 200);
            var result = new LeaderboardResult { board = board };
            GetSeason(out result.seasonNumber, out result.seasonEndsUtc);

            switch (board)
            {
                case RankingBoard.Global: BuildPlayers(result, count, 1001, 3950, 4, 22, 180000f, 520f, true); break;
                case RankingBoard.Local:
                    result.regionName = "Your Region";
                    BuildPlayers(result, count, 2002, 2650, 8, 30, 9000f, 600f, false);
                    break;
                default: BuildClans(result, count); break;
            }

            onComplete?.Invoke(result);
        }

        // ───────────────────────────── Players ─────────────────────────────

        private static void BuildPlayers(LeaderboardResult result, int count, int seed, int topTrophies,
            int stepMin, int stepMax, float rankScale, float rankFalloff, bool showFlags)
        {
            var rng = new System.Random(seed);
            var used = new HashSet<string>();
            int trophies = topTrophies;
            int prevAvatar = -1, prevAvatar2 = -1;

            for (int i = 0; i < count; i++)
            {
                if (i > 0) trophies -= rng.Next(stepMin, stepMax + 1);
                int avatar;
                do { avatar = rng.Next(0, AVATAR_COUNT); } while (avatar == prevAvatar || avatar == prevAvatar2);
                prevAvatar2 = prevAvatar;
                prevAvatar = avatar;
                int level = Mathf.Clamp(50 - i / 4 - rng.Next(0, 4), 12, 50);
                result.entries.Add(new RankedEntry
                {
                    id = "p" + seed + "_" + i,
                    name = MakePlayerName(rng, used),
                    subtitle = rng.Next(0, 7) == 0 ? "No Clan" : MakeClanName(rng),
                    trophies = Mathf.Max(0, trophies),
                    avatarIndex = avatar,
                    level = level,
                    flagIndex = showFlags ? rng.Next(0, FLAG_COUNT) : -1,
                });
            }

            var self = MakeLocalPlayer();
            int lowest = result.entries[result.entries.Count - 1].trophies;
            if (self.trophies > lowest)
            {
                // Good enough for the board: replace the last entry and sort the player in
                result.entries.RemoveAt(result.entries.Count - 1);
                result.entries.Add(self);
                result.entries.Sort((a, b) => b.trophies.CompareTo(a.trophies));
            }

            for (int i = 0; i < result.entries.Count; i++) result.entries[i].rank = i + 1;

            if (!result.entries.Contains(self))
                self.rank = Mathf.Max(count + 1, Mathf.RoundToInt(rankScale * Mathf.Exp(-self.trophies / rankFalloff)));
            result.self = self;
        }

        private static RankedEntry MakeLocalPlayer()
        {
            int level = 1;
            try
            {
                var inv = Siege.Cards.CardInventoryManager.Instance;
                if (inv != null) PlayerProfileData.ComputeLevel(inv, out level, out _, out _);
            }
            catch (Exception) { level = 1; }

            string clan = PlayerPrefs.GetString(PlayerProfileData.CLAN_NAME_KEY, "");
            return new RankedEntry
            {
                id = "local",
                name = PlayerProfileData.PlayerName,
                subtitle = string.IsNullOrEmpty(clan) ? "No Clan" : clan,
                trophies = PlayerProfileData.Trophies,
                avatarIndex = Mathf.Clamp(PlayerProfileData.AvatarIndex, 0, AVATAR_COUNT - 1),
                level = Mathf.Max(1, level),
                flagIndex = -1,
                isLocal = true,
            };
        }

        private static string MakePlayerName(System.Random rng, HashSet<string> used)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                string core = Cores[rng.Next(Cores.Length)];
                string n;
                switch (rng.Next(0, 4))
                {
                    case 0: n = Prefixes[rng.Next(Prefixes.Length)] + " " + core; break;
                    case 1: n = core + Suffixes[rng.Next(Suffixes.Length)]; break;
                    case 2: n = Prefixes[rng.Next(Prefixes.Length)] + core; break;
                    default: n = core; break;
                }
                if (used.Add(n)) return n;
            }
            string fallback = Cores[rng.Next(Cores.Length)] + rng.Next(100, 999);
            used.Add(fallback);
            return fallback;
        }

        private static string MakeClanName(System.Random rng)
        {
            return ClanWordA[rng.Next(ClanWordA.Length)] + " " + ClanWordB[rng.Next(ClanWordB.Length)];
        }

        // ───────────────────────────── Clans ─────────────────────────────

        private static void BuildClans(LeaderboardResult result, int count)
        {
            var rng = new System.Random(3003);
            var used = new HashSet<string>();
            int trophies = 64000;

            for (int i = 0; i < count; i++)
            {
                if (i > 0) trophies -= rng.Next(300, 1100);
                string name;
                int guard = 0;
                do { name = MakeClanName(rng); } while (!used.Add(name) && ++guard < 30);
                if (guard >= 30) { name += " " + (i + 1); used.Add(name); }

                int members = rng.Next(32, 51);
                result.entries.Add(new RankedEntry
                {
                    id = "c" + i,
                    name = name,
                    subtitle = members + "/50 members",
                    trophies = Mathf.Max(0, trophies),
                    memberCount = members,
                    clanBadge = rng.Next(0, CLAN_BADGE_COUNT),
                    flagIndex = rng.Next(0, FLAG_COUNT),
                    isClan = true,
                });
            }
            for (int i = 0; i < result.entries.Count; i++) result.entries[i].rank = i + 1;

            string localClan = PlayerPrefs.GetString(PlayerProfileData.CLAN_NAME_KEY, "");
            if (string.IsNullOrEmpty(localClan)) { result.self = null; return; }

            int clanTrophies = PlayerPrefs.GetInt(PlayerProfileData.CLAN_TROPHIES_KEY, 0);
            result.self = new RankedEntry
            {
                id = "local_clan",
                name = localClan,
                subtitle = "Your clan",
                trophies = clanTrophies,
                rank = Mathf.Max(count + 1, Mathf.RoundToInt(60000f * Mathf.Exp(-clanTrophies / 9000f))),
                clanBadge = Mathf.Abs(PlayerPrefs.GetInt(PlayerProfileData.CLAN_BADGE_KEY, 0)) % CLAN_BADGE_COUNT,
                memberCount = 12,
                isClan = true,
                isLocal = true,
            };
        }

        // ───────────────────────────── Season ─────────────────────────────

        /// <summary>28-day seasons counted from 2026-01-05 (a Monday), UTC.</summary>
        public static void GetSeason(out int number, out DateTime endsUtc)
        {
            var epoch = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            double days = (DateTime.UtcNow - epoch).TotalDays;
            int index = Mathf.Max(0, (int)Math.Floor(days / SEASON_DAYS));
            number = index + 1;
            endsUtc = epoch.AddDays((index + 1) * SEASON_DAYS);
        }
    }
}
