using System;
using UnityEngine;

namespace Siege.Chests
{
    public enum ChestType
    {
        Silver,
        Gold,
        Master
    }

    public enum ChestSlotState
    {
        Locked,     // Timer not started (displays total duration and 'Unlock' button)
        Waiting,    // Waiting in queue / pending start (displays 'Waiting' with grey button)
        Unlocking,  // Timer is actively counting down (displays live countdown timer)
        Ready,      // Countdown completed, ready to tap & open!
        Empty       // Chest collected, slot empty/awaiting next chest
    }

    [System.Serializable]
    public class ChestConfig
    {
        public ChestType type;
        public string displayName;
        [Tooltip("Unlock countdown duration in hours.")]
        public float unlockHours;
        public int minCoins;
        public int maxCoins;
        public int armyCopies;
        public int powerCopies;

        [Header("Sprites & Art")]
        public Sprite closedSprite;
        public Sprite openSprite;
        public Sprite[] animationFrames; // Frames 01 to 12

        public static ChestConfig CreateDefault(ChestType type)
        {
            switch (type)
            {
                case ChestType.Silver:
                    return new ChestConfig
                    {
                        type = ChestType.Silver,
                        displayName = "Silver Chest",
                        unlockHours = 2f,
                        minCoins = 20,
                        maxCoins = 200,
                        armyCopies = 2,
                        powerCopies = 2
                    };
                case ChestType.Gold:
                    return new ChestConfig
                    {
                        type = ChestType.Gold,
                        displayName = "Gold Chest",
                        unlockHours = 8f,
                        minCoins = 100,
                        maxCoins = 500,
                        armyCopies = 4,
                        powerCopies = 4
                    };
                case ChestType.Master:
                    return new ChestConfig
                    {
                        type = ChestType.Master,
                        displayName = "Master Chest",
                        unlockHours = 24f,
                        minCoins = 500,
                        maxCoins = 1000,
                        armyCopies = 10,
                        powerCopies = 10
                    };
                default:
                    return new ChestConfig();
            }
        }
    }

    [System.Serializable]
    public class ChestReward
    {
        public ChestType chestType;
        public int coins;

        public string armyCardName;
        public Sprite armyCardSprite;
        public int armyCopies;

        public string powerCardName;
        public Sprite powerCardSprite;
        public int powerCopies;
    }
}
