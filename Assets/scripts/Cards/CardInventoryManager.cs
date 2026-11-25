using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Siege.Cards
{
    /// <summary>
    /// Persistent singleton managing player's card inventory, battle deck (4 Units + 8 Powerups),
    /// and level upgrade state up to Level 10. Uses PlayerPrefs JSON serialization for cross-session persistence.
    /// Integrates seamlessly with ChestSystem rewards (Coins & Card Copies).
    /// </summary>
    public class CardInventoryManager : MonoBehaviour
    {
        private static CardInventoryManager instance;
        public static CardInventoryManager Instance => instance;

        private const string PREFS_KEY_INVENTORY = "KoS_CardInventory_v2";
        public const int MAX_UNIT_SLOTS = 4;
        public const int MAX_POWERUP_SLOTS = 8;
        public const int MAX_DECK_SIZE = MAX_UNIT_SLOTS + MAX_POWERUP_SLOTS; // 12 total

        [Header("Library Reference")]
        [SerializeField] private CardLibrary cardLibrary;

        [Header("Debug / Defaults")]
        [SerializeField] private bool autoPopulateOnFirstLaunch = true;

        // Runtime data
        private Dictionary<string, PlayerCardEntry> inventory = new Dictionary<string, PlayerCardEntry>();
        private List<string> unitDeck = new List<string>();       // Exactly 4 unit slots
        private List<string> powerupDeck = new List<string>();    // Exactly 8 powerup slots

        // Events
        public event Action OnInventoryChanged;
        public event Action OnDeckChanged;
        public event Action<CardDefinition, int> OnCardUpgraded; // (def, newLevel)

        public CardLibrary Library => cardLibrary;
        public IReadOnlyList<string> UnitDeck => unitDeck.AsReadOnly();
        public IReadOnlyList<string> PowerupDeck => powerupDeck.AsReadOnly();
        public IReadOnlyList<string> BattleDeck => unitDeck.Concat(powerupDeck).ToList().AsReadOnly();

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();
        }

        public void EnsureLoaded()
        {
            if (inventory == null || inventory.Count == 0)
            {
                LoadData();
            }
        }

        // ─── Inventory Queries ──────────────────────────────────────────────

        public PlayerCardEntry GetEntry(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            inventory.TryGetValue(cardId, out var entry);
            return entry;
        }

        public bool HasCard(string cardId) => !string.IsNullOrEmpty(cardId) && inventory.ContainsKey(cardId);

        public List<PlayerCardEntry> GetAllOwnedEntries() => inventory.Values.ToList();

        public List<PlayerCardEntry> GetFoundCards() => inventory.Values.Where(e => e.isUnlocked).ToList();

        public int TotalCardCount => cardLibrary != null ? cardLibrary.Cards.Count : 0;
        public int OwnedCardCount => inventory.Count;

        // ─── Deck Operations ────────────────────────────────────────────────

        public bool IsInDeck(string cardId) => unitDeck.Contains(cardId) || powerupDeck.Contains(cardId);
        public bool IsInUnitDeck(string cardId) => unitDeck.Contains(cardId);
        public bool IsInPowerupDeck(string cardId) => powerupDeck.Contains(cardId);

        public bool AddToDeck(string cardId)
        {
            if (string.IsNullOrEmpty(cardId) || !HasCard(cardId)) return false;
            var def = cardLibrary?.GetCard(cardId);
            if (def == null) return false;

            if (def.cardType == CardType.Army)
            {
                return AddToUnitDeck(cardId);
            }
            else
            {
                return AddToPowerupDeck(cardId);
            }
        }

        public bool AddToUnitDeck(string cardId)
        {
            if (unitDeck.Contains(cardId)) return false;
            if (unitDeck.Count >= MAX_UNIT_SLOTS) return false;
            if (!HasCard(cardId)) return false;

            unitDeck.Add(cardId);
            SaveData();
            OnDeckChanged?.Invoke();
            return true;
        }

        public bool AddToPowerupDeck(string cardId)
        {
            if (powerupDeck.Contains(cardId)) return false;
            if (powerupDeck.Count >= MAX_POWERUP_SLOTS) return false;
            if (!HasCard(cardId)) return false;

            powerupDeck.Add(cardId);
            SaveData();
            OnDeckChanged?.Invoke();
            return true;
        }

        public bool RemoveFromDeck(string cardId)
        {
            bool removed = false;
            if (unitDeck.Remove(cardId)) removed = true;
            if (powerupDeck.Remove(cardId)) removed = true;

            if (removed)
            {
                SaveData();
                OnDeckChanged?.Invoke();
            }
            return removed;
        }

        public bool SwapUnitSlot(int index, string addCardId)
        {
            if (index < 0 || index >= MAX_UNIT_SLOTS) return false;
            if (string.IsNullOrEmpty(addCardId) || !HasCard(addCardId)) return false;

            int existingIndex = unitDeck.IndexOf(addCardId);
            if (existingIndex >= 0)
            {
                if (index < unitDeck.Count)
                {
                    string temp = unitDeck[index];
                    unitDeck[index] = addCardId;
                    unitDeck[existingIndex] = temp;
                }
            }
            else
            {
                if (index < unitDeck.Count)
                {
                    unitDeck[index] = addCardId;
                }
                else
                {
                    unitDeck.Add(addCardId);
                }
            }

            SaveData();
            OnDeckChanged?.Invoke();
            return true;
        }

        public bool SwapPowerupSlot(int index, string addCardId)
        {
            if (index < 0 || index >= MAX_POWERUP_SLOTS) return false;
            if (string.IsNullOrEmpty(addCardId) || !HasCard(addCardId)) return false;

            int existingIndex = powerupDeck.IndexOf(addCardId);
            if (existingIndex >= 0)
            {
                if (index < powerupDeck.Count)
                {
                    string temp = powerupDeck[index];
                    powerupDeck[index] = addCardId;
                    powerupDeck[existingIndex] = temp;
                }
            }
            else
            {
                if (index < powerupDeck.Count)
                {
                    powerupDeck[index] = addCardId;
                }
                else
                {
                    powerupDeck.Add(addCardId);
                }
            }

            SaveData();
            OnDeckChanged?.Invoke();
            return true;
        }

        public void ClearDeck()
        {
            unitDeck.Clear();
            powerupDeck.Clear();
            SaveData();
            OnDeckChanged?.Invoke();
        }

        public float GetDeckElixirAverage()
        {
            var allDeck = BattleDeck;
            if (allDeck.Count == 0 || cardLibrary == null) return 0f;
            float total = 0f;
            int count = 0;
            foreach (var id in allDeck)
            {
                var def = cardLibrary.GetCard(id);
                if (def != null) { total += def.elixirCost; count++; }
            }
            return count > 0 ? total / count : 0f;
        }

        // ─── Chest Rewards & Copies Operations ──────────────────────────────

        /// <summary>
        /// Authoritatively adds card copies (e.g. from chest opening or shop purchase).
        /// </summary>
        public void AddCardCopies(string cardId, int copies)
        {
            if (string.IsNullOrEmpty(cardId) || copies <= 0) return;
            EnsureLoaded();

            if (!inventory.TryGetValue(cardId, out var entry))
            {
                entry = new PlayerCardEntry { cardId = cardId, level = 1, copiesOwned = 0, isUnlocked = true, isNew = true };
                inventory[cardId] = entry;
            }
            entry.copiesOwned += copies;
            entry.isNew = true;
            SaveData();
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Matches a reward from chest opening (by sprite or name) and authoritatively grants copies.
        /// </summary>
        public void AddCardCopiesByReward(string rawCardName, Sprite cardSprite, int copies)
        {
            if (copies <= 0) return;
            EnsureLoaded();

            if (cardLibrary == null)
            {
                Debug.LogWarning("[CardInventoryManager] Cannot add reward copies: cardLibrary is null.");
                return;
            }

            CardDefinition match = null;

            // 1. Match by sprite reference
            if (cardSprite != null)
            {
                match = cardLibrary.Cards.FirstOrDefault(c => c.cardSprite == cardSprite);
            }

            // 2. Match by display name (case-insensitive)
            if (match == null && !string.IsNullOrEmpty(rawCardName))
            {
                match = cardLibrary.Cards.FirstOrDefault(c => string.Equals(c.displayName, rawCardName, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Match by cardId (clean formatted)
            if (match == null && !string.IsNullOrEmpty(rawCardName))
            {
                string cleanId = rawCardName.ToLower().Replace(" ", "_");
                match = cardLibrary.Cards.FirstOrDefault(c => string.Equals(c.cardId, cleanId, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
            {
                AddCardCopies(match.cardId, copies);
                Debug.Log($"<color=#00E5FF>[CardInventoryManager] Chest Reward Credited: +{copies}x '{match.displayName}' (Now: {GetEntry(match.cardId)?.copiesOwned} copies)</color>");
            }
            else
            {
                Debug.LogWarning($"[CardInventoryManager] Could not find card match for chest reward '{rawCardName}'!");
            }
        }

        // ─── Level Upgrade System (Up To Level 10) ──────────────────────────

        /// <summary>
        /// Checks whether a card is currently eligible to be upgraded to the next level.
        /// </summary>
        public bool CanUpgradeCard(string cardId, out int copiesNeeded, out int coinsNeeded, out string failureReason)
        {
            copiesNeeded = 0;
            coinsNeeded = 0;
            failureReason = "";

            if (string.IsNullOrEmpty(cardId) || !inventory.TryGetValue(cardId, out var entry))
            {
                failureReason = "Card not found in inventory.";
                return false;
            }

            var def = cardLibrary?.GetCard(cardId);
            if (def == null)
            {
                failureReason = "Card definition not found.";
                return false;
            }

            if (def.IsMaxLevel(entry.level))
            {
                failureReason = $"Card is already at Max Level ({CardDefinition.MAX_CARD_LEVEL}).";
                return false;
            }

            copiesNeeded = def.CopiesToUpgrade(entry.level);
            coinsNeeded = def.CoinsToUpgrade(entry.level);

            if (entry.copiesOwned < copiesNeeded)
            {
                failureReason = $"Need {copiesNeeded - entry.copiesOwned} more copies.";
                return false;
            }

            int currentCoins = PlayerPrefs.GetInt("PlayerCoins", 0);
            if (currentCoins < coinsNeeded)
            {
                failureReason = $"Need {coinsNeeded - currentCoins} more coins.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes card upgrade up to Level 10: consumes required copies, deducts gold coins,
        /// increments level, persists changes, and dispatches events.
        /// </summary>
        public bool TryUpgradeCard(string cardId)
        {
            if (!CanUpgradeCard(cardId, out int copiesNeeded, out int coinsNeeded, out string reason))
            {
                Debug.LogWarning($"[CardInventoryManager] Cannot upgrade '{cardId}': {reason}");
                return false;
            }

            var entry = inventory[cardId];
            var def = cardLibrary.GetCard(cardId);

            // Deduct copies
            entry.copiesOwned -= copiesNeeded;
            entry.level++;

            // Deduct coins from persistent PlayerCoins
            int currentCoins = PlayerPrefs.GetInt("PlayerCoins", 0);
            int newCoins = Mathf.Max(0, currentCoins - coinsNeeded);
            PlayerPrefs.SetInt("PlayerCoins", newCoins);
            PlayerPrefs.Save();

            SaveData();

            Debug.Log($"<color=#2ECC71>[CardInventoryManager] Successfully upgraded '{def.displayName}' to Level {entry.level}! Spent {coinsNeeded} Coins, {copiesNeeded} Copies. Remaining coins: {newCoins}</color>");

            OnInventoryChanged?.Invoke();
            OnCardUpgraded?.Invoke(def, entry.level);
            if (IsInDeck(cardId))
            {
                OnDeckChanged?.Invoke();
            }
            return true;
        }

        /// <summary>
        /// Returns total count of unlocked cards ready for level upgrade (has copies + coins).
        /// Used for notification badges on the Collection tab and main bottom nav bar.
        /// </summary>
        public int GetUpgradeableCardsCount()
        {
            if (cardLibrary == null) return 0;
            int count = 0;
            int playerCoins = PlayerPrefs.GetInt("PlayerCoins", 0);

            foreach (var kvp in inventory)
            {
                var entry = kvp.Value;
                if (!entry.isUnlocked || entry.level >= CardDefinition.MAX_CARD_LEVEL) continue;
                var def = cardLibrary.GetCard(entry.cardId);
                if (def == null) continue;

                int neededCopies = def.CopiesToUpgrade(entry.level);
                int neededCoins = def.CoinsToUpgrade(entry.level);

                if (entry.copiesOwned >= neededCopies && playerCoins >= neededCoins)
                {
                    count++;
                }
            }
            return count;
        }

        public void ClearNewFlag(string cardId)
        {
            if (inventory.TryGetValue(cardId, out var entry))
                entry.isNew = false;
        }

        // ─── Persistence ────────────────────────────────────────────────────

        [Serializable]
        private class InventorySaveDataV2
        {
            public List<PlayerCardEntry> inventory = new List<PlayerCardEntry>();
            public List<string> unitDeck = new List<string>();
            public List<string> powerupDeck = new List<string>();
        }

        private void SaveData()
        {
            var data = new InventorySaveDataV2
            {
                inventory = inventory.Values.ToList(),
                unitDeck = new List<string>(unitDeck),
                powerupDeck = new List<string>(powerupDeck)
            };
            PlayerPrefs.SetString(PREFS_KEY_INVENTORY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadData()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY_INVENTORY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<InventorySaveDataV2>(json);
                    inventory = data.inventory.ToDictionary(e => e.cardId, e => e);
                    unitDeck = data.unitDeck ?? new List<string>();
                    powerupDeck = data.powerupDeck ?? new List<string>();

                    // Clamp any levels to MAX_CARD_LEVEL (10)
                    foreach (var entry in inventory.Values)
                    {
                        if (entry.level > CardDefinition.MAX_CARD_LEVEL)
                            entry.level = CardDefinition.MAX_CARD_LEVEL;
                    }
                }
                catch
                {
                    inventory = new Dictionary<string, PlayerCardEntry>();
                    unitDeck = new List<string>();
                    powerupDeck = new List<string>();
                }
            }

            if (autoPopulateOnFirstLaunch && (inventory.Count == 0 || unitDeck.Count == 0 || powerupDeck.Count == 0))
            {
                PopulateDefaults();
            }

            // Sync any legacy rewards deposited by chests before initialization
            SyncLegacyChestRewards();
        }

        /// <summary>
        /// Reads legacy CardCopies_* and PowerCopies_* PlayerPrefs keys from previous chest openings and merges them.
        /// </summary>
        private void SyncLegacyChestRewards()
        {
            if (cardLibrary == null) return;
            bool changed = false;

            foreach (var card in cardLibrary.Cards)
            {
                string armyKey = $"CardCopies_{card.displayName}";
                string powerKey = $"PowerCopies_{card.displayName}";

                int armySaved = PlayerPrefs.GetInt(armyKey, 0);
                if (armySaved > 0)
                {
                    if (!inventory.TryGetValue(card.cardId, out var entry))
                    {
                        entry = new PlayerCardEntry { cardId = card.cardId, level = 1, copiesOwned = 0, isUnlocked = true };
                        inventory[card.cardId] = entry;
                    }
                    entry.copiesOwned += armySaved;
                    PlayerPrefs.DeleteKey(armyKey);
                    changed = true;
                }

                int powerSaved = PlayerPrefs.GetInt(powerKey, 0);
                if (powerSaved > 0)
                {
                    if (!inventory.TryGetValue(card.cardId, out var entry))
                    {
                        entry = new PlayerCardEntry { cardId = card.cardId, level = 1, copiesOwned = 0, isUnlocked = true };
                        inventory[card.cardId] = entry;
                    }
                    entry.copiesOwned += powerSaved;
                    PlayerPrefs.DeleteKey(powerKey);
                    changed = true;
                }
            }

            if (changed)
            {
                PlayerPrefs.Save();
                SaveData();
            }
        }

        public void PopulateDefaults()
        {
            if (cardLibrary == null)
            {
                var guids = RuntimeAssets.FindAssets("t:CardLibrary");
                if (guids.Length > 0)
                    cardLibrary = RuntimeAssets.LoadAssetAtPath<CardLibrary>(RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
            if (cardLibrary == null) return;

            // Unlock cards for smooth player progression
            var allCards = cardLibrary.GetAllCards();
            foreach (var def in allCards)
            {
                if (!inventory.ContainsKey(def.cardId))
                {
                    inventory[def.cardId] = new PlayerCardEntry
                    {
                        cardId = def.cardId,
                        level = 1,
                        copiesOwned = 2,
                        isUnlocked = true,
                        isNew = false
                    };
                }
            }

            // Populate 4 default battle units (Army)
            if (unitDeck.Count == 0)
            {
                var defaultUnits = new string[] { "swordsman", "spearman", "archer", "king" };
                foreach (var id in defaultUnits)
                {
                    if (cardLibrary.GetCard(id) != null && !unitDeck.Contains(id) && unitDeck.Count < MAX_UNIT_SLOTS)
                        unitDeck.Add(id);
                }
                if (unitDeck.Count < MAX_UNIT_SLOTS)
                {
                    foreach (var c in allCards.Where(c => c.cardType == CardType.Army))
                    {
                        if (!unitDeck.Contains(c.cardId) && unitDeck.Count < MAX_UNIT_SLOTS)
                            unitDeck.Add(c.cardId);
                    }
                }
            }

            // Populate 8 default battle powerups (Power)
            if (powerupDeck.Count == 0)
            {
                var defaultPowers = new string[] {
                    "arrow_shower", "fire_arrows", "big_fireball", "archer_tower",
                    "reinforcement_cart", "wood_rolling", "steel_trap", "fence"
                };
                foreach (var id in defaultPowers)
                {
                    if (cardLibrary.GetCard(id) != null && !powerupDeck.Contains(id) && powerupDeck.Count < MAX_POWERUP_SLOTS)
                        powerupDeck.Add(id);
                }
                if (powerupDeck.Count < MAX_POWERUP_SLOTS)
                {
                    foreach (var c in allCards.Where(c => c.cardType == CardType.Power))
                    {
                        if (!powerupDeck.Contains(c.cardId) && powerupDeck.Count < MAX_POWERUP_SLOTS)
                            powerupDeck.Add(c.cardId);
                    }
                }
            }

            SaveData();
        }

        [ContextMenu("Reset All Card Data")]
        public void ResetAllData()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY_INVENTORY);
            inventory.Clear();
            unitDeck.Clear();
            powerupDeck.Clear();
            if (autoPopulateOnFirstLaunch) PopulateDefaults();
            OnInventoryChanged?.Invoke();
            OnDeckChanged?.Invoke();
        }
    }
}
