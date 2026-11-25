using System;
using UnityEngine;

namespace Siege.Cards
{
    public enum CardType { Army, Power }
    public enum CardRarity { Common = 1, Rare = 3, Epic = 5, Legendary = 8 }

    [Serializable]
    public class CardDefinition
    {
        public const int MAX_CARD_LEVEL = 10;

        [Header("Identity")]
        public string cardId;
        public string displayName;
        public CardType cardType;
        public CardRarity rarity;
        public int elixirCost = 3;

        [Header("Visuals")]
        public Sprite cardSprite;
        public Color rarityColor = Color.white;

        [Header("Unlock Requirement")]
        public int arenaUnlockLevel = 1;
        public bool isUnlockedByDefault = false;

        [Header("Level & Upgrade")]
        public int maxLevel = MAX_CARD_LEVEL;

        /// <summary>Whether this card has reached the maximum upgrade level (Level 10).</summary>
        public bool IsMaxLevel(int currentLevel) => currentLevel >= MAX_CARD_LEVEL;

        /// <summary>Returns card copies needed to reach the NEXT level from currentLevel (returns 0 if already at Max Level 10).</summary>
        public int CopiesToUpgrade(int currentLevel)
        {
            if (currentLevel >= MAX_CARD_LEVEL) return 0;
            switch (rarity)
            {
                case CardRarity.Common:    return CommonCopiesTable(currentLevel);
                case CardRarity.Rare:      return RareCopiesTable(currentLevel);
                case CardRarity.Epic:      return EpicCopiesTable(currentLevel);
                case CardRarity.Legendary: return LegendaryCopiesTable(currentLevel);
                default: return 999;
            }
        }

        /// <summary>Returns gold coins required to upgrade to the NEXT level from currentLevel (returns 0 if already at Max Level 10).</summary>
        public int CoinsToUpgrade(int currentLevel)
        {
            if (currentLevel >= MAX_CARD_LEVEL) return 0;
            switch (rarity)
            {
                case CardRarity.Common:    return CommonCoinsTable(currentLevel);
                case CardRarity.Rare:      return RareCoinsTable(currentLevel);
                case CardRarity.Epic:      return EpicCoinsTable(currentLevel);
                case CardRarity.Legendary: return LegendaryCoinsTable(currentLevel);
                default: return 999;
            }
        }

        /// <summary>Returns stat scaling multiplier for this level (e.g. +10% per level).</summary>
        public float GetStatMultiplier(int level)
        {
            int clamped = Mathf.Clamp(level, 1, MAX_CARD_LEVEL);
            return 1f + (clamped - 1) * 0.10f;
        }

        // ─── Copies Progression Tables (Level 1 -> 10) ───────────────────
        // Index 0: 1->2, Index 1: 2->3, ..., Index 8: 9->10
        private static int CommonCopiesTable(int lvl)
        {
            int[] table = { 2, 4, 10, 20, 50, 100, 200, 400, 800 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int RareCopiesTable(int lvl)
        {
            int[] table = { 2, 4, 10, 20, 40, 80, 160, 300, 500 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int EpicCopiesTable(int lvl)
        {
            int[] table = { 1, 2, 4, 8, 16, 30, 60, 120, 200 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int LegendaryCopiesTable(int lvl)
        {
            int[] table = { 1, 1, 2, 3, 5, 8, 12, 18, 25 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        // ─── Gold / Coins Progression Tables (Level 1 -> 10) ──────────────
        private static int CommonCoinsTable(int lvl)
        {
            int[] table = { 5, 20, 50, 150, 400, 1000, 2000, 4000, 8000 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int RareCoinsTable(int lvl)
        {
            int[] table = { 10, 30, 80, 200, 600, 1500, 3000, 6000, 12000 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int EpicCoinsTable(int lvl)
        {
            int[] table = { 20, 50, 150, 400, 1000, 2500, 5000, 10000, 20000 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }

        private static int LegendaryCoinsTable(int lvl)
        {
            int[] table = { 50, 150, 400, 1000, 2500, 5000, 10000, 20000, 40000 };
            return lvl >= 1 && lvl <= table.Length ? table[lvl - 1] : 0;
        }
    }

    [Serializable]
    public class PlayerCardEntry
    {
        public string cardId;
        public int level = 1;
        public int copiesOwned = 0;
        public bool isNew = false;
        public bool isUnlocked = true;
    }
}
