using DG.Tweening;
using Siege.League;
using Siege.Profile;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.KingdomRoad
{
    /// <summary>
    /// Data only: works out claim state, locked / claimable rewards and the player marker position,
    /// then updates the prebuilt UI. Never creates or destroys rows.
    /// </summary>
    public class KingdomRoadManager : MonoBehaviour
    {
        [Header("Window")]
        public GameObject window;
        public RectTransform panel;
        public CanvasGroup canvasGroup;
        public Button closeButton;
        public Image markerBand;
        private Tween _progressTween;
        private Tween _scrollTween;

        [Header("Header")]
        public TMP_Text headerLeague;
        public TMP_Text headerRoman;
        public TMP_Text headerTrophies;
        public TMP_Text headerNext;
        public Image headerShield;
        public Sprite[] leagueShields;   // one per league index (Wood .. Champions), fallback only
        public Sprite[] rankBadgeByDivision;   // 28 UIPACK badges, slot = PlayerProfileData.RankTier.badgeIndex (27 = Kings)
        public RectTransform progressFill;

        [Header("Timeline")]
        public ScrollRect scroll;
        public RectTransform content;
        public KingdomRoadRow[] rows;             // top to bottom (highest trophies first)
        public RectTransform playerMarker;
        public TMP_Text playerMarkerText;

        [Header("Colours")]
        public Color cardColor = new Color(0.086f, 0.165f, 0.416f, 1f);      // #162A6A
        public Color nextColor = new Color(0.11f, 0.36f, 0.55f, 1f);         // highlighted "next" row
        public Color lockedColor = new Color(0.60f, 0.65f, 0.78f, 1f);
        public Color claimableColor = new Color(0.95f, 0.75f, 0.19f, 1f);
        public Color claimedColor = new Color(0.24f, 0.86f, 0.44f, 1f);

        private static readonly string[] Roman = { "I", "II", "III" };

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (window != null) window.SetActive(false);
        }

        private void OnEnable() => PlayerProfileData.OnProfileChanged += OnProfileChanged;
        private void OnDisable() => PlayerProfileData.OnProfileChanged -= OnProfileChanged;

        private void OnProfileChanged()
        {
            if (window != null && window.activeSelf) Refresh();
        }

        public void Open()
        {
            if (window == null) return;
            window.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panel != null)
            {
                panel.localScale = Vector3.one * 0.85f;
                panel.DOKill();
                panel.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            if (canvasGroup != null) canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
            if (markerBand != null)
            {
                markerBand.DOKill();
                markerBand.DOFade(0.45f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            Refresh();
            ScrollToPlayer();
        }

        public void Close()
        {
            if (window == null) return;
            if (panel == null || canvasGroup == null)
            {
                window.SetActive(false);
                return;
            }
            panel.DOKill();
            canvasGroup.DOKill();
            panel.DOScale(0.9f, 0.2f).SetUpdate(true);
            canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => window.SetActive(false));
        }

        public void Refresh()
        {
            int trophies = PlayerProfileData.Trophies;
            LeagueInfo info = LeagueSystem.Get(trophies);

            // Header
            if (headerLeague != null) headerLeague.text = info.league;
            if (headerRoman != null) headerRoman.text = info.division > 0 ? Roman[info.division - 1] : "";
            if (headerShield != null)
            {
                int badgeIdx = info.division == 0 ? PlayerProfileData.RANK_BADGE_KINGS_INDEX
                    : info.leagueIndex * LeagueSystem.DIVISIONS_PER_LEAGUE + (info.division - 1);
                Sprite s = null;
                if (rankBadgeByDivision != null && badgeIdx < rankBadgeByDivision.Length) s = rankBadgeByDivision[badgeIdx];
                if (s == null && leagueShields != null && leagueShields.Length > 0)
                    s = leagueShields[Mathf.Clamp(info.leagueIndex, 0, leagueShields.Length - 1)];
                if (s != null) headerShield.sprite = s;
            }
            if (headerLeague != null && info.division > 0) headerLeague.text = info.league + " " + Roman[info.division - 1];
            if (headerTrophies != null) headerTrophies.text = trophies + " trophies";
            if (headerNext != null)
            {
                if (info.maxTrophies < int.MaxValue)
                {
                    LeagueInfo nextInfo = LeagueSystem.Get(info.maxTrophies + 1);
                    headerNext.text = nextInfo.label + " at " + nextInfo.minTrophies;
                }
                else headerNext.text = "";
            }
            if (progressFill != null)
            {
                float span = info.maxTrophies == int.MaxValue ? 1f : info.maxTrophies - info.minTrophies + 1;
                float t = Mathf.Clamp01((trophies - info.minTrophies) / span);
                var fill = progressFill;
                _progressTween?.Kill();
                _progressTween = DOTween.To(() => fill.anchorMax.x, x => fill.anchorMax = new Vector2(x, 1f), t, 0.6f)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            }

            // Next reward = smallest non-banner threshold above the player
            KingdomRoadRow next = null;
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    if (r == null || r.isDivisionBanner) continue;
                    if (r.threshold > trophies && (next == null || r.threshold < next.threshold)) next = r;
                }
            }

            // Row states
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    if (r == null || r.isDivisionBanner) continue;
                    bool reached = trophies >= r.threshold;
                    bool claimed = reached && PlayerPrefs.GetInt(r.ClaimKey, 0) == 1;
                    bool isNext = r == next;

                    bool locked = !reached && !isNext;
                    if (r.claimedIcon != null) r.claimedIcon.SetActive(claimed);
                    if (r.glow != null) r.glow.gameObject.SetActive(reached && !claimed);
                    if (r.button != null) r.button.interactable = reached && !claimed;
                    if (r.background != null) r.background.gameObject.SetActive(isNext);

                    if (r.subtitleText != null)
                    {
                        r.subtitleText.margin = new Vector4(claimed ? 46f : 0f, 0f, 0f, 0f);
                        if (claimed) { r.subtitleText.text = "Claimed"; r.subtitleText.color = lockedColor; }
                        else if (reached) { r.subtitleText.text = "Tap to claim"; r.subtitleText.color = claimableColor; }
                        else if (isNext) { r.subtitleText.text = (r.threshold - trophies) + " trophies away"; r.subtitleText.color = lockedColor; }
                        else { r.subtitleText.text = ""; }
                    }

                    // Title sits centred when there is no subtitle, like the reference.
                    if (r.titleText != null)
                    {
                        var trt = r.titleText.rectTransform;
                        bool hasSub = r.subtitleText != null && !string.IsNullOrEmpty(r.subtitleText.text);
                        trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, hasSub ? 22f : 0f);
                    }

                    // Lock sits right after the title text.
                    if (r.lockIcon != null)
                    {
                        r.lockIcon.SetActive(locked);
                        if (locked && r.titleText != null)
                        {
                            var trt = r.titleText.rectTransform;
                            var lrt = (RectTransform)r.lockIcon.transform;
                            float w = Mathf.Min(r.titleText.preferredWidth, trt.rect.width);
                            lrt.anchoredPosition = new Vector2(trt.anchoredPosition.x + w + 44f, trt.anchoredPosition.y);
                        }
                    }
                }
            }

            PlaceMarker(trophies);
        }

        /// <summary>Moves the "You" marker to sit just above the first row the player has not reached yet.</summary>
        private void PlaceMarker(int trophies)
        {
            if (playerMarker == null) return;
            if (playerMarkerText != null) playerMarkerText.text = "You · " + trophies;

            KingdomRoadRow target = null;
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    if (r == null) continue;
                    if (r.threshold <= trophies) { target = r; break; }
                }
            }

            if (target == null)
            {
                playerMarker.SetAsLastSibling();
                return;
            }

            int targetIdx = target.transform.GetSiblingIndex();
            if (playerMarker.GetSiblingIndex() < targetIdx) playerMarker.SetSiblingIndex(targetIdx - 1);
            else playerMarker.SetSiblingIndex(targetIdx);
        }

        private void ScrollToPlayer()
        {
            if (scroll == null || playerMarker == null || content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float viewH = scroll.viewport != null ? scroll.viewport.rect.height : 0f;
            float contentH = content.rect.height;
            float span = contentH - viewH;
            if (span <= 0f) { scroll.verticalNormalizedPosition = 1f; return; }
            float y = -playerMarker.anchoredPosition.y - viewH * 0.5f;
            float target = 1f - Mathf.Clamp01(y / span);
            _scrollTween?.Kill();
            scroll.verticalNormalizedPosition = 1f;
            _scrollTween = DOTween.To(() => scroll.verticalNormalizedPosition, v => scroll.verticalNormalizedPosition = v, target, 0.7f)
                .SetDelay(0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        /// <summary>Called by a row's tap. Grants the reward when the player has reached it and it is not claimed yet.</summary>
        public void Claim(KingdomRoadRow row)
        {
            if (row == null || row.isDivisionBanner) return;
            if (PlayerProfileData.Trophies < row.threshold) return;
            if (PlayerPrefs.GetInt(row.ClaimKey, 0) == 1) return;

            PlayerPrefs.SetInt(row.ClaimKey, 1);
            PlayerPrefs.Save();
            Grant(row);
            row.transform.DOKill();
            row.transform.DOPunchScale(Vector3.one * 0.06f, 0.35f, 6, 0.5f).SetUpdate(true);
            if (row.icon != null)
            {
                row.icon.transform.DOKill();
                row.icon.transform.DOPunchRotation(new Vector3(0, 0, 12f), 0.4f, 6).SetUpdate(true);
            }
            Refresh();
        }

        private static void Grant(KingdomRoadRow row)
        {
            switch (row.kind)
            {
                case RewardKind.Coins:
                    PlayerWallet.AddCoins(row.amount);
                    break;
                default:
                    // No inventory system for chests / cards / badges yet: count them in PlayerPrefs (same as Daily Rewards).
                    string key = "KoS_Inv_KingdomRoad_" + row.kind;
                    PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + Mathf.Max(1, row.amount));
                    PlayerPrefs.Save();
                    break;
            }
        }
    }
}
