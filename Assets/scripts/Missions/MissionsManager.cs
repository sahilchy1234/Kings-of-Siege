using System;
using System.Collections.Generic;
using Siege.Shop;
using UnityEngine;

namespace Siege.Missions
{
    /// <summary>
    /// Mission catalog and progress.
    ///
    /// Catalog: a MissionsManager asset at Resources/Missions/MissionsManager.asset listing MissionData assets.
    /// When that asset is missing or empty, a built-in default set is used so the page always works.
    ///
    /// Progress (PlayerPrefs only):
    ///   KoS_Mission_Total_{stat}              lifetime totals, written by RecordMatchResult
    ///   KoS_Mission_Base_{stat}_{period}      total at the start of a daily/weekly period (progress = total - base)
    ///   KoS_Mission_Claim_{id}_{period}       1 when claimed
    ///   KoS_Season_Points                     Season Pass points
    /// Achievements measure lifetime totals and are claimed once.
    /// </summary>
    [CreateAssetMenu(fileName = "MissionsManager", menuName = "Kings of Siege/Missions/Missions Catalog")]
    public class MissionsManager : ScriptableObject
    {
        public const int SEASON_TARGET = 1000;

        public List<MissionData> missions = new List<MissionData>();

        private const string TOTAL_PREFIX = "KoS_Mission_Total_";
        private const string BASE_PREFIX = "KoS_Mission_Base_";
        private const string CLAIM_PREFIX = "KoS_Mission_Claim_";
        private const string SEASON_KEY = "KoS_Season_Points";

        private static MissionsManager catalog;
        private static List<MissionData> defaults;

        /// <summary>Raised after match results are recorded (not after claims; the screen refreshes those itself).</summary>
        public static event Action OnProgressChanged;

        // ───────────────────────────── Catalog ─────────────────────────────

        private static List<MissionData> All
        {
            get
            {
                if (catalog == null) catalog = Resources.Load<MissionsManager>("Missions/MissionsManager");
                if (catalog != null && catalog.missions != null && catalog.missions.Count > 0) return catalog.missions;
                if (defaults == null) defaults = BuildDefaults();
                return defaults;
            }
        }

        public static List<MissionData> GetMissions(MissionType type)
        {
            var list = new List<MissionData>();
            foreach (var m in All) if (m != null && m.type == type) list.Add(m);
            return list;
        }

        // ───────────────────────────── Periods ─────────────────────────────

        public static DateTime PeriodStart(MissionType type, DateTime now)
        {
            if (type == MissionType.Daily) return now.Date;
            if (type == MissionType.Weekly) return now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)); // Monday
            return DateTime.MinValue;
        }

        public static DateTime NextReset(MissionType type, DateTime now)
        {
            if (type == MissionType.Daily) return PeriodStart(type, now).AddDays(1);
            if (type == MissionType.Weekly) return PeriodStart(type, now).AddDays(7);
            return DateTime.MaxValue;
        }

        public static string PeriodKey(MissionType type, DateTime now)
        {
            return type == MissionType.Achievement ? "all" : PeriodStart(type, now).ToString("yyyyMMdd");
        }

        // ───────────────────────────── Progress ─────────────────────────────

        public static int Total(MissionStat stat) => PlayerPrefs.GetInt(TOTAL_PREFIX + stat, 0);

        public static int SeasonPoints => PlayerPrefs.GetInt(SEASON_KEY, 0);

        private static string BaseKey(MissionStat stat, MissionType type, DateTime now)
            => BASE_PREFIX + stat + "_" + PeriodKey(type, now);

        private static string ClaimKey(MissionData m, DateTime now)
            => CLAIM_PREFIX + m.Id + "_" + PeriodKey(m.type, now);

        /// <summary>Store a baseline for every stat's current period the first time it is seen. Call before changing totals.</summary>
        private static void EnsureBaselines(DateTime now)
        {
            foreach (MissionStat stat in Enum.GetValues(typeof(MissionStat)))
            {
                foreach (MissionType type in new[] { MissionType.Daily, MissionType.Weekly })
                {
                    string key = BaseKey(stat, type, now);
                    if (!PlayerPrefs.HasKey(key)) PlayerPrefs.SetInt(key, Total(stat));
                }
            }
        }

        /// <summary>Progress towards a mission in its current period, capped at the target.</summary>
        public static int Progress(MissionData m, DateTime now)
        {
            if (m == null) return 0;
            int baseline = 0;
            if (m.type != MissionType.Achievement)
            {
                EnsureBaselines(now);
                baseline = PlayerPrefs.GetInt(BaseKey(m.stat, m.type, now), 0);
            }
            return Mathf.Clamp(Total(m.stat) - baseline, 0, m.target);
        }

        public static bool IsClaimed(MissionData m, DateTime now) => m != null && PlayerPrefs.GetInt(ClaimKey(m, now), 0) == 1;

        public static bool CanClaim(MissionData m, DateTime now)
            => m != null && !IsClaimed(m, now) && Progress(m, now) >= m.target;

        public static int CountReady(MissionType type, DateTime now)
        {
            int n = 0;
            foreach (var m in GetMissions(type)) if (CanClaim(m, now)) n++;
            return n;
        }

        /// <summary>Pays out a completed mission and adds its Season Pass points. Returns false if it cannot be claimed.</summary>
        public static bool Claim(MissionData m, DateTime now)
        {
            if (!CanClaim(m, now)) return false;
            PlayerPrefs.SetInt(ClaimKey(m, now), 1);
            PlayerPrefs.SetInt(SEASON_KEY, SeasonPoints + m.SeasonPoints);
            PlayerPrefs.Save();
            if (m.coinReward > 0) PlayerWallet.AddCoins(m.coinReward);
            if (m.gemReward > 0) PlayerWallet.AddGems(m.gemReward);
            return true;
        }

        // ───────────────────────────── Recording ─────────────────────────────

        private static void Add(MissionStat stat, int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(TOTAL_PREFIX + stat, Total(stat) + amount);
        }

        /// <summary>
        /// Called once per finished match from ProfileProgress (the same place wins and trophies are recorded).
        /// </summary>
        public static void RecordMatchResult(bool victory, bool draw, int stars, int trophyGain, int towersDestroyed, bool baseDestroyed)
        {
            DateTime now = DateTime.Now;
            EnsureBaselines(now);

            Add(MissionStat.Matches, 1);
            if (draw) Add(MissionStat.Draws, 1);
            else if (victory)
            {
                Add(MissionStat.Wins, 1);
                if (stars >= 3) Add(MissionStat.ThreeStarWins, 1);
            }
            Add(MissionStat.TrophiesGained, trophyGain);
            Add(MissionStat.TowersDestroyed, towersDestroyed);
            if (baseDestroyed) Add(MissionStat.BasesDestroyed, 1);

            PlayerPrefs.Save();
            OnProgressChanged?.Invoke();
        }

        // ───────────────────────────── Defaults ─────────────────────────────

        private static List<MissionData> BuildDefaults()
        {
            return new List<MissionData>
            {
                Make("daily_play2", "Play 2 battles", MissionType.Daily, MissionStat.Matches, 2, MissionDifficulty.Easy, 80, 0, ""),
                Make("daily_win1", "Win 1 battle", MissionType.Daily, MissionStat.Wins, 1, MissionDifficulty.Medium, 150, 0, ""),
                Make("daily_towers2", "Destroy 2 enemy towers", MissionType.Daily, MissionStat.TowersDestroyed, 2, MissionDifficulty.Medium, 180, 0, ""),
                Make("weekly_win7", "Win 7 battles", MissionType.Weekly, MissionStat.Wins, 7, MissionDifficulty.Medium, 500, 0, "Silver Chest"),
                Make("weekly_three3", "Earn 3 three-star wins", MissionType.Weekly, MissionStat.ThreeStarWins, 3, MissionDifficulty.Hard, 600, 20, ""),
                Make("weekly_trophies30", "Gain 30 trophies", MissionType.Weekly, MissionStat.TrophiesGained, 30, MissionDifficulty.Medium, 400, 0, ""),
                Make("weekly_bases2", "Destroy 2 enemy castles", MissionType.Weekly, MissionStat.BasesDestroyed, 2, MissionDifficulty.Hard, 800, 0, "Gold Chest"),
                Make("ach_matches10", "Play 10 battles", MissionType.Achievement, MissionStat.Matches, 10, MissionDifficulty.Easy, 200, 0, ""),
                Make("ach_wins50", "Win 50 battles", MissionType.Achievement, MissionStat.Wins, 50, MissionDifficulty.Hard, 1500, 0, "Gold Chest"),
                Make("ach_towers100", "Destroy 100 enemy towers", MissionType.Achievement, MissionStat.TowersDestroyed, 100, MissionDifficulty.Hard, 1000, 0, ""),
                Make("ach_three10", "Earn 10 three-star wins", MissionType.Achievement, MissionStat.ThreeStarWins, 10, MissionDifficulty.Medium, 500, 30, ""),
                Make("ach_bases10", "Destroy 10 enemy castles", MissionType.Achievement, MissionStat.BasesDestroyed, 10, MissionDifficulty.Medium, 700, 0, "Silver Chest"),
            };
        }

        private static MissionData Make(string id, string goal, MissionType type, MissionStat stat, int target,
            MissionDifficulty difficulty, int coins, int gems, string chest)
        {
            var m = CreateInstance<MissionData>();
            m.hideFlags = HideFlags.DontSave;
            m.id = id;
            m.goal = goal;
            m.type = type;
            m.stat = stat;
            m.target = target;
            m.difficulty = difficulty;
            m.coinReward = coins;
            m.gemReward = gems;
            m.chestName = chest;
            return m;
        }
    }
}
