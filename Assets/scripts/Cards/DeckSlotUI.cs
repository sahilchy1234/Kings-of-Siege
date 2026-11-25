using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using Siege.Cards;

/// <summary>
/// One card slot in the Battle Deck grid (8 slots total).
/// Can hold a card or be empty. Supports drag-to-swap, DOTween juicy bounce, and "empty slot" state.
/// </summary>
[DisallowMultipleComponent]
public class DeckSlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Slot Elements")]
    [SerializeField] private RectTransform slotRoot;
    [SerializeField] private Image cardArtImage;
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image cardFrameOverlay;

    [Header("Rarity Frames")]
    [SerializeField] private Sprite frameCommon;
    [SerializeField] private Sprite frameRare;
    [SerializeField] private Sprite frameEpic;
    [SerializeField] private Sprite frameLegendary;

    [Header("Cost Badge")]
    [SerializeField] private RectTransform costBadgeRect;
    [SerializeField] private Image costBadgeImage;
    [SerializeField] private TMP_Text costText;

    [Header("Level Label")]
    [SerializeField] private RectTransform levelBadgeRect;
    [SerializeField] private TMP_Text levelText;

    [Header("Copies Bar")]
    [SerializeField] private RectTransform copiesBarRoot;
    [SerializeField] private Image copiesBarFill;
    [SerializeField] private TMP_Text copiesText;

    [Header("'New!' Badge")]
    [SerializeField] private RectTransform newBadgeRect;

    [Header("Empty State")]
    [SerializeField] private GameObject emptySlotIndicator;
    [SerializeField] private Image emptySlotIcon;      // Plus sign or add icon

    [Header("Selected Highlight")]
    [SerializeField] private Image selectionRing;

    [Header("Juice")]
    [SerializeField, Range(0.85f, 0.98f)] private float pressScale = 0.92f;

    // Runtime
    private string occupiedCardId;
    private CardDefinition cardDef;
    private PlayerCardEntry cardEntry;
    private Vector3 restScale = Vector3.one;
    private System.Action<DeckSlotUI> onSlotClicked;
    private bool isSelected;

    private void Awake()
    {
        if (slotRoot == null) slotRoot = GetComponent<RectTransform>();
        restScale = slotRoot ? slotRoot.localScale : Vector3.one;
        if (restScale == Vector3.zero) restScale = Vector3.one;
    }

    public void Setup(System.Action<DeckSlotUI> clickCallback)
    {
        onSlotClicked = clickCallback;
        SetEmpty();
    }

    public void SetCard(CardDefinition def, PlayerCardEntry entry)
    {
        cardDef = def;
        cardEntry = entry;
        occupiedCardId = def?.cardId;
        RefreshCard();
    }

    public void SetEmpty()
    {
        cardDef = null;
        cardEntry = null;
        occupiedCardId = null;
        ShowEmpty();
    }

    private void RefreshCard()
    {
        bool hasCard = cardDef != null;

        if (emptySlotIndicator != null) emptySlotIndicator.SetActive(!hasCard);
        if (cardArtImage != null) cardArtImage.gameObject.SetActive(hasCard);
        if (costBadgeRect != null) costBadgeRect.gameObject.SetActive(hasCard);
        if (levelBadgeRect != null) levelBadgeRect.gameObject.SetActive(hasCard);
        if (copiesBarRoot != null) copiesBarRoot.gameObject.SetActive(hasCard);

        if (!hasCard) return;

        if (cardArtImage != null)
            cardArtImage.sprite = cardDef.cardSprite;

        if (costText != null)
            costText.text = cardDef.elixirCost.ToString();

        if (costBadgeImage != null)
            costBadgeImage.color = Color.white; // Keep native vibrant magenta specular droplet

        bool isMax = cardDef.IsMaxLevel(cardEntry?.level ?? 1);
        if (levelText != null)
        {
            levelText.text = isMax ? $"Level {cardEntry?.level ?? 10} <color=#FFD700>(Max)</color>" : $"Level {cardEntry?.level ?? 1}";
        }

        if (cardEntry != null && copiesBarRoot != null)
        {
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
                if (copiesBarFill != null)
                {
                    copiesBarFill.fillAmount = needed > 0 ? Mathf.Clamp01((float)owned / needed) : 1f;
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

        bool isNew = cardEntry != null && cardEntry.isNew;
        if (newBadgeRect != null) newBadgeRect.gameObject.SetActive(isNew);

        // Frame by Rarity
        if (slotBackground != null)
        {
            switch (cardDef.rarity)
            {
                case CardRarity.Legendary:
                    if (frameLegendary != null) slotBackground.sprite = frameLegendary;
                    break;
                case CardRarity.Epic:
                    if (frameEpic != null) slotBackground.sprite = frameEpic;
                    break;
                case CardRarity.Rare:
                    if (frameRare != null) slotBackground.sprite = frameRare;
                    break;
                case CardRarity.Common:
                default:
                    if (frameCommon != null) slotBackground.sprite = frameCommon;
                    break;
            }
            slotBackground.color = Color.white;
        }
    }

    private void ShowEmpty()
    {
        if (emptySlotIndicator != null) emptySlotIndicator.SetActive(true);
        if (cardArtImage != null) cardArtImage.gameObject.SetActive(false);
        if (costBadgeRect != null) costBadgeRect.gameObject.SetActive(false);
        if (levelBadgeRect != null) levelBadgeRect.gameObject.SetActive(false);
        if (copiesBarRoot != null) copiesBarRoot.gameObject.SetActive(false);
        if (newBadgeRect != null) newBadgeRect.gameObject.SetActive(false);
        if (slotBackground != null) slotBackground.color = new Color(0.08f, 0.15f, 0.28f, 0.6f);
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

    // ─── Selection Highlight ─────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionRing != null)
        {
            selectionRing.DOKill();
            selectionRing.transform.DOKill();
            if (selected)
            {
                selectionRing.gameObject.SetActive(true);
                selectionRing.transform.localScale = Vector3.one * 1.02f;
                selectionRing.transform.DOScale(Vector3.one * 1.08f, 0.45f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
            else
            {
                selectionRing.transform.localScale = Vector3.one;
                selectionRing.gameObject.SetActive(false);
            }
        }
    }

    // ─── Entrance Anim ───────────────────────────────────────────────────────

    public void PlayEntranceAnim(float delay)
    {
        if (slotRoot == null) return;
        if (restScale == Vector3.zero) restScale = Vector3.one;

        if (!Application.isPlaying)
        {
            slotRoot.localScale = restScale;
            return;
        }

        slotRoot.DOKill();
        slotRoot.localScale = Vector3.zero;
        slotRoot.DOScale(restScale, 0.32f)
            .SetDelay(delay)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    public void PlaySwapAnim()
    {
        if (slotRoot == null) return;
        if (restScale == Vector3.zero) restScale = Vector3.one;
        slotRoot.DOKill();
        slotRoot.localScale = restScale;
        slotRoot.DOPunchScale(Vector3.one * 0.18f, 0.25f, 6, 0.5f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    public void PlayUpgradeCelebrationAnim()
    {
        if (slotRoot == null) return;
        if (restScale == Vector3.zero) restScale = Vector3.one;
        slotRoot.DOKill();
        slotRoot.localScale = restScale;
        slotRoot.DOPunchScale(Vector3.one * 0.28f, 0.45f, 6, 0.55f)
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

    public void PlayRemoveAnim(System.Action onComplete)
    {
        if (slotRoot == null) { onComplete?.Invoke(); return; }
        slotRoot.DOKill();
        slotRoot.DOScale(restScale * 1.1f, 0.08f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                slotRoot.DOScale(Vector3.zero, 0.18f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        onComplete?.Invoke();
                        slotRoot.DOScale(restScale, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
                    });
            });
    }

    // ─── Pointer ─────────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData e)
    {
        if (slotRoot == null) return;
        slotRoot.DOKill();
        slotRoot.DOScale(restScale * pressScale, 0.09f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (slotRoot == null) return;
        slotRoot.DOKill();
        slotRoot.DOScale(restScale, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData e)
    {
        onSlotClicked?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (slotRoot != null) slotRoot.DOKill();
        if (newBadgeRect != null) newBadgeRect.DOKill();
        if (selectionRing != null) selectionRing.DOKill();
    }

    // ─── Accessors ───────────────────────────────────────────────────────────

    public string OccupiedCardId => occupiedCardId;
    public bool IsEmpty => string.IsNullOrEmpty(occupiedCardId);
    public CardDefinition CardDef => cardDef;
}
