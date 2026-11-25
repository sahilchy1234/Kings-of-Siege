using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// Ultra-juicy, arcade-quality War Complete / Battle Result modal (Victory & Defeat)
/// built with punchy DOTween animations, calibrated portrait layout coordinates,
/// and live Edit Mode / Play Mode preview capability.
/// </summary>
[DisallowMultipleComponent]
public class WarCompleteUIController : MonoBehaviour
{
    private static WarCompleteUIController instance;
    public static WarCompleteUIController Instance => instance;

    // --- Calibrated Bedrock Portrait Layout Coordinates ---
    // Canvas: ReferenceResolution (800, 600), MatchHeight = 1 -> Total height = 600 (-300 to +300).
    public static readonly Vector2 TITLE_CREST_RESTING_POS = new Vector2(0f, 75f);
    public static readonly Vector3 TITLE_CREST_RESTING_SCALE = new Vector3(0.38f, 0.38f, 0.38f);

    public static readonly Vector2 LEAF_RESTING_POS = new Vector2(0f, 20f);
    public static readonly Vector2 LEAF_RESTING_SIZE = new Vector2(416f, 150f);

    public static readonly Vector2 CROWN_RESTING_POS = new Vector2(0f, 75f); // Snugly nestled in laurel cradle
    public static readonly Vector2 CROWN_RESTING_SIZE = new Vector2(225f, 200f);
    public static readonly Vector2 CROWN_TEXT_RESTING_POS = new Vector2(0f, -22f);
    public static readonly Vector2 CROWN_TEXT_RESTING_SIZE = new Vector2(100f, 60f);

    public static readonly Vector2 RIBBON_RESTING_POS = new Vector2(0f, -75f); // Below laurels and crown
    public static readonly Vector2 RIBBON_RESTING_SIZE = new Vector2(880f, 160f);
    public static readonly Vector2 RIBBON_TEXT_RESTING_POS = new Vector2(0f, 14f);
    public static readonly Vector2 RIBBON_TEXT_RESTING_SIZE = new Vector2(700f, 100f);

    public static readonly Vector2 HF_STARS_RESTING_POS = new Vector2(0f, -28f); // Directly below ribbon
    public static readonly Vector2 HF_STARS_RESTING_SIZE = new Vector2(300f, 110f);

    // 3-Star Arch inside HorizontalFrame01
    public static readonly Vector2[] STAR_RESTING_POSITIONS = new Vector2[]
    {
        new Vector2(-88f, -8f), // Left Star
        new Vector2(0f, 12f),   // Center Star (Prominent centerpiece)
        new Vector2(88f, -8f)   // Right Star
    };

    public static readonly Vector2[] STAR_RESTING_SIZES = new Vector2[]
    {
        new Vector2(76f, 76f),  // Left
        new Vector2(98f, 98f),  // Center (Larger majestic centerpiece)
        new Vector2(76f, 76f)   // Right
    };

    public static readonly float[] STAR_RESTING_TILTS = new float[] { 12f, 0f, -12f };

    public static readonly Vector2 CONTINUE_VICTORY_POS = new Vector2(0f, -118f); // Below 3 stars, clear of cards
    public static readonly Vector2 CONTINUE_DEFEAT_POS = new Vector2(0f, -40f);   // Below DEFEAT ribbon
    public static readonly Vector3 CONTINUE_RESTING_SCALE = new Vector3(0.42f, 0.42f, 0.42f);

    [Header("Main Root & Backdrop")]
    [Tooltip("The root warcomplete panel GameObject.")]
    [SerializeField] private GameObject warCompleteRoot;

    [Tooltip("The dark background overlay (Dimed).")]
    [SerializeField] private Graphic backdropDim;

    [Header("Title Crest Elements")]
    [Tooltip("Container holding Crown, Laurels, Rays and Ribbon.")]
    [SerializeField] private RectTransform groupTitle;

    [Tooltip("The decorative golden laurels hugging the crown.")]
    [SerializeField] private RectTransform leafTransform;

    [Tooltip("The crown at the top.")]
    [SerializeField] private RectTransform crownTransform;

    [Tooltip("The text inside the crown showing the earned stars or level.")]
    [SerializeField] private TextMeshProUGUI crownLevelText;

    [Tooltip("Glow / light ray decorations behind the crown.")]
    [SerializeField] private RectTransform[] glowRays;

    [Tooltip("The decorative ribbon banner holding the title.")]
    [SerializeField] private RectTransform ribbonTransform;

    [Tooltip("The victory / defeat title text on the ribbon.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("3-Star Celebration")]
    [Tooltip("Container holding the 3 stars.")]
    [SerializeField] private RectTransform horizontalFrame;

    [Tooltip("Star images inside HorizontalFrame01 (Left, Center, Right).")]
    [SerializeField] private Image[] starImages;

    [Header("Ribbon Sprites (Auto-loaded from UIPACK)")]
    [Tooltip("Sky Blue ribbon sprite for Victory.")]
    [SerializeField] private Sprite victoryRibbonSprite;

    [Tooltip("Crimson Red ribbon sprite for Defeat.")]
    [SerializeField] private Sprite defeatRibbonSprite;

    [Header("Star Tuning")]
    [Tooltip("Sprite for 3D gold star (512x512).")]
    [SerializeField] private Sprite starEarnedSprite;

    [Tooltip("Vibrant golden tint for earned stars.")]
    [SerializeField] private Color starEarnedColor = Color.white;

    [Tooltip("Dark translucent silhouette tint for unearned / empty stars.")]
    [SerializeField] private Color starEmptyColor = new Color(0.2f, 0.24f, 0.32f, 0.40f);

    [Tooltip("Crown tint on Victory (Golden White).")]
    [SerializeField] private Color victoryCrownColor = Color.white;

    [Tooltip("Crown tint on Defeat (Wounded Crimson).")]
    [SerializeField] private Color defeatCrownColor = new Color(0.88f, 0.28f, 0.28f, 0.95f);

    [Tooltip("Dim overlay color on Victory.")]
    [SerializeField] private Color victoryBackdropColor = new Color(0.04f, 0.08f, 0.16f, 0.88f);

    [Tooltip("Dim overlay color on Defeat.")]
    [SerializeField] private Color defeatBackdropColor = new Color(0.18f, 0.03f, 0.05f, 0.92f);

    [Header("Interactive Continue / Replay")]
    [Tooltip("Tap to continue button & text.")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI continueText;

    [Header("Match Summary")]
    [Tooltip("Shows the real trophy change and coin reward. Created at runtime if left empty.")]
    [SerializeField] private TextMeshProUGUI summaryText;

    [Header("Audio Effects (Optional)")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip starPopSound;
    private AudioSource audioSource;

    private CanvasGroup canvasGroup;
    private Sequence activeSeq;
    private readonly List<Tween> idleTweens = new List<Tween>();
    private bool isShowing = false;

    private void Awake()
    {
        if (instance == null) instance = this;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        AutoFindReferences();
    }

    private void Start()
    {
        if (warCompleteRoot != null) warCompleteRoot.SetActive(false);
        else gameObject.SetActive(false);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    /// <summary>
    /// Finds and wires all UI components automatically.
    /// </summary>
    public void AutoFindReferences()
    {
        if (warCompleteRoot == null)
            warCompleteRoot = gameObject;

        canvasGroup = warCompleteRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = warCompleteRoot.AddComponent<CanvasGroup>();

        // 1. Dimed Backdrop
        if (backdropDim == null)
        {
            Transform dim = transform.Find("Dimed");
            if (dim == null) dim = FindDeepChild(transform, "Dimed");
            if (dim != null) backdropDim = dim.GetComponent<Graphic>();
        }

        // 2. Group_Title
        if (groupTitle == null)
        {
            Transform gt = transform.Find("Group_Title");
            if (gt == null) gt = FindDeepChild(transform, "Group_Title");
            if (gt != null) groupTitle = gt.GetComponent<RectTransform>();
        }

        // 3. Laurel Leaves
        if (leafTransform == null && groupTitle != null)
        {
            Transform leaf = groupTitle.Find("Image_Leaf");
            if (leaf == null) leaf = FindDeepChild(groupTitle, "Image_Leaf");
            if (leaf != null) leafTransform = leaf.GetComponent<RectTransform>();
        }

        // 4. Crown
        if (crownTransform == null && groupTitle != null)
        {
            Transform crown = groupTitle.Find("Image_Crown");
            if (crown == null) crown = FindDeepChild(groupTitle, "Image_Crown");
            if (crown != null) crownTransform = crown.GetComponent<RectTransform>();
        }
        if (crownTransform != null && crownLevelText == null)
        {
            crownLevelText = crownTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 5. Glow Rays
        if (groupTitle != null && (glowRays == null || glowRays.Length == 0))
        {
            var rays = new List<RectTransform>();
            foreach (Transform child in groupTitle)
            {
                if (child.name.StartsWith("Image_Deco"))
                {
                    RectTransform rt = child.GetComponent<RectTransform>();
                    if (rt != null) rays.Add(rt);
                }
            }
            glowRays = rays.ToArray();
        }

        // 6. Ribbon & Title
        if (ribbonTransform == null && groupTitle != null)
        {
            Transform ribbon = groupTitle.Find("Label_Ribbon");
            if (ribbon == null) ribbon = FindDeepChild(groupTitle, "Label_Ribbon");
            if (ribbon != null) ribbonTransform = ribbon.GetComponent<RectTransform>();
        }
        if (titleText == null && ribbonTransform != null)
        {
            titleText = ribbonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 7. HorizontalFrame01 & 3 Stars
        if (horizontalFrame == null)
        {
            Transform hf = transform.Find("HorizontalFrame01");
            if (hf == null) hf = FindDeepChild(transform, "HorizontalFrame01");
            if (hf != null) horizontalFrame = hf.GetComponent<RectTransform>();
        }

        if (horizontalFrame != null)
        {
            var stars = new List<Image>();
            foreach (Transform child in horizontalFrame)
            {
                if (child.name.StartsWith("Image") && !child.name.Contains("Crown") && !child.name.Contains("Deco"))
                {
                    Image img = child.GetComponent<Image>();
                    if (img != null) stars.Add(img);
                }
            }

            if (stars.Count >= 3)
            {
                stars.Sort((a, b) => a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x));
                starImages = stars.ToArray();
            }
        }

        // 8. Continue button & text
        if (continueText == null)
        {
            Transform tabText = transform.Find("Text_Tab");
            if (tabText == null) tabText = FindDeepChild(transform, "Text_Tab");
            if (tabText != null) continueText = tabText.GetComponent<TextMeshProUGUI>();
        }

        if (continueButton == null)
        {
            continueButton = GetComponentInChildren<Button>(true);
            if (continueButton == null && warCompleteRoot != null)
            {
                continueButton = warCompleteRoot.GetComponent<Button>();
                if (continueButton == null) continueButton = warCompleteRoot.AddComponent<Button>();
            }
        }

        // Auto-load crisp sprites from UIPACK if not assigned
        if (victoryRibbonSprite == null)
        {
            victoryRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Sky.png");
        }
        if (defeatRibbonSprite == null)
        {
            defeatRibbonSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png");
        }
        if (starEarnedSprite == null)
        {
            starEarnedSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Star.png");
            if (starEarnedSprite == null)
            {
                starEarnedSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Star03_l.png");
            }
        }
    }

    /// <summary>
    /// Snaps, cleans, and centers all elements to calibrated portrait screen coordinates.
    /// Perfectly nestles crown into laurels, centers the ribbon, aligns the 3-star arch,
    /// and positions continue prompt safely above the cards.
    /// </summary>
    public void AutoLayoutUI()
    {
        AutoFindReferences();

        // 1. Group_Title Crest
        if (groupTitle != null)
        {
            groupTitle.gameObject.SetActive(true);
            groupTitle.anchoredPosition = TITLE_CREST_RESTING_POS;
            groupTitle.localScale = TITLE_CREST_RESTING_SCALE;
            groupTitle.localEulerAngles = Vector3.zero;
        }

        // 2. Laurel Leaves
        if (leafTransform != null)
        {
            leafTransform.gameObject.SetActive(true);
            leafTransform.anchoredPosition = LEAF_RESTING_POS;
            leafTransform.sizeDelta = LEAF_RESTING_SIZE;
            leafTransform.localScale = Vector3.one;
            leafTransform.localEulerAngles = Vector3.zero;
        }

        // 3. Crown resting in laurel cradle
        if (crownTransform != null)
        {
            crownTransform.gameObject.SetActive(true);
            crownTransform.anchoredPosition = CROWN_RESTING_POS;
            crownTransform.sizeDelta = CROWN_RESTING_SIZE;
            crownTransform.localScale = Vector3.one;
            crownTransform.localEulerAngles = Vector3.zero;

            if (crownLevelText != null)
            {
                crownLevelText.rectTransform.anchoredPosition = CROWN_TEXT_RESTING_POS;
                crownLevelText.rectTransform.sizeDelta = CROWN_TEXT_RESTING_SIZE;
                crownLevelText.rectTransform.localScale = Vector3.one;
                crownLevelText.alignment = TextAlignmentOptions.Center;
                crownLevelText.fontSize = 44f;
                crownLevelText.fontStyle = FontStyles.Bold;
            }
        }

        // 4. Glow Rays
        if (glowRays != null)
        {
            foreach (var ray in glowRays)
            {
                if (ray == null) continue;
                ray.anchoredPosition = new Vector2(0f, 50f);
                ray.sizeDelta = new Vector2(450f, 450f);
                ray.localScale = Vector3.one;
            }
        }

        // 5. Ribbon & Title Text
        if (ribbonTransform != null)
        {
            ribbonTransform.gameObject.SetActive(true);
            ribbonTransform.anchoredPosition = RIBBON_RESTING_POS;
            ribbonTransform.sizeDelta = RIBBON_RESTING_SIZE;
            ribbonTransform.localScale = Vector3.one;
            ribbonTransform.localEulerAngles = Vector3.zero;

            if (titleText != null)
            {
                titleText.rectTransform.anchoredPosition = RIBBON_TEXT_RESTING_POS;
                titleText.rectTransform.sizeDelta = RIBBON_TEXT_RESTING_SIZE;
                titleText.rectTransform.localScale = Vector3.one;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.fontSize = 54f;
                titleText.fontStyle = FontStyles.Bold;
            }
        }

        // 6. HorizontalFrame01 & 3 Stars
        if (horizontalFrame != null)
        {
            horizontalFrame.gameObject.SetActive(true);
            horizontalFrame.anchoredPosition = HF_STARS_RESTING_POS;
            horizontalFrame.sizeDelta = HF_STARS_RESTING_SIZE;
            horizontalFrame.localScale = Vector3.one;
            horizontalFrame.localEulerAngles = Vector3.zero;

            // Clean up old demo items
            Transform groupList = horizontalFrame.Find("Group_List");
            if (groupList != null) groupList.gameObject.SetActive(false);
            Transform oldTitle = horizontalFrame.Find("Text_Title");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);
            Transform oldGo = horizontalFrame.Find("GameObject");
            if (oldGo != null) oldGo.gameObject.SetActive(false);

            // Align 3 Stars in triumphant arch
            if (starImages != null && starImages.Length >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (starImages[i] == null) continue;
                    RectTransform sRT = starImages[i].rectTransform;
                    sRT.anchoredPosition = STAR_RESTING_POSITIONS[i];
                    sRT.sizeDelta = STAR_RESTING_SIZES[i];
                    sRT.localEulerAngles = new Vector3(0f, 0f, STAR_RESTING_TILTS[i]);
                    sRT.localScale = Vector3.one;

                    if (starEarnedSprite != null) starImages[i].sprite = starEarnedSprite;
                    starImages[i].color = starEarnedColor;
                }
            }
        }

        // 7. Continue Text
        if (continueText != null)
        {
            continueText.gameObject.SetActive(true);
            continueText.rectTransform.anchoredPosition = CONTINUE_VICTORY_POS;
            continueText.rectTransform.sizeDelta = new Vector2(400f, 60f);
            continueText.rectTransform.localScale = CONTINUE_RESTING_SCALE;
            continueText.alignment = TextAlignmentOptions.Center;
            continueText.fontSize = 42f;
            continueText.fontStyle = FontStyles.Bold;
            continueText.text = "TAP TO CONTINUE";
            continueText.alpha = 1f;
        }

        // 8. Fullscreen Dimed Backdrop
        if (backdropDim != null)
        {
            backdropDim.rectTransform.anchorMin = Vector2.zero;
            backdropDim.rectTransform.anchorMax = Vector2.one;
            backdropDim.rectTransform.anchoredPosition = Vector2.zero;
            backdropDim.rectTransform.sizeDelta = Vector2.zero;
        }

        Debug.Log("[WarCompleteUIController] ✓ AutoLayoutUI: Calibrated & aligned all components to portrait layout perfectly!");
    }

    /// <summary>
    /// Plays the full cinematic, ultra-juicy warcomplete modal animation using DOTween.
    /// Punchy, arcade-style feel with instant visual impact, crown slam, and 3-star pop-in celebrations.
    /// </summary>
    public void PlayWarCompleteAnimation(bool isVictory, int starsEarned, bool force = false)
    {
        if (isShowing && !force) return;
        isShowing = true;

        // In play mode the redesigned result screen replaces the old crest modal.
        if (Application.isPlaying)
        {
            MatchResultPanel panel = MatchResultPanel.GetOrCreate(gameObject);
            if (panel != null)
            {
                GameManager.MatchResult result = GameManager.Instance != null && GameManager.Instance.Result != GameManager.MatchResult.Ongoing
                    ? GameManager.Instance.Result
                    : (isVictory ? GameManager.MatchResult.Victory : GameManager.MatchResult.Defeat);
                if (force) panel.Hide();
                panel.Show(result, starsEarned);
                return;
            }
        }

        AutoFindReferences();
        KillAllTweens();

        // Ensure parent Canvas is active so elements are visible
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && !parentCanvas.gameObject.activeSelf)
        {
            parentCanvas.gameObject.SetActive(true);
        }

        if (warCompleteRoot != null) warCompleteRoot.SetActive(true);
        else gameObject.SetActive(true);

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // Group_Title setup
        if (groupTitle != null)
        {
            groupTitle.gameObject.SetActive(true);
            groupTitle.anchoredPosition = TITLE_CREST_RESTING_POS;
            groupTitle.localScale = TITLE_CREST_RESTING_SCALE;
            groupTitle.localEulerAngles = Vector3.zero;
        }

        if (leafTransform != null)
        {
            leafTransform.gameObject.SetActive(true);
            leafTransform.anchoredPosition = LEAF_RESTING_POS;
            leafTransform.sizeDelta = LEAF_RESTING_SIZE;
            leafTransform.localScale = Vector3.one;
        }

        // Backdrop setup
        Color targetDimColor = isVictory ? victoryBackdropColor : defeatBackdropColor;
        if (backdropDim != null)
        {
            Color c = targetDimColor;
            c.a = 0f;
            backdropDim.color = c;
            backdropDim.rectTransform.anchoredPosition = Vector2.zero;
        }

        // Crown initial state:
        // Positioned at resting position, but scales down from oversized 2.2x impact slam!
        if (crownTransform != null)
        {
            crownTransform.gameObject.SetActive(true);
            crownTransform.anchoredPosition = CROWN_RESTING_POS;
            crownTransform.sizeDelta = CROWN_RESTING_SIZE;
            crownTransform.localScale = Vector3.one * (isVictory ? 2.2f : 1.8f);
            crownTransform.localEulerAngles = Vector3.zero;

            Image crownImg = crownTransform.GetComponent<Image>();
            if (crownImg != null) crownImg.color = isVictory ? victoryCrownColor : defeatCrownColor;
        }

        // Crown level number
        if (crownLevelText != null)
        {
            if (isVictory)
            {
                crownLevelText.gameObject.SetActive(true);
                crownLevelText.text = Mathf.Max(1, starsEarned).ToString();
                crownLevelText.transform.localScale = Vector3.one;
            }
            else
            {
                crownLevelText.gameObject.SetActive(false);
            }
        }

        // Glow rays
        if (glowRays != null)
        {
            foreach (var ray in glowRays)
            {
                if (ray == null) continue;
                ray.gameObject.SetActive(isVictory);
                ray.anchoredPosition = new Vector2(0f, 50f);
                ray.localScale = Vector3.zero;
                ray.localEulerAngles = Vector3.zero;
            }
        }

        // Ribbon initial state: scales X out from 0
        if (ribbonTransform != null)
        {
            ribbonTransform.gameObject.SetActive(true);
            ribbonTransform.anchoredPosition = RIBBON_RESTING_POS;
            ribbonTransform.sizeDelta = RIBBON_RESTING_SIZE;
            ribbonTransform.localScale = new Vector3(0f, 1f, 1f);
            ribbonTransform.localEulerAngles = Vector3.zero;

            Image ribbonImg = ribbonTransform.GetComponent<Image>();
            if (ribbonImg != null)
            {
                Sprite targetSprite = isVictory ? victoryRibbonSprite : defeatRibbonSprite;
                if (targetSprite != null)
                {
                    ribbonImg.sprite = targetSprite;
                    ribbonImg.color = Color.white;
                }
                else
                {
                    ribbonImg.color = isVictory ? new Color(0.15f, 0.6f, 1f, 1f) : new Color(0.9f, 0.15f, 0.15f, 1f);
                }
            }
        }

        // Title Text initial state
        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
            titleText.text = isVictory ? "VICTORY!" : "DEFEAT";
            titleText.color = isVictory ? new Color(1f, 0.95f, 0.35f, 1f) : new Color(1f, 0.28f, 0.28f, 1f);
            titleText.transform.localScale = Vector3.zero;
        }

        // Horizontal Frame & 3-Star Arch
        if (horizontalFrame != null)
        {
            horizontalFrame.anchoredPosition = HF_STARS_RESTING_POS;
            horizontalFrame.sizeDelta = HF_STARS_RESTING_SIZE;
            horizontalFrame.gameObject.SetActive(isVictory);
            horizontalFrame.localScale = Vector3.zero;

            Transform groupList = horizontalFrame.Find("Group_List");
            if (groupList != null) groupList.gameObject.SetActive(false);
            Transform oldTitle = horizontalFrame.Find("Text_Title");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);
        }

        if (starImages != null && starImages.Length >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                Image star = starImages[i];
                if (star == null) continue;

                star.gameObject.SetActive(isVictory);
                star.transform.localScale = Vector3.zero;
                star.rectTransform.anchoredPosition = STAR_RESTING_POSITIONS[i];
                star.rectTransform.sizeDelta = STAR_RESTING_SIZES[i];
                star.rectTransform.localEulerAngles = new Vector3(0f, 0f, STAR_RESTING_TILTS[i]);

                if (starEarnedSprite != null) star.sprite = starEarnedSprite;

                bool isEarned = (i < starsEarned);
                star.color = isEarned ? starEarnedColor : starEmptyColor;
            }
        }

        // Continue Button & Text
        Vector2 targetContinuePos = isVictory ? CONTINUE_VICTORY_POS : CONTINUE_DEFEAT_POS;
        if (continueText != null)
        {
            continueText.gameObject.SetActive(true);
            continueText.rectTransform.anchoredPosition = targetContinuePos;
            continueText.alpha = 0f;
            continueText.transform.localScale = Vector3.zero;
            continueText.text = isVictory ? "★  TAP TO CONTINUE  ★" : "TAP TO RETRY";
            continueText.color = isVictory ? Color.white : new Color(1f, 0.8f, 0.8f, 1f);
        }

        // Match summary: real numbers recorded by ProfileProgress before this modal opens
        if (summaryText == null && warCompleteRoot != null)
        {
            var go = new GameObject("Text_Summary", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(warCompleteRoot.transform, false);
            summaryText = go.GetComponent<TextMeshProUGUI>();
            summaryText.alignment = TextAlignmentOptions.Center;
            summaryText.fontSize = 36f;
            summaryText.fontStyle = FontStyles.Bold;
            summaryText.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(0f, -175f);
            rt.sizeDelta = new Vector2(700f, 60f);
        }
        if (summaryText != null)
        {
            int delta = Siege.Profile.ProfileProgress.LastTrophyDelta;
            int coins = Siege.Profile.ProfileProgress.LastCoinReward;
            summaryText.gameObject.SetActive(true);
            summaryText.text = (delta >= 0 ? "+" : "") + delta + " trophies    +" + coins + " coins";
            summaryText.color = delta >= 0 ? new Color(1f, 0.92f, 0.45f, 1f) : new Color(1f, 0.55f, 0.55f, 1f);
        }

        // Play Sound
        if (audioSource != null)
        {
            AudioClip clip = isVictory ? victorySound : defeatSound;
            KoSAudio.StopMusic(0.5f);
            if (clip == null) KoSAudio.Play2D(isVictory ? "victory_jingle" : "defeat_jingle");
            if (clip != null) KoSAudio.PlayOneShot(audioSource, clip, 0.95f, "result");
        }

        // --- Build Master DOTween Sequence ---
        UpdateType updateMode = Application.isPlaying ? UpdateType.Normal : UpdateType.Manual;
        activeSeq = DOTween.Sequence().SetUpdate(updateMode, true);

        // STEP 1: Backdrop Dim Fade-in (0.14s) - Swift and immersive!
        if (backdropDim != null)
        {
            activeSeq.Append(backdropDim.DOFade(targetDimColor.a, 0.14f).SetEase(Ease.OutQuad));
        }

        // STEP 2: Ribbon Elastic Snap & Title Pop (0.24s)
        if (ribbonTransform != null)
        {
            activeSeq.Append(ribbonTransform.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack, 2.8f));
        }
        if (titleText != null)
        {
            activeSeq.Join(titleText.transform.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack, 3.2f));
            if (!isVictory)
            {
                activeSeq.Append(titleText.transform.DOShakePosition(0.25f, new Vector3(6f, 3f, 0), 16));
            }
        }

        // STEP 3: Crown Grand Slam Impact into Laurels (0.30s)
        if (crownTransform != null)
        {
            if (isVictory)
            {
                // Victory: Explosive slam down into laurels with juicy bounce
                activeSeq.Append(crownTransform.DOScale(Vector3.one, 0.30f).SetEase(Ease.OutBounce));

                // Screen camera shake & crown number pop
                activeSeq.AppendCallback(() =>
                {
                    if (backdropDim != null)
                    {
                        backdropDim.rectTransform.DOShakeAnchorPos(0.16f, new Vector2(8f, 6f), 18)
                            .SetUpdate(updateMode, true);
                    }
                    if (crownLevelText != null)
                    {
                        crownLevelText.transform.DOPunchScale(Vector3.one * 0.45f, 0.24f, 6)
                            .SetUpdate(updateMode, true);
                    }
                });
            }
            else
            {
                // Defeat: Crown slams with a dull thud and tilts sideways like a broken crooked crown!
                activeSeq.Append(crownTransform.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutQuad));
                activeSeq.Join(crownTransform.DOLocalRotate(new Vector3(0, 0, -16f), 0.28f).SetEase(Ease.OutBack));

                activeSeq.AppendCallback(() =>
                {
                    if (backdropDim != null)
                    {
                        backdropDim.rectTransform.DOShakeAnchorPos(0.18f, new Vector2(5f, 4f), 12)
                            .SetUpdate(updateMode, true);
                    }
                });
            }
        }

        // STEP 4: Horizontal Frame & 3-Star Celebration (VICTORY ONLY)
        if (isVictory)
        {
            if (horizontalFrame != null)
            {
                activeSeq.Append(horizontalFrame.DOScale(Vector3.one, 0.18f).SetEase(Ease.OutBack));
            }

            // Glow rays expand
            if (glowRays != null)
            {
                foreach (var ray in glowRays)
                {
                    if (ray != null)
                    {
                        activeSeq.Join(ray.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack));
                    }
                }
            }

            // Pop in each star in sequence
            if (starImages != null && starImages.Length >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    int idx = i;
                    Image star = starImages[idx];
                    if (star == null) continue;

                    bool isEarned = (idx < starsEarned);
                    float targetTilt = STAR_RESTING_TILTS[idx];
                    float popMultiplier = (idx == 1) ? 1.6f : 1.45f;

                    activeSeq.AppendCallback(() =>
                    {
                        if (isEarned && audioSource != null && starPopSound != null)
                        {
                            audioSource.pitch = 1.0f + (idx * 0.18f); // Rising triumphant tone
                            KoSAudio.PlayOneShot(audioSource, starPopSound, 0.95f, "star_pop");
                        }
                    });

                    if (isEarned)
                    {
                        activeSeq.Append(star.transform.DOScale(Vector3.one * popMultiplier, 0.10f).SetEase(Ease.OutQuad));
                        activeSeq.Append(star.transform.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBounce));

                        if (idx == 1)
                        {
                            // Centerpiece star triggers extra screen punch
                            activeSeq.AppendCallback(() =>
                            {
                                if (backdropDim != null)
                                {
                                    backdropDim.rectTransform.DOShakeAnchorPos(0.16f, new Vector2(8f, 6f), 18)
                                        .SetUpdate(updateMode, true);
                                }
                                if (crownLevelText != null)
                                {
                                    crownLevelText.transform.DOPunchScale(Vector3.one * 0.35f, 0.20f, 6)
                                        .SetUpdate(updateMode, true);
                                }
                            });
                        }
                    }
                    else
                    {
                        // Unearned slot pops in softly
                        activeSeq.Append(star.transform.DOScale(Vector3.one * 0.9f, 0.10f).SetEase(Ease.OutQuad));
                        activeSeq.Append(star.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad));
                    }

                    activeSeq.AppendInterval(0.04f);
                }
            }
        }
        else
        {
            activeSeq.AppendInterval(0.10f);
        }

        // STEP 5: "Tap to Continue" Prompt Pop
        if (continueText != null)
        {
            activeSeq.Append(continueText.DOFade(1f, 0.20f));
            activeSeq.Join(continueText.transform.DOScale(CONTINUE_RESTING_SCALE, 0.20f).SetEase(Ease.OutBack));
        }

        // STEP 6: Looping Celebration Idles
        activeSeq.OnComplete(() =>
        {
            StartVictoryIdleLoops(isVictory, starsEarned);
        });
    }

    /// <summary>
    /// Gentle living loops while waiting for player tap.
    /// </summary>
    private void StartVictoryIdleLoops(bool isVictory, int starsEarned)
    {
        UpdateType updateMode = Application.isPlaying ? UpdateType.Normal : UpdateType.Manual;

        if (isVictory)
        {
            // Crown gentle golden float
            if (crownTransform != null)
            {
                Tween tCrown = crownTransform.DOAnchorPosY(CROWN_RESTING_POS.y + 6f, 1.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(updateMode, true);
                idleTweens.Add(tCrown);
            }

            // Glow rays slow majestic rotation
            if (glowRays != null)
            {
                foreach (var ray in glowRays)
                {
                    if (ray != null)
                    {
                        Tween tRay = ray.DORotate(new Vector3(0, 0, 360f), 24f, RotateMode.FastBeyond360)
                            .SetLoops(-1, LoopType.Restart)
                            .SetEase(Ease.Linear)
                            .SetUpdate(updateMode, true);
                        idleTweens.Add(tRay);
                    }
                }
            }

            // Earned stars living breathe
            if (starImages != null && starImages.Length >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i < starsEarned && starImages[i] != null)
                    {
                        float cycleDuration = (i == 1) ? 1.0f : 1.3f;
                        float scalePunch = (i == 1) ? 1.08f : 1.05f;

                        Tween tStar = starImages[i].transform.DOScale(Vector3.one * scalePunch, cycleDuration)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetDelay(i * 0.15f)
                            .SetUpdate(updateMode, true);
                        idleTweens.Add(tStar);
                    }
                }
            }
        }

        // Continue prompt soft breathing
        if (continueText != null)
        {
            Tween t2 = continueText.DOFade(0.65f, 0.85f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(updateMode, true);
            idleTweens.Add(t2);

            Tween t3 = continueText.transform.DOScale(CONTINUE_RESTING_SCALE * 1.06f, 0.85f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(updateMode, true);
            idleTweens.Add(t3);
        }
    }

    public void KillAllTweens()
    {
        if (activeSeq != null && activeSeq.IsActive())
        {
            activeSeq.Kill();
            activeSeq = null;
        }

        foreach (var t in idleTweens)
        {
            if (t != null && t.IsActive()) t.Kill();
        }
        idleTweens.Clear();

        if (crownTransform != null) crownTransform.DOKill();
        if (ribbonTransform != null) ribbonTransform.DOKill();
        if (titleText != null) titleText.transform.DOKill();
        if (horizontalFrame != null) horizontalFrame.DOKill();
        if (glowRays != null)
        {
            foreach (var r in glowRays) if (r != null) r.DOKill();
        }
        if (continueText != null)
        {
            continueText.DOKill();
            continueText.transform.DOKill();
        }
        if (backdropDim != null)
        {
            backdropDim.DOKill();
            backdropDim.rectTransform.DOKill();
        }

        if (starImages != null)
        {
            foreach (var s in starImages)
            {
                if (s != null)
                {
                    s.DOKill();
                    s.transform.DOKill();
                }
            }
        }
    }

    private void OnContinueClicked()
    {
        Debug.Log("[WarCompleteUIController] Tap to Continue clicked!");

        KillAllTweens();

        Sequence exitSeq = DOTween.Sequence().SetUpdate(true);
        if (warCompleteRoot != null)
        {
            exitSeq.Append(warCompleteRoot.transform.DOScale(1.08f, 0.20f).SetEase(Ease.InCubic));
        }
        if (canvasGroup != null)
        {
            exitSeq.Join(canvasGroup.DOFade(0f, 0.20f));
        }

        exitSeq.OnComplete(() =>
        {
            ResetModal();
            ReturnToMainMenu();
        });
    }

    /// <summary>
    /// Leaves any Photon room, resets mode state and loads MainMenu (same path as NetworkBattleBridge.LeaveMatchAndReturnToMenu).
    /// Restores timeScale in case a cinematic or slow-motion left it paused.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (Photon.Pun.PhotonNetwork.InRoom)
        {
            Photon.Pun.PhotonNetwork.LeaveRoom();
        }

        GameModeManager.ResetToDefault();
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Resets all transforms and state back to pristine resting defaults and hides modal.
    /// </summary>
    public void ResetModal()
    {
        KillAllTweens();
        isShowing = false;

        if (groupTitle != null)
        {
            groupTitle.anchoredPosition = TITLE_CREST_RESTING_POS;
            groupTitle.localScale = TITLE_CREST_RESTING_SCALE;
            groupTitle.localEulerAngles = Vector3.zero;
        }

        if (crownTransform != null)
        {
            crownTransform.anchoredPosition = CROWN_RESTING_POS;
            crownTransform.sizeDelta = CROWN_RESTING_SIZE;
            crownTransform.localScale = Vector3.one;
            crownTransform.localEulerAngles = Vector3.zero;
        }

        if (leafTransform != null)
        {
            leafTransform.anchoredPosition = LEAF_RESTING_POS;
            leafTransform.sizeDelta = LEAF_RESTING_SIZE;
            leafTransform.localScale = Vector3.one;
            leafTransform.localEulerAngles = Vector3.zero;
        }

        if (ribbonTransform != null)
        {
            ribbonTransform.anchoredPosition = RIBBON_RESTING_POS;
            ribbonTransform.sizeDelta = RIBBON_RESTING_SIZE;
            ribbonTransform.localScale = Vector3.one;
            ribbonTransform.localEulerAngles = Vector3.zero;
        }

        if (titleText != null)
        {
            titleText.transform.localScale = Vector3.one;
            titleText.transform.localPosition = Vector3.zero;
        }

        if (horizontalFrame != null)
        {
            horizontalFrame.anchoredPosition = HF_STARS_RESTING_POS;
            horizontalFrame.sizeDelta = HF_STARS_RESTING_SIZE;
            horizontalFrame.localScale = Vector3.one;
        }

        if (continueText != null)
        {
            continueText.rectTransform.anchoredPosition = CONTINUE_VICTORY_POS;
            continueText.rectTransform.sizeDelta = new Vector2(400f, 60f);
            continueText.rectTransform.localScale = CONTINUE_RESTING_SCALE;
            continueText.alpha = 1f;
        }

        if (starImages != null && starImages.Length >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                if (starImages[i] != null)
                {
                    starImages[i].transform.localScale = Vector3.one;
                    starImages[i].rectTransform.anchoredPosition = STAR_RESTING_POSITIONS[i];
                    starImages[i].rectTransform.sizeDelta = STAR_RESTING_SIZES[i];
                    starImages[i].transform.localEulerAngles = new Vector3(0f, 0f, STAR_RESTING_TILTS[i]);
                    starImages[i].color = starEarnedColor;
                }
            }
        }

        if (backdropDim != null)
        {
            Color c = backdropDim.color;
            c.a = 0f;
            backdropDim.color = c;
            backdropDim.rectTransform.anchoredPosition = Vector2.zero;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (warCompleteRoot != null)
        {
            warCompleteRoot.SetActive(false);
            warCompleteRoot.transform.localScale = Vector3.one;
        }
        else
        {
            gameObject.SetActive(false);
            transform.localScale = Vector3.one;
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Layout UI (Align & Center)")]
    public void EditorAutoLayoutUI()
    {
        AutoLayoutUI();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [ContextMenu("Test Victory (3 Stars)")]
    public void EditorTestVictory3Stars()
    {
        PlayWarCompleteAnimation(true, 3, true);
    }

    [ContextMenu("Test Victory (2 Stars)")]
    public void EditorTestVictory2Stars()
    {
        PlayWarCompleteAnimation(true, 2, true);
    }

    [ContextMenu("Test Victory (1 Star)")]
    public void EditorTestVictory1Star()
    {
        PlayWarCompleteAnimation(true, 1, true);
    }

    [ContextMenu("Test Defeat")]
    public void EditorTestDefeat()
    {
        PlayWarCompleteAnimation(false, 0, true);
    }

    [ContextMenu("Reset Modal")]
    public void EditorResetModal()
    {
        ResetModal();
    }
#endif
}
