using System;
using UnityEngine;

namespace Siege.Shop
{
    /// <summary>
    /// Single source of truth for the player's soft (Coins) and hard (Gems) currency.
    /// Uses the same PlayerPrefs keys the rest of the game already reads
    /// ("PlayerCoins" / "PlayerGems"), so ChestSystemController, CardInventoryManager and
    /// CardsPageController all stay in sync without any changes.
    ///
    /// Also fixes the starting-coin mismatch: before the first scene loads, a brand new
    /// save gets PlayerCoins = 12,500 written explicitly, so every script that reads the
    /// key (whatever default it passes) sees the same canonical value.
    /// </summary>
    public static class PlayerWallet
    {
        public const string COINS_KEY = "PlayerCoins";
        public const string GEMS_KEY = "PlayerGems";

        /// <summary>Canonical starting coins for a fresh save.</summary>
        public const int STARTING_COINS = 12500;
        /// <summary>Starting gems for a fresh save (matches the shop mock-up).</summary>
        public const int STARTING_GEMS = 1280;

        /// <summary>(oldValue, newValue)</summary>
        public static event Action<int, int> OnCoinsChanged;
        /// <summary>(oldValue, newValue)</summary>
        public static event Action<int, int> OnGemsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SeedDefaultsBeforeSceneLoad()
        {
            EnsureSeeded();
        }

        /// <summary>Writes the canonical starting values if this save has never stored them.</summary>
        public static void EnsureSeeded()
        {
            bool dirty = false;
            if (!PlayerPrefs.HasKey(COINS_KEY))
            {
                PlayerPrefs.SetInt(COINS_KEY, STARTING_COINS);
                dirty = true;
            }
            if (!PlayerPrefs.HasKey(GEMS_KEY))
            {
                PlayerPrefs.SetInt(GEMS_KEY, STARTING_GEMS);
                dirty = true;
            }
            if (dirty) PlayerPrefs.Save();
        }

        public static int Coins => PlayerPrefs.GetInt(COINS_KEY, STARTING_COINS);
        public static int Gems => PlayerPrefs.GetInt(GEMS_KEY, STARTING_GEMS);

        public static bool CanAffordCoins(int amount) => Coins >= amount;
        public static bool CanAffordGems(int amount) => Gems >= amount;

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;
            int old = Coins;
            int now = Mathf.Max(0, old + amount);
            PlayerPrefs.SetInt(COINS_KEY, now);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(old, now);
        }

        public static void AddGems(int amount)
        {
            if (amount == 0) return;
            int old = Gems;
            int now = Mathf.Max(0, old + amount);
            PlayerPrefs.SetInt(GEMS_KEY, now);
            PlayerPrefs.Save();
            OnGemsChanged?.Invoke(old, now);
        }

        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            AddCoins(-amount);
            return true;
        }

        public static bool TrySpendGems(int amount)
        {
            if (amount < 0 || Gems < amount) return false;
            AddGems(-amount);
            return true;
        }
    }
}
