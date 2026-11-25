using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Siege.Cards
{
    /// <summary>
    /// ScriptableObject containing ALL card definitions in the game.
    /// Create via: Assets > Create > Kings of Siege > Card Library
    /// </summary>
    [CreateAssetMenu(fileName = "CardLibrary", menuName = "Kings of Siege/Card Library")]
    public class CardLibrary : ScriptableObject
    {
        [Header("All Cards")]
        [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>();

        public IReadOnlyList<CardDefinition> Cards => cards;

        public CardDefinition GetCard(string cardId)
        {
            return cards.FirstOrDefault(c => c.cardId == cardId);
        }

        public List<CardDefinition> GetCardsByType(CardType type)
        {
            return cards.Where(c => c.cardType == type).ToList();
        }

        public List<CardDefinition> GetAllCards()
        {
            return new List<CardDefinition>(cards);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-assign Card IDs")]
        private void AutoAssignIDs()
        {
            foreach (var card in cards)
            {
                if (string.IsNullOrEmpty(card.cardId))
                    card.cardId = card.displayName.ToLower().Replace(" ", "_");
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
