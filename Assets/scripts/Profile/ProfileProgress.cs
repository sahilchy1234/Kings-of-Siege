using Siege.Cards;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Siege.Profile
{
    /// <summary>
    /// Makes the profile "real" without touching existing scripts:
    ///  - In every scene with a GameManager it listens to GameManager.OnMatchFinished and records the result
    ///    (trophies, wins / losses / draws, 3-star wins) through PlayerProfileData.RecordMatch.
    ///    Win = +5, draw = +1, loss = -3 trophies (never below 0). Values live in PlayerProfileData.
    ///  - In the main menu it keeps the home league banner (object "leauge": "ranktext" + "ranktext (1)")
    ///    in sync with the real rank and trophy count.
    /// Starts itself automatically after a scene loads (no scene setup needed).
    /// </summary>
    public class ProfileProgress : MonoBehaviour
    {
        private static ProfileProgress instance;
        private GameManager hooked;
        private TMP_Text rankText, trophyText, topBarName, levelText;
        private int lastLevel = -1;
        private UnityEngine.UI.Image leagueBadge;
        private int lastBadgeIndex = -1;
        private TMP_InputField menuNameInput;
        private string lastRank, lastName;
        private int lastTrophies = -1;
        private float timer;

        /// <summary>Trophy change of the last finished match (for result screens that want to show it).</summary>
        public static int LastTrophyDelta { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance == null)
            {
                var go = new GameObject("[ProfileProgress]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ProfileProgress>();
            }
            instance.Rescan();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayerProfileData.OnProfileChanged += UpdateBanner;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PlayerProfileData.OnProfileChanged -= UpdateBanner;
        }
        private void OnSceneLoaded(Scene s, LoadSceneMode m) { Rescan(); }

        private void Rescan()
        {
            // Match results
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != hooked)
            {
                if (hooked != null) hooked.OnMatchFinished -= HandleMatchFinished;
                hooked = gm;
                if (hooked != null) hooked.OnMatchFinished += HandleMatchFinished;
            }

            // Home league banner
            rankText = trophyText = topBarName = levelText = null;
            lastLevel = -1;
            leagueBadge = null;
            menuNameInput = null;
            lastRank = lastName = null;
            lastTrophies = -1;
            lastBadgeIndex = -1;
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.transform.parent == null) continue;
                string parent = t.transform.parent.name;
                // Home top bar player name (Canvas/Panel/toppanel/avataricon/playername)
                if (t.name == "playername" && string.Equals(parent, "avataricon", System.StringComparison.OrdinalIgnoreCase)) { topBarName = t; continue; }
                if (!string.Equals(parent, "leauge", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (t.name == "ranktext") rankText = t;
                else if (t.name == "ranktext (1)") trophyText = t;
            }
            // King level number in the top bar (the badge under avataricon, next to the player name)
            if (topBarName != null) levelText = FindKingLevelText(topBarName.transform.parent);

            // Home league banner badge image: the same 28-slot badge as the rank (name contains badge or shield)
            foreach (var root in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!string.Equals(root.name, "leauge", System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var img in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    string n = img.name.ToLowerInvariant();
                    if (img.transform == root) continue;
                    if (n.Contains("badge") || n.Contains("shield")) { leagueBadge = img; break; }
                }
                if (leagueBadge != null) break;
            }
            // Nickname field on the matchmaking card (its text is copied into the match intro)
            foreach (var f in FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.name == "NicknameInputField") { menuNameInput = f; break; }
            UpdateBanner();
        }

        private void HandleMatchFinished(GameManager.MatchResult result, int stars)
        {
            bool victory = result == GameManager.MatchResult.Victory;
            bool draw = result == GameManager.MatchResult.Draw;
            LastTrophyDelta = PlayerProfileData.RecordMatch(victory, draw, stars);

            int elapsed = 0, towers = 0;
            bool baseDown = false;
            if (hooked != null) hooked.GetMatchStats(out elapsed, out towers, out baseDown);
            // Missions page: wins, draws, 3-star wins, trophies gained, towers and castles destroyed.
            Siege.Missions.MissionsManager.RecordMatchResult(victory, draw, stars, LastTrophyDelta, towers, baseDown);
            var reward = PlayerProfileData.CalculateMatchReward(victory, draw, elapsed, towers, baseDown);
            PlayerWallet.AddCoins(reward.coins);
            if (reward.gems > 0) PlayerWallet.AddGems(reward.gems);
            LastCoinReward = reward.coins;
        }

        /// <summary>Coins earned by the last finished match (before any ad doubling).</summary>
        public static int LastCoinReward { get; private set; }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            UpdateBanner();
        }

        private void UpdateBanner()
        {
            string playerName = PlayerProfileData.PlayerName;
            if (playerName != lastName)
            {
                if (topBarName != null) topBarName.text = playerName;
                if (menuNameInput != null && menuNameInput.text != playerName) menuNameInput.SetTextWithoutNotify(playerName);
                lastName = playerName;
            }

            if (levelText != null)
            {
                PlayerProfileData.ComputeLevel(CardInventoryManager.Instance, out int level, out _, out _);
                if (level != lastLevel) { levelText.text = level.ToString(); lastLevel = level; }
            }

            if (rankText == null && trophyText == null) return;
            int trophies = PlayerProfileData.Trophies;
            var tier = PlayerProfileData.GetRank(trophies);
            if (leagueBadge != null && tier.badgeIndex != lastBadgeIndex)
            {
                var pc = Object.FindFirstObjectByType<ProfileController>(FindObjectsInactive.Include);
                var sprite = pc != null ? pc.RankSpriteFor(tier.badgeIndex) : null;
                if (sprite != null)
                {
                    leagueBadge.sprite = sprite;
                    leagueBadge.preserveAspect = true;
                    lastBadgeIndex = tier.badgeIndex;   // retry next tick until the profile controller exists
                }
            }
            string rank = tier.label.ToUpperInvariant();
            if (rankText != null && rank != lastRank) { rankText.text = rank; lastRank = rank; }
            if (trophyText != null && trophies != lastTrophies) { trophyText.text = PlayerProfileData.Format(trophies); lastTrophies = trophies; }
        }

        /// <summary>Finds the level number text under the top bar avatar: a name containing level/lvl, else a text that is only digits.</summary>
        private static TMP_Text FindKingLevelText(Transform avatarRoot)
        {
            if (avatarRoot == null) return null;
            TMP_Text numeric = null;
            foreach (var t in avatarRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == "playername") continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("level") || n.Contains("lvl")) return t;
                if (numeric == null && IsDigits(t.text)) numeric = t;
            }
            return numeric;
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string v = s.Trim();
            if (v.Length == 0) return false;
            foreach (char c in v) if (!char.IsDigit(c)) return false;
            return true;
        }

        private void OnDestroy()
        {
            if (hooked != null) hooked.OnMatchFinished -= HandleMatchFinished;
            if (instance == this) instance = null;
        }
    }
}
