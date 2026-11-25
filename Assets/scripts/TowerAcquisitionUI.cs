using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// High-juice, AAA mobile RTS acquisition feedback and UI system powered by DOTween.
/// Configured directly in the scene as concrete GameObjects:
/// - Screen-Space HUD Notification Toast (under Canvas/TowerAcquisitionToast)
/// - 3D World-Space Floating Billboard Banner (under each capturable wall tower)
/// 
/// Designers can click, view, reposition, and customize every visual element in the Unity Hierarchy & Scene View!
/// Does NOT destroy scene objects after playing, allowing repeated captures and persistent editing.
/// </summary>
[DisallowMultipleComponent]
public class TowerAcquisitionUI : MonoBehaviour
{
    private static TowerAcquisitionUI instance;
    public static TowerAcquisitionUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TowerAcquisitionUI>();
                if (instance == null)
                {
                    // Look for TowerAcquisitionToast under screen Canvas
                    Canvas canvas = FindScreenCanvas();
                    if (canvas != null)
                    {
                        Transform toastTr = canvas.transform.Find("TowerAcquisitionToast");
                        if (toastTr != null)
                        {
                            instance = toastTr.GetComponent<TowerAcquisitionUI>();
                            if (instance == null) instance = toastTr.gameObject.AddComponent<TowerAcquisitionUI>();
                        }
                    }
                }
                if (instance == null)
                {
                    GameObject go = new GameObject("[TowerAcquisitionUI]");
                    instance = go.AddComponent<TowerAcquisitionUI>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Screen Toast UI Hierarchy References")]
    [SerializeField] private RectTransform toastRect;
    [SerializeField] private CanvasGroup toastCanvasGroup;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image leftIcon;
    [SerializeField] private Image rightIcon;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private RectTransform shineSlice;

    [Header("Screen Toast Layout & Animation Settings")]
    [Tooltip("Target anchored Y position when toast is visible below top screen edge.")]
    [SerializeField] private float visiblePosY = -38f;
    [Tooltip("Offscreen hidden anchored Y position above screen top.")]
    [SerializeField] private float hiddenPosY = 110f;
    [SerializeField] private float dropDuration = 0.42f;
    [SerializeField] private float holdDuration = 2.4f;
    [SerializeField] private float dismissDuration = 0.32f;
    [SerializeField] private Vector3 punchScaleAmount = new Vector3(0.08f, 0.08f, 0f);

    [Header("Theming Sprites")]
    [SerializeField] private Sprite blueRibbonSprite;
    [SerializeField] private Sprite redRibbonSprite;
    [SerializeField] private Sprite yellowRibbonSprite;
    [SerializeField] private Sprite crownIconSprite;
    [SerializeField] private Sprite shieldIconSprite;
    [SerializeField] private Sprite swordsIconSprite;
    [SerializeField] private Sprite starIconSprite;
    [SerializeField] private Sprite auraGlowSprite;

    [Header("Theming Colors")]
    [SerializeField] private Color playerRibbonColor = new Color(0.04f, 0.52f, 0.89f, 1f); // Vibrant Royal Blue (#0984E3)
    [SerializeField] private Color playerAccentColor = new Color(0.95f, 0.77f, 0.25f, 1f); // Royal Gold (#F1C40F)
    [SerializeField] private Color enemyRibbonColor = new Color(0.84f, 0.19f, 0.19f, 1f);  // Fiery Crimson (#D63031)
    [SerializeField] private Color enemyAccentColor = new Color(1.0f, 0.40f, 0.40f, 1f);  // Bright Coral
    [SerializeField] private Color neutralRibbonColor = new Color(0.95f, 0.65f, 0.15f, 1f); // Amber Orange

    [Header("Text Presets")]
    [SerializeField] private string playerToastTitle = "★ TOWER ACQUIRED! ★";
    [SerializeField] private string playerToastSubtitle = "DEFENSIVE OUTPOST ACTIVE";
    [SerializeField] private string enemyToastTitle = "⚠ ENEMY ACQUIRED TOWER! ⚠";
    [SerializeField] private string enemyToastSubtitle = "ENEMY OUTPOST REINFORCED";
    [SerializeField] private string neutralToastTitle = "★ TOWER CLAIMED! ★";
    [SerializeField] private string neutralToastSubtitle = "OUTPOST OCCUPIED";

    [Header("Audio & Particle VFX")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private GameObject levelUpVfxPrefab;
    [SerializeField] private GameObject fireworksVfxPrefab;
    [SerializeField] private AudioClip fanfareSound;

    private Sequence activeToastSequence;
    private Tween activeShineTween;

    private void Awake()
    {
        if (instance == null) instance = this;
        CacheToastReferences();
        LoadDefaultAssets();

        // Start hidden in gameplay
        if (Application.isPlaying)
        {
            HideToastImmediate();
        }
    }

    public void CacheToastReferences()
    {
        if (toastRect == null) toastRect = GetComponent<RectTransform>();
        if (toastCanvasGroup == null) toastCanvasGroup = GetComponent<CanvasGroup>();

        if (bgImage == null)
        {
            Transform t = transform.Find("ToastBg");
            if (t != null) bgImage = t.GetComponent<Image>();
        }
        if (leftIcon == null)
        {
            Transform t = transform.Find("LeftIcon");
            if (t != null) leftIcon = t.GetComponent<Image>();
        }
        if (rightIcon == null)
        {
            Transform t = transform.Find("RightIcon");
            if (t != null) rightIcon = t.GetComponent<Image>();
        }
        if (titleText == null)
        {
            Transform t = transform.Find("ToastTitle");
            if (t == null) t = transform.Find("ToastText");
            if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
        }
        if (subtitleText == null)
        {
            Transform t = transform.Find("ToastSubtitle");
            if (t != null) subtitleText = t.GetComponent<TextMeshProUGUI>();
        }
        if (shineSlice == null)
        {
            Transform t = transform.Find("ShineSlice");
            if (t != null) shineSlice = t.GetComponent<RectTransform>();
        }
    }

    public void LoadDefaultAssets()
    {
        if (blueRibbonSprite == null)
            blueRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/blue_name_ribbon.png");
        if (blueRibbonSprite == null)
            blueRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Blue.png");

        if (redRibbonSprite == null)
            redRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/red_name_ribbon.png");
        if (redRibbonSprite == null)
            redRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png");

        if (yellowRibbonSprite == null)
            yellowRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Yellow.png");

        if (crownIconSprite == null)
            crownIconSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crown.png");
        if (crownIconSprite == null)
            crownIconSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Badge_Crown.png");

        if (shieldIconSprite == null)
            shieldIconSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Shield01.png");
        if (swordsIconSprite == null)
            swordsIconSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Weapon_SwordShield.png");

        if (starIconSprite == null)
            starIconSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Star01_l.png");
        if (auraGlowSprite == null)
            auraGlowSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png");

        if (fontAsset == null)
            fontAsset = RuntimeAssets.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (levelUpVfxPrefab == null)
            levelUpVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_LevelUp_01.prefab");
        if (fireworksVfxPrefab == null)
            fireworksVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Fireworks_Group_01.prefab");

        if (fanfareSound == null)
            fanfareSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/nhlk-revolution-108386.mp3");
        if (fanfareSound == null)
            fanfareSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/audioatlant-total-war-epic-action-cinematic-trailer-version-2-513669.mp3");
        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fanfareSound == null)
            fanfareSound = Resources.Load<AudioClip>("Sounds/arrowrelease");
    }

    /// <summary>
    /// Master entry point called when any tower is acquired / captured / occupied.
    /// Triggers complete multi-layer feedback:
    /// - 3D World-Space Floating Billboard Banner on the tower
    /// - Screen-Space Top HUD Notification Toast
    /// - Procedural Expanding Ground Shockwave Ring
    /// - Tower Physical Punch & Surge
    /// - Celebratory Particle VFX & Fanfare Audio
    /// </summary>
    public static void Show(TowerCombatController tower, int acquiringTeam)
    {
        if (tower == null) return;
        Instance.TriggerAcquisition(tower, acquiringTeam);
    }

    public void TriggerAcquisition(TowerCombatController tower, int acquiringTeam)
    {
        if (tower == null) return;
        LoadDefaultAssets();

        // 1. Ensure NO destruction prefab is active and all tower renderers are visible!
        if (tower.AfterObject != null)
        {
            tower.AfterObject.SetActive(false);
        }
        Renderer[] allRenderers = tower.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (tower.AfterObject == null || !allRenderers[i].transform.IsChildOf(tower.AfterObject.transform))
            {
                allRenderers[i].enabled = true;
            }
        }

        // 2. Tower Physical Juice with DOTween
        tower.transform.DOKill();
        tower.transform.DOPunchScale(new Vector3(0.09f, 0.20f, 0.09f), 0.7f, 6, 0.5f);

        // 3. Procedural Expanding Ground Shockwave Ring
        SpawnGroundShockwave(tower.transform.position, acquiringTeam);

        // 4. Celebratory Particle VFX
        SpawnCelebrationVfx(tower, acquiringTeam);

        // 5. Fanfare Audio
        PlayFanfare(tower, acquiringTeam);

        // 6. World-Space Floating 3D Billboard Banner
        // Check if tower already has a pre-configured TowerWorldBanner child
        TowerWorldBanner banner = tower.GetComponentInChildren<TowerWorldBanner>(true);
        if (banner != null)
        {
            banner.PlayAcquisition(acquiringTeam);
        }
        else
        {
            // Fallback dynamic banner if none is pre-placed on tower
            SpawnDynamicWorldBanner(tower, acquiringTeam);
        }

        // 7. Screen-Space Top HUD Toast
        ShowToast(acquiringTeam);
    }

    #region Screen-Space HUD Toast (Reusable Scene GameObject)

    /// <summary>
    /// Plays the juicy drop-down, shine sweep, scale punch, hold, and dismiss animation on the Screen Toast.
    /// Reuses the scene object without destroying it!
    /// </summary>
    public void ShowToast(int acquiringTeam)
    {
        CacheToastReferences();
        if (toastRect == null)
        {
            // If toast doesn't exist, try finding it under Canvas
            Canvas canvas = FindScreenCanvas();
            if (canvas != null)
            {
                Transform t = canvas.transform.Find("TowerAcquisitionToast");
                if (t != null)
                {
                    toastRect = t.GetComponent<RectTransform>();
                    toastCanvasGroup = t.GetComponent<CanvasGroup>();
                    CacheToastReferences();
                }
            }
        }

        if (toastRect == null) return;

        KillToastTweens();

        int localTeam = GameModeManager.LocalTeamId;
        bool isFriendly = (acquiringTeam == localTeam);
        bool isNeutral = (acquiringTeam < 0);

        ApplyToastTheme(isFriendly, isNeutral);

        // Reset positions
        toastRect.anchoredPosition = new Vector2(0f, hiddenPosY);
        toastRect.localScale = Vector3.one;
        if (toastCanvasGroup != null) toastCanvasGroup.alpha = 0f;

        if (shineSlice != null)
        {
            shineSlice.anchoredPosition = new Vector2(-260f, 0f);
        }

        // DOTween Sequence
        activeToastSequence = DOTween.Sequence();

        // 1. Drop down with juicy overshoot
        activeToastSequence.Append(toastRect.DOAnchorPosY(visiblePosY, dropDuration).SetEase(Ease.OutBack, 1.45f));
        if (toastCanvasGroup != null)
        {
            activeToastSequence.Join(toastCanvasGroup.DOFade(1f, 0.16f).SetEase(Ease.Linear));
        }

        // 2. Shine sweep slice
        if (shineSlice != null)
        {
            activeToastSequence.Insert(dropDuration * 0.4f, shineSlice.DOAnchorPosX(260f, 0.65f).SetEase(Ease.InOutSine));
        }

        // 3. Punch scale settle
        activeToastSequence.Insert(dropDuration * 0.7f, toastRect.DOPunchScale(punchScaleAmount, 0.35f, 4, 0.5f));

        // 4. Hold & slide back up
        float totalShowTime = dropDuration + holdDuration;
        activeToastSequence.Insert(totalShowTime, toastRect.DOAnchorPosY(hiddenPosY, dismissDuration).SetEase(Ease.InBack, 1.25f));
        if (toastCanvasGroup != null)
        {
            activeToastSequence.Join(toastCanvasGroup.DOFade(0f, dismissDuration * 0.85f).SetEase(Ease.Linear));
        }

        activeToastSequence.OnComplete(() =>
        {
            HideToastImmediate();
        });
    }

    private void ApplyToastTheme(bool isFriendly, bool isNeutral)
    {
        // Ribbon Background
        if (bgImage != null)
        {
            Sprite rSprite = isFriendly ? blueRibbonSprite : (isNeutral ? yellowRibbonSprite : redRibbonSprite);
            if (rSprite != null)
            {
                bgImage.sprite = rSprite;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = isFriendly ? playerRibbonColor : (isNeutral ? neutralRibbonColor : enemyRibbonColor);
            }
        }

        // Crest Icons
        Sprite chosenIcon = isFriendly ? (crownIconSprite != null ? crownIconSprite : shieldIconSprite)
                                       : (swordsIconSprite != null ? swordsIconSprite : shieldIconSprite);
        if (leftIcon != null)
        {
            if (chosenIcon != null)
            {
                leftIcon.sprite = chosenIcon;
                leftIcon.color = Color.white;
            }
            else
            {
                leftIcon.color = isFriendly ? playerAccentColor : enemyAccentColor;
            }
        }
        if (rightIcon != null)
        {
            if (chosenIcon != null)
            {
                rightIcon.sprite = chosenIcon;
                rightIcon.color = Color.white;
            }
            else
            {
                rightIcon.color = isFriendly ? playerAccentColor : enemyAccentColor;
            }
        }

        // Title and Subtitle Text
        if (titleText != null)
        {
            titleText.text = isFriendly ? playerToastTitle : (isNeutral ? neutralToastTitle : enemyToastTitle);
        }
        if (subtitleText != null)
        {
            subtitleText.text = isFriendly ? playerToastSubtitle : (isNeutral ? neutralToastSubtitle : enemyToastSubtitle);
            subtitleText.color = isFriendly ? playerAccentColor : (isNeutral ? Color.yellow : enemyAccentColor);
        }
    }

    public void KillToastTweens()
    {
        activeToastSequence?.Kill();
        activeToastSequence = null;

        activeShineTween?.Kill();
        activeShineTween = null;

        if (toastRect != null) toastRect.DOKill();
        if (toastCanvasGroup != null) toastCanvasGroup.DOKill();
        if (shineSlice != null) shineSlice.DOKill();
    }

    /// <summary>
    /// Displays Toast in resting state in Edit Mode so the designer can visually customize it in Scene View.
    /// </summary>
    [ContextMenu("Show in Scene (Edit Mode)")]
    public void ShowInSceneEditMode()
    {
        CacheToastReferences();
        KillToastTweens();

        ApplyToastTheme(true, false);

        if (toastRect != null)
        {
            toastRect.anchoredPosition = new Vector2(0f, visiblePosY);
            toastRect.localScale = Vector3.one;
        }
        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// Hides the Toast offscreen and fades it out so it is ready for play mode.
    /// </summary>
    [ContextMenu("Hide (Gameplay Ready)")]
    public void HideToastImmediate()
    {
        CacheToastReferences();
        KillToastTweens();

        if (toastRect != null)
        {
            toastRect.anchoredPosition = new Vector2(0f, hiddenPosY);
        }
        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha = 0f;
        }
    }

    [ContextMenu("Preview Toast (Player - Blue)")]
    public void PreviewToastPlayer()
    {
        ShowToast(0);
    }

    [ContextMenu("Preview Toast (Enemy - Red)")]
    public void PreviewToastEnemy()
    {
        ShowToast(1);
    }

    #endregion

    #region Procedural Ground Shockwave Ring (DOTween)

    private void SpawnGroundShockwave(Vector3 centerPos, int acquiringTeam)
    {
        int localTeam = GameModeManager.LocalTeamId;
        bool isFriendly = (acquiringTeam == localTeam);
        Color waveColor = isFriendly ? new Color(0.1f, 0.65f, 1f, 0.9f) : new Color(1f, 0.25f, 0.25f, 0.9f);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "AcquisitionShockwave";
        Transform tr = quad.transform;

        // Position flat on the ground
        tr.position = new Vector3(centerPos.x, centerPos.y + 0.08f, centerPos.z);
        tr.rotation = Quaternion.Euler(90f, 0f, 0f);
        tr.localScale = Vector3.zero;

        Collider c = quad.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = quad.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // "Sprites/Default" is always included in builds (Android too) and is unlit + alpha blended,
        // so the wave looks the same as in the editor. URP/Unlit can be stripped from player builds.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", waveColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", waveColor);
        rend.sharedMaterial = mat;

        float duration = 0.85f;
        tr.DOScale(new Vector3(16f, 16f, 1f), duration).SetEase(Ease.OutCubic);

        DOVirtual.Float(waveColor.a, 0f, duration, alpha =>
        {
            if (mat != null)
            {
                Color clr = waveColor;
                clr.a = alpha;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", clr);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", clr);
            }
        }).SetEase(Ease.InQuad).OnComplete(() =>
        {
            if (mat != null) Destroy(mat);
            if (quad != null) Destroy(quad);
        });
    }

    #endregion

    #region Celebratory Particles & Fanfare

    private void SpawnCelebrationVfx(TowerCombatController tower, int acquiringTeam)
    {
        Vector3 platformPos = tower.transform.position + Vector3.up * tower.PlatformHeightOffset;

        // 1. Pillar of Light / Level-up burst at platform
        GameObject vfxToSpawn = tower.CaptureVfxPrefab != null ? tower.CaptureVfxPrefab : levelUpVfxPrefab;
        if (vfxToSpawn != null)
        {
            GameObject levelVfx = Instantiate(vfxToSpawn, platformPos, Quaternion.identity);
            Destroy(levelVfx, 4.5f);
        }

        // Fireworks in the sky above the tower were removed on request; all other capture effects stay.

        // 2. Spinning ring of light around the archer (built in code: works on Android, no shaders/prefabs needed)
        int localTeam = GameModeManager.LocalTeamId;
        int team = acquiringTeam;
        Color ringColor = (team == localTeam) ? new Color(0.3f, 0.8f, 1f, 1f) : new Color(1f, 0.35f, 0.3f, 1f);
        StartCoroutine(ArcherOrbitRingRoutine(platformPos, ringColor));
    }

    private static Sprite glowDotSprite;

    private static Sprite GetGlowDotSprite()
    {
        if (glowDotSprite != null) return glowDotSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        glowDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return glowDotSprite;
    }

    private static Sprite ringSprite;

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.07f);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) / 0.025f) * 0.8f;
                // dashed rune marks between the two circles
                float ang = Mathf.Atan2(dy, dx);
                float dash = (Mathf.Sin(ang * 16f) > 0.55f && d > 0.66f && d < 0.8f) ? 0.7f : 0f;
                float a = Mathf.Clamp01(ring + inner);
                if (d > 1f) a = 0f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return ringSprite;
    }

    private SpriteRenderer MakeGlow(Transform parent, Sprite sprite, Color color, int order)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        return sr;
    }

    private static Material lineMat;

    private LineRenderer MakeRingLine(Transform parent, int segments)
    {
        if (lineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            lineMat = new Material(sh);
        }
        var go = new GameObject("RingLine");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = lineMat;
        lr.useWorldSpace = true;
        lr.positionCount = segments;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = 60;
        return lr;
    }

    // Draws an arc (or full circle when sweep = 360) around center
    private static void SetArc(LineRenderer lr, Vector3 center, float radius, float height, float startDeg, float sweepDeg)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float ang = (startDeg + sweepDeg * i / (n - 1)) * Mathf.Deg2Rad;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(ang) * radius, height, Mathf.Sin(ang) * radius));
        }
    }

    private static Gradient FadeGradient(Color c, float alpha, bool tail)
    {
        var g = new Gradient();
        Color white = Color.Lerp(c, Color.white, 0.6f);
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(tail ? white : c, 1f) },
            tail ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 1f) }
                 : new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
        return g;
    }

    private IEnumerator ArcherOrbitRingRoutine(Vector3 center, Color color)
    {
        const float duration = 1.5f;
        Color hot = Color.Lerp(color, Color.white, 0.5f);

        var root = new GameObject("ArcherCaptureRing");
        root.transform.position = center;

        // Smooth flat rune circle under the archer
        var halo = MakeGlow(root.transform, GetRingSprite(), new Color(hot.r, hot.g, hot.b, 0f), 56);
        halo.transform.position = center + Vector3.up * 0.05f;
        // Soft glow disc
        var disc = MakeGlow(root.transform, GetGlowDotSprite(), new Color(color.r, color.g, color.b, 0f), 55);
        disc.transform.position = center + Vector3.up * 0.04f;
        disc.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Two solid rings + two bright comet arcs spinning on them
        var ringLow = MakeRingLine(root.transform, 64);
        var ringHigh = MakeRingLine(root.transform, 64);
        var cometA = MakeRingLine(root.transform, 24);
        var cometB = MakeRingLine(root.transform, 24);

        float t = 0f;
        while (t < duration && root != null)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(n / 0.25f), 3f);   // ease-out pop
            float fade = 1f - Mathf.Clamp01((n - 0.65f) / 0.35f);
            float alpha = grow * fade;

            halo.transform.localScale = Vector3.one * (2.8f * grow + n * 0.8f);
            halo.transform.rotation = Quaternion.Euler(90f, t * 120f, 0f);
            halo.color = new Color(hot.r, hot.g, hot.b, 0.85f * alpha);
            disc.transform.localScale = Vector3.one * 3.4f * grow;
            disc.color = new Color(color.r, color.g, color.b, 0.4f * alpha);

            float rLow = 1.3f * grow + n * 0.4f;
            float rHigh = 0.95f * grow;
            float hLow = 0.25f + n * 0.5f;
            float hHigh = 0.6f + n * 1.4f;
            float spin = t * 520f;

            SetArc(ringLow, center, rLow, hLow, 0f, 360f);
            ringLow.widthMultiplier = 0.09f;
            ringLow.colorGradient = FadeGradient(color, 0.75f * alpha, false);

            SetArc(ringHigh, center, rHigh, hHigh, 0f, 360f);
            ringHigh.widthMultiplier = 0.06f;
            ringHigh.colorGradient = FadeGradient(hot, 0.6f * alpha, false);

            SetArc(cometA, center, rLow, hLow + 0.01f, spin, 140f);
            cometA.widthMultiplier = 0.2f;
            cometA.colorGradient = FadeGradient(color, alpha, true);

            SetArc(cometB, center, rHigh, hHigh + 0.01f, -spin + 180f, -140f);
            cometB.widthMultiplier = 0.15f;
            cometB.colorGradient = FadeGradient(hot, alpha, true);

            yield return null;
        }
        if (root != null) Destroy(root);
    }

    private void PlayFanfare(TowerCombatController tower, int acquiringTeam)
    {
        AudioSource audio = tower.GetComponent<AudioSource>();
        if (audio == null) return;

        AudioClip clip = fanfareSound != null ? fanfareSound : tower.CaptureSound;
        if (clip != null)
        {
            int localTeam = GameModeManager.LocalTeamId;
            audio.pitch = (acquiringTeam == localTeam) ? 1.25f : 0.88f;
            KoSAudio.PlayOneShot(audio, clip, 0.95f, "tower_capture");
        }
    }

    #endregion

    #region Fallback Dynamic World Banner

    private void SpawnDynamicWorldBanner(TowerCombatController tower, int acquiringTeam)
    {
        // Creates a lightweight fallback billboard banner if no pre-authored scene banner exists
        GameObject bannerGo = new GameObject("TowerAcquisitionBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        bannerGo.transform.SetParent(tower.transform, false);

        float height = tower.PlatformHeightOffset + 3.2f;
        bannerGo.transform.position = tower.transform.position + Vector3.up * height;

        Canvas canvas = bannerGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = bannerGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        TowerWorldBanner banner = bannerGo.AddComponent<TowerWorldBanner>();
        banner.HeightOffset = height;
        banner.PlayAcquisition(acquiringTeam);
    }

    private static Canvas FindScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.isActiveAndEnabled && c.renderMode != RenderMode.WorldSpace)
            {
                if (c.gameObject.name == "Canvas") return c;
            }
        }
        foreach (var c in canvases)
        {
            if (c.isActiveAndEnabled && c.renderMode != RenderMode.WorldSpace)
            {
                return c;
            }
        }
        return null;
    }

    #endregion
}
