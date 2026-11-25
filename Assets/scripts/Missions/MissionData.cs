using UnityEngine;

namespace Siege.Missions
{
    public enum MissionType { Daily, Weekly, Achievement }

    /// <summary>The running total a mission measures. Totals are recorded by MissionsManager.RecordMatchResult.</summary>
    public enum MissionStat { Matches, Wins, Draws, ThreeStarWins, TrophiesGained, TowersDestroyed, BasesDestroyed }

    public enum MissionDifficulty { Easy, Medium, Hard }

    /// <summary>
    /// One mission. Create with Assets > Create > Kings of Siege > Missions > Mission, then add it to the
    /// MissionsManager catalog asset (Resources/Missions/MissionsManager).
    /// </summary>
    [CreateAssetMenu(fileName = "Mission", menuName = "Kings of Siege/Missions/Mission")]
    public class MissionData : ScriptableObject
    {
        [Tooltip("Unique id used to save claim state. Keep it stable once shipped.")]
        public string id = "new_mission";
        [TextArea] public string goal = "Win 1 battle";
        public MissionType type = MissionType.Daily;
        public MissionStat stat = MissionStat.Wins;
        [Min(1)] public int target = 1;
        public MissionDifficulty difficulty = MissionDifficulty.Easy;
        [Min(0)] public int coinReward = 100;
        [Min(0)] public int gemReward;
        [Tooltip("Optional chest shown in the reward column, e.g. 'Silver Chest'.")]
        public string chestName = "";
        [Tooltip("Optional icon. A letter badge is shown when empty.")]
        public Sprite icon;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        /// <summary>Season Pass points granted when the mission is claimed.</summary>
        public int SeasonPoints => difficulty == MissionDifficulty.Easy ? 10 : difficulty == MissionDifficulty.Medium ? 20 : 40;

        /// <summary>Difficulty ring colour: green easy, blue medium, purple hard.</summary>
        public Color RingColor
        {
            get
            {
                switch (difficulty)
                {
                    case MissionDifficulty.Easy: return new Color(0.30f, 0.92f, 0.32f);
                    case MissionDifficulty.Medium: return new Color(0.22f, 0.62f, 1f);
                    default: return new Color(0.72f, 0.38f, 1f);
                }
            }
        }
    }
}
