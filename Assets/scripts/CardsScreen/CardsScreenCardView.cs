using DG.Tweening;
using Siege.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Siege.CardsScreen
{
    public enum CardViewContext { DeckSlot, Collection }

    /// <summary>
    /// One card tile on the Cards screen (deck slot or collection cell).
    /// The controller builds the hierarchy and fills the visuals; this component only owns
    /// input (tap, press squash, hold-and-drag in the deck, scroll forwarding) and idle tweens.
    /// </summary>
    [DisallowMultipleComponent]
    public class CardsScreenCardView : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ── Hierarchy (assigned by CardsScreenController) ──
        public RectTransform root;
        public RectTransform visual;
        public CanvasGroup visualGroup;
        public Image glow;
        public Image plate;
        public Image art;
        public TextMeshProUGUI artFallbackText;
        public Image levelBand;
        public TextMeshProUGUI levelText;
        public RectTransform barRoot;
        public Image barFill;
        public RectTransform barArrow;
        public TextMeshProUGUI barText;
        public RectTransform costBadge;
        public TextMeshProUGUI costText;
        public RectTransform newBadge;
        public Image newGlow;
        public RectTransform lockedRoot;
        public TextMeshProUGUI lockedText;
        public RectTransform inDeckTag;
        public RectTransform emptyRoot;
        public Image highlight;

        public CardViewContext context;
        /// <summary>Deck slot index: 0..3 = army, 4..11 = powers. -1 in the collection.</summary>
        public int slotIndex = -1;

        public CardDefinition Def { get; private set; }
        public PlayerCardEntry Entry { get; private set; }
        public bool IsFound { get; private set; }
        public bool IsNew { get; private set; }
        public bool IsUpgradeReady { get; private set; }
        public bool IsEmpty => Def == null;
        public CardType SlotType => slotIndex >= 0 && slotIndex < 4 ? CardType.Army : CardType.Power;

        internal CardsScreenController owner;
        internal ScrollRect scrollParent;

        private const float HoldToDragSeconds = 0.2f;

        private Sequence newPulse;
        private Tween arrowBounce;
        private Tween highlightPulse;
        private bool pressed;
        private bool lifted;
        private bool cardDrag;
        private bool forwardDrag;
        private bool dragHappened;
        private float pressTime;

        // ─────────────────────────── State ───────────────────────────

        internal void SetState(CardDefinition def, PlayerCardEntry entry, bool found, bool isNew, bool upgradeReady)
        {
            Def = def;
            Entry = entry;
            IsFound = found;
            IsNew = isNew;
            IsUpgradeReady = upgradeReady;
            RefreshIdle();
        }

        internal void RefreshIdle()
        {
            KillIdle();
            if (!isActiveAndEnabled) return;

            if (IsNew && newBadge != null && newBadge.gameObject.activeSelf)
            {
                newBadge.localScale = Vector3.one;
                newPulse = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Yoyo);
                newPulse.Append(newBadge.DOScale(1.12f, 0.55f).SetEase(Ease.InOutSine));
                if (newGlow != null)
                {
                    var c = newGlow.color; c.a = 0.25f; newGlow.color = c;
                    newPulse.Join(newGlow.DOFade(0.95f, 0.55f).SetEase(Ease.InOutSine));
                }
            }

            if (IsUpgradeReady && barArrow != null)
            {
                barArrow.anchoredPosition = new Vector2(barArrow.anchoredPosition.x, 0f);
                float h = barArrow.rect.height;
                arrowBounce = barArrow.DOAnchorPosY(h * 0.18f, 0.45f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
        }

        internal void SetSwapTarget(bool on)
        {
            highlightPulse?.Kill();
            highlightPulse = null;
            if (highlight == null) return;
            highlight.gameObject.SetActive(on);
            if (!on) return;
            var c = highlight.color; c.a = 0.35f; highlight.color = c;
            highlightPulse = highlight.DOFade(1f, 0.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            if (visual != null)
            {
                visual.DOKill();
                visual.localRotation = Quaternion.identity;
                visual.DOPunchRotation(new Vector3(0f, 0f, 4f), 0.5f, 8, 0.6f).SetUpdate(true);
            }
        }

        private void KillIdle()
        {
            newPulse?.Kill();
            newPulse = null;
            arrowBounce?.Kill();
            arrowBounce = null;
            if (newBadge != null) newBadge.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            if (Def != null) RefreshIdle();
        }

        private void OnDisable()
        {
            KillIdle();
            highlightPulse?.Kill();
            if (visual != null)
            {
                visual.DOKill();
                visual.localScale = Vector3.one;
                visual.localRotation = Quaternion.identity;
            }
            pressed = lifted = cardDrag = forwardDrag = false;
        }

        private void OnDestroy()
        {
            KillIdle();
            highlightPulse?.Kill();
            if (visual != null) visual.DOKill();
        }

        // ─────────────────────────── Input ───────────────────────────

        private void Update()
        {
            if (!pressed || lifted || dragHappened || visual == null) return;
            if (context != CardViewContext.DeckSlot || IsEmpty || owner == null || !owner.CanDragCard(this)) return;
            if (Time.unscaledTime - pressTime < HoldToDragSeconds) return;
            // Long-press "lift" tells the player the card can now be dragged.
            lifted = true;
            visual.DOKill();
            visual.DOScale(1.08f, 0.16f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true);
            owner.PlayLiftSound();
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            lifted = false;
            dragHappened = false;
            pressTime = Time.unscaledTime;
            if (visual == null) return;
            visual.DOKill();
            visual.localRotation = Quaternion.identity;
            visual.DOScale(0.93f, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            if (cardDrag || visual == null) return;
            visual.DOKill();
            visual.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 3f).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (!pressed || cardDrag || lifted || visual == null) return;
            visual.DOKill();
            visual.DOScale(1f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (dragHappened || owner == null) return;
            owner.OnCardTapped(this);
        }

        public void OnInitializePotentialDrag(PointerEventData e)
        {
            if (scrollParent != null) scrollParent.OnInitializePotentialDrag(e);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            dragHappened = true;
            bool held = Time.unscaledTime - pressTime >= HoldToDragSeconds;
            bool horizontal = Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y) * 1.2f;
            if (context == CardViewContext.DeckSlot && owner != null && owner.CanDragCard(this) && (held || horizontal))
            {
                cardDrag = true;
                owner.BeginCardDrag(this, e);
            }
            else
            {
                forwardDrag = true;
                if (visual != null)
                {
                    visual.DOKill();
                    visual.DOScale(1f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
                if (scrollParent != null) scrollParent.OnBeginDrag(e);
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (cardDrag) owner.DragCard(this, e);
            else if (forwardDrag && scrollParent != null) scrollParent.OnDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (cardDrag)
            {
                cardDrag = false;
                owner.EndCardDrag(this, e);
            }
            else if (forwardDrag)
            {
                forwardDrag = false;
                if (scrollParent != null) scrollParent.OnEndDrag(e);
            }
            pressed = false;
            lifted = false;
        }
    }
}
