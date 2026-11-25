using System.Collections.Generic;
using DG.Tweening;
using Siege.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Match-end result screen (Victory / Defeat / Draw) for BattleScene, styled with the
/// medieval arena art set (UIPACK/battlevs) and the TiltWarp game font.
/// Builds its own overlay canvas once, then fills it with real data:
///  - player and opponent names + avatars (MatchReadyScreen)
///  - stars earned and enemy towers destroyed (GameManager)
///  - trophy change, trophy total and coin reward (ProfileProgress / PlayerProfileData)
/// Main Menu always returns to MainMenu; Play Again reloads the battle in VS AI matches only.
/// No per-frame work: everything is built on first Show and animated with DOTween (unscaled time).
/// </summary>
[DisallowMultipleComponent]
public class MatchResultPanel : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    [Header("Fonts (auto-filled in the editor)")]
    [SerializeField] private TMP_FontAsset titleFont;   // TiltWarp with outline
    [SerializeField] private TMP_FontAsset bodyFont;    // TiltWarp

    [Header("Art (auto-filled in the editor from UIPACK)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite victoryRibbonSprite;
    [SerializeField] private Sprite defeatRibbonSprite;
    [SerializeField] private Sprite victoryGlowSprite;
    [SerializeField] private Sprite defeatGlowSprite;
    [SerializeField] private Sprite particlesSprite;
    [SerializeField] private Sprite crownSprite;
    [SerializeField] private Sprite laurelSprite;
    [SerializeField] private Sprite swordsSprite;
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Sprite trophySprite;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite rowSprite;
    [SerializeField] private Sprite playerFrameSprite;
    [SerializeField] private Sprite opponentFrameSprite;
    [SerializeField] private Sprite primaryButtonSprite;
    [SerializeField] private Sprite secondaryButtonSprite;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip popSound;

    // Palette
    private static readonly Color Gold = new Color(1f, 0.83f, 0.27f, 1f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.88f, 1f);
    private static readonly Color Muted = new Color(0.80f, 0.86f, 1f, 0.85f);
    private static readonly Color Win = new Color(0.55f, 1f, 0.40f, 1f);
    private static readonly Color Loss = new Color(1f, 0.42f, 0.36f, 1f);
    private static readonly Color StarOff = new Color(0.10f, 0.14f, 0.30f, 0.9f);

    // Built UI
    private bool built;
    private Canvas canvas;
    private CanvasGroup rootGroup;
    private Image dim;
    private RectTransform glowRect, particlesRect, laurelRect;
    private Image glowImage;
    private RectTransform bannerRect;
    private Image bannerImage;
    private TextMeshProUGUI bannerText;
    private RectTransform cardRect;
    private CanvasGroup cardGroup;
    private Image playerAvatar, opponentAvatar;
    private TextMeshProUGUI playerName, opponentName;
    private RectTransform playerCrown, opponentCrown, playerShield, opponentShield;
    private readonly Image[] stars = new Image[3];
    private TextMeshProUGUI towersValue, trophyDeltaText, trophyTotalText, coinText;
    private readonly List<RectTransform> rows = new List<RectTransform>();
    private Button mainMenuButton, playAgainButton;
    private RectTransform mainMenuRect, playAgainRect;
    private AudioSource audioSource;

    private Sequence showSeq;
    private readonly List<Tween> idleTweens = new List<Tween>();
    private bool leaving;

    public bool IsShowing { get; private set; }

    private static MatchResultPanel instance;

    /// <summary>Finds the panel in the scene, or adds one to the given host object.</summary>
    public static MatchResultPanel GetOrCreate(GameObject host)
    {
        if (instance != null) return instance;
        instance = FindFirstObjectByType<MatchResultPanel>(FindObjectsInactive.Include);
        if (instance == null && host != null) instance = host.AddComponent<MatchResultPanel>();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnDestroy()
    {
        KillTweens();
        if (instance == this) instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

#if UNITY_EDITOR
    private void Reset() { AutoAssignAssets(false); }
    private void OnValidate() { AutoAssignAssets(false); }

    [ContextMenu("Re-assign UIPACK art and fonts")]
    private void ForceAssign() { AutoAssignAssets(true); UnityEditor.EditorUtility.SetDirty(this); }

    public void AutoAssignAssets(bool force)
    {
        const string V = "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/";
        const string C = "Assets/UIPACK/ResourcesData/Sprites/Components/";
        const string F = "Assets/UIPACK/ResourcesData/Fonts/";
        LoadFont(ref titleFont, F + "TMP_TiltWarp_Outline.asset", force);
        LoadFont(ref bodyFont, F + "TMP_TiltWarp.asset", force);
        Load(ref panelSprite, V + "Panels/ornate_matchmaking_panel.png", force);
        Load(ref victoryRibbonSprite, V + "Banners/blue_name_ribbon.png", force);
        Load(ref defeatRibbonSprite, V + "Banners/red_name_ribbon.png", force);
        Load(ref victoryGlowSprite, V + "Effects/blue_energy_glow_transparent.png", force);
        Load(ref defeatGlowSprite, V + "Effects/Glowing Red Energy Starburst.png", force);
        Load(ref particlesSprite, V + "Effects/gold_particles.png", force);
        Load(ref crownSprite, V + "Icons/crown.png", force);
        Load(ref laurelSprite, V + "Icons/laurel_wreath.png", force);
        Load(ref swordsSprite, V + "Icons/crossed_swords.png", force);
        Load(ref trophySprite, V + "Icons/trophy.png", force);
        Load(ref starSprite, C + "Icon_ItemIcons/512/ItemIcon_Star.png", force);
        Load(ref coinSprite, "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/coin.png", force);
        Load(ref rowSprite, V + "Panels/wide_status_panel.png", force);
        Load(ref playerFrameSprite, V + "Frames/blue_player_shield_frame.png", force);
        Load(ref opponentFrameSprite, V + "Frames/red_rival_shield_frame.png", force);
        Load(ref primaryButtonSprite, "Assets/UIPACK/Chest_UI_Pack/Buttons/Green_Empty.png", force);
        Load(ref secondaryButtonSprite, "Assets/UIPACK/Chest_UI_Pack/Buttons/Gold_Empty.png", force);
    }

    private static void Load(ref Sprite field, string path, bool force)
    {
        if (field != null && !force) return;
        var s = RuntimeAssets.LoadAssetAtPath<Sprite>(path);
        if (s != null) field = s;
    }

    private static void LoadFont(ref TMP_FontAsset field, string path, bool force)
    {
        if (field != null && !force) return;
        var f = RuntimeAssets.LoadAssetAtPath<TMP_FontAsset>(path);
        if (f != null) field = f;
    }
#endif

    // ───────────────────────────── Public API ─────────────────────────────

    /// <summary>Shows the result screen with the real match data.</summary>
    public void Show(GameManager.MatchResult result, int starsEarned)
    {
        if (IsShowing) return;
        IsShowing = true;
        leaving = false;

        Build();
        EnsureEventSystem();
        Fill(result, starsEarned);
        canvas.gameObject.SetActive(true);
        PlayEntrance(result, starsEarned);
    }

    public void Hide()
    {
        KillTweens();
        IsShowing = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    // ───────────────────────────── Data ─────────────────────────────

    private void Fill(GameManager.MatchResult result, int starsEarned)
    {
        bool victory = result == GameManager.MatchResult.Victory;
        bool draw = result == GameManager.MatchResult.Draw;

        bannerText.text = victory ? "VICTORY" : draw ? "DRAW" : "DEFEAT";
        bannerImage.sprite = victory ? victoryRibbonSprite : defeatRibbonSprite;
        bannerImage.color = draw ? new Color(0.75f, 0.75f, 0.8f, 1f) : Color.white;
        bannerText.color = victory ? Gold : Cream;

        glowImage.sprite = victory ? victoryGlowSprite : defeatGlowSprite;
        glowImage.color = new Color(1f, 1f, 1f, draw ? 0.35f : 0.8f);
        particlesRect.gameObject.SetActive(victory);
        laurelRect.gameObject.SetActive(victory);

        // Names and avatars (same ones the VS intro showed)
        MatchReadyScreen mrs = MatchReadyScreen.Instance;
        playerName.text = MatchReadyScreen.GetLocalPlayerNickname();
        opponentName.text = mrs != null ? mrs.OpponentDisplayName : (MatchReadyScreen.PendingAiOpponentName ?? "Opponent");
        SetAvatar(playerAvatar, mrs != null ? mrs.LocalAvatarSprite : null);
        SetAvatar(opponentAvatar, mrs != null ? mrs.OpponentAvatarSprite : null);

        // Winner's crown; loser's shield dims
        playerCrown.gameObject.SetActive(victory);
        opponentCrown.gameObject.SetActive(result == GameManager.MatchResult.Defeat);
        SetShieldDim(playerShield, result == GameManager.MatchResult.Defeat);
        SetShieldDim(opponentShield, victory);

        for (int i = 0; i < 3; i++)
        {
            stars[i].color = i < starsEarned ? Color.white : StarOff;
            stars[i].transform.localScale = Vector3.zero;
        }

        // Towers destroyed (enemy side, counted by GameManager)
        int towers = 0, elapsed = 0;
        bool baseDown = false;
        if (GameManager.Instance != null) GameManager.Instance.GetMatchStats(out elapsed, out towers, out baseDown);
        towersValue.text = baseDown ? towers + " + Castle" : towers.ToString();

        // Trophies and coins were recorded by ProfileProgress when the match ended
        int delta = ProfileProgress.LastTrophyDelta;
        trophyDeltaText.text = "0";
        trophyDeltaText.color = delta > 0 ? Win : delta < 0 ? Loss : Cream;
        trophyTotalText.text = "Total " + PlayerProfileData.Format(PlayerProfileData.Trophies);
        coinText.text = "0";

        // Play Again goes through the main menu's matchmaking, so it works for every mode
        playAgainButton.gameObject.SetActive(true);
        mainMenuRect.anchoredPosition = new Vector2(-180f, mainMenuRect.anchoredPosition.y);
    }

    private static void SetAvatar(Image img, Sprite s)
    {
        img.gameObject.SetActive(s != null);
        if (s != null) img.sprite = s;
    }

    private static void SetShieldDim(RectTransform shield, bool dimmed)
    {
        var g = shield.GetComponent<CanvasGroup>();
        if (g != null) g.alpha = dimmed ? 0.6f : 1f;
    }

    // ───────────────────────────── Navigation ─────────────────────────────

    private void OnMainMenu()
    {
        if (leaving) return;
        leaving = true;
        ExitThen(() =>
        {
            Time.timeScale = 1f;
            if (Photon.Pun.PhotonNetwork.InRoom) Photon.Pun.PhotonNetwork.LeaveRoom();
            GameModeManager.ResetToDefault();
            SceneManager.LoadScene(MAIN_MENU_SCENE);
        });
    }

    /// <summary>
    /// Play Again: back to the main menu, then start matchmaking there automatically
    /// (same as the player tapping the menu's BATTLE button).
    /// </summary>
    private void OnPlayAgain()
    {
        if (leaving) return;
        leaving = true;
        ExitThen(() =>
        {
            Time.timeScale = 1f;
            if (Photon.Pun.PhotonNetwork.InRoom) Photon.Pun.PhotonNetwork.LeaveRoom();
            GameModeManager.ResetToDefault();
            SceneManager.sceneLoaded += StartMatchmakingWhenMenuLoads;
            SceneManager.LoadScene(MAIN_MENU_SCENE);
        });
    }

    private static void StartMatchmakingWhenMenuLoads(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MAIN_MENU_SCENE) return;
        SceneManager.sceneLoaded -= StartMatchmakingWhenMenuLoads;

        // Give the menu a moment to run its entrance and start connecting, then press BATTLE for the player
        DOTween.Sequence().SetUpdate(true).AppendInterval(0.9f).AppendCallback(() =>
        {
            MainMenuController menu = FindFirstObjectByType<MainMenuController>();
            if (menu != null) { menu.OnBattleButtonClicked(); return; }

            MainMenuManager manager = FindFirstObjectByType<MainMenuManager>();
            if (manager != null) manager.OnPlayMultiplayerClicked();
            else Debug.LogWarning("[MatchResultPanel] Play Again: no main menu found to start matchmaking.");
        });
    }

    private void ExitThen(TweenCallback onDone)
    {
        KillTweens();
        rootGroup.interactable = false;
        DOTween.Sequence().SetUpdate(true)
            .Append(rootGroup.DOFade(0f, 0.22f))
            .Join(cardRect.DOScale(0.92f, 0.22f).SetEase(Ease.InBack))
            .OnComplete(onDone);
    }

    // ───────────────────────────── Animation ─────────────────────────────

    private static readonly float[] STAR_TILTS = { 10f, 0f, -10f };
    private static readonly Vector2 CROWN_REST = new Vector2(0f, 190f);

    private void PlayEntrance(GameManager.MatchResult result, int starsEarned)
    {
        KillTweens();
        bool victory = result == GameManager.MatchResult.Victory;
        bool defeat = result == GameManager.MatchResult.Defeat;

        // ── Starting states (everything reset, so a second show animates the same way)
        rootGroup.alpha = 1f;
        dim.color = defeat ? new Color(0.12f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0f);
        glowRect.localScale = Vector3.zero;
        glowRect.localEulerAngles = Vector3.zero;
        particlesRect.localScale = Vector3.zero;
        laurelRect.localScale = Vector3.zero;
        bannerRect.anchoredPosition = new Vector2(0f, 1400f);
        bannerRect.localScale = victory ? new Vector3(0.2f, 1f, 1f) : Vector3.one;
        bannerRect.localEulerAngles = Vector3.zero;
        bannerText.transform.localScale = Vector3.one * 2.6f;
        bannerText.alpha = 0f;
        cardRect.localScale = Vector3.one * (victory ? 0.6f : 1f);
        cardRect.anchoredPosition = new Vector2(0f, victory ? -260f : -1400f);
        cardRect.localEulerAngles = Vector3.zero;
        cardGroup.alpha = victory ? 0f : 1f;
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform r = rows[i];
            r.GetComponent<CanvasGroup>().alpha = 0f;
            r.localScale = Vector3.one;
            r.anchoredPosition = new Vector2(i % 2 == 0 ? -900f : 900f, r.anchoredPosition.y);
        }
        playerShield.anchoredPosition = new Vector2(-560f, playerShield.anchoredPosition.y);
        opponentShield.anchoredPosition = new Vector2(560f, opponentShield.anchoredPosition.y);
        playerShield.localEulerAngles = opponentShield.localEulerAngles = Vector3.zero;
        mainMenuRect.localScale = Vector3.zero;
        playAgainRect.localScale = Vector3.zero;
        foreach (RectTransform c in new[] { playerCrown, opponentCrown })
        {
            c.localScale = Vector3.one;
            c.anchoredPosition = CROWN_REST + new Vector2(0f, 700f);
            c.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        }
        for (int i = 0; i < 3; i++)
        {
            stars[i].transform.localScale = Vector3.zero;
            stars[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, STAR_TILTS[i]);
        }
        foreach (var piece in confetti) piece.gameObject.SetActive(false);

        KoSAudio.StopMusic(0.5f);
        if ((victory ? victorySound : defeatSound) == null) KoSAudio.Play2D(victory ? "victory_jingle" : "defeat_jingle");
        Play(victory ? victorySound : defeatSound, 0.9f);

        int delta = ProfileProgress.LastTrophyDelta;
        int coins = ProfileProgress.LastCoinReward;

        showSeq = DOTween.Sequence().SetUpdate(true);
        showSeq.Append(dim.DOFade(0.9f, 0.3f));

        if (victory) BuildVictoryIntro();
        else BuildDefeatIntro(defeat);

        // ── Winner crown drops from the sky onto the shield
        RectTransform crown = victory ? playerCrown : (defeat ? opponentCrown : null);
        if (crown != null)
        {
            Image crownImg = crown.GetComponent<Image>();
            showSeq.Append(crown.DOAnchorPos(CROWN_REST, 0.45f).SetEase(Ease.OutBounce));
            showSeq.Join(crownImg.DOFade(1f, 0.15f));
            showSeq.Join(crown.DOPunchRotation(new Vector3(0f, 0f, 25f), 0.6f, 8, 0.6f));
            if (defeat) showSeq.Append(playerShield.DOShakeRotation(0.4f, new Vector3(0f, 0f, 12f), 16));
        }

        // ── Stars: earned ones spin in and land with a sparkle burst, empty ones thud in grey
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            bool earned = idx < starsEarned;
            RectTransform s = stars[idx].rectTransform;
            if (earned)
            {
                showSeq.AppendCallback(() => Play(popSound, 0.8f, 1f + idx * 0.15f));
                showSeq.Append(s.DOScale(1.5f, 0.18f).SetEase(Ease.OutQuad));
                showSeq.Join(s.DOLocalRotate(new Vector3(0f, 0f, STAR_TILTS[idx] - 360f), 0.32f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
                showSeq.Append(s.DOScale(1f, 0.16f).SetEase(Ease.OutBack, 3f));
                showSeq.AppendCallback(() => BurstConfetti(StarWorldPos(idx), 6, 180f, starSprite));
                if (idx == 1) showSeq.AppendCallback(() => Shake(18f));
            }
            else
            {
                showSeq.Append(s.DOScale(1.2f, 0.08f));
                showSeq.Append(s.DOScale(1f, 0.12f).SetEase(Ease.InQuad));
                showSeq.Join(s.DOPunchAnchorPos(new Vector2(0f, -10f), 0.2f, 4));
            }
        }

        // ── Stat plaques slide in from alternate sides
        float rowsStart = showSeq.Duration();
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform r = rows[i];
            float at = rowsStart + 0.1f * i;
            showSeq.Insert(at, r.DOAnchorPosX(0f, 0.4f).SetEase(Ease.OutBack, 1.1f));
            showSeq.Insert(at, r.GetComponent<CanvasGroup>().DOFade(1f, 0.25f));
        }

        // ── Count trophies and coins up, then punch and flash them
        showSeq.Append(DOTween.To(() => 0, v => trophyDeltaText.text = (v > 0 ? "+" : "") + v, delta, 0.7f).SetEase(Ease.OutCubic));
        showSeq.Join(DOTween.To(() => 0, v => coinText.text = "+" + PlayerProfileData.Format(v), coins, 0.7f).SetEase(Ease.OutCubic));
        showSeq.Append(trophyDeltaText.transform.DOPunchScale(Vector3.one * 0.35f, 0.3f, 6));
        showSeq.Join(coinText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6));
        if (victory) showSeq.AppendCallback(() => BurstConfetti(RowWorldPos(2), 8, 220f, coinSprite));

        // ── Buttons bounce in
        showSeq.Append(mainMenuRect.DOScale(1f, 0.35f).SetEase(Ease.OutElastic, 1f, 0.5f));
        if (playAgainButton.gameObject.activeSelf)
            showSeq.Insert(showSeq.Duration() - 0.27f, playAgainRect.DOScale(1f, 0.35f).SetEase(Ease.OutElastic, 1f, 0.5f));

        showSeq.OnComplete(() => StartIdle(victory));

        // Buttons are live from the start; tapping the backdrop skips the animation
        rootGroup.interactable = true;
    }

    /// <summary>Victory: ribbon unrolls, title slams in, confetti bursts, card springs up.</summary>
    private void BuildVictoryIntro()
    {
        showSeq.Append(bannerRect.DOAnchorPosY(BANNER_Y, 0.4f).SetEase(Ease.OutBack, 1.2f));
        showSeq.Append(bannerRect.DOScaleX(1f, 0.32f).SetEase(Ease.OutBack, 2.2f));     // ribbon unrolls
        showSeq.Join(glowRect.DOScale(1f, 0.6f).SetEase(Ease.OutBack));
        showSeq.Append(bannerText.transform.DOScale(1f, 0.28f).SetEase(Ease.InCubic));  // title slams down
        showSeq.Join(bannerText.DOFade(1f, 0.12f));
        showSeq.AppendCallback(() =>
        {
            Shake(22f);
            BurstConfetti(bannerRect.position, 22, 520f, null);
        });
        showSeq.Append(bannerRect.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.35f, 7, 0.6f));
        showSeq.Join(bannerText.DOColor(Color.white, 0.08f).SetLoops(2, LoopType.Yoyo));
        showSeq.Join(laurelRect.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 2.5f));
        showSeq.Join(particlesRect.DOScale(1f, 0.45f).SetEase(Ease.OutBack));

        // Card springs up, shields swing in from the sides
        showSeq.Append(cardRect.DOAnchorPosY(CARD_Y, 0.4f).SetEase(Ease.OutBack, 1.2f));
        showSeq.Join(cardRect.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 1.4f));
        showSeq.Join(cardGroup.DOFade(1f, 0.2f));
        showSeq.Append(playerShield.DOAnchorPosX(-SHIELD_X, 0.4f).SetEase(Ease.OutBack, 1.4f));
        showSeq.Join(opponentShield.DOAnchorPosX(SHIELD_X, 0.4f).SetEase(Ease.OutBack, 1.4f));
        showSeq.Join(playerShield.DOPunchRotation(new Vector3(0f, 0f, -10f), 0.5f, 6));
        showSeq.Join(opponentShield.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.5f, 6));
    }

    /// <summary>Defeat (and draw): heavy banner crash, crooked ribbon, card drops in and shudders.</summary>
    private void BuildDefeatIntro(bool defeat)
    {
        showSeq.Append(bannerRect.DOAnchorPosY(BANNER_Y, 0.32f).SetEase(Ease.InQuad));   // falls like a weight
        showSeq.Join(glowRect.DOScale(1f, 0.5f).SetEase(Ease.OutQuad));
        showSeq.AppendCallback(() => Shake(defeat ? 30f : 12f));
        showSeq.Append(bannerRect.DOPunchAnchorPos(new Vector2(0f, -40f), 0.35f, 5, 0.4f));
        showSeq.Join(bannerText.DOFade(1f, 0.1f));
        showSeq.Join(bannerText.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBounce));
        if (defeat)
        {
            // Ribbon swings and settles crooked, title shudders
            showSeq.Append(bannerRect.DOLocalRotate(new Vector3(0f, 0f, -5f), 0.6f).SetEase(Ease.OutElastic, 1.2f, 0.35f));
            showSeq.Join(bannerText.transform.DOShakePosition(0.4f, new Vector3(10f, 4f, 0f), 20));
            showSeq.Join(bannerText.DOColor(new Color(1f, 0.55f, 0.5f), 0.12f).SetLoops(2, LoopType.Yoyo));
        }

        // Card drops in from below the banner and lands with a shudder
        showSeq.Append(cardRect.DOAnchorPosY(CARD_Y, 0.45f).SetEase(Ease.OutBounce));
        showSeq.Append(playerShield.DOAnchorPosX(-SHIELD_X, 0.35f).SetEase(Ease.OutCubic));
        showSeq.Join(opponentShield.DOAnchorPosX(SHIELD_X, 0.35f).SetEase(Ease.OutBack, 1.6f));
        if (defeat) showSeq.Append(playerShield.DOLocalRotate(new Vector3(0f, 0f, 6f), 0.3f).SetEase(Ease.OutBack));
    }

    private void StartIdle(bool victory)
    {
        idleTweens.Add(glowRect.DORotate(new Vector3(0f, 0f, -360f), 24f, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear).SetUpdate(true));
        idleTweens.Add(glowImage.DOFade(glowImage.color.a * 0.55f, 1.3f)
            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));

        if (victory)
        {
            // Banner floats, title breathes, crown bobs, stars twinkle one after another
            idleTweens.Add(bannerRect.DOAnchorPosY(BANNER_Y + 10f, 1.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            idleTweens.Add(bannerText.transform.DOScale(1.05f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            idleTweens.Add(playerCrown.DOAnchorPosY(CROWN_REST.y + 10f, 1.1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            idleTweens.Add(particlesRect.DORotate(new Vector3(0f, 0f, 360f), 30f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear).SetUpdate(true));

            Sequence twinkle = DOTween.Sequence().SetUpdate(true).SetLoops(-1);
            for (int i = 0; i < 3; i++)
            {
                if (stars[i].color.a < 1f || stars[i].color == StarOff) continue;
                twinkle.Append(stars[i].transform.DOPunchScale(Vector3.one * 0.18f, 0.35f, 4, 0.5f));
            }
            twinkle.AppendInterval(1.6f);
            idleTweens.Add(twinkle);
        }
        else
        {
            // Crooked ribbon sways slowly; the dim pulses a little
            float baseZ = bannerRect.localEulerAngles.z;
            idleTweens.Add(bannerRect.DOLocalRotate(new Vector3(0f, 0f, baseZ + 2.5f), 2.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            idleTweens.Add(dim.DOFade(0.8f, 1.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
        }

        RectTransform primary = playAgainButton.gameObject.activeSelf ? playAgainRect : mainMenuRect;
        idleTweens.Add(primary.DOScale(1.07f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
    }

    // ───────────────────────────── FX helpers ─────────────────────────────

    private const int CONFETTI_COUNT = 30;
    private readonly List<Image> confetti = new List<Image>();
    private readonly List<Tween> fxTweens = new List<Tween>();
    private RectTransform fxLayer;
    private static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.84f, 0.25f), new Color(0.35f, 0.65f, 1f), new Color(1f, 0.4f, 0.35f),
        new Color(0.5f, 1f, 0.45f), Color.white
    };

    private void BuildConfetti(RectTransform root)
    {
        fxLayer = NewRect("FX", root);
        Stretch(fxLayer);
        for (int i = 0; i < CONFETTI_COUNT; i++)
        {
            Image piece = NewImage("Confetti" + i, fxLayer, null, Color.white);
            piece.gameObject.SetActive(false);
            confetti.Add(piece);
        }
    }

    /// <summary>Bursts pooled confetti pieces outward from a world point; they then flutter down and fade.</summary>
    private void BurstConfetti(Vector3 worldPos, int count, float spread, Sprite sprite)
    {
        if (fxLayer == null) return;
        Vector2 origin = fxLayer.InverseTransformPoint(worldPos);
        int used = 0;
        for (int i = 0; i < confetti.Count && used < count; i++)
        {
            Image p = confetti[i];
            if (p.gameObject.activeSelf) continue;
            used++;

            RectTransform rt = p.rectTransform;
            bool useSprite = sprite != null;
            p.sprite = sprite;
            p.preserveAspect = useSprite;
            p.color = useSprite ? Color.white : ConfettiColors[Random.Range(0, ConfettiColors.Length)];
            float size = useSprite ? Random.Range(34f, 56f) : Random.Range(14f, 26f);
            Place(rt, origin, useSprite ? new Vector2(size, size) : new Vector2(size, size * 1.6f));
            rt.localScale = Vector3.one;
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            p.gameObject.SetActive(true);

            float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            Vector2 peak = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(spread * 0.5f, spread);
            float fall = Random.Range(260f, 520f);
            float t1 = Random.Range(0.35f, 0.5f), t2 = Random.Range(0.9f, 1.4f);

            Sequence s = DOTween.Sequence().SetUpdate(true);
            s.Append(rt.DOAnchorPos(peak, t1).SetEase(Ease.OutCubic));
            s.Append(rt.DOAnchorPosY(peak.y - fall, t2).SetEase(Ease.InQuad));
            s.Join(rt.DOAnchorPosX(peak.x + Random.Range(-60f, 60f), t2).SetEase(Ease.InOutSine));
            s.Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-540f, 540f)), t1 + t2, RotateMode.FastBeyond360));
            s.Insert(t1 + t2 * 0.5f, p.DOFade(0f, t2 * 0.5f));
            s.OnComplete(() => p.gameObject.SetActive(false));
            fxTweens.Add(s);
        }
    }

    /// <summary>Short screen shake of the banner and card.</summary>
    private void Shake(float strength)
    {
        fxTweens.Add(cardRect.DOShakeAnchorPos(0.3f, new Vector2(strength, strength * 0.6f), 22, 90f, false, true).SetUpdate(true));
        fxTweens.Add(bannerRect.DOShakeAnchorPos(0.3f, new Vector2(strength * 0.7f, strength * 0.5f), 22, 90f, false, true).SetUpdate(true));
    }

    private Vector3 StarWorldPos(int i) => stars[i].rectTransform.position;
    private Vector3 RowWorldPos(int i) => i < rows.Count ? rows[i].position : cardRect.position;

    private void KillTweens()
    {
        if (showSeq != null && showSeq.IsActive()) showSeq.Kill(true);
        showSeq = null;
        foreach (var t in idleTweens) if (t != null && t.IsActive()) t.Kill();
        idleTweens.Clear();
        foreach (var t in fxTweens) if (t != null && t.IsActive()) t.Kill();
        fxTweens.Clear();
        foreach (var p in confetti) if (p != null) p.gameObject.SetActive(false);
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.pitch = pitch;
        KoSAudio.PlayOneShot(audioSource, clip, volume, "result");
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    // ───────────────────────────── Build (once) ─────────────────────────────

    // Layout in a 1080 x 1920 reference canvas, origin at screen centre
    private const float BANNER_Y = 735f;
    private const float CARD_Y = -110f;
    private const float SHIELD_X = 215f;

    private void Build()
    {
        if (built) return;
        built = true;

        // Own overlay canvas so the layout doesn't depend on the battle HUD's scaler
        var root = new GameObject("[MatchResultPanel]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = root.GetComponent<CanvasGroup>();
        RectTransform rootRt = (RectTransform)root.transform;

        // Dim (also blocks taps on the battlefield; tapping it skips the entrance)
        dim = NewImage("Dim", rootRt, null, Color.black);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        var skip = dim.gameObject.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(() => { if (showSeq != null && showSeq.IsActive()) showSeq.Complete(true); });

        // Energy glow behind the banner
        glowImage = NewImage("Glow", rootRt, victoryGlowSprite, Color.white);
        glowImage.preserveAspect = true;
        glowRect = glowImage.rectTransform;
        Place(glowRect, new Vector2(0f, BANNER_Y + 10f), new Vector2(1250f, 1200f));

        // ── Card: ornate gold-trimmed panel
        Image card = NewImage("Card", rootRt, panelSprite, Color.white);
        cardRect = card.rectTransform;
        Place(cardRect, new Vector2(0f, CARD_Y), new Vector2(880f, 1252f));   // 487 x 693 art ratio
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();

        // Shields with portraits, names on the shield ribbon, crown for the winner
        BuildShield(cardRect, -SHIELD_X, playerFrameSprite, out playerShield, out playerAvatar, out playerName, out playerCrown);
        BuildShield(cardRect, SHIELD_X, opponentFrameSprite, out opponentShield, out opponentAvatar, out opponentName, out opponentCrown);

        Image swords = NewImage("Swords", cardRect, swordsSprite, Color.white);
        swords.preserveAspect = true;
        Place(swords.rectTransform, new Vector2(0f, 330f), new Vector2(170f, 136f));
        var vs = NewText("VS", cardRect, "VS", 64f, Gold, titleFont);
        Place(vs.rectTransform, new Vector2(0f, 235f), new Vector2(160f, 90f));

        // Stars
        float[] sx = { -140f, 0f, 140f };
        float[] sz = { 118f, 150f, 118f };
        float[] sy = { 70f, 88f, 70f };
        for (int i = 0; i < 3; i++)
        {
            stars[i] = NewImage("Star" + i, cardRect, starSprite, Color.white);
            stars[i].preserveAspect = true;
            Place(stars[i].rectTransform, new Vector2(sx[i], sy[i]), new Vector2(sz[i], sz[i]));
            stars[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 10f : i == 2 ? -10f : 0f);
        }

        // Stat plaques
        RectTransform towerRow = BuildStatRow(cardRect, -75f, swordsSprite, "Towers", out towersValue);
        RectTransform trophyRow = BuildStatRow(cardRect, -215f, trophySprite, "Trophies", out trophyDeltaText);
        // Trophy total sits under the label, inside the plaque
        Transform trophyLabel = trophyRow.Find("Label");
        if (trophyLabel != null) ((RectTransform)trophyLabel).anchoredPosition = new Vector2(-50f, 14f);
        trophyTotalText = NewText("Total", trophyRow, "", 26f, Gold, bodyFont);
        trophyTotalText.alignment = TextAlignmentOptions.Left;
        Place(trophyTotalText.rectTransform, new Vector2(-50f, -24f), new Vector2(330f, 34f));
        BuildStatRow(cardRect, -355f, coinSprite, "Gold", out coinText);

        // Buttons
        mainMenuButton = NewButton("Btn_MainMenu", cardRect, secondaryButtonSprite, "MAIN MENU", new Vector2(-180f, -500f), out mainMenuRect);
        playAgainButton = NewButton("Btn_PlayAgain", cardRect, primaryButtonSprite, "PLAY AGAIN", new Vector2(180f, -500f), out playAgainRect);
        mainMenuButton.onClick.AddListener(OnMainMenu);
        playAgainButton.onClick.AddListener(OnPlayAgain);

        // ── Banner on top of everything: ribbon, laurel + sparkles behind the title
        laurelRect = NewImage("Laurel", rootRt, laurelSprite, Color.white).rectTransform;
        laurelRect.GetComponent<Image>().preserveAspect = true;
        Place(laurelRect, new Vector2(0f, BANNER_Y + 118f), new Vector2(214f, 187f));
        Image crownTop = NewImage("Crown", laurelRect, crownSprite, Color.white);
        crownTop.preserveAspect = true;
        Place(crownTop.rectTransform, new Vector2(0f, 6f), new Vector2(140f, 118f));

        particlesRect = NewImage("Sparkles", rootRt, particlesSprite, Color.white).rectTransform;
        Place(particlesRect, new Vector2(0f, BANNER_Y), new Vector2(900f, 840f));

        bannerImage = NewImage("Banner", rootRt, victoryRibbonSprite, Color.white);
        bannerRect = bannerImage.rectTransform;
        Place(bannerRect, new Vector2(0f, BANNER_Y), new Vector2(980f, 266f));   // 350 x 95 art ratio
        bannerText = NewText("Title", bannerRect, "VICTORY", 120f, Gold, titleFont);
        bannerText.characterSpacing = 4f;
        Place(bannerText.rectTransform, new Vector2(0f, 14f), new Vector2(820f, 170f));

        // Pooled confetti on top of everything (no raycasts, reused every show)
        BuildConfetti(rootRt);

        root.SetActive(false);
    }

    private void BuildShield(RectTransform parent, float x, Sprite frame, out RectTransform shield, out Image avatar, out TextMeshProUGUI name, out RectTransform crown)
    {
        shield = NewRect(x < 0f ? "PlayerShield" : "OpponentShield", parent);
        Place(shield, new Vector2(x, 300f), new Vector2(300f, 353f));        // 152 x 179 art ratio
        shield.gameObject.AddComponent<CanvasGroup>();

        Image f = NewImage("Frame", shield, frame, Color.white);
        Stretch(f.rectTransform);

        // Portrait sits inside the shield's upper face
        avatar = NewImage("Avatar", shield, null, Color.white);
        avatar.preserveAspect = true;
        Place(avatar.rectTransform, new Vector2(0f, 38f), new Vector2(196f, 196f));

        // Name on the shield's ribbon
        name = NewText("Name", shield, "", 30f, Cream, titleFont);
        name.enableAutoSizing = true;
        name.fontSizeMin = 18f;
        name.fontSizeMax = 30f;
        Place(name.rectTransform, new Vector2(0f, -100f), new Vector2(232f, 46f));

        Image c = NewImage("Crown", shield, crownSprite, Color.white);
        c.preserveAspect = true;
        crown = c.rectTransform;
        Place(crown, new Vector2(0f, 190f), new Vector2(124f, 104f));
        crown.localEulerAngles = new Vector3(0f, 0f, x < 0f ? 10f : -10f);
    }

    private RectTransform BuildStatRow(RectTransform parent, float y, Sprite icon, string label, out TextMeshProUGUI value)
    {
        Image plaque = NewImage("Row_" + label, parent, rowSprite, Color.white);
        // 9-sliced (border set on the sprite import) so the gold rim keeps its thickness when widened
        if (rowSprite != null && rowSprite.border.sqrMagnitude > 0f) plaque.type = Image.Type.Sliced;
        RectTransform row = plaque.rectTransform;
        Place(row, new Vector2(0f, y), new Vector2(680f, 128f));
        row.gameObject.AddComponent<CanvasGroup>();
        rows.Add(row);

        Image ic = NewImage("Icon", row, icon, Color.white);
        ic.preserveAspect = true;
        Place(ic.rectTransform, new Vector2(-262f, 2f), new Vector2(92f, 80f));

        var l = NewText("Label", row, label.ToUpperInvariant(), 36f, Muted, bodyFont);
        l.alignment = TextAlignmentOptions.Left;
        Place(l.rectTransform, new Vector2(-50f, 0f), new Vector2(330f, 64f));

        value = NewText("Value", row, "0", 56f, Cream, titleFont);
        value.alignment = TextAlignmentOptions.Right;
        Place(value.rectTransform, new Vector2(170f, 2f), new Vector2(260f, 74f));
        return row;
    }

    private Button NewButton(string name, RectTransform parent, Sprite sprite, string label, Vector2 pos, out RectTransform rt)
    {
        Image img = NewImage(name, parent, sprite, Color.white);
        img.raycastTarget = true;
        rt = img.rectTransform;
        Place(rt, pos, new Vector2(330f, 162f));                              // 183 x 90 art ratio
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = NewText("Text", rt, label, 46f, Color.white, titleFont);
        t.enableAutoSizing = true;
        t.fontSizeMin = 28f;
        t.fontSizeMax = 46f;
        Place(t.rectTransform, new Vector2(0f, 6f), new Vector2(270f, 90f));
        return btn;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Victory (3 stars)")] private void PreviewVictory() { IsShowing = false; Show(GameManager.MatchResult.Victory, 3); }
    [ContextMenu("Preview Defeat")] private void PreviewDefeat() { IsShowing = false; Show(GameManager.MatchResult.Defeat, 0); }
#endif
}
