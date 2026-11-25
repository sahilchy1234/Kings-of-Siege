using System;
using System.Globalization;
using UnityEngine;

namespace Siege.DailyRewards
{
    /// <summary>
    /// Data-only logic for the 7-day Daily Login Rewards streak.
    /// Stores progress in PlayerPrefs and never touches UI objects.
    ///
    /// Keys:
    ///   KoS_Daily_Claimed  - how many days of the current cycle are claimed (0..7)
    ///   KoS_Daily_LastClaim - date of the last claim, "yyyy-MM-dd" (local time)
    /// </summary>
    public static class DailyRewardsManager
    {
        public const int DayCount = 7;

        private const string KEY_CLAIMED = "KoS_Daily_Claimed";
        private const string KEY_LAST = "KoS_Daily_LastClaim";
        private const string DATE_FORMAT = "yyyy-MM-dd";

        /// <summary>
        /// false (default): missing a whole day resets the streak to Day 1.
        /// true: the streak keeps going after a missed day.
        /// Change this one line to switch behaviour.
        /// </summary>
        public static bool ContinueStreakOnMiss = false;

        public struct Status
        {
            /// <summary>0-based index of the day that is "today" (claimable, or already claimed).</summary>
            public int todayIndex;
            /// <summary>True when today's reward has not been claimed yet.</summary>
            public bool canClaim;
            /// <summary>Number of days claimed in the current cycle (0..7).</summary>
            public int claimedCount;
        }

        public static Status GetStatus()
        {
            int claimed = PlayerPrefs.GetInt(KEY_CLAIMED, 0);
            DateTime today = DateTime.Now.Date;

            if (!DateTime.TryParseExact(PlayerPrefs.GetString(KEY_LAST, ""), DATE_FORMAT,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime last))
            {
                claimed = 0; // never claimed
                return new Status { todayIndex = 0, canClaim = true, claimedCount = 0 };
            }

            int gap = (today - last).Days;
            if (gap == 0)
            {
                // Already claimed today.
                claimed = Mathf.Clamp(claimed, 1, DayCount);
                return new Status { todayIndex = claimed - 1, canClaim = false, claimedCount = claimed };
            }

            // A new day has started.
            if (gap > 1 && !ContinueStreakOnMiss) claimed = 0;
            if (claimed >= DayCount) claimed = 0; // full week done, start the next cycle
            return new Status { todayIndex = claimed, canClaim = true, claimedCount = claimed };
        }

        /// <summary>
        /// Marks today's reward as claimed. Returns false if nothing is claimable.
        /// The caller grants the reward; this only records the state.
        /// </summary>
        public static bool TryClaim(out int dayIndex)
        {
            Status s = GetStatus();
            dayIndex = s.todayIndex;
            if (!s.canClaim) return false;

            PlayerPrefs.SetInt(KEY_CLAIMED, s.todayIndex + 1);
            PlayerPrefs.SetString(KEY_LAST, DateTime.Now.Date.ToString(DATE_FORMAT, CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Debug: forget all streak progress.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KEY_CLAIMED);
            PlayerPrefs.DeleteKey(KEY_LAST);
            PlayerPrefs.Save();
        }

        /// <summary>Debug: pretend the last claim was yesterday so tomorrow's reward unlocks.</summary>
        public static void DebugAdvanceOneDay()
        {
            if (!DateTime.TryParseExact(PlayerPrefs.GetString(KEY_LAST, ""), DATE_FORMAT,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime last)) return;
            PlayerPrefs.SetString(KEY_LAST, last.AddDays(-1).ToString(DATE_FORMAT, CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }
    }
}
