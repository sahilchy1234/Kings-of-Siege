using System;
using System.Globalization;
using Siege.Cards;
using UnityEngine;

namespace Siege.Profile
{
    /// <summary>One league tier shown as the coloured rank banner on the profile.</summary>
    public struct RankTier
    {
        public string title;        // "Captain"
        public string tierName;     // "Gold"
        public string label;        // "Gold II" (league + division)
        public int minTrophies;
        public int nextAt;          // trophies for the next division, or -1 at the top
        public string nextLabel;
        public int badgeIndex;      // 0..26 = league*3 + (division-1), 27 = Kings (see ProfileController.rankBadgeByDivision)
        public Color top, bottom, rim, text;
    }

    public struct ClanInfo
    {
        public bool hasClan;
        public string name;
        public string role;
        public int trophies;
        public int badge;   // index, free for a future clan system
    }

    /// <summary>
    /// Read/write access to everything the profile page shows. It only uses data that already exists in the
    /// game (PlayerPrefs keys + CardInventoryManager) and adds three small prefs of its own.
    ///
    /// Existing keys used:
    ///   "PUN_PlayerNickName" - player name (same key MainMenuManager saves and sends to Photon)
    ///   "trophies"           - trophy count (same key MainMenuManager reads)
    ///   CardInventoryManager - card levels / deck (level + XP are derived from it, there is no XP system yet)
    ///
    /// New keys (all prefixed KoS_Profile):
    ///   KoS_ProfileTag, KoS_ProfileAvatar, KoS_ProfileBestTrophies
    ///
    /// Clan: no clan system exists yet. Whatever sets these prefs shows up on the page, otherwise it reads "No Clan":
    ///   KoS_ClanName, KoS_ClanRole (Leader/Co-Leader/Elder/Member), KoS_ClanTrophies, KoS_ClanBadge
    /// </summary>
    public static class PlayerProfileData
    {
        public const string NAME_KEY = "PUN_PlayerNickName";
        public const string TROPHIES_KEY = "trophies";
        public const string TAG_KEY = "KoS_ProfileTag";
        public const string AVATAR_KEY = "KoS_ProfileAvatar";
        public const string BEST_TROPHIES_KEY = "KoS_ProfileBestTrophies";
        public const string CLAN_NAME_KEY = "KoS_ClanName";
        public const string CLAN_ROLE_KEY = "KoS_ClanRole";
        public const string CLAN_TROPHIES_KEY = "KoS_ClanTrophies";
        public const string CLAN_BADGE_KEY = "KoS_ClanBadge";

        public const int MAX_NAME_LENGTH = 14;
        public const int MIN_NAME_LENGTH = 2;
        public const int MAX_LEVEL = 50;

        public static event Action OnProfileChanged;

        // ───────────────────────────── Name / tag / avatar ─────────────────────────────

        public static string PlayerName
        {
            get
            {
                string n = PlayerPrefs.GetString(NAME_KEY, "");
                if (string.IsNullOrWhiteSpace(n))
                {
                    n = "Commander_" + UnityEngine.Random.Range(100, 1000);
                    PlayerPrefs.SetString(NAME_KEY, n);
                    PlayerPrefs.Save();
                }
                return n;
            }
        }

        public static bool TrySetPlayerName(string raw, out string error)
        {
            string n = (raw ?? "").Trim();
            if (n.Length < MIN_NAME_LENGTH) { error = "Name is too short"; return false; }
            if (n.Length > MAX_NAME_LENGTH) n = n.Substring(0, MAX_NAME_LENGTH);
            error = null;
            PlayerPrefs.SetString(NAME_KEY, n);
            PlayerPrefs.Save();
            try { Photon.Pun.PhotonNetwork.NickName = n; } catch (Exception) { /* Photon not ready: pref is enough */ }
            OnProfileChanged?.Invoke();
            return true;
        }

        /// <summary>Stable "#2PYLQ8GR" style id, generated once per install.</summary>
        public static string PlayerTag
        {
            get
            {
                string t = PlayerPrefs.GetString(TAG_KEY, "");
                if (string.IsNullOrEmpty(t))
                {
                    const string alphabet = "0289PYLQGRJCUV";
                    var chars = new char[8];
                    for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
                    t = "#" + new string(chars);
                    PlayerPrefs.SetString(TAG_KEY, t);
                    PlayerPrefs.Save();
                }
                return t;
            }
        }

        public static int AvatarIndex
        {
            get => PlayerPrefs.GetInt(AVATAR_KEY, 0);
            set
            {
                PlayerPrefs.SetInt(AVATAR_KEY, Mathf.Max(0, value));
                PlayerPrefs.Save();
                OnProfileChanged?.Invoke();
            }
        }

        // ───────────────────────────── Trophies / rank ─────────────────────────────

        public static int Trophies => Mathf.Max(0, PlayerPrefs.GetInt(TROPHIES_KEY, 0));

        /// <summary>Highest trophy count ever seen (kept up to date every time the profile is read).</summary>
        public static int BestTrophies
        {
            get
            {
                int cur = Trophies;
                int best = PlayerPrefs.GetInt(BEST_TROPHIES_KEY, 0);
                if (cur > best)
                {
                    best = cur;
                    PlayerPrefs.SetInt(BEST_TROPHIES_KEY, best);
                    PlayerPrefs.Save();
                }
                return best;
            }
        }

        public const int DIVISION_SIZE = Siege.League.LeagueSystem.DIVISION_SIZE;   // trophies per division (I, II, III)

        /// <summary>League + division for a trophy count, e.g. 750 -> "Gold II" (see LeagueSystem for the table).</summary>
        /// <param name="isTop100">True when the player is in the global top 100 (Kings league).</param>
        /// <summary>Rank badge slot for Kings (the 28th badge). Slots 0..26 are the 9 leagues x 3 divisions.</summary>
        public const int RANK_BADGE_KINGS_INDEX = 27;
        public const int RANK_BADGE_COUNT = 28;
        /// <summary>UIPACK/badges file names (no extension) in slot order: Wood 1 .. Champions 3, then Kings.</summary>
        public static readonly string[] RankBadgeFileNames =
        {
            "Wood 1", "Wood 2", "Wood 3",
            "Silver 1", "Silver 2", "Silver 3",
            "Gold 1", "Gold 2", "Gold 3",
            "Platinum 1", "Platinum 2", "Platinum 3",
            "Teal 1", "Teal 2", "Teal 3",
            "Epic 1", "Epic 2", "Epic 3",
            "Super 1", "Super 2", "Super 3",
            "Legend 1", "Legend 2", "Legend 3",
            "Champions 1", "Champions 2", "Champions 3",
            "Kings",
        };

        public static RankTier GetRank(int trophies, bool isTop100 = false)
        {
            var info = Siege.League.LeagueSystem.Get(trophies, isTop100);
            Siege.League.LeagueSystem.GetColors(info.leagueIndex, out Color top, out Color bottom, out Color rim);
            var t = new RankTier
            {
                title = info.league.ToUpperInvariant(),
                tierName = info.league,
                label = info.label,
                minTrophies = info.minTrophies,
                nextAt = -1,
                nextLabel = null,
                badgeIndex = info.division == 0 ? RANK_BADGE_KINGS_INDEX : info.leagueIndex * Siege.League.LeagueSystem.DIVISIONS_PER_LEAGUE + (info.division - 1),
                top = top, bottom = bottom, rim = rim, text = Color.white
            };
            if (info.division == 0) return t;          // Kings: no divisions, no next step
            if (info.maxTrophies == int.MaxValue) return t; // Champions III: top of the ladder

            t.nextAt = info.maxTrophies + 1;
            var next = Siege.League.LeagueSystem.Get(t.nextAt, isTop100);
            t.nextLabel = next.label;
            return t;
        }

        // ───────────────────────────── Battle record ─────────────────────────────

        public const string WINS_KEY = "KoS_ProfileWins";
        public const string LOSSES_KEY = "KoS_ProfileLosses";
        public const string DRAWS_KEY = "KoS_ProfileDraws";
        public const string THREE_STARS_KEY = "KoS_ProfileThreeStars";

        public static int Wins => PlayerPrefs.GetInt(WINS_KEY, 0);
        public static int Losses => PlayerPrefs.GetInt(LOSSES_KEY, 0);
        public static int Draws => PlayerPrefs.GetInt(DRAWS_KEY, 0);
        public static int ThreeStarWins => PlayerPrefs.GetInt(THREE_STARS_KEY, 0);
        public static int Matches => Wins + Losses + Draws;
        public static int WinRatePercent => Matches > 0 ? Mathf.RoundToInt(100f * Wins / Matches) : 0;

        public const int WIN_TROPHIES = 5;
        public const int DRAW_TROPHIES = 1;
        public const int LOSS_TROPHIES = 3;   // lost trophies (subtracted)

        public static int WinTrophies(int stars) => WIN_TROPHIES;

        /// <summary>Records a finished match and moves the trophy count. Returns the trophy change.</summary>
        public static int RecordMatch(bool victory, bool draw, int stars)
        {
            int before = Trophies;
            int delta = 0;
            if (draw)
            {
                PlayerPrefs.SetInt(DRAWS_KEY, Draws + 1);
                delta = DRAW_TROPHIES;
            }
            else if (victory)
            {
                PlayerPrefs.SetInt(WINS_KEY, Wins + 1);
                if (stars >= 3) PlayerPrefs.SetInt(THREE_STARS_KEY, ThreeStarWins + 1);
                delta = WIN_TROPHIES;
            }
            else
            {
                PlayerPrefs.SetInt(LOSSES_KEY, Losses + 1);
                delta = -LOSS_TROPHIES;
            }
            int after = Mathf.Max(0, before + delta);
            PlayerPrefs.SetInt(TROPHIES_KEY, after);
            if (after > PlayerPrefs.GetInt(BEST_TROPHIES_KEY, 0)) PlayerPrefs.SetInt(BEST_TROPHIES_KEY, after);
            PlayerPrefs.Save();
            OnProfileChanged?.Invoke();
            return after - before;
        }

        // ───────────────────────────── Coin rewards ─────────────────────────────

        public struct MatchReward
        {
            public int coins;
            public int gems;
        }

        /// <summary>
        /// Coin + gem reward for one match.
        ///   Win:  500 + max(0, 300 - elapsedSeconds) + destruction
        ///   Draw: 100 + destruction
        ///   Loss: destruction only if elapsed >= 60s, otherwise 0
        ///   Destruction: 0 structures = 0, 1 tower = 50, 2 towers = 100, enemy base = 200
        ///   Gems: win = 1, everything else = 0
        /// The optional ad doubles coins only (not trophies or gems); pass doubleCoins = true for that.
        /// </summary>
        public static MatchReward CalculateMatchReward(bool victory, bool draw, int elapsedSeconds, int towersDestroyed, bool baseDestroyed, bool doubleCoins = false)
        {
            int destruction = baseDestroyed ? 200 : Mathf.Clamp(towersDestroyed, 0, 2) * 50;
            int coins;
            int gems = 0;
            if (victory)
            {
                coins = 500 + Mathf.Max(0, 300 - Mathf.Max(0, elapsedSeconds)) + destruction;
                gems = 1;
            }
            else if (draw)
            {
                coins = 100 + destruction;
            }
            else
            {
                coins = elapsedSeconds >= 60 ? destruction : 0;
            }
            if (doubleCoins) coins *= 2;
            return new MatchReward { coins = coins, gems = gems };
        }

        // ───────────────────────────── King level ─────────────────────────────

        /// <summary>
        /// There is no XP system yet, so the King level is derived from card progress:
        /// every card found gives 20 XP and every card level-up gives 10 x rarity x new level XP.
        /// </summary>
        public static int ComputeXp(CardInventoryManager inv)
        {
            if (inv == null || inv.Library == null) return 0;
            int xp = 0;
            foreach (var e in inv.GetFoundCards())
            {
                var def = inv.Library.GetCard(e.cardId);
                xp += 20;
                if (def == null) continue;
                int mult = RarityMultiplier(def.rarity);
                for (int lvl = 2; lvl <= Mathf.Clamp(e.level, 1, CardDefinition.MAX_CARD_LEVEL); lvl++) xp += 10 * mult * lvl;
            }
            return xp;
        }

        private static int RarityMultiplier(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare: return 2;
                case CardRarity.Epic: return 3;
                case CardRarity.Legendary: return 4;
                default: return 1;
            }
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
        public static int XpForNextLevel(int level) => 150 + 100 * Mathf.Max(0, level - 1);

        public static void ComputeLevel(CardInventoryManager inv, out int level, out int xpInto, out int xpNeeded)
        {
            int xp = ComputeXp(inv);
            level = 1;
            while (level < MAX_LEVEL && xp >= XpForNextLevel(level))
            {
                xp -= XpForNextLevel(level);
                level++;
            }
            xpNeeded = XpForNextLevel(level);
            xpInto = level >= MAX_LEVEL ? xpNeeded : xp;
        }

        // ───────────────────────────── Clan ─────────────────────────────

        public static ClanInfo GetClan()
        {
            string n = PlayerPrefs.GetString(CLAN_NAME_KEY, "");
            var info = new ClanInfo { hasClan = !string.IsNullOrWhiteSpace(n), name = n };
            if (info.hasClan)
            {
                info.role = PlayerPrefs.GetString(CLAN_ROLE_KEY, "Member");
                info.trophies = Mathf.Max(0, PlayerPrefs.GetInt(CLAN_TROPHIES_KEY, 0));
                info.badge = PlayerPrefs.GetInt(CLAN_BADGE_KEY, 0);
            }
            return info;
        }

        public static string Format(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
    }
}
