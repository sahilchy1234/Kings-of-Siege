using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// World-Space 3D Floating Billboard Acquisition Banner.
/// Placed directly in the scene as a child of a defense tower (e.g. walltowerour/TowerAcquisitionBanner).
/// Designers can click, view, reposition, and customize every visual element in the Unity Scene View!
/// Features:
/// - World-Space billboarding facing camera
/// - Sliced team ribbon, rotating aura glow, crest emblem punch, living idle bobbing
/// - Snappy DOTween entrance and exit without destroying the GameObject
/// - Full Edit Mode preview support
/// </summary>
[DisallowMultipleComponent]
public class TowerWorldBanner : MonoBehaviour
{
    [Header("Hierarchy References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform bannerContainer;
    [SerializeField] private RectTransform auraRect;
    [SerializeField] private Image auraImage;
    [SerializeField] private Image ribbonImage;
    [SerializeField] private Image emblemImage;
    [SerializeField] private Image starLeft;
    [SerializeField] private Image starRight;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Positioning & Scale")]
    [Tooltip("Height above tower center in world units.")]
    [SerializeField] private float heightOffset = 11.2f;
    [Tooltip("Base banner width in canvas pixels.")]
    [SerializeField] private float baseBannerWidth = 300f;
    [Tooltip("Target physical world width in meters.")]
    [SerializeField] private float targetWorldWidth = 4.2f;

    [Header("Theming Sprites")]
    [SerializeField] private Sprite playerRibbonSprite;
    [SerializeField] private Sprite enemyRibbonSprite;
    [SerializeField] private Sprite neutralRibbonSprite;
    [SerializeField] private Sprite crownIconSprite;
    [SerializeField] private Sprite shieldIconSprite;
    [SerializeField] private Sprite swordsIconSprite;
    [SerializeField] private Sprite auraGlowSprite;
    [SerializeField] private Sprite starSprite;

    [Header("Theming Colors")]
    [SerializeField] private Color playerRibbonColor = new Color(0.04f, 0.52f, 0.89f, 1f); // Royal Blue (#0984E3)
    [SerializeField] private Color playerAccentColor = new Color(0.95f, 0.77f, 0.25f, 1f); // Royal Gold (#F1C40F)
    [SerializeField] private Color enemyRibbonColor = new Color(0.84f, 0.19f, 0.19f, 1f);  // Crimson Red (#D63031)
    [SerializeField] private Color enemyAccentColor = new Color(1.0f, 0.40f, 0.40f, 1f);  // Bright Coral
    [SerializeField] private Color neutralRibbonColor = new Color(0.95f, 0.65f, 0.15f, 1f); // Amber Orange

    [Header("Text Presets")]
    [SerializeField] private string playerTitle = "TOWER ACQUIRED!";
    [SerializeField] private string playerSubtitle = "★ DEFENSIVE OUTPOST ACTIVE ★";
    [SerializeField] private string enemyTitle = "TOWER LOST!";
    [SerializeField] private string enemySubtitle = "⚠ ENEMY OUTPOST ACTIVE ⚠";
    [SerializeField] private string neutralTitle = "TOWER CLAIMED!";
    [SerializeField] private string neutralSubtitle = "★ OUTPOST OCCUPIED ★";

    [Header("Animation Settings")]
    [SerializeField] private float holdDuration = 2.6f;
    [SerializeField] private float popInDuration = 0.45f;
    [SerializeField] private float dismissDuration = 0.28f;

    private Sequence activeSequence;
    private Tween auraRotateTween;
    private Tween floatBobTween;
    private Transform mainCameraTransform;
    private bool isPreviewing = false;
    private Vector3 restingScale;

    public float HeightOffset
    {
        get => heightOffset;
        set => heightOffset = value;
    }

    private void Awake()
    {
        CacheComponents();

        // In gameplay, banner starts hidden until tower is captured
        if (Application.isPlaying)
        {
            HideImmediate();
        }
    }

    private void Start()
    {
        if (Camera.main != null) mainCameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        // Smooth billboarding facing main camera
        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        if (mainCameraTransform != null && (Application.isPlaying || isPreviewing))
        {
            transform.rotation = mainCameraTransform.rotation;
        }
    }

    public void CacheComponents()
    {
        if (worldCanvas == null) worldCanvas = GetComponent<Canvas>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (bannerContainer == null)
        {
            Transform tr = transform.Find("BannerContainer");
            if (tr != null) bannerContainer = tr.GetComponent<RectTransform>();
            else bannerContainer = GetComponent<RectTransform>();
        }

        if (bannerContainer != null)
        {
            if (auraRect == null)
            {
                Transform aTr = bannerContainer.Find("AuraGlow");
                if (aTr != null)
                {
                    auraRect = aTr.GetComponent<RectTransform>();
                    auraImage = aTr.GetComponent<Image>();
                }
            }
            if (ribbonImage == null)
            {
                Transform rTr = bannerContainer.Find("Ribbon");
                if (rTr != null) ribbonImage = rTr.GetComponent<Image>();
            }
            if (emblemImage == null)
            {
                Transform eTr = bannerContainer.Find("EmblemIcon");
                if (eTr != null) emblemImage = eTr.GetComponent<Image>();
            }
            if (starLeft == null)
            {
                Transform slTr = bannerContainer.Find("StarLeft");
                if (slTr != null) starLeft = slTr.GetComponent<Image>();
            }
            if (starRight == null)
            {
                Transform srTr = bannerContainer.Find("StarRight");
                if (srTr != null) starRight = srTr.GetComponent<Image>();
            }
            if (titleText == null)
            {
                Transform tTr = bannerContainer.Find("TitleText");
                if (tTr != null) titleText = tTr.GetComponent<TextMeshProUGUI>();
            }
            if (subtitleText == null)
            {
                Transform sTr = bannerContainer.Find("SubtitleText");
                if (sTr != null) subtitleText = sTr.GetComponent<TextMeshProUGUI>();
            }
        }

        float worldScale = (baseBannerWidth > 0f) ? (targetWorldWidth / baseBannerWidth) : 0.014f;
        restingScale = Vector3.one * worldScale;
    }

    /// <summary>
    /// Triggers the full celebratory 3D banner animation over the tower.
    /// Uses DOTween elastic overshoot, continuous aura spin, crest punch, living bob, and exit.
    /// Resets without destroying the GameObject so it remains intact in the scene!
    /// </summary>
    public void PlayAcquisition(int acquiringTeam)
    {
        CacheComponents();
        KillActiveTweens();

        int localTeam = GameModeManager.LocalTeamId;
        bool isFriendly = (acquiringTeam == localTeam);
        bool isNeutral = (acquiringTeam < 0);

        ApplyTheme(isFriendly, isNeutral);

        // Position above tower
        transform.localPosition = new Vector3(0f, heightOffset, 0f);

        // Normalize target scale accounting for parent lossy scale
        Vector3 pLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float px = Mathf.Abs(pLossy.x) > 0.001f ? pLossy.x : 1f;
        float py = Mathf.Abs(pLossy.y) > 0.001f ? pLossy.y : 1f;
        float pz = Mathf.Abs(pLossy.z) > 0.001f ? pLossy.z : 1f;
        float baseScale = (baseBannerWidth > 0f) ? (targetWorldWidth / baseBannerWidth) : 0.014f;
        Vector3 targetScale = new Vector3(baseScale / px, baseScale / py, baseScale / pz);

        // Initial setup for entrance
        if (bannerContainer != null)
        {
            bannerContainer.localScale = Vector3.zero;
            bannerContainer.localPosition = Vector3.down * 25f;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Sequence
        activeSequence = DOTween.Sequence();

        // 1. Pop-in Elastic Entrance
        if (bannerContainer != null)
        {
            activeSequence.Append(bannerContainer.DOScale(targetScale * 1.25f, popInDuration * 0.45f).SetEase(Ease.OutQuad));
            activeSequence.Append(bannerContainer.DOScale(targetScale, popInDuration * 0.55f).SetEase(Ease.OutElastic, 1.35f, 0.6f));
            activeSequence.Join(bannerContainer.DOLocalMoveY(0f, popInDuration * 0.6f).SetEase(Ease.OutCubic));
        }
        if (canvasGroup != null)
        {
            activeSequence.Join(canvasGroup.DOFade(1f, 0.16f).SetEase(Ease.Linear));
        }

        // 2. Emblem Punch Scale
        activeSequence.InsertCallback(popInDuration * 0.5f, () =>
        {
            if (emblemImage != null)
            {
                emblemImage.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.45f, 6, 0.6f);
            }
        });

        // 3. Continuous Aura Glow Rotation
        if (auraRect != null)
        {
            auraRotateTween = auraRect.DORotate(new Vector3(0f, 0f, -360f), 5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }

        // 4. Subtle Living Float Bob
        activeSequence.InsertCallback(popInDuration + 0.1f, () =>
        {
            if (bannerContainer != null)
            {
                floatBobTween = bannerContainer.DOLocalMoveY(12f, 1.2f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        });

        // 5. Dismiss after presentation hold
        float totalShowTime = popInDuration + holdDuration;
        if (bannerContainer != null)
        {
            activeSequence.Insert(totalShowTime, bannerContainer.DOScale(targetScale * 1.08f, 0.08f).SetEase(Ease.OutQuad));
            activeSequence.Append(bannerContainer.DOScale(Vector3.zero, dismissDuration).SetEase(Ease.InBack));
            activeSequence.Join(bannerContainer.DOLocalMoveY(35f, dismissDuration).SetEase(Ease.InQuad));
        }
        if (canvasGroup != null)
        {
            activeSequence.Join(canvasGroup.DOFade(0f, dismissDuration * 0.8f).SetEase(Ease.Linear));
        }

        activeSequence.OnComplete(() =>
        {
            KillActiveTweens();
            HideImmediate();
        });
    }

    private void ApplyTheme(bool isFriendly, bool isNeutral)
    {
        // Ribbon Sprite and Color
        if (ribbonImage != null)
        {
            Sprite rSprite = isFriendly ? playerRibbonSprite : (isNeutral ? neutralRibbonSprite : enemyRibbonSprite);
            if (rSprite != null)
            {
                ribbonImage.sprite = rSprite;
                ribbonImage.color = Color.white;
            }
            else
            {
                ribbonImage.color = isFriendly ? playerRibbonColor : (isNeutral ? neutralRibbonColor : enemyRibbonColor);
            }
        }

        // Emblem Sprite and Color
        if (emblemImage != null)
        {
            Sprite chosenIcon = isFriendly ? (crownIconSprite != null ? crownIconSprite : shieldIconSprite)
                                           : (swordsIconSprite != null ? swordsIconSprite : shieldIconSprite);
            if (chosenIcon != null)
            {
                emblemImage.sprite = chosenIcon;
                emblemImage.color = Color.white;
            }
            else
            {
                emblemImage.color = isFriendly ? playerAccentColor : enemyAccentColor;
            }
        }

        // Aura Glow Tint
        if (auraImage != null)
        {
            if (auraGlowSprite != null) auraImage.sprite = auraGlowSprite;
            auraImage.color = isFriendly ? new Color(0.2f, 0.75f, 1f, 0.75f) : (isNeutral ? new Color(1f, 0.85f, 0.3f, 0.75f) : new Color(1f, 0.3f, 0.3f, 0.75f));
        }

        // Flanking Stars Tint
        Color starColor = isFriendly ? playerAccentColor : (isNeutral ? Color.yellow : enemyAccentColor);
        if (starLeft != null)
        {
            if (starSprite != null) starLeft.sprite = starSprite;
            starLeft.color = starColor;
        }
        if (starRight != null)
        {
            if (starSprite != null) starRight.sprite = starSprite;
            starRight.color = starColor;
        }

        // Title and Subtitle Text
        if (titleText != null)
        {
            titleText.text = isFriendly ? playerTitle : (isNeutral ? neutralTitle : enemyTitle);
        }
        if (subtitleText != null)
        {
            subtitleText.text = isFriendly ? playerSubtitle : (isNeutral ? neutralSubtitle : enemySubtitle);
            subtitleText.color = starColor;
        }
    }

    public void KillActiveTweens()
    {
        activeSequence?.Kill();
        activeSequence = null;

        auraRotateTween?.Kill();
        auraRotateTween = null;

        floatBobTween?.Kill();
        floatBobTween = null;

        if (bannerContainer != null) bannerContainer.DOKill();
        if (auraRect != null) auraRect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();
    }

    /// <summary>
    /// Displays banner in resting state in Edit Mode so the designer can visually adjust it in Scene View.
    /// </summary>
    [ContextMenu("Show in Scene (Edit Mode)")]
    public void ShowInSceneEditMode()
    {
        CacheComponents();
        KillActiveTweens();

        ApplyTheme(true, false);
        transform.localPosition = new Vector3(0f, heightOffset, 0f);

        Vector3 pLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float px = Mathf.Abs(pLossy.x) > 0.001f ? pLossy.x : 1f;
        float py = Mathf.Abs(pLossy.y) > 0.001f ? pLossy.y : 1f;
        float pz = Mathf.Abs(pLossy.z) > 0.001f ? pLossy.z : 1f;
        float baseScale = (baseBannerWidth > 0f) ? (targetWorldWidth / baseBannerWidth) : 0.014f;
        Vector3 targetScale = new Vector3(baseScale / px, baseScale / py, baseScale / pz);

        if (bannerContainer != null)
        {
            bannerContainer.localScale = targetScale;
            bannerContainer.localPosition = Vector3.zero;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (worldCanvas != null) worldCanvas.enabled = true;

        isPreviewing = true;
    }

    /// <summary>
    /// Hides the banner so it does not clutter play mode until a tower is captured.
    /// </summary>
    [ContextMenu("Hide (Gameplay Ready)")]
    public void HideImmediate()
    {
        CacheComponents();
        KillActiveTweens();

        if (bannerContainer != null)
        {
            bannerContainer.localScale = Vector3.zero;
            bannerContainer.localPosition = Vector3.zero;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        isPreviewing = false;
    }

    [ContextMenu("Preview Acquisition (Player - Blue)")]
    public void PreviewPlayer()
    {
        PlayAcquisition(0);
    }

    [ContextMenu("Preview Acquisition (Enemy - Red)")]
    public void PreviewEnemy()
    {
        PlayAcquisition(1);
    }
}
