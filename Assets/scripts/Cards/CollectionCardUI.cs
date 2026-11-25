using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using Siege.Cards;

/// <summary>
/// Individual card UI element in the Collection grid.
/// Handles visual state (found/not-found), level badge, copies bar, "New!" badge, 
/// and juicy DOTween interactions (press squash, bounce release, idle breathing).
/// </summary>
[DisallowMultipleComponent]
public class CollectionCardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Card Frame Elements")]
    [SerializeField] private RectTransform cardRoot;
    [SerializeField] private Image cardBackground;        // Rarity-tinted background
    [SerializeField] private Image cardArtImage;          // Main character/spell art
    [SerializeField] private Image cardFrameOverlay;      // Frame border overlay
    [SerializeField] private Image greyscaleOverlay;      // For locked/not-found state

    [Header("Cost Badge (top-left circle)")]
    [SerializeField] private RectTransform costBadgeRect;
    [SerializeField] private Image costBadgeImage;
    [SerializeField] private TMP_Text costText;

    [Header("Level Label (bottom center)")]
    [SerializeField] private RectTransform levelBadgeRect;
    [SerializeField] private Image levelBadgeBackground;
    [SerializeField] private TMP_Text levelText;

    [Header("Copies Progress Bar")]
    [SerializeField] private RectTransform copiesBarRoot;
    [SerializeField] private Image copiesBarFill;
    [SerializeField] private TMP_Text copiesText;
    [SerializeField] private Image upgradeArrowIcon;

    [Header("'New!' Ribbon")]
    [SerializeField] private RectTransform newBadgeRect;
    [SerializeField] private TMP_Text newBadgeText;

    [Header("'Unlocks at Arena X' overlay")]
    [SerializeField] private RectTransform lockedOverlayRect;
    [SerializeField] private TMP_Text lockedArenaText;

    [Header("Rarity Frames")]
    [SerializeField] private Sprite frameCommon;
    [SerializeField] private Sprite frameRare;
    [SerializeField] private Sprite frameEpic;
    [SerializeField] private Sprite frameLegendary;
    [SerializeField] private Sprite frameLocked;

    [Header("Juice Config")]
    [SerializeField, Range(0.85f, 0.98f)] private float pressScale = 0.92f;
    [SerializeField] private bool enableIdleBreathing = false;

    // Runtime state
    private CardDefinition cardDef;
    private PlayerCardEntry cardEntry;
    private bool isFound;
    private Vector3 restScale = Vector3.one;
    private Tween breathTween;
    private System.Action<CollectionCardUI> onCardClicked;

    // ─── Setup ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (cardRoot == null) cardRoot = GetComponent<RectTransform>();
        restScale = (cardRoot && cardRoot.localScale != Vector3.zero) ? cardRoot.localScale : Vector3.one;
    }

    public void Setup(CardDefinition def, PlayerCardEntry entry, System.Action<CollectionCardUI> clickCallback)
    {
        if (cardRoot == null) cardRoot = GetComponent<RectTransform>();
        if (restScale == Vector3.zero) restScale = Vector3.one;
        if (cardRoot != null && cardRoot.localScale == Vector3.zero) cardRoot.localScale = Vector3.one;

        cardDef = def;
        cardEntry = entry;
        onCardClicked = clickCallback;

        isFound = entry != null && entry.isUnlocked;
        Refresh();
    }

    public void Refresh()
    {
        if (cardDef == null) return;

        bool found = isFound;
        bool isNew = found && cardEntry != null && cardEntry.isNew;

        // ─ Art & Rarity ─
        if (cardArtImage != null)
        {
            cardArtImage.sprite = cardDef.cardSprite;
            cardArtImage.color = found ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        // Greyscale overlay for locked cards
        if (greyscaleOverlay != null)
            greyscaleOverlay.gameObject.SetActive(!found);

        // ─ Cost Badge ─
        if (costText != null) costText.text = cardDef.elixirCost.ToString();
        if (costBadgeRect != null) costBadgeRect.gameObject.SetActive(found);

        // ─ Level ─
        if (levelText != null)
        {
            if (!found)
            {
                levelText.text = "";
            }
            else if (cardDef.IsMaxLevel(cardEntry?.level ?? 1))
            {
                levelText.text = $"Level {cardEntry?.level ?? 10} <color=#FFD700>(Max)</color>";
            }
            else
            {
                levelText.text = $"Level {cardEntry?.level ?? 1}";
            }
        }
        if (levelBadgeRect != null)
            levelBadgeRect.gameObject.SetActive(found);

        // ─ Copies Bar ─
        if (copiesBarRoot != null)
        {
            if (found && cardEntry != null)
            {
                copiesBarRoot.gameObject.SetActive(true);
                bool isMax = cardDef.IsMaxLevel(cardEntry.level);
                if (isMax)
                {
                    if (copiesBarFill != null)
                    {
                        copiesBarFill.fillAmount = 1f;
                        copiesBarFill.color = new Color(0.95f, 0.78f, 0.20f); // Golden Max Bar
                    }
                    if (copiesText != null) copiesText.text = "MAX";
                }
                else
                {
                    int needed = cardDef.CopiesToUpgrade(cardEntry.level);
                    int owned = cardEntry.copiesOwned;
                    float fillAmt = needed > 0 ? Mathf.Clamp01((float)owned / needed) : 1f;
                    if (copiesBarFill != null)
                    {
                        copiesBarFill.fillAmount = fillAmt;
                        // Emerald green if upgradeable, else vibrant cyan
                        copiesBarFill.color = owned >= needed
                            ? new Color(0.18f, 0.82f, 0.38f) // Vibrant Green
                            : new Color(0.12f, 0.65f, 0.95f); // Vibrant Blue
                    }
                    if (copiesText != null)
                    {
                        copiesText.text = owned >= needed
                            ? $"<b>{owned}/{needed} ▲</b>"
                            : $"{owned}/{needed}";
                    }
                }
            }
            else
            {
                copiesBarRoot.gameObject.SetActive(false);
            }
        }

        // ─ 'New!' badge ─
        if (newBadgeRect != null) newBadgeRect.gameObject.SetActive(isNew);

        // ─ Locked overlay ─
        if (lockedOverlayRect != null)
        {
            lockedOverlayRect.gameObject.SetActive(!found);
            if (!found && lockedArenaText != null)
                lockedArenaText.text = $"Unlocks at\n<color=#FF4D8B>Arena {cardDef.arenaUnlockLevel}</color>";
        }

        // ─ Rarity badge color (cost circle) ─
        if (costBadgeImage != null)
        {
            costBadgeImage.color = Color.white; // Keep native vibrant magenta specular droplet
        }

        // ─ Background Frame by Rarity ─
        if (cardBackground != null)
        {
            if (!found)
            {
                if (frameLocked != null) cardBackground.sprite = frameLocked;
                cardBackground.color = Color.white;
            }
            else
            {
                switch (cardDef.rarity)
                {
                    case CardRarity.Legendary:
                        if (frameLegendary != null) cardBackground.sprite = frameLegendary;
                        break;
                    case CardRarity.Epic:
                        if (frameEpic != null) cardBackground.sprite = frameEpic;
                        break;
                    case CardRarity.Rare:
                        if (frameRare != null) cardBackground.sprite = frameRare;
                        break;
                    case CardRarity.Common:
                    default:
                        if (frameCommon != null) cardBackground.sprite = frameCommon;
                        break;
                }
                cardBackground.color = Color.white;
            }
        }

        if (enableIdleBreathing && found) StartBreathing();
        else StopBreathing();
    }

    private static Color RarityColor(CardRarity r)
    {
        switch (r)
        {
            case CardRarity.Common:    return new Color(0.55f, 0.60f, 0.70f);
            case CardRarity.Rare:      return new Color(0.25f, 0.50f, 0.95f);
            case CardRarity.Epic:      return new Color(0.65f, 0.25f, 0.90f);
            case CardRarity.Legendary: return new Color(1.00f, 0.75f, 0.10f);
            default:                   return Color.white;
        }
    }

    // ─── Pointer Events ───────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData e)
    {
        StopBreathing();
        if (cardRoot == null) return;
        cardRoot.DOKill();
        cardRoot.DOScale(restScale * pressScale, 0.09f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (cardRoot == null) return;
        cardRoot.DOKill();
        cardRoot.DOScale(restScale, 0.22f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .OnComplete(() => { if (enableIdleBreathing && isFound) StartBreathing(); });
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (cardDef == null) return;
        onCardClicked?.Invoke(this);

        // Clear "new" flag on tap
        if (cardEntry != null && cardEntry.isNew)
        {
            CardInventoryManager.Instance?.ClearNewFlag(cardDef.cardId);
            if (newBadgeRect != null)
            {
                newBadgeRect.DOKill();
                newBadgeRect.DOScale(Vector3.zero, 0.15f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .OnComplete(() => newBadgeRect.gameObject.SetActive(false));
            }
        }
    }

    // ─── Entrance Animation ───────────────────────────────────────────────────

    public void PlayEntranceAnim(float delay)
    {
        if (cardRoot == null) return;
        if (restScale == Vector3.zero) restScale = Vector3.one;

        if (!Application.isPlaying)
        {
            cardRoot.localScale = restScale;
            if (isFound && cardEntry != null && cardEntry.isNew && newBadgeRect != null)
                newBadgeRect.localScale = Vector3.one;
            return;
        }

        cardRoot.localScale = Vector3.zero;
        cardRoot.DOKill();
        cardRoot.DOScale(restScale, 0.35f)
            .SetDelay(delay)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        if (isFound && cardEntry != null && cardEntry.isNew && newBadgeRect != null)
        {
            newBadgeRect.localScale = Vector3.zero;
            newBadgeRect.DOScale(Vector3.one, 0.3f)
                .SetDelay(delay + 0.25f)
                .SetEase(Ease.OutElastic)
                .SetUpdate(true);
        }
    }

    public void PlayDeckAddedAnim()
    {
        if (cardRoot == null) return;
        cardRoot.DOKill();
        cardRoot.DOPunchScale(Vector3.one * 0.15f, 0.28f, 5, 0.6f)
            .SetEase(Ease.OutElastic)
            .SetUpdate(true);
    }

    public void PlayUpgradeCelebrationAnim()
    {
        if (cardRoot == null) return;
        cardRoot.DOKill();
        cardRoot.localScale = restScale;
        cardRoot.DOPunchScale(Vector3.one * 0.28f, 0.45f, 6, 0.55f)
            .SetEase(Ease.OutElastic)
            .SetUpdate(true);

        if (levelBadgeRect != null)
        {
            levelBadgeRect.DOKill();
            levelBadgeRect.DOPunchScale(Vector3.one * 0.35f, 0.38f, 5, 0.6f).SetUpdate(true);
        }

        if (copiesBarFill != null)
        {
            copiesBarFill.DOKill();
            Color orig = copiesBarFill.color;
            copiesBarFill.color = Color.white;
            copiesBarFill.DOColor(orig, 0.4f).SetUpdate(true);
        }
    }

    // ─── Breathing ────────────────────────────────────────────────────────────

    private void StartBreathing()
    {
        StopBreathing();
        if (cardRoot == null) return;
        breathTween = cardRoot.DOScale(restScale * 1.025f, 0.9f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopBreathing()
    {
        if (breathTween != null && breathTween.IsActive()) breathTween.Kill();
        breathTween = null;
        if (restScale == Vector3.zero) restScale = Vector3.one;
        if (cardRoot == null) cardRoot = GetComponent<RectTransform>();
        if (cardRoot != null) cardRoot.localScale = restScale;
    }

    private void OnDestroy()
    {
        StopBreathing();
        if (cardRoot != null) cardRoot.DOKill();
        if (newBadgeRect != null) newBadgeRect.DOKill();
    }

    // ─── Public Accessors ─────────────────────────────────────────────────────

    public CardDefinition Definition => cardDef;
    public PlayerCardEntry Entry => cardEntry;
    public bool IsFound => isFound;
}
