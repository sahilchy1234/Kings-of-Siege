using System;
using System.Collections.Generic;
using DG.Tweening;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Siege.DailyRewards
{
    public enum DailyRewardKind { Coins, Gems, ChestKey, SilverChest, GoldChest }

    [Serializable]
    public class DailyReward
    {
        public string label = "Reward";
        public DailyRewardKind kind = DailyRewardKind.Coins;
        public int amount = 1;
        public Sprite icon;
        [Tooltip("Extra gems granted on top of the main reward (e.g. Day 7 Gold Chest + 25 Gems).")]
        public int bonusGems;
    }

    /// <summary>
    /// The Daily Rewards page. Everything it shows is prebuilt in the scene.
    /// At runtime this script only updates values, states and DOTween animations on those
    /// existing objects. It never creates or destroys GameObjects.
    /// </summary>
    public class DailyRewardsScreenController : MonoBehaviour
    {
        [Serializable]
        public class DayTile
        {
            public Image frame;          // bevel frame, sprite swapped per state
            public Image body;           // royal-blue panel
            public Image icon;           // reward icon
            public TMP_Text dayLabel;    // "Day 1"
            public TMP_Text amountLabel; // "+500"
            public GameObject checkmark; // shown when claimed
            [FormerlySerializedAs("lockIcon")]
            public GameObject claimedOverlay; // dark veil shown when claimed
            public Image glow;           // gold outline for today
        }

        [Header("Page")]
        [SerializeField] private GameObject pageRoot;      // child that is shown/hidden
        [SerializeField] private CanvasGroup dimmer;       // backdrop fade
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;   // "Streak: 3 / 7"

        [Header("Days (size 7)")]
        [SerializeField] private List<DayTile> tiles = new List<DayTile>();
        [SerializeField] private List<DailyReward> rewards = new List<DailyReward>();

        [Header("Claim")]
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;

        [Header("Tile frames per state")]
        [SerializeField] private Sprite upcomingFrame;  // silver
        [SerializeField] private Sprite todayFrame;     // gold
        [SerializeField] private Sprite claimedFrame;   // grey
        [SerializeField] private TMP_Text timerLabel;   // "Next reward in 05:12:33"

        [Header("Claim button sprites")]
        [SerializeField] private Image claimButtonImage;
        [SerializeField] private Sprite claimReadySprite;   // green
        [SerializeField] private Sprite claimWaitSprite;    // grey

        [Header("Juice (DOTween) - all optional")]
        [SerializeField] private RectTransform window;        // main framed panel
        [SerializeField] private RectTransform ribbon;        // title ribbon (drops in)
        [SerializeField] private RectTransform calendarIcon;  // pops and wiggles
        [SerializeField] private RectTransform streakPill;
        [SerializeField] private RectTransform chestGlow;     // Day 7 halo (spins + breathes)
        [SerializeField] private List<RectTransform> sparkles = new List<RectTransform>();
        [SerializeField] private RectTransform coinTarget;    // HUD coin icon that coin rewards fly to
        [SerializeField] private RectTransform gemTarget;     // HUD gem icon that gem rewards fly to
        [SerializeField, Range(0.3f, 2f)] private float juiceSpeed = 1f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip claimSound;

        // Tween ids so groups can be killed together.
        private readonly object introId = new object();
        private readonly object idleId = new object();
        private readonly object claimId = new object();

        private struct RectPose { public Vector2 pos; public Vector3 scale; public Quaternion rot; }
        private readonly Dictionary<RectTransform, RectPose> poses = new Dictionary<RectTransform, RectPose>();
        private readonly List<int> tileOrder = new List<int>();
        private bool claimAnimating;
        private bool lastCanClaim;
        private float timerTick;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimPressed);
            CachePoses();
            if (pageRoot != null) pageRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            KillAll();
        }

        /// <summary>Hook this to the side-panel button_reward OnClick.</summary>
        public void Open()
        {
            if (pageRoot == null)
            {
                Debug.LogWarning("[DailyRewards] pageRoot is not set.");
                return;
            }

            KillAll();
            RestorePoses(false);
            pageRoot.SetActive(true);
            claimAnimating = false;
            Refresh(false);
            PlayIntro();
        }

        public void Close()
        {
            if (pageRoot == null || !pageRoot.activeSelf) return;
            KillAll();
            claimAnimating = false;
            float d = 0.18f / juiceSpeed;
            var seq = DOTween.Sequence().SetUpdate(true).SetId(introId);
            if (window != null) seq.Join(window.DOScale(Pose(window).scale * 0.75f, d).SetEase(Ease.InBack));
            if (dimmer != null) seq.Join(dimmer.DOFade(0f, d));
            seq.OnComplete(() =>
            {
                RestorePoses(false);
                pageRoot.SetActive(false);
            });
        }

        // ── Intro ──────────────────────────────────────────────────────────

        private void PlayIntro()
        {
            float k = 1f / juiceSpeed;
            var seq = DOTween.Sequence().SetUpdate(true).SetId(introId);

            if (dimmer != null)
            {
                dimmer.alpha = 0f;
                seq.Insert(0f, dimmer.DOFade(1f, 0.25f * k));
            }

            if (window != null)
            {
                window.localScale = Pose(window).scale * 0.55f;
                window.localRotation = Quaternion.Euler(0, 0, -4f);
                seq.Insert(0f, window.DOScale(Pose(window).scale, 0.45f * k).SetEase(Ease.OutBack, 1.6f));
                seq.Insert(0f, window.DOLocalRotateQuaternion(Pose(window).rot, 0.6f * k).SetEase(Ease.OutElastic));
            }

            if (ribbon != null)
            {
                Vector2 home = Pose(ribbon).pos;
                ribbon.anchoredPosition = home + new Vector2(0f, 420f);
                seq.Insert(0.15f * k, ribbon.DOAnchorPos(home, 0.5f * k).SetEase(Ease.OutBounce));
                seq.Insert(0.65f * k, ribbon.DOPunchScale(new Vector3(0.12f, -0.08f, 0f), 0.35f * k, 6, 0.5f));
            }

            if (calendarIcon != null)
            {
                calendarIcon.localScale = Vector3.zero;
                seq.Insert(0.35f * k, calendarIcon.DOScale(Pose(calendarIcon).scale, 0.4f * k).SetEase(Ease.OutBack, 2.5f));
                seq.Insert(0.6f * k, calendarIcon.DOPunchRotation(new Vector3(0, 0, 25f), 0.6f * k, 8, 0.6f));
            }

            if (closeButton != null)
            {
                var c = (RectTransform)closeButton.transform;
                c.localScale = Vector3.zero;
                seq.Insert(0.45f * k, c.DOScale(Pose(c).scale, 0.35f * k).SetEase(Ease.OutBack, 3f));
                seq.Insert(0.45f * k, c.DOLocalRotate(new Vector3(0, 0, 360f), 0.45f * k, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            }

            if (streakPill != null)
            {
                streakPill.localScale = new Vector3(0f, Pose(streakPill).scale.y, 1f);
                seq.Insert(0.3f * k, streakPill.DOScale(Pose(streakPill).scale, 0.35f * k).SetEase(Ease.OutBack));
            }

            // Tiles cascade in; Day 7 slams down last and shakes the window.
            for (int i = 0; i < tiles.Count; i++)
            {
                var root = TileRoot(i);
                if (root == null) continue;
                bool grand = i == tiles.Count - 1;
                float at = (0.25f + i * 0.06f) * k;

                if (grand)
                {
                    at += 0.1f * k;
                    root.localScale = Pose(root).scale * 1.6f;
                    var cg = root.GetComponent<CanvasGroup>();
                    if (cg != null) { cg.alpha = 0f; seq.Insert(at, cg.DOFade(1f, 0.15f * k)); }
                    seq.Insert(at, root.DOScale(Pose(root).scale, 0.3f * k).SetEase(Ease.InQuad));
                    seq.InsertCallback(at + 0.3f * k, () => ShakeWindow(14f, 0.3f));
                    seq.Insert(at + 0.3f * k, root.DOPunchScale(Vector3.one * 0.06f, 0.3f * k, 6, 0.5f));
                }
                else
                {
                    root.localScale = Vector3.zero;
                    root.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 12f : -12f);
                    seq.Insert(at, root.DOScale(Pose(root).scale, 0.38f * k).SetEase(Ease.OutBack, 2f));
                    seq.Insert(at, root.DOLocalRotateQuaternion(Pose(root).rot, 0.38f * k).SetEase(Ease.OutBack));
                }

                var icon = tiles[i].icon != null ? tiles[i].icon.rectTransform : null;
                if (icon != null)
                {
                    icon.localScale = Vector3.zero;
                    seq.Insert(at + 0.2f * k, icon.DOScale(Pose(icon).scale, 0.35f * k).SetEase(Ease.OutBack, 3f));
                }
            }

            if (claimButton != null)
            {
                var b = (RectTransform)claimButton.transform;
                Vector2 home = Pose(b).pos;
                b.anchoredPosition = home + new Vector2(0f, -260f);
                seq.Insert(0.75f * k, b.DOAnchorPos(home, 0.45f * k).SetEase(Ease.OutBack, 2f));
            }
            if (timerLabel != null)
            {
                timerLabel.alpha = 0f;
                seq.Insert(0.85f * k, timerLabel.DOFade(1f, 0.3f * k));
            }

            seq.OnComplete(StartIdle);
        }

        // ── Idle loops ─────────────────────────────────────────────────────

        private void StartIdle()
        {
            DOTween.Kill(idleId);
            if (pageRoot == null || !pageRoot.activeInHierarchy) return;
            var s = DailyRewardsManager.GetStatus();

            // Today's tile: pulsing gold outline, breathing tile, bobbing + rocking icon.
            if (s.canClaim && s.todayIndex < tiles.Count && tiles[s.todayIndex] != null)
            {
                var t = tiles[s.todayIndex];
                if (t.glow != null)
                {
                    var gc = t.glow.color; gc.a = 0.25f; t.glow.color = gc;
                    t.glow.DOFade(1f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                }
                var root = TileRoot(s.todayIndex);
                if (root != null)
                    root.DOScale(Pose(root).scale * 1.05f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                if (t.icon != null)
                {
                    var ic = t.icon.rectTransform;
                    ic.DOAnchorPosY(Pose(ic).pos.y + 12f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                    ic.localRotation = Quaternion.Euler(0, 0, -6f);
                    ic.DOLocalRotate(new Vector3(0, 0, 6f), 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                }
            }

            // Day 7 chest floats and does a little hop every few seconds.
            int g = tiles.Count - 1;
            if (g >= 0 && tiles[g] != null && tiles[g].icon != null && !(s.canClaim && g == s.todayIndex))
            {
                var chest = tiles[g].icon.rectTransform;
                chest.DOAnchorPosY(Pose(chest).pos.y + 10f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                DOTween.Sequence().SetUpdate(true).SetId(idleId).SetLoops(-1)
                    .AppendInterval(2.2f)
                    .Append(chest.DOPunchRotation(new Vector3(0, 0, 10f), 0.5f, 10, 0.5f))
                    .Join(chest.DOPunchScale(Vector3.one * 0.08f, 0.5f, 6, 0.5f));
            }

            // Halo behind the grand chest spins and breathes.
            if (chestGlow != null)
            {
                chestGlow.DOLocalRotate(new Vector3(0, 0, -360f), 9f, RotateMode.FastBeyond360).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear).SetUpdate(true).SetId(idleId);
                chestGlow.DOScale(Pose(chestGlow).scale * 1.12f, 1.1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
            }

            // Sparkles twinkle in turn.
            for (int i = 0; i < sparkles.Count; i++)
            {
                var sp = sparkles[i];
                if (sp == null) continue;
                sp.localScale = Vector3.zero;
                DOTween.Sequence().SetUpdate(true).SetId(idleId).SetLoops(-1)
                    .AppendInterval(0.35f + i * 0.55f)
                    .Append(sp.DOScale(Pose(sp).scale * 1.3f, 0.25f).SetEase(Ease.OutBack))
                    .Join(sp.DOLocalRotate(new Vector3(0, 0, 90f), 0.6f, RotateMode.FastBeyond360).SetRelative())
                    .Append(sp.DOScale(0f, 0.3f).SetEase(Ease.InBack))
                    .AppendInterval(0.9f);
            }

            // Claim button breathes and nudges the player.
            if (claimButton != null && s.canClaim)
            {
                var b = (RectTransform)claimButton.transform;
                b.DOScale(Pose(b).scale * 1.06f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetId(idleId);
                DOTween.Sequence().SetUpdate(true).SetId(idleId).SetLoops(-1)
                    .AppendInterval(2.5f)
                    .Append(b.DOPunchRotation(new Vector3(0, 0, 6f), 0.45f, 10, 0.6f));
            }

            if (calendarIcon != null)
                DOTween.Sequence().SetUpdate(true).SetId(idleId).SetLoops(-1)
                    .AppendInterval(3.5f)
                    .Append(calendarIcon.DOPunchRotation(new Vector3(0, 0, 18f), 0.6f, 8, 0.5f));
        }

        // ── State ──────────────────────────────────────────────────────────

        /// <summary>Re-reads the streak and updates every tile, the subtitle and the claim button.</summary>
        public void Refresh() { Refresh(true); }

        private void Refresh(bool restartIdle)
        {
            DailyRewardsManager.Status s = DailyRewardsManager.GetStatus();

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null) continue;

                bool isToday = i == s.todayIndex;
                bool claimed = i < s.todayIndex || (isToday && !s.canClaim);
                bool locked = i > s.todayIndex;

                DailyReward r = i < rewards.Count ? rewards[i] : null;
                // Day labels are prebuilt text in the scene; edit them in the Inspector.
                if (tile.amountLabel != null) tile.amountLabel.text = r != null ? FormatAmount(r) : "";
                if (tile.icon != null)
                {
                    tile.icon.sprite = r != null ? r.icon : null;
                    tile.icon.enabled = tile.icon.sprite != null;
                    tile.icon.color = locked ? new Color(1f, 1f, 1f, 0.85f) : Color.white;
                }

                if (tile.checkmark != null) tile.checkmark.SetActive(claimed);
                if (tile.claimedOverlay != null) tile.claimedOverlay.SetActive(claimed);
                if (tile.glow != null) tile.glow.gameObject.SetActive(isToday && s.canClaim);

                if (tile.frame != null)
                {
                    Sprite f = claimed ? claimedFrame : (isToday && s.canClaim) ? todayFrame : upcomingFrame;
                    if (f != null) tile.frame.sprite = f;
                }
            }

            if (subtitleLabel != null)
                subtitleLabel.text = "Streak: " + s.claimedCount + " / " + DailyRewardsManager.DayCount;

            if (claimButton != null) claimButton.interactable = s.canClaim;
            if (claimLabel != null) claimLabel.text = s.canClaim ? "CLAIM" : "CLAIMED";
            if (claimButtonImage != null)
            {
                Sprite b = s.canClaim ? claimReadySprite : claimWaitSprite;
                if (b != null) claimButtonImage.sprite = b;
            }
            lastCanClaim = s.canClaim;
            UpdateTimer();

            if (restartIdle && !claimAnimating && pageRoot != null && pageRoot.activeInHierarchy)
            {
                DOTween.Kill(idleId);
                RestorePoses(true);
                StartIdle();
            }
        }

        private void Update()
        {
            if (pageRoot == null || !pageRoot.activeInHierarchy) return;
            timerTick -= Time.unscaledDeltaTime;
            if (timerTick > 0f) return;
            timerTick = 1f;
            // Midnight rolled over while the page is open: unlock the next day.
            if (!lastCanClaim && !claimAnimating && DailyRewardsManager.GetStatus().canClaim) Refresh();
            else UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (timerLabel == null) return;
            if (lastCanClaim) { timerLabel.text = "Your reward is ready!"; return; }
            TimeSpan left = DateTime.Now.Date.AddDays(1) - DateTime.Now;
            timerLabel.text = string.Format("Next reward in {0:00}:{1:00}:{2:00}", (int)left.TotalHours, left.Minutes, left.Seconds);
        }

        // ── Claim ──────────────────────────────────────────────────────────

        private void OnClaimPressed()
        {
            if (claimAnimating) return;
            if (!DailyRewardsManager.TryClaim(out int dayIndex)) return;

            DailyReward r = dayIndex < rewards.Count ? rewards[dayIndex] : null;
            if (r != null)
            {
                GrantReward(r);
                if (r.bonusGems > 0) PlayerWallet.AddGems(r.bonusGems);
            }
            if (audioSource != null && claimSound != null) audioSource.PlayOneShot(claimSound);

            PlayClaim(dayIndex, r);
        }

        private void PlayClaim(int dayIndex, DailyReward r)
        {
            claimAnimating = true;
            DOTween.Kill(idleId);
            RestorePoses(true);
            if (claimButton != null) claimButton.interactable = false;

            float k = 1f / juiceSpeed;
            DayTile tile = dayIndex >= 0 && dayIndex < tiles.Count ? tiles[dayIndex] : null;
            RectTransform root = TileRoot(dayIndex);
            var seq = DOTween.Sequence().SetUpdate(true).SetId(claimId);

            // Button squash.
            if (claimButton != null)
                seq.Insert(0f, ((RectTransform)claimButton.transform).DOPunchScale(new Vector3(0.2f, -0.15f, 0f), 0.3f * k, 8, 0.6f));

            // Tile pops forward and wobbles.
            if (root != null)
            {
                root.SetAsLastSibling();
                seq.Insert(0f, root.DOScale(Pose(root).scale * 1.18f, 0.18f * k).SetEase(Ease.OutQuad));
                seq.Insert(0.18f * k, root.DOPunchRotation(new Vector3(0, 0, 10f), 0.35f * k, 10, 0.6f));
            }

            // Icon spins up big, then flies into the wallet (or rockets up if there is no target).
            float flyStart = 0.3f * k;
            float flyTime = 0.55f * k;
            if (tile != null && tile.icon != null)
            {
                var ic = tile.icon.rectTransform;
                seq.Insert(0f, ic.DOScale(Pose(ic).scale * 1.6f, 0.25f * k).SetEase(Ease.OutBack, 3f));
                seq.Insert(0f, ic.DOLocalRotate(new Vector3(0, 0, 360f), 0.3f * k, RotateMode.FastBeyond360));

                RectTransform target = r == null ? null :
                    r.kind == DailyRewardKind.Coins ? coinTarget :
                    r.kind == DailyRewardKind.Gems ? gemTarget : null;

                if (target != null && target.gameObject.activeInHierarchy)
                {
                    seq.Insert(flyStart, ic.DOJump(target.position, 220f * CanvasScale(ic), 1, flyTime).SetEase(Ease.InQuad));
                    seq.Insert(flyStart, ic.DOScale(Pose(ic).scale * 0.6f, flyTime).SetEase(Ease.InQuad));
                    seq.InsertCallback(flyStart + flyTime, () =>
                    {
                        target.DOKill(true);
                        target.DOPunchScale(Vector3.one * 0.45f, 0.4f, 8, 0.6f).SetUpdate(true).SetId(claimId)
                            .OnComplete(() => target.localScale = Pose(target).scale);
                    });
                }
                else
                {
                    seq.Insert(flyStart, ic.DOAnchorPosY(Pose(ic).pos.y + 160f, 0.45f * k).SetEase(Ease.OutCubic));
                    seq.Insert(flyStart + 0.15f * k, tile.icon.DOFade(0f, 0.3f * k));
                }
            }
            if (tile != null && tile.amountLabel != null)
                seq.Insert(0.1f * k, tile.amountLabel.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.4f * k, 7, 0.6f));

            // Swap to claimed visuals and stamp the checkmark.
            seq.InsertCallback(flyStart + flyTime, () =>
            {
                Refresh(false);
                RestorePoses(true);
                if (tile == null) return;

                if (tile.icon != null)
                {
                    var ic = tile.icon.rectTransform;
                    ic.localScale = Vector3.zero;
                    ic.DOScale(Pose(ic).scale, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetId(claimId);
                }
                if (tile.claimedOverlay != null)
                {
                    var ov = tile.claimedOverlay.GetComponent<Image>();
                    if (ov != null)
                    {
                        float a = ov.color.a;
                        var c = ov.color; c.a = 0f; ov.color = c;
                        ov.DOFade(a, 0.25f).SetUpdate(true).SetId(claimId);
                    }
                }
                if (tile.checkmark != null)
                {
                    var ck = (RectTransform)tile.checkmark.transform;
                    ck.localScale = Pose(ck).scale * 3f;
                    ck.localRotation = Quaternion.Euler(0, 0, -30f);
                    ck.DOScale(Pose(ck).scale, 0.3f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true).SetId(claimId);
                    ck.DOLocalRotateQuaternion(Pose(ck).rot, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetId(claimId);
                }
                ShakeWindow(18f, 0.35f);
                if (streakPill != null) streakPill.DOPunchScale(Vector3.one * 0.25f, 0.4f, 8, 0.6f).SetUpdate(true).SetId(claimId);
            });

            seq.AppendInterval(0.45f * k);
            seq.OnComplete(() =>
            {
                claimAnimating = false;
                RestorePoses(true);
                Refresh(false);
                StartIdle();
            });
        }

        private static float CanvasScale(RectTransform rt)
        {
            var c = rt.GetComponentInParent<Canvas>();
            return c != null ? c.rootCanvas.transform.lossyScale.y : 1f;
        }

        private void ShakeWindow(float strength, float duration)
        {
            if (window == null) return;
            window.DOKill(true);
            window.anchoredPosition = Pose(window).pos;
            window.DOShakeAnchorPos(duration, strength, 22, 90f, false, true).SetUpdate(true).SetId(claimId)
                .OnComplete(() => window.anchoredPosition = Pose(window).pos);
        }

        // ── Rewards ────────────────────────────────────────────────────────

        private static void GrantReward(DailyReward r)
        {
            switch (r.kind)
            {
                case DailyRewardKind.Coins: PlayerWallet.AddCoins(r.amount); break;
                case DailyRewardKind.Gems: PlayerWallet.AddGems(r.amount); break;
                case DailyRewardKind.ChestKey: AddInventory("ChestKey", r.amount); break;
                case DailyRewardKind.SilverChest: AddInventory("SilverChest", r.amount); break;
                case DailyRewardKind.GoldChest: AddInventory("GoldChest", r.amount); break;
            }
        }

        // Keys and chests have no wallet yet, so they are counted in PlayerPrefs ("KoS_Inv_<name>").
        private static void AddInventory(string item, int amount)
        {
            string key = "KoS_Inv_" + item;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
            PlayerPrefs.Save();
        }

        private static string FormatAmount(DailyReward r)
        {
            switch (r.kind)
            {
                case DailyRewardKind.Coins:
                case DailyRewardKind.Gems:
                    return "+" + r.amount.ToString("N0");
                default:
                    return r.amount > 1 ? "x" + r.amount : r.label;
            }
        }

        // ── Pose helpers (every animated rect snaps back to its scene layout) ──

        private RectTransform TileRoot(int i)
        {
            if (i < 0 || i >= tiles.Count || tiles[i] == null) return null;
            if (tiles[i].frame != null) return tiles[i].frame.transform.parent as RectTransform;
            if (tiles[i].body != null) return tiles[i].body.transform.parent as RectTransform;
            return null;
        }

        private void Track(RectTransform rt)
        {
            if (rt == null || poses.ContainsKey(rt)) return;
            poses[rt] = new RectPose { pos = rt.anchoredPosition, scale = rt.localScale, rot = rt.localRotation };
        }

        private void CachePoses()
        {
            poses.Clear();
            Track(window); Track(ribbon); Track(calendarIcon); Track(streakPill); Track(chestGlow);
            Track(coinTarget); Track(gemTarget);
            if (closeButton != null) Track((RectTransform)closeButton.transform);
            if (claimButton != null) Track((RectTransform)claimButton.transform);
            foreach (var sp in sparkles) Track(sp);
            tileOrder.Clear();
            for (int i = 0; i < tiles.Count; i++)
            {
                var root = TileRoot(i);
                Track(root);
                tileOrder.Add(root != null ? root.GetSiblingIndex() : -1);
                var t = tiles[i];
                if (t == null) continue;
                if (t.icon != null) Track(t.icon.rectTransform);
                if (t.amountLabel != null) Track(t.amountLabel.rectTransform);
                if (t.checkmark != null) Track((RectTransform)t.checkmark.transform);
            }
        }

        private RectPose Pose(RectTransform rt)
        {
            if (rt == null) return new RectPose { scale = Vector3.one, rot = Quaternion.identity };
            Track(rt);
            return poses[rt];
        }

        private void RestorePoses(bool keepWindow)
        {
            foreach (var kv in poses)
            {
                if (kv.Key == null) continue;
                if (keepWindow && kv.Key == window) continue;
                kv.Key.anchoredPosition = kv.Value.pos;
                kv.Key.localScale = kv.Value.scale;
                kv.Key.localRotation = kv.Value.rot;
            }
            for (int i = 0; i < tiles.Count && i < tileOrder.Count; i++)
            {
                var root = TileRoot(i);
                if (root != null && tileOrder[i] >= 0) root.SetSiblingIndex(tileOrder[i]);
            }
            if (timerLabel != null) timerLabel.alpha = 1f;
        }

        private void KillAll()
        {
            DOTween.Kill(introId);
            DOTween.Kill(idleId);
            DOTween.Kill(claimId);
        }
    }
}
