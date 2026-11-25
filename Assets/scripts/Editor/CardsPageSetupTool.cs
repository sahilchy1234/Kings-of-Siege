#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Siege.Cards;
using DG.Tweening;

/// <summary>
/// One-click editor tool that builds the complete, pixel-perfect Clash Royale style
/// Cards Page UI hierarchy (Collection grid + Battle Deck panel + card detail popup).
/// Calibrated for mobile portrait phone layout (1080x1920) with authentic game assets.
/// Menu: Kings of Siege > Build Cards Page
/// </summary>
public class CardsPageSetupTool : EditorWindow
{
    [MenuItem("Kings of Siege/Build Cards Page")]
    public static void OpenWindow()
    {
        var win = GetWindow<CardsPageSetupTool>("Cards Page Builder");
        win.minSize = new Vector2(420, 320);
        win.Show();
    }

    [MenuItem("Kings of Siege/Run One-Click Cards Page Build")]
    public static void RunBuildDirect()
    {
        BuildCardsPage(null, null);
    }

    private Canvas targetCanvas;
    private CardLibrary cardLibrary;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Kings of Siege — Cards Page Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds the AAA Clash Royale-style Cards Page (Decks + Collection tabs) " +
            "calibrated for portrait phone layout using authentic game assets.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", targetCanvas, typeof(Canvas), allowSceneObjects: true);
        if (targetCanvas == null)
            targetCanvas = Object.FindObjectOfType<Canvas>();

        cardLibrary = (CardLibrary)EditorGUILayout.ObjectField("Card Library", cardLibrary, typeof(CardLibrary), allowSceneObjects: false);

        EditorGUILayout.Space(14);
        GUI.backgroundColor = new Color(0.2f, 0.75f, 0.35f);
        if (GUILayout.Button("BUILD CARDS PAGE (ONE-CLICK)", GUILayout.Height(46)))
        {
            BuildCardsPage(targetCanvas, cardLibrary);
        }
        GUI.backgroundColor = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────────
    public static GameObject BuildCardsPage(Canvas targetCanvas = null, CardLibrary cardLibrary = null)
    {
        if (targetCanvas == null)
        {
            targetCanvas = Object.FindObjectOfType<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogError("[CardsPageSetupTool] No Canvas found in scene!");
                return null;
            }
        }

        // 1. Ensure CardLibrary ScriptableObject
        if (cardLibrary == null)
            cardLibrary = CreateAndPopulateCardLibrary();

        // 2. Ensure CardInventoryManager singleton
        var invManager = EnsureInventoryManager(cardLibrary);

        // Remove old [CardsPage] if present
        var old = targetCanvas.transform.Find("[CardsPage]");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 3. Root GameObject with RectTransform
        var rootGO = new GameObject("[CardsPage]", typeof(RectTransform));
        rootGO.transform.SetParent(targetCanvas.transform, false);
        var rootRect = rootGO.GetComponent<RectTransform>();
        StretchFull(rootRect);

        var rootCG = rootGO.AddComponent<CanvasGroup>();
        rootCG.alpha = 1f;

        // 100% Solid Royal Blue Background (ZERO castle bleed)
        var bgSolid = rootGO.AddComponent<Image>();
        bgSolid.color = new Color(0.04f, 0.12f, 0.28f, 1f);

        // Tiled Diamond Quilted Mesh Overlay (Clash Royale authentic look)
        var patternGO = MakeUIGameObject("PatternOverlay", rootGO.transform);
        StretchFull(patternGO);
        var patternImg = patternGO.gameObject.AddComponent<Image>();
        var patternSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Background/Pattern_1024.png");
        if (patternSpr != null)
        {
            patternImg.sprite = patternSpr;
            patternImg.type = Image.Type.Tiled;
            patternImg.color = new Color(0.12f, 0.32f, 0.65f, 0.38f);
            patternImg.raycastTarget = false;
        }

        // Soft Vignette / Ambient Lighting Gradients
        var vignetteTop = MakeUIGameObject("VignetteTop", rootGO.transform);
        vignetteTop.anchorMin = new Vector2(0, 0.70f); vignetteTop.anchorMax = new Vector2(1, 1);
        vignetteTop.offsetMin = Vector2.zero; vignetteTop.offsetMax = Vector2.zero;
        var vtImg = vignetteTop.gameObject.AddComponent<Image>();
        vtImg.color = new Color(0.02f, 0.06f, 0.15f, 0.40f);
        vtImg.raycastTarget = false;

        // 4. HUD Bar (Top 74px)
        var hudBar = BuildHUDBar(rootGO);

        // 5. Tab Bar (Decks | Collection) - 68px
        var (tabBar, decksTabBtn, collectionTabBtn, decksTabRect, collectionTabRect, tabUnderline,
             decksLbl, collectionLbl, decksBg, collBg, notifBadge, notifText) = BuildTabBar(rootGO);

        // 6. Content Area (Between TabBar and Bottom Navigation)
        var contentArea = MakeUIGameObject("ContentArea", rootGO.transform);
        contentArea.anchorMin = new Vector2(0, 0);
        contentArea.anchorMax = new Vector2(1, 1);
        contentArea.offsetMin = new Vector2(0,  135); // Above bottom nav
        contentArea.offsetMax = new Vector2(0, -150); // Below HUD + TabBar

        // 7. Decks Panel (Battle Deck 8 slots + Elixir Avg + King/Princess Footer)
        var (decksPanel, deckSlots, elixirText, elixirDot, deckNameText,
             kingTowerRoot, kingTowerImg, princessTowerImg, kingLvlText, kingLvlBar) = BuildDecksPanel(contentArea.gameObject);

        // 8. Collection Panel (Header + SubTabs + Filters + 4-column Scroll View)
        var (collPanel, scrollRect, contentRoot, foundGrid, notFoundGrid,
             foundHeader, notFoundHeader, foundCountText, collTitle) = BuildCollectionPanel(contentArea.gameObject);

        // 9. Card Prefabs (236x314) - Force Rebuild
        var collectionCardPrefab = BuildCollectionCardPrefab(forceRebuild: true);
        var deckSlotPrefab       = BuildDeckSlotPrefab(forceRebuild: true);

        // 10. Card Detail Popup (Juicy Modal with Upgrade & Use)
        var (detailPopup, detailCG, detailArt, detailName, detailLevel, detailCopies, detailDesc,
             addBtn, removeBtn, closeBtn, addBtnLabel, dimmerBg,
             upgBtn, upgLbl, upgCostTxt, upgCoinImg) = BuildCardDetailPopup(rootGO);

        // 11. Upgrade Celebration Overlay (AAA Clash Royale Level-Up Sequence)
        var upgradeOverlay = BuildUpgradeCelebrationOverlay(rootGO);

        // 12. Attach & Wire CardsPageController
        var ctrl = rootGO.AddComponent<CardsPageController>();
        var so = new SerializedObject(ctrl);

        AssignField(so, "pageRoot",                   rootGO);
        AssignField(so, "pageCanvasGroup",             rootCG);
        AssignField(so, "pageBackground",              bgSolid);

        AssignField(so, "decksTabButton",              decksTabBtn);
        AssignField(so, "collectionTabButton",         collectionTabBtn);
        AssignField(so, "decksTabRect",                decksTabRect);
        AssignField(so, "collectionTabRect",           collectionTabRect);
        AssignField(so, "tabUnderline",                tabUnderline);
        AssignField(so, "decksTabLabel",               decksLbl);
        AssignField(so, "collectionTabLabel",          collectionLbl);
        AssignField(so, "decksTabBackground",          decksBg);
        AssignField(so, "collectionTabBackground",     collBg);
        AssignField(so, "collectionTabNotifBadge",     notifBadge);
        AssignField(so, "collectionTabNotifText",      notifText);

        AssignField(so, "decksPanel",                  decksPanel);
        AssignField(so, "deckNameText",                deckNameText);
        AssignField(so, "elixirAverageText",           elixirText);
        AssignField(so, "elixirDotImage",              elixirDot);
        AssignField(so, "kingTowerPreviewRoot",        kingTowerRoot);
        AssignField(so, "kingTowerImage",              kingTowerImg);
        AssignField(so, "princessTowerImage",          princessTowerImg);
        AssignField(so, "kingTowerLevelText",          kingLvlText);
        AssignField(so, "kingTowerLevelBar",           kingLvlBar);

        var slotsProperty = so.FindProperty("deckSlots");
        slotsProperty.arraySize = deckSlots.Length;
        for (int i = 0; i < deckSlots.Length; i++)
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = deckSlots[i];

        AssignField(so, "collectionPanel",             collPanel);
        AssignField(so, "collectionTitleText",         collTitle);
        AssignField(so, "foundCountText",              foundCountText);
        AssignField(so, "collectionScrollRect",        scrollRect);
        AssignField(so, "collectionContentRoot",       contentRoot);
        AssignField(so, "foundCardsGrid",              foundGrid);
        AssignField(so, "notFoundCardsGrid",           notFoundGrid);
        AssignField(so, "foundSectionHeader",          foundHeader);
        AssignField(so, "notFoundSectionHeader",       notFoundHeader);
        AssignField(so, "collectionCardPrefab",        collectionCardPrefab);
        AssignField(so, "deckSlotPrefab",              deckSlotPrefab);

        AssignField(so, "cardDetailPopup",             detailPopup);
        AssignField(so, "cardDetailCanvasGroup",       detailCG);
        AssignField(so, "cardDetailArt",               detailArt);
        AssignField(so, "cardDetailName",              detailName);
        AssignField(so, "cardDetailLevel",             detailLevel);
        AssignField(so, "cardDetailCopies",            detailCopies);
        AssignField(so, "cardDetailDescription",       detailDesc);
        AssignField(so, "cardDetailAddToDeckButton",   addBtn);
        AssignField(so, "cardDetailRemoveButton",      removeBtn);
        AssignField(so, "cardDetailCloseButton",       closeBtn);
        AssignField(so, "cardDetailAddButtonLabel",    addBtnLabel);
        AssignField(so, "cardDetailDimmerBackground",  dimmerBg);
        AssignField(so, "cardDetailUpgradeButton",     upgBtn);
        AssignField(so, "cardDetailUpgradeLabel",      upgLbl);
        AssignField(so, "cardDetailUpgradeCostText",   upgCostTxt);
        AssignField(so, "cardDetailUpgradeCoinIcon",   upgCoinImg);
        AssignField(so, "upgradeCelebrationOverlay",    upgradeOverlay);

        AssignField(so, "inventoryManager",            invManager);
        AssignField(so, "cardLibrary",                 cardLibrary);

        // Auto-detect HUD coin/gem texts
        foreach (var t in hudBar.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "CoinText") AssignField(so, "hudCoinsText", t);
            if (t.name == "GemText")  AssignField(so, "hudGemsText",  t);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rootGO);

        // Ensure bottom navigation bar ('downpanel') is rendered on top of CardsPage
        var down = targetCanvas.transform.Find("Panel/downpanel");
        if (down != null)
        {
            down.SetParent(targetCanvas.transform, true);
            down.SetAsLastSibling();
        }
        else
        {
            var downCanvas = targetCanvas.transform.Find("downpanel");
            if (downCanvas != null) downCanvas.SetAsLastSibling();
        }

        // Open and refresh page
        rootGO.SetActive(true);
        ctrl.OpenPage();

        Selection.activeGameObject = rootGO;
        Undo.RegisterCreatedObjectUndo(rootGO, "Build Cards Page");
        Debug.Log("[CardsPageSetupTool] AAA Clash Royale Cards Page built and refreshed successfully!");
        return rootGO;
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region HUD Bar (Top 74px)

    private static GameObject BuildHUDBar(GameObject parent)
    {
        var hud = MakeRectChild(parent, "HUDBar");
        hud.anchorMin = new Vector2(0, 1); hud.anchorMax = new Vector2(1, 1);
        hud.sizeDelta = new Vector2(0, 74); hud.anchoredPosition = new Vector2(0, -42);

        var bg = hud.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.08f, 0.18f, 0.98f);

        // Claim button (left)
        var claimBtnRT = MakeRectChild(hud.gameObject, "ClaimButton", 170, 50);
        claimBtnRT.anchorMin = new Vector2(0, 0.5f); claimBtnRT.anchorMax = new Vector2(0, 0.5f);
        claimBtnRT.pivot = new Vector2(0, 0.5f);
        claimBtnRT.anchoredPosition = new Vector2(24, 0);

        var claimImg = claimBtnRT.gameObject.AddComponent<Image>();
        claimImg.color = new Color(0.96f, 0.68f, 0.12f);
        claimImg.sprite = CreateRoundedRectSprite();
        claimImg.type = Image.Type.Sliced;

        // Bottom bevel shadow for 3D look
        var claimShadow = MakeRectChild(claimBtnRT.gameObject, "Shadow");
        claimShadow.anchorMin = new Vector2(0.04f, 0); claimShadow.anchorMax = new Vector2(0.96f, 0.12f);
        claimShadow.offsetMin = Vector2.zero; claimShadow.offsetMax = Vector2.zero;
        var csImg = claimShadow.gameObject.AddComponent<Image>();
        csImg.color = new Color(0.70f, 0.42f, 0.05f, 0.9f);
        csImg.sprite = CreateRoundedRectSprite();
        csImg.type = Image.Type.Sliced;
        csImg.raycastTarget = false;

        var claimCrownRT = MakeRectChild(claimBtnRT.gameObject, "CrownIcon", 32, 32);
        claimCrownRT.anchorMin = new Vector2(0.18f, 0.55f); claimCrownRT.anchorMax = new Vector2(0.18f, 0.55f);
        var crownImg = claimCrownRT.gameObject.AddComponent<Image>();
        var crownSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Crown.png");
        if (crownSpr != null) { crownImg.sprite = crownSpr; crownImg.preserveAspect = true; }

        var claimTxt = MakeTextChild(claimBtnRT.gameObject, "ClaimTxt", "Claim", 21, Color.white);
        claimTxt.anchorMin = new Vector2(0.36f, 0); claimTxt.anchorMax = new Vector2(0.95f, 1);
        var ct = claimTxt.GetComponent<TMP_Text>();
        ct.alignment = TextAlignmentOptions.MidlineLeft;
        ct.fontStyle = FontStyles.Bold;

        // Notification badge on Claim button
        var notifBadgeRT = MakeRectChild(claimBtnRT.gameObject, "ClaimNotifBadge", 24, 24);
        notifBadgeRT.anchorMin = new Vector2(1, 1); notifBadgeRT.anchorMax = new Vector2(1, 1);
        notifBadgeRT.anchoredPosition = new Vector2(-4, -4);
        var nbImg = notifBadgeRT.gameObject.AddComponent<Image>();
        nbImg.sprite = CreateCircleSprite();
        nbImg.color = new Color(1f, 0.2f, 0.35f);
        var nbTxt = MakeTextChild(notifBadgeRT.gameObject, "Num", "1", 13, Color.white);
        StretchFull(nbTxt);
        nbTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        nbTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // Coin counter container pill (center)
        var coinPill = MakeRectChild(hud.gameObject, "CoinPill", 184, 48);
        coinPill.anchorMin = new Vector2(0.50f, 0.5f); coinPill.anchorMax = new Vector2(0.50f, 0.5f);
        coinPill.anchoredPosition = new Vector2(0, 0);
        var coinPillBg = coinPill.gameObject.AddComponent<Image>();
        coinPillBg.sprite = CreateRoundedRectSprite();
        coinPillBg.type = Image.Type.Sliced;
        coinPillBg.color = new Color(0.04f, 0.09f, 0.18f, 0.95f);

        var coinIcon = MakeRectChild(coinPill.gameObject, "CoinIcon", 36, 36);
        coinIcon.anchorMin = new Vector2(0, 0.5f); coinIcon.anchorMax = new Vector2(0, 0.5f);
        coinIcon.anchoredPosition = new Vector2(24, 0);
        var coinImg = coinIcon.gameObject.AddComponent<Image>();
        var coinSpr = LoadAsset<Sprite>("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/coin.png");
        if (coinSpr != null) { coinImg.sprite = coinSpr; coinImg.preserveAspect = true; }

        var coinTxt = MakeTextChild(coinPill.gameObject, "CoinText", "31,329", 22, Color.white);
        coinTxt.anchorMin = new Vector2(0, 0); coinTxt.anchorMax = new Vector2(1, 1);
        coinTxt.offsetMin = new Vector2(48, 0); coinTxt.offsetMax = new Vector2(-10, 0);
        coinTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
        coinTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

        // Gem counter container pill (right)
        var gemPill = MakeRectChild(hud.gameObject, "GemPill", 155, 48);
        gemPill.anchorMin = new Vector2(0.85f, 0.5f); gemPill.anchorMax = new Vector2(0.85f, 0.5f);
        gemPill.anchoredPosition = new Vector2(0, 0);
        var gemPillBg = gemPill.gameObject.AddComponent<Image>();
        gemPillBg.sprite = CreateRoundedRectSprite();
        gemPillBg.type = Image.Type.Sliced;
        gemPillBg.color = new Color(0.04f, 0.09f, 0.18f, 0.95f);

        var gemIcon = MakeRectChild(gemPill.gameObject, "GemIcon", 34, 34);
        gemIcon.anchorMin = new Vector2(0, 0.5f); gemIcon.anchorMax = new Vector2(0, 0.5f);
        gemIcon.anchoredPosition = new Vector2(24, 0);
        var gemImg = gemIcon.gameObject.AddComponent<Image>();
        var gemSpr = LoadAsset<Sprite>("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/gem.png");
        if (gemSpr != null) { gemImg.sprite = gemSpr; gemImg.preserveAspect = true; }

        var gemTxt = MakeTextChild(gemPill.gameObject, "GemText", "100", 22, new Color(0.22f, 0.95f, 0.52f));
        gemTxt.anchorMin = new Vector2(0, 0); gemTxt.anchorMax = new Vector2(1, 1);
        gemTxt.offsetMin = new Vector2(48, 0); gemTxt.offsetMax = new Vector2(-10, 0);
        gemTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
        gemTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

        return hud.gameObject;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Tab Bar (Decks | Collection)

    private static (RectTransform tabBar,
             Button decksBtn, Button collBtn,
             RectTransform decksRect, RectTransform collRect,
             RectTransform underline,
             TMP_Text decksLbl, TMP_Text collLbl,
             Image decksBg, Image collBg,
             Image notifBadge, TMP_Text notifText)
        BuildTabBar(GameObject parent)
    {
        var bar = MakeRectChild(parent, "TabBar");
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
        bar.sizeDelta = new Vector2(0, 68); bar.anchoredPosition = new Vector2(0, -114);

        // Recessed dark container track
        var barBg = bar.gameObject.AddComponent<Image>();
        barBg.color = new Color(0.03f, 0.08f, 0.18f, 0.95f);

        // Decks tab pill (Left 50%, with 16px margins)
        var decksRect = MakeRectChild(bar.gameObject, "DecksTab");
        decksRect.anchorMin = new Vector2(0.02f, 0.08f); decksRect.anchorMax = new Vector2(0.49f, 0.92f);
        decksRect.offsetMin = Vector2.zero; decksRect.offsetMax = Vector2.zero;
        var decksBtn = decksRect.gameObject.AddComponent<Button>();
        var decksBg = decksRect.gameObject.AddComponent<Image>();
        decksBg.sprite = CreateRoundedRectSprite();
        decksBg.type = Image.Type.Sliced;
        decksBg.color = new Color(0.11f, 0.65f, 0.98f, 1f); // Vibrant Clash Royale sky blue pill
        decksBtn.targetGraphic = decksBg;

        // Bottom bevel shadow on active pill for 3D tactile feel
        var decksShadow = MakeRectChild(decksRect.gameObject, "BevelShadow");
        decksShadow.anchorMin = new Vector2(0.04f, 0); decksShadow.anchorMax = new Vector2(0.96f, 0.12f);
        decksShadow.offsetMin = Vector2.zero; decksShadow.offsetMax = Vector2.zero;
        var dsImg = decksShadow.gameObject.AddComponent<Image>();
        dsImg.color = new Color(0.05f, 0.40f, 0.72f, 0.9f);
        dsImg.sprite = CreateRoundedRectSprite();
        dsImg.type = Image.Type.Sliced;
        dsImg.raycastTarget = false;

        var decksLbl = MakeTextChild(decksRect.gameObject, "DecksLabel", "Decks", 26, Color.white);
        StretchFull(decksLbl);
        var dlTMP = decksLbl.GetComponent<TMP_Text>();
        dlTMP.alignment = TextAlignmentOptions.Center;
        dlTMP.fontStyle = FontStyles.Bold;

        // Collection tab pill (Right 50%, with 16px margins)
        var collRect = MakeRectChild(bar.gameObject, "CollectionTab");
        collRect.anchorMin = new Vector2(0.51f, 0.08f); collRect.anchorMax = new Vector2(0.98f, 0.92f);
        collRect.offsetMin = Vector2.zero; collRect.offsetMax = Vector2.zero;
        var collBtn = collRect.gameObject.AddComponent<Button>();
        var collBg = collRect.gameObject.AddComponent<Image>();
        collBg.sprite = CreateRoundedRectSprite();
        collBg.type = Image.Type.Sliced;
        collBg.color = new Color(0.05f, 0.14f, 0.28f, 0.95f); // Inactive dark navy pill
        collBtn.targetGraphic = collBg;

        var collLbl = MakeTextChild(collRect.gameObject, "CollLabel", "Collection", 26, new Color(0.65f, 0.76f, 0.90f));
        StretchFull(collLbl);
        var clTMP = collLbl.GetComponent<TMP_Text>();
        clTMP.alignment = TextAlignmentOptions.Center;
        clTMP.fontStyle = FontStyles.Bold;

        // Notification badge on collection tab
        var badgeRect = MakeRectChild(collRect.gameObject, "NotifBadge", 28, 28);
        badgeRect.anchorMin = new Vector2(0.92f, 0.80f); badgeRect.anchorMax = new Vector2(0.92f, 0.80f);
        var badgeImg = badgeRect.gameObject.AddComponent<Image>();
        badgeImg.sprite = CreateCircleSprite();
        badgeImg.color = new Color(1f, 0.22f, 0.35f);
        var notifTxt = MakeTextChild(badgeRect.gameObject, "NotifText", "2", 14, Color.white);
        StretchFull(notifTxt);
        notifTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        notifTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // Underline dummy (kept for API compatibility, invisible)
        var underline = MakeRectChild(bar.gameObject, "TabUnderline");
        underline.anchorMin = Vector2.zero; underline.anchorMax = Vector2.zero;
        underline.sizeDelta = Vector2.zero;
        underline.gameObject.SetActive(false);

        return (bar, decksBtn, collBtn, decksRect, collRect, underline,
                dlTMP, clTMP, decksBg, collBg, badgeImg, notifTxt.GetComponent<TMP_Text>());
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Decks Panel (Battle Deck 8 slots + 4 units + Elixir Row + Footer)

    private static (RectTransform panel,
             DeckSlotUI[] slots,
             TMP_Text elixirText, Image elixirDot,
             TMP_Text deckNameText,
             RectTransform kingRoot, Image kingImg, Image princessImg, TMP_Text kingLvl, Image kingBar)
        BuildDecksPanel(GameObject parent)
    {
        var panel = MakeRectChild(parent, "DecksPanel");
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero; panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<CanvasGroup>();

        // Smooth vertical ScrollRect
        var scrollRect = panel.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.08f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 28f;

        var viewport = MakeRectChild(panel.gameObject, "Viewport");
        StretchFull(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.005f); // Transparent raycast target

        var content = MakeRectChild(viewport.gameObject, "Content");
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0, 1860);
        content.anchoredPosition = Vector2.zero;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        // 1. Battle Deck Header Plaque (Clash Royale Style Heraldic Banner)
        var headerRow = MakeRectChild(content.gameObject, "DeckHeader");
        headerRow.anchorMin = new Vector2(0.04f, 1); headerRow.anchorMax = new Vector2(0.96f, 1);
        headerRow.sizeDelta = new Vector2(0, 58); headerRow.anchoredPosition = new Vector2(0, -32);
        var headerBg = headerRow.gameObject.AddComponent<Image>();
        headerBg.sprite = CreateRoundedRectSprite();
        headerBg.type = Image.Type.Sliced;
        headerBg.color = new Color(0.09f, 0.32f, 0.68f, 0.98f); // 3D Blue ribbon plaque

        // Bottom bevel shadow
        var hShadow = MakeRectChild(headerRow.gameObject, "HShadow");
        hShadow.anchorMin = new Vector2(0.02f, 0); hShadow.anchorMax = new Vector2(0.98f, 0.12f);
        hShadow.offsetMin = Vector2.zero; hShadow.offsetMax = Vector2.zero;
        var hsImg = hShadow.gameObject.AddComponent<Image>();
        hsImg.color = new Color(0.04f, 0.18f, 0.42f, 0.95f);
        hsImg.sprite = CreateRoundedRectSprite();
        hsImg.type = Image.Type.Sliced;
        hsImg.raycastTarget = false;

        // Cards shield icon badge on left of ribbon
        var deckIconRT = MakeRectChild(headerRow.gameObject, "DeckIcon", 48, 48);
        deckIconRT.anchorMin = new Vector2(0, 0.5f); deckIconRT.anchorMax = new Vector2(0, 0.5f);
        deckIconRT.anchoredPosition = new Vector2(34, 0);
        var deckIconImg = deckIconRT.gameObject.AddComponent<Image>();
        var cardsSpr = LoadAsset<Sprite>("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/cards.png");
        if (cardsSpr != null) { deckIconImg.sprite = cardsSpr; deckIconImg.preserveAspect = true; }

        var deckNameRT = MakeTextChild(headerRow.gameObject, "DeckNameText", "Battle Deck", 28, Color.white);
        deckNameRT.anchorMin = new Vector2(0, 0); deckNameRT.anchorMax = new Vector2(1, 1);
        deckNameRT.offsetMin = new Vector2(78, 0); deckNameRT.offsetMax = Vector2.zero;
        var dnTMP = deckNameRT.GetComponent<TMP_Text>();
        dnTMP.fontStyle = FontStyles.Bold;
        dnTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // 2. Units Section Header (Crossed Swords Heraldic Banner)
        var unitHeaderRT = MakeRectChild(content.gameObject, "UnitSectionHeader");
        unitHeaderRT.anchorMin = new Vector2(0.04f, 1); unitHeaderRT.anchorMax = new Vector2(0.96f, 1);
        unitHeaderRT.sizeDelta = new Vector2(0, 44); unitHeaderRT.anchoredPosition = new Vector2(0, -98);
        var uhImg = unitHeaderRT.gameObject.AddComponent<Image>();
        uhImg.sprite = CreateRoundedRectSprite();
        uhImg.type = Image.Type.Sliced;
        uhImg.color = new Color(0.06f, 0.18f, 0.38f, 0.95f);

        // Crossed swords icon badge
        var uIconRT = MakeRectChild(unitHeaderRT.gameObject, "UnitIconBadge", 34, 34);
        uIconRT.anchorMin = new Vector2(0, 0.5f); uIconRT.anchorMax = new Vector2(0, 0.5f);
        uIconRT.anchoredPosition = new Vector2(24, 0);
        var uiImg = uIconRT.gameObject.AddComponent<Image>();
        var swordSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Sword04.png");
        if (swordSpr != null) { uiImg.sprite = swordSpr; uiImg.preserveAspect = true; }

        var uhTxt = MakeTextChild(unitHeaderRT.gameObject, "Title", "BATTLE UNITS", 21, new Color(0.40f, 0.88f, 1f));
        uhTxt.anchorMin = new Vector2(0, 0); uhTxt.anchorMax = new Vector2(1, 1);
        uhTxt.offsetMin = new Vector2(50, 0); uhTxt.offsetMax = Vector2.zero;
        var uhTMP = uhTxt.GetComponent<TMP_Text>();
        uhTMP.alignment = TextAlignmentOptions.MidlineLeft;
        uhTMP.fontStyle = FontStyles.Bold;

        // Count Pill badge "(4)"
        var uCountPill = MakeRectChild(unitHeaderRT.gameObject, "CountPill", 54, 28);
        uCountPill.anchorMin = new Vector2(1, 0.5f); uCountPill.anchorMax = new Vector2(1, 0.5f);
        uCountPill.anchoredPosition = new Vector2(-36, 0);
        var ucpImg = uCountPill.gameObject.AddComponent<Image>();
        ucpImg.sprite = CreateRoundedRectSprite();
        ucpImg.type = Image.Type.Sliced;
        ucpImg.color = new Color(0.12f, 0.55f, 0.96f, 0.9f);
        var ucpTxt = MakeTextChild(uCountPill.gameObject, "Num", "4", 16, Color.white);
        StretchFull(ucpTxt);
        ucpTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        ucpTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // ─ 4 Units Grid: 1 row × 4 columns, 236×314 cell size ─
        var unitGridParent = MakeRectChild(content.gameObject, "UnitDeckGrid");
        unitGridParent.anchorMin = new Vector2(0, 1); unitGridParent.anchorMax = new Vector2(1, 1);
        unitGridParent.sizeDelta = new Vector2(0, 324);
        unitGridParent.anchoredPosition = new Vector2(0, -282);

        var uglg = unitGridParent.gameObject.AddComponent<GridLayoutGroup>();
        uglg.cellSize = new Vector2(236, 314);
        uglg.spacing = new Vector2(18, 16);
        uglg.padding = new RectOffset(20, 20, 4, 4);
        uglg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        uglg.constraintCount = 4;
        uglg.childAlignment = TextAnchor.MiddleCenter;

        // 3. Powerups Section Header (Magic Fireball Heraldic Banner)
        var powerHeaderRT = MakeRectChild(content.gameObject, "PowerupSectionHeader");
        powerHeaderRT.anchorMin = new Vector2(0.04f, 1); powerHeaderRT.anchorMax = new Vector2(0.96f, 1);
        powerHeaderRT.sizeDelta = new Vector2(0, 44); powerHeaderRT.anchoredPosition = new Vector2(0, -472);
        var phImg = powerHeaderRT.gameObject.AddComponent<Image>();
        phImg.sprite = CreateRoundedRectSprite();
        phImg.type = Image.Type.Sliced;
        phImg.color = new Color(0.24f, 0.10f, 0.44f, 0.95f); // Royal purple banner

        // Magic flame icon badge
        var pIconRT = MakeRectChild(powerHeaderRT.gameObject, "PowerIconBadge", 34, 34);
        pIconRT.anchorMin = new Vector2(0, 0.5f); pIconRT.anchorMax = new Vector2(0, 0.5f);
        pIconRT.anchoredPosition = new Vector2(24, 0);
        var piImg = pIconRT.gameObject.AddComponent<Image>();
        var fireSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Fire01.png");
        if (fireSpr != null) { piImg.sprite = fireSpr; piImg.preserveAspect = true; }

        var phTxt = MakeTextChild(powerHeaderRT.gameObject, "Title", "POWERUPS", 21, new Color(1f, 0.78f, 0.28f));
        phTxt.anchorMin = new Vector2(0, 0); phTxt.anchorMax = new Vector2(1, 1);
        phTxt.offsetMin = new Vector2(50, 0); phTxt.offsetMax = Vector2.zero;
        var phTMP = phTxt.GetComponent<TMP_Text>();
        phTMP.alignment = TextAlignmentOptions.MidlineLeft;
        phTMP.fontStyle = FontStyles.Bold;

        // Count Pill badge "(8)"
        var pCountPill = MakeRectChild(powerHeaderRT.gameObject, "CountPill", 54, 28);
        pCountPill.anchorMin = new Vector2(1, 0.5f); pCountPill.anchorMax = new Vector2(1, 0.5f);
        pCountPill.anchoredPosition = new Vector2(-36, 0);
        var pcpImg = pCountPill.gameObject.AddComponent<Image>();
        pcpImg.sprite = CreateRoundedRectSprite();
        pcpImg.type = Image.Type.Sliced;
        pcpImg.color = new Color(0.85f, 0.45f, 0.10f, 0.9f);
        var pcpTxt = MakeTextChild(pCountPill.gameObject, "Num", "8", 16, Color.white);
        StretchFull(pcpTxt);
        pcpTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        pcpTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // ─ 8 Powerups Grid: 2 rows × 4 columns, 236×314 cell size ─
        var powerGridParent = MakeRectChild(content.gameObject, "PowerupDeckGrid");
        powerGridParent.anchorMin = new Vector2(0, 1); powerGridParent.anchorMax = new Vector2(1, 1);
        powerGridParent.sizeDelta = new Vector2(0, 654);
        powerGridParent.anchoredPosition = new Vector2(0, -825);

        var pglg = powerGridParent.gameObject.AddComponent<GridLayoutGroup>();
        pglg.cellSize = new Vector2(236, 314);
        pglg.spacing = new Vector2(18, 16);
        pglg.padding = new RectOffset(20, 20, 4, 4);
        pglg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        pglg.constraintCount = 4;
        pglg.childAlignment = TextAnchor.MiddleCenter;

        var slots = new DeckSlotUI[12];
        // Build 4 unit slots (indices 0..3)
        for (int i = 0; i < 4; i++)
        {
            var slotRT = MakeUIGameObject($"DeckSlot_{i}", unitGridParent);
            var slot = BuildDeckSlotInline(slotRT.gameObject);
            slots[i] = slot;
        }
        // Build 8 powerup slots (indices 4..11)
        for (int i = 0; i < 8; i++)
        {
            var slotRT = MakeUIGameObject($"DeckSlot_{4 + i}", powerGridParent);
            var slot = BuildDeckSlotInline(slotRT.gameObject);
            slots[4 + i] = slot;
        }

        // 4. Elixir Average Row
        var elixirRow = MakeRectChild(content.gameObject, "ElixirRow");
        elixirRow.anchorMin = new Vector2(0, 1); elixirRow.anchorMax = new Vector2(1, 1);
        elixirRow.sizeDelta = new Vector2(0, 48);
        elixirRow.anchoredPosition = new Vector2(0, -1180);

        // Elixir pill container
        var elixirPillRT = MakeRectChild(elixirRow.gameObject, "ElixirPill", 146, 46);
        elixirPillRT.anchorMin = new Vector2(0, 0.5f); elixirPillRT.anchorMax = new Vector2(0, 0.5f);
        elixirPillRT.anchoredPosition = new Vector2(105, 0);
        var elixirPillBg = elixirPillRT.gameObject.AddComponent<Image>();
        elixirPillBg.sprite = CreateRoundedRectSprite();
        elixirPillBg.type = Image.Type.Sliced;
        elixirPillBg.color = new Color(0.06f, 0.14f, 0.28f, 0.95f);

        var elixirDotRT = MakeRectChild(elixirPillRT.gameObject, "ElixirDot", 34, 38);
        elixirDotRT.anchorMin = new Vector2(0, 0.5f); elixirDotRT.anchorMax = new Vector2(0, 0.5f);
        elixirDotRT.anchoredPosition = new Vector2(26, 0);
        var elixirDotImg = elixirDotRT.gameObject.AddComponent<Image>();
        var dropletSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ElixirDroplet.png");
        if (dropletSpr != null) elixirDotImg.sprite = dropletSpr;
        elixirDotImg.color = Color.white;
        elixirDotImg.preserveAspect = true;

        var elixirTxtRT = MakeTextChild(elixirPillRT.gameObject, "ElixirText", "2.8", 26, Color.white);
        elixirTxtRT.anchorMin = new Vector2(0, 0); elixirTxtRT.anchorMax = new Vector2(1, 1);
        elixirTxtRT.offsetMin = new Vector2(54, 0); elixirTxtRT.offsetMax = new Vector2(-10, 0);
        var elTMP = elixirTxtRT.GetComponent<TMP_Text>();
        elTMP.fontStyle = FontStyles.Bold;
        elTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // Subtle glowing divider line across width
        var divLine = MakeRectChild(elixirRow.gameObject, "DividerLine");
        divLine.anchorMin = new Vector2(0.03f, 0); divLine.anchorMax = new Vector2(0.97f, 0);
        divLine.sizeDelta = new Vector2(0, 2); divLine.anchoredPosition = Vector2.zero;
        divLine.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.35f, 0.65f, 0.70f);

        // 5. Pedestal area omitted - 12 cards fill portrait screen cleanly without clutter
        content.sizeDelta = new Vector2(0, 1260);

        return (panel, slots, elTMP, elixirDotImg, dnTMP,
                null, null, null, null, null);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Collection Panel (SubTabs + Filters + 4-column Scroll View)

    private static (RectTransform panel,
             ScrollRect scrollRect,
             RectTransform contentRoot,
             RectTransform foundGrid,
             RectTransform notFoundGrid,
             GameObject foundHeader,
             GameObject notFoundHeader,
             TMP_Text foundCountText,
             TMP_Text collectionTitleText)
        BuildCollectionPanel(GameObject parent)
    {
        var panel = MakeRectChild(parent, "CollectionPanel");
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero; panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.SetActive(false);

        // 5 Category Sub-Tabs Row matching Clash Royale Collection
        var subTabsRT = MakeRectChild(panel.gameObject, "SubTabBar");
        subTabsRT.anchorMin = new Vector2(0.04f, 1); subTabsRT.anchorMax = new Vector2(0.96f, 1);
        subTabsRT.sizeDelta = new Vector2(0, 52); subTabsRT.anchoredPosition = new Vector2(0, -32);

        var stHlg = subTabsRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        stHlg.spacing = 10;
        stHlg.childForceExpandWidth = true;
        stHlg.childForceExpandHeight = true;
        stHlg.childControlWidth = true;
        stHlg.childControlHeight = true;

        string[] subIcons = new string[] {
            "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/cards_navigation.png",
            "Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Star.png",
            "Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Chat.Png",
            "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/home_castle_navigation.png",
            "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/laurel_wreath.png"
        };

        for (int i = 0; i < 5; i++)
        {
            var subTabBtn = MakeUIGameObject($"SubTab_{i}", subTabsRT);
            var stImg = subTabBtn.gameObject.AddComponent<Image>();
            stImg.sprite = CreateRoundedRectSprite();
            stImg.type = Image.Type.Sliced;
            stImg.color = i == 0 ? new Color(0.14f, 0.48f, 0.92f, 0.95f) : new Color(0.06f, 0.12f, 0.24f, 0.8f);

            var iconRT = MakeRectChild(subTabBtn.gameObject, "Icon", 36, 36);
            var iconImg = iconRT.gameObject.AddComponent<Image>();
            var spr = LoadAsset<Sprite>(subIcons[i]);
            if (spr != null) { iconImg.sprite = spr; iconImg.preserveAspect = true; }
            if (i > 0) iconImg.color = new Color(0.6f, 0.72f, 0.88f);

            if (i == 0) // Red badge "2" on cards sub-tab
            {
                var sBadge = MakeRectChild(subTabBtn.gameObject, "Badge", 22, 22);
                sBadge.anchorMin = new Vector2(1, 1); sBadge.anchorMax = new Vector2(1, 1);
                sBadge.anchoredPosition = new Vector2(2, 2);
                var sbImg = sBadge.gameObject.AddComponent<Image>();
                sbImg.sprite = CreateCircleSprite();
                sbImg.color = new Color(1f, 0.2f, 0.35f);
                var sbTxt = MakeTextChild(sBadge.gameObject, "Txt", "2", 12, Color.white);
                StretchFull(sbTxt);
                sbTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
                sbTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            }
        }

        // Title & Filter row (Combined clean row matching Clash Royale)
        var titleRow = MakeRectChild(panel.gameObject, "CollectionTitleRow");
        titleRow.anchorMin = new Vector2(0.04f, 1); titleRow.anchorMax = new Vector2(0.96f, 1);
        titleRow.sizeDelta = new Vector2(0, 56); titleRow.anchoredPosition = new Vector2(0, -100);

        var titleTxt = MakeTextChild(titleRow.gameObject, "CollectionTitle", "Card Collection", 26, Color.white);
        titleTxt.anchorMin = new Vector2(0, 0.46f); titleTxt.anchorMax = new Vector2(0.55f, 1f);
        titleTxt.offsetMin = Vector2.zero; titleTxt.offsetMax = Vector2.zero;
        var tTMP = titleTxt.GetComponent<TMP_Text>();
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var foundCountTxt = MakeTextChild(titleRow.gameObject, "FoundCount", "Found: 8/36", 17, new Color(0.42f, 0.82f, 1f));
        foundCountTxt.anchorMin = new Vector2(0, 0f); foundCountTxt.anchorMax = new Vector2(0.55f, 0.46f);
        foundCountTxt.offsetMin = Vector2.zero; foundCountTxt.offsetMax = Vector2.zero;
        var fcTMP = foundCountTxt.GetComponent<TMP_Text>();
        fcTMP.alignment = TextAlignmentOptions.MidlineLeft;
        fcTMP.fontStyle = FontStyles.Bold;

        // Filter Controls on the Right side of Title Row
        var slidersSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_TuningSliders.png");
        var filterBar = MakeRectChild(titleRow.gameObject, "FilterBar");
        filterBar.anchorMin = new Vector2(0.56f, 0); filterBar.anchorMax = new Vector2(1f, 1);
        filterBar.offsetMin = Vector2.zero; filterBar.offsetMax = Vector2.zero;

        // Tuning sliders button (Green rounded button)
        var slidersBtnRT = MakeRectChild(filterBar.gameObject, "SlidersBtn", 44, 44);
        slidersBtnRT.anchorMin = new Vector2(0.06f, 0.5f); slidersBtnRT.anchorMax = new Vector2(0.06f, 0.5f);
        var sImg = slidersBtnRT.gameObject.AddComponent<Image>();
        sImg.sprite = CreateRoundedRectSprite();
        sImg.type = Image.Type.Sliced;
        sImg.color = new Color(0.18f, 0.72f, 0.35f);
        if (slidersSpr != null) {
            var sIcon = MakeRectChild(slidersBtnRT.gameObject, "Icon", 26, 26);
            var siImg = sIcon.gameObject.AddComponent<Image>();
            siImg.sprite = slidersSpr;
            siImg.preserveAspect = true;
        }

        // Up arrow button (Cyan rounded button)
        var upBtnRT = MakeRectChild(filterBar.gameObject, "UpArrowBtn", 44, 44);
        upBtnRT.anchorMin = new Vector2(0.32f, 0.5f); upBtnRT.anchorMax = new Vector2(0.32f, 0.5f);
        var uImg = upBtnRT.gameObject.AddComponent<Image>();
        uImg.sprite = CreateRoundedRectSprite();
        uImg.type = Image.Type.Sliced;
        uImg.color = new Color(0.12f, 0.75f, 0.85f);
        var upTxt = MakeTextChild(upBtnRT.gameObject, "Arrow", "▲", 18, Color.white);
        StretchFull(upTxt);
        upTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        // "By Level" button (Blue/navy rounded button)
        var sortBtnRT = MakeRectChild(filterBar.gameObject, "SortBtn", 130, 44);
        sortBtnRT.anchorMin = new Vector2(0.74f, 0.5f); sortBtnRT.anchorMax = new Vector2(0.74f, 0.5f);
        var sortImg = sortBtnRT.gameObject.AddComponent<Image>();
        sortImg.sprite = CreateRoundedRectSprite();
        sortImg.type = Image.Type.Sliced;
        sortImg.color = new Color(0.10f, 0.28f, 0.54f);
        var sortTxt = MakeTextChild(sortBtnRT.gameObject, "SortLabel", "By Level", 16, Color.white);
        StretchFull(sortTxt);
        var stTMP = sortTxt.GetComponent<TMP_Text>();
        stTMP.alignment = TextAlignmentOptions.Center;
        stTMP.fontStyle = FontStyles.Bold;

        // Scroll View with VerticalLayoutGroup and separate 4-column Grids for Found and Not Found
        var scrollRT = MakeUIGameObject("CollectionScrollView", panel);
        scrollRT.anchorMin = new Vector2(0, 0); scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0, -145);
        scrollRT.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        var scrollRect = scrollRT.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var vpRT = MakeUIGameObject("Viewport", scrollRT);
        StretchFull(vpRT);
        vpRT.gameObject.AddComponent<RectMask2D>();
        vpRT.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.005f);

        var contentRT = MakeUIGameObject("Content", vpRT);
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0, 2000);

        var vlg = contentRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(0, 0, 8, 30);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentRT.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ─ Found Section Header ─
        var foundHdrRT = MakeUIGameObject("FoundHeader", contentRT);
        foundHdrRT.sizeDelta = new Vector2(0, 38);
        var fHdrImg = foundHdrRT.gameObject.AddComponent<Image>();
        fHdrImg.color = new Color(0, 0, 0, 0);
        var fTxt = MakeTextChild(foundHdrRT.gameObject, "Text", "Found", 22, Color.white);
        StretchFull(fTxt);
        fTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        fTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // ─ Found Grid (4 Columns, 236×314) ─
        var foundGridRT = MakeUIGameObject("FoundGrid", contentRT);
        var fglg = foundGridRT.gameObject.AddComponent<GridLayoutGroup>();
        fglg.cellSize = new Vector2(236, 314);
        fglg.spacing = new Vector2(18, 16);
        fglg.padding = new RectOffset(20, 20, 6, 16);
        fglg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        fglg.constraintCount = 4;
        fglg.childAlignment = TextAnchor.UpperCenter;
        var fcsf = foundGridRT.gameObject.AddComponent<ContentSizeFitter>();
        fcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ─ Not Found Section Header ─
        var notFoundHdrRT = MakeUIGameObject("NotFoundHeader", contentRT);
        notFoundHdrRT.sizeDelta = new Vector2(0, 38);
        var nfHdrImg = notFoundHdrRT.gameObject.AddComponent<Image>();
        nfHdrImg.color = new Color(0, 0, 0, 0);
        var nfTxt = MakeTextChild(notFoundHdrRT.gameObject, "Text", "Not found", 22, new Color(0.70f, 0.78f, 0.90f));
        StretchFull(nfTxt);
        nfTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        nfTxt.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        // ─ Not Found Grid (4 Columns, 236×314) ─
        var notFoundGridRT = MakeUIGameObject("NotFoundGrid", contentRT);
        var nfglg = notFoundGridRT.gameObject.AddComponent<GridLayoutGroup>();
        nfglg.cellSize = new Vector2(236, 314);
        nfglg.spacing = new Vector2(18, 16);
        nfglg.padding = new RectOffset(20, 20, 6, 24);
        nfglg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        nfglg.constraintCount = 4;
        nfglg.childAlignment = TextAnchor.UpperCenter;
        var nfcsf = notFoundGridRT.gameObject.AddComponent<ContentSizeFitter>();
        nfcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRT;
        scrollRect.content = contentRT;

        return (panel, scrollRect, contentRT, foundGridRT, notFoundGridRT,
                foundHdrRT.gameObject, notFoundHdrRT.gameObject, fcTMP, tTMP);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Card Slot Construction (236×314)

    private static DeckSlotUI BuildDeckSlotInline(GameObject slotGO)
    {
        var slotRect = slotGO.GetComponent<RectTransform>() ?? slotGO.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(236, 314);

        // Load authentic Clash Royale sliced frames and elements
        var frameCommon   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Common.png");
        var frameRare     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        var frameEpic     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Epic.png");
        var frameLegend   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Legendary.png");
        var baseSpr       = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Base_Sliced.png");
        var levelBanner   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_LevelBanner_Sliced.png");
        var elixirDroplet = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ElixirDroplet.png");
        var sliderBgSpr   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Slider/Slider_Basic02_Demo_Bg.Png");
        var sliderFillSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Progress_Fill.png");
        var sliderIconSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Slider/Slider_Basic01_Icon_Arrow.png");
        var ribbonSpr     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Pink.png");

        // Outer 3D Rarity Frame Background
        var bg = slotGO.AddComponent<Image>();
        bg.sprite = frameCommon != null ? frameCommon : CreateRoundedRectSprite();
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        // Masked Card Art Window (Character fills upper ~78% with rounded corners)
        var maskRT = MakeUIGameObject("ArtMask", slotRect);
        maskRT.anchorMin = new Vector2(0.04f, 0.17f); maskRT.anchorMax = new Vector2(0.96f, 0.96f);
        maskRT.offsetMin = Vector2.zero; maskRT.offsetMax = Vector2.zero;
        var maskImg = maskRT.gameObject.AddComponent<Image>();
        maskImg.sprite = baseSpr != null ? baseSpr : CreateRoundedRectSprite();
        maskImg.type = Image.Type.Sliced;
        var maskComp = maskRT.gameObject.AddComponent<Mask>();
        maskComp.showMaskGraphic = true;

        var artRT = MakeUIGameObject("CardArt", maskRT);
        StretchFull(artRT);
        var artImg = artRT.gameObject.AddComponent<Image>();
        artImg.preserveAspect = true;

        // Level Badge (Bronze/Wood banner across bottom of card art)
        var levelBadgeRT = MakeUIGameObject("LevelBadge", slotRect);
        levelBadgeRT.anchorMin = new Vector2(0.04f, 0.12f); levelBadgeRT.anchorMax = new Vector2(0.96f, 0.25f);
        levelBadgeRT.offsetMin = Vector2.zero; levelBadgeRT.offsetMax = Vector2.zero;
        var levelBg = levelBadgeRT.gameObject.AddComponent<Image>();
        if (levelBanner != null) levelBg.sprite = levelBanner;
        else levelBg.color = new Color(0.45f, 0.22f, 0.08f, 1f);
        levelBg.type = Image.Type.Sliced;
        levelBg.color = Color.white;

        var levelTxt = MakeTextChild(levelBadgeRT.gameObject, "LevelText", "Level 1", 17, Color.white);
        StretchFull(levelTxt);
        var ltTMP = levelTxt.GetComponent<TMP_Text>();
        ltTMP.alignment = TextAlignmentOptions.Center;
        ltTMP.fontStyle = FontStyles.Bold;

        // Copies Progress Bar (attached directly under level banner)
        var copiesBarRT = MakeUIGameObject("CopiesBar", slotRect);
        copiesBarRT.anchorMin = new Vector2(0.04f, 0.02f); copiesBarRT.anchorMax = new Vector2(0.96f, 0.12f);
        copiesBarRT.offsetMin = Vector2.zero; copiesBarRT.offsetMax = Vector2.zero;
        var copiesBg = copiesBarRT.gameObject.AddComponent<Image>();
        if (sliderBgSpr != null) copiesBg.sprite = sliderBgSpr;
        else copiesBg.color = new Color(0.06f, 0.12f, 0.24f, 0.95f);
        copiesBg.type = Image.Type.Sliced;

        var copiesFillRT = MakeUIGameObject("Fill", copiesBarRT);
        StretchFull(copiesFillRT);
        var copiesFillImg = copiesFillRT.gameObject.AddComponent<Image>();
        if (sliderFillSpr != null) copiesFillImg.sprite = sliderFillSpr;
        else copiesFillImg.sprite = CreateRoundedRectSprite();
        copiesFillImg.type = Image.Type.Filled;
        copiesFillImg.fillMethod = Image.FillMethod.Horizontal;
        copiesFillImg.fillAmount = 0.5f;
        copiesFillImg.color = Color.white;

        var copiesTxt = MakeTextChild(copiesBarRT.gameObject, "CopiesText", "0/4", 13, Color.white);
        StretchFull(copiesTxt);
        var cpTMP = copiesTxt.GetComponent<TMP_Text>();
        cpTMP.alignment = TextAlignmentOptions.Center;
        cpTMP.fontStyle = FontStyles.Bold;

        // Up arrow icon on progress bar
        var arrowRT = MakeUIGameObject("UpArrow", copiesBarRT);
        arrowRT.anchorMin = new Vector2(0, 0.5f); arrowRT.anchorMax = new Vector2(0, 0.5f);
        arrowRT.sizeDelta = new Vector2(16, 16); arrowRT.anchoredPosition = new Vector2(12, 0);
        var arrowImg = arrowRT.gameObject.AddComponent<Image>();
        if (sliderIconSpr != null) arrowImg.sprite = sliderIconSpr;
        arrowImg.preserveAspect = true;

        // Elixir Cost Droplet Badge (TOP-LEFT CORNER OVERLAPPING CARD BORDER)
        var costBadgeRT = MakeUIGameObject("CostBadge", slotRect);
        costBadgeRT.anchorMin = new Vector2(0, 1); costBadgeRT.anchorMax = new Vector2(0, 1);
        costBadgeRT.sizeDelta = new Vector2(56, 64);
        costBadgeRT.anchoredPosition = new Vector2(16, -14); // Overlaps top-left border
        var costBadgeImg = costBadgeRT.gameObject.AddComponent<Image>();
        if (elixirDroplet != null) costBadgeImg.sprite = elixirDroplet;
        costBadgeImg.color = Color.white; // Preserve vibrant native magenta/purple droplet
        costBadgeImg.preserveAspect = true;

        var costTxt = MakeTextChild(costBadgeRT.gameObject, "CostText", "3", 24, Color.white);
        costTxt.anchorMin = new Vector2(0, 0); costTxt.anchorMax = new Vector2(1, 0.78f);
        costTxt.offsetMin = Vector2.zero; costTxt.offsetMax = Vector2.zero;
        var ctTMP = costTxt.GetComponent<TMP_Text>();
        ctTMP.alignment = TextAlignmentOptions.Center;
        ctTMP.fontStyle = FontStyles.Bold;

        // "New!" Ribbon (Top-Right)
        var newRT = MakeUIGameObject("NewBadge", slotRect);
        newRT.anchorMin = new Vector2(1, 1); newRT.anchorMax = new Vector2(1, 1);
        newRT.sizeDelta = new Vector2(64, 26); newRT.anchoredPosition = new Vector2(-32, -13);
        var newBg = newRT.gameObject.AddComponent<Image>();
        if (ribbonSpr != null) newBg.sprite = ribbonSpr;
        else newBg.color = new Color(1f, 0.20f, 0.45f);
        newBg.type = Image.Type.Sliced;
        var newTxt = MakeTextChild(newRT.gameObject, "NewText", "New!", 13, Color.white);
        StretchFull(newTxt);
        var ntTMP = newTxt.GetComponent<TMP_Text>();
        ntTMP.alignment = TextAlignmentOptions.Center;
        ntTMP.fontStyle = FontStyles.Bold;
        newRT.gameObject.SetActive(false);

        // Empty Slot Indicator (+)
        var emptyRT = MakeUIGameObject("EmptySlot", slotRect);
        StretchFull(emptyRT);
        var emptyBg = emptyRT.gameObject.AddComponent<Image>();
        emptyBg.color = new Color(0.06f, 0.14f, 0.28f, 0.6f);
        var plusTxt = MakeTextChild(emptyRT.gameObject, "PlusMark", "+", 52, new Color(0.35f, 0.65f, 0.95f));
        StretchFull(plusTxt);
        plusTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        // Selection Ring (For swap mode: pulsating golden glow)
        var ringRT = MakeUIGameObject("SelectionRing", slotRect);
        StretchFull(ringRT);
        ringRT.offsetMin = new Vector2(-6, -6); ringRT.offsetMax = new Vector2(6, 6);
        var ringImg = ringRT.gameObject.AddComponent<Image>();
        ringImg.color = new Color(1f, 0.85f, 0.10f, 0.95f);
        ringImg.sprite = CreateRoundedRectSprite();
        ringImg.type = Image.Type.Sliced;
        ringImg.fillCenter = false;
        ringRT.gameObject.SetActive(false);

        // Attach DeckSlotUI
        var slotComp = slotGO.AddComponent<DeckSlotUI>();
        var so = new SerializedObject(slotComp);
        AssignField(so, "slotRoot",          slotRect);
        AssignField(so, "cardArtImage",      artImg);
        AssignField(so, "slotBackground",    bg);
        AssignField(so, "frameCommon",       frameCommon);
        AssignField(so, "frameRare",         frameRare);
        AssignField(so, "frameEpic",         frameEpic);
        AssignField(so, "frameLegendary",    frameLegend);
        AssignField(so, "costBadgeRect",     costBadgeRT);
        AssignField(so, "costBadgeImage",    costBadgeImg);
        AssignField(so, "costText",          ctTMP);
        AssignField(so, "levelBadgeRect",    levelBadgeRT);
        AssignField(so, "levelText",         ltTMP);
        AssignField(so, "copiesBarRoot",     copiesBarRT);
        AssignField(so, "copiesBarFill",     copiesFillImg);
        AssignField(so, "copiesText",        cpTMP);
        AssignField(so, "newBadgeRect",      newRT);
        AssignField(so, "emptySlotIndicator",emptyRT.gameObject);
        AssignField(so, "selectionRing",     ringImg);
        so.ApplyModifiedProperties();

        return slotComp;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region CollectionCard & DeckSlot Prefab Building

    private static CollectionCardUI BuildCollectionCardPrefab(bool forceRebuild = true)
    {
        string path = "Assets/prefabs/CollectionCard.prefab";
        if (!forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var comp = existing.GetComponent<CollectionCardUI>();
                if (comp != null) return comp;
            }
        }

        var cardRT = MakeUIGameObject("CollectionCard");
        cardRT.sizeDelta = new Vector2(236, 314);

        var frameCommon   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Common.png");
        var frameRare     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        var frameEpic     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Epic.png");
        var frameLegend   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Legendary.png");
        var frameLocked   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Locked.png");
        var baseSpr       = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Base_Sliced.png");
        var levelBanner   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_LevelBanner_Sliced.png");
        var elixirDroplet = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ElixirDroplet.png");
        var sliderBgSpr   = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Slider/Slider_Basic02_Demo_Bg.Png");
        var sliderFillSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Progress_Fill.png");
        var sliderIconSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Slider/Slider_Basic01_Icon_Arrow.png");
        var ribbonSpr     = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Pink.png");

        var bg = cardRT.gameObject.AddComponent<Image>();
        bg.sprite = frameCommon != null ? frameCommon : CreateRoundedRectSprite();
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var maskRT = MakeUIGameObject("ArtMask", cardRT);
        maskRT.anchorMin = new Vector2(0.04f, 0.17f); maskRT.anchorMax = new Vector2(0.96f, 0.96f);
        maskRT.offsetMin = Vector2.zero; maskRT.offsetMax = Vector2.zero;
        var maskImg = maskRT.gameObject.AddComponent<Image>();
        maskImg.sprite = baseSpr != null ? baseSpr : CreateRoundedRectSprite();
        maskImg.type = Image.Type.Sliced;
        var maskComp = maskRT.gameObject.AddComponent<Mask>();
        maskComp.showMaskGraphic = true;

        var artRT = MakeUIGameObject("CardArt", maskRT);
        StretchFull(artRT);
        var artImg = artRT.gameObject.AddComponent<Image>();
        artImg.preserveAspect = true;

        var costBadgeRT = MakeUIGameObject("CostBadge", cardRT);
        costBadgeRT.anchorMin = new Vector2(0, 1); costBadgeRT.anchorMax = new Vector2(0, 1);
        costBadgeRT.sizeDelta = new Vector2(56, 64);
        costBadgeRT.anchoredPosition = new Vector2(16, -14); // Overlaps top-left border
        var costBadgeImg = costBadgeRT.gameObject.AddComponent<Image>();
        if (elixirDroplet != null) costBadgeImg.sprite = elixirDroplet;
        costBadgeImg.color = Color.white;
        costBadgeImg.preserveAspect = true;

        var costTxt = MakeTextChild(costBadgeRT.gameObject, "CostText", "3", 24, Color.white);
        costTxt.anchorMin = new Vector2(0, 0); costTxt.anchorMax = new Vector2(1, 0.78f);
        costTxt.offsetMin = Vector2.zero; costTxt.offsetMax = Vector2.zero;
        var ctTMP = costTxt.GetComponent<TMP_Text>();
        ctTMP.alignment = TextAlignmentOptions.Center;
        ctTMP.fontStyle = FontStyles.Bold;

        var levelBadgeRT = MakeUIGameObject("LevelBadge", cardRT);
        levelBadgeRT.anchorMin = new Vector2(0.04f, 0.12f); levelBadgeRT.anchorMax = new Vector2(0.96f, 0.25f);
        levelBadgeRT.offsetMin = Vector2.zero; levelBadgeRT.offsetMax = Vector2.zero;
        var levelBg = levelBadgeRT.gameObject.AddComponent<Image>();
        if (levelBanner != null) levelBg.sprite = levelBanner;
        levelBg.type = Image.Type.Sliced;
        levelBg.color = Color.white;

        var levelTxt = MakeTextChild(levelBadgeRT.gameObject, "LevelText", "Level 1", 17, Color.white);
        StretchFull(levelTxt);
        var ltTMP = levelTxt.GetComponent<TMP_Text>();
        ltTMP.alignment = TextAlignmentOptions.Center;
        ltTMP.fontStyle = FontStyles.Bold;

        var copiesBarRT = MakeUIGameObject("CopiesBar", cardRT);
        copiesBarRT.anchorMin = new Vector2(0.04f, 0.02f); copiesBarRT.anchorMax = new Vector2(0.96f, 0.12f);
        copiesBarRT.offsetMin = Vector2.zero; copiesBarRT.offsetMax = Vector2.zero;
        var copiesBg = copiesBarRT.gameObject.AddComponent<Image>();
        if (sliderBgSpr != null) copiesBg.sprite = sliderBgSpr;
        else copiesBg.color = new Color(0.06f, 0.12f, 0.24f, 0.95f);
        copiesBg.type = Image.Type.Sliced;

        var fillRT = MakeUIGameObject("Fill", copiesBarRT);
        StretchFull(fillRT);
        var fillImg = fillRT.gameObject.AddComponent<Image>();
        if (sliderFillSpr != null) fillImg.sprite = sliderFillSpr;
        else fillImg.sprite = CreateRoundedRectSprite();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0.5f;
        fillImg.color = Color.white;

        var copiesTxt = MakeTextChild(copiesBarRT.gameObject, "CopiesText", "0/4", 13, Color.white);
        StretchFull(copiesTxt);
        var cpTMP = copiesTxt.GetComponent<TMP_Text>();
        cpTMP.alignment = TextAlignmentOptions.Center;
        cpTMP.fontStyle = FontStyles.Bold;

        var arrowRT = MakeUIGameObject("UpArrow", copiesBarRT);
        arrowRT.anchorMin = new Vector2(0, 0.5f); arrowRT.anchorMax = new Vector2(0, 0.5f);
        arrowRT.sizeDelta = new Vector2(16, 16); arrowRT.anchoredPosition = new Vector2(12, 0);
        var arrowImg = arrowRT.gameObject.AddComponent<Image>();
        if (sliderIconSpr != null) arrowImg.sprite = sliderIconSpr;
        arrowImg.preserveAspect = true;

        var newBadgeRT = MakeUIGameObject("NewBadge", cardRT);
        newBadgeRT.anchorMin = new Vector2(1, 1); newBadgeRT.anchorMax = new Vector2(1, 1);
        newBadgeRT.sizeDelta = new Vector2(64, 26); newBadgeRT.anchoredPosition = new Vector2(-32, -13);
        var newBg = newBadgeRT.gameObject.AddComponent<Image>();
        if (ribbonSpr != null) newBg.sprite = ribbonSpr;
        else newBg.color = new Color(1f, 0.20f, 0.45f);
        newBg.type = Image.Type.Sliced;
        var newTxt = MakeTextChild(newBadgeRT.gameObject, "NewText", "New!", 13, Color.white);
        StretchFull(newTxt);
        var ntTMP = newTxt.GetComponent<TMP_Text>();
        ntTMP.alignment = TextAlignmentOptions.Center;
        ntTMP.fontStyle = FontStyles.Bold;
        newBadgeRT.gameObject.SetActive(false);

        // Greyscale overlay for locked cards
        var greyRT = MakeUIGameObject("GreyscaleOverlay", cardRT);
        StretchFull(greyRT);
        var greyImg = greyRT.gameObject.AddComponent<Image>();
        greyImg.color = new Color(0.04f, 0.06f, 0.12f, 0.78f);
        greyRT.gameObject.SetActive(false);

        // "Unlocks at Arena X" banner
        var lockedRT = MakeUIGameObject("LockedOverlay", cardRT);
        lockedRT.anchorMin = new Vector2(0.04f, 0.03f); lockedRT.anchorMax = new Vector2(0.96f, 0.27f);
        lockedRT.offsetMin = Vector2.zero; lockedRT.offsetMax = Vector2.zero;
        var lockedBg = lockedRT.gameObject.AddComponent<Image>();
        lockedBg.color = new Color(0.04f, 0.08f, 0.18f, 0.96f);
        lockedBg.sprite = CreateRoundedRectSprite();
        lockedBg.type = Image.Type.Sliced;
        var lockedTxt = MakeTextChild(lockedRT.gameObject, "LockedText", "<size=13><color=#B0BAC8>Unlocks at</color></size>\n<size=17><b><color=#FF4D8B>Arena 1</color></b></size>", 15, Color.white);
        StretchFull(lockedTxt);
        var lktTMP = lockedTxt.GetComponent<TMP_Text>();
        lktTMP.alignment = TextAlignmentOptions.Center;
        lktTMP.fontStyle = FontStyles.Bold;
        lockedRT.gameObject.SetActive(false);

        // Attach CollectionCardUI
        var cardComp = cardRT.gameObject.AddComponent<CollectionCardUI>();
        var so = new SerializedObject(cardComp);
        AssignField(so, "cardRoot",          cardRT);
        AssignField(so, "cardBackground",    bg);
        AssignField(so, "cardArtImage",      artImg);
        AssignField(so, "frameCommon",       frameCommon);
        AssignField(so, "frameRare",         frameRare);
        AssignField(so, "frameEpic",         frameEpic);
        AssignField(so, "frameLegendary",    frameLegend);
        AssignField(so, "frameLocked",       frameLocked);
        AssignField(so, "greyscaleOverlay",  greyImg);
        AssignField(so, "costBadgeRect",     costBadgeRT);
        AssignField(so, "costBadgeImage",    costBadgeImg);
        AssignField(so, "costText",          ctTMP);
        AssignField(so, "levelBadgeRect",    levelBadgeRT);
        AssignField(so, "levelBadgeBackground", levelBg);
        AssignField(so, "levelText",         ltTMP);
        AssignField(so, "copiesBarRoot",     copiesBarRT);
        AssignField(so, "copiesBarFill",     fillImg);
        AssignField(so, "copiesText",        cpTMP);
        AssignField(so, "upgradeArrowIcon",  arrowImg);
        AssignField(so, "newBadgeRect",      newBadgeRT);
        AssignField(so, "lockedOverlayRect", lockedRT);
        AssignField(so, "lockedArenaText",   lktTMP);
        so.ApplyModifiedProperties();

        var prefab = PrefabUtility.SaveAsPrefabAsset(cardRT.gameObject, path);
        Object.DestroyImmediate(cardRT.gameObject);
        return prefab.GetComponent<CollectionCardUI>();
    }

    private static DeckSlotUI BuildDeckSlotPrefab(bool forceRebuild = true)
    {
        string path = "Assets/prefabs/DeckSlot_Prefab.prefab";
        if (!forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var comp = existing.GetComponent<DeckSlotUI>();
                if (comp != null) return comp;
            }
        }

        var slotRT = MakeUIGameObject("DeckSlot_Prefab");
        var slotComp = BuildDeckSlotInline(slotRT.gameObject);
        var prefab = PrefabUtility.SaveAsPrefabAsset(slotRT.gameObject, path);
        Object.DestroyImmediate(slotRT.gameObject);
        return prefab.GetComponent<DeckSlotUI>();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Card Detail Popup (Juicy Modal)

    private static (RectTransform popup,
             CanvasGroup cg,
             Image art,
             TMP_Text name,
             TMP_Text level,
             TMP_Text copies,
             TMP_Text desc,
             Button addBtn,
             Button removeBtn,
             Button closeBtn,
             TMP_Text addBtnLabel,
             Image dimmer,
             Button upgradeBtn,
             TMP_Text upgradeLabel,
             TMP_Text upgradeCostText,
             Image upgradeCoinIcon)
        BuildCardDetailPopup(GameObject parent)
    {
        // Full screen dimmer
        var dimmerRT = MakeUIGameObject("CardDetailDimmer", parent.transform);
        StretchFull(dimmerRT);
        var dimmerImg = dimmerRT.gameObject.AddComponent<Image>();
        dimmerImg.color = new Color(0.01f, 0.04f, 0.10f, 0.85f);
        dimmerRT.gameObject.SetActive(false);

        // Popup Container
        var popupRT = MakeRectChild(parent, "CardDetailPopup", 720, 960);
        popupRT.anchorMin = new Vector2(0.5f, 0.5f); popupRT.anchorMax = new Vector2(0.5f, 0.5f);
        popupRT.anchoredPosition = Vector2.zero;
        var popupBg = popupRT.gameObject.AddComponent<Image>();
        popupBg.sprite = CreateRoundedRectSprite();
        popupBg.type = Image.Type.Sliced;
        popupBg.color = new Color(0.06f, 0.15f, 0.32f, 0.98f);
        var cg = popupRT.gameObject.AddComponent<CanvasGroup>();
        popupRT.gameObject.SetActive(false);

        // Close button (X)
        var closeBtnRT = MakeRectChild(popupRT.gameObject, "CloseBtn", 52, 52);
        closeBtnRT.anchorMin = new Vector2(1, 1); closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.anchoredPosition = new Vector2(-30, -30);
        var closeBtnImg = closeBtnRT.gameObject.AddComponent<Image>();
        closeBtnImg.sprite = CreateCircleSprite();
        closeBtnImg.color = new Color(0.85f, 0.20f, 0.22f, 1f);
        var closeBtn = closeBtnRT.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;
        var closeTxt = MakeTextChild(closeBtnRT.gameObject, "X", "X", 26, Color.white);
        StretchFull(closeTxt);
        closeTxt.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        // Card Preview in Popup (270x360 with authentic 3D Frame)
        var cardPreviewRT = MakeRectChild(popupRT.gameObject, "CardPreview", 270, 360);
        cardPreviewRT.anchoredPosition = new Vector2(0, 210);
        var cpFrame = cardPreviewRT.gameObject.AddComponent<Image>();
        var frameRareSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        cpFrame.sprite = frameRareSpr != null ? frameRareSpr : CreateRoundedRectSprite();
        cpFrame.type = Image.Type.Sliced;

        var maskRT = MakeRectChild(cardPreviewRT.gameObject, "Mask");
        maskRT.anchorMin = new Vector2(0.04f, 0.17f); maskRT.anchorMax = new Vector2(0.96f, 0.96f);
        maskRT.offsetMin = Vector2.zero; maskRT.offsetMax = Vector2.zero;
        var mask = maskRT.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        var mImg = maskRT.gameObject.AddComponent<Image>();
        var baseSlicedSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Base_Sliced.png");
        mImg.sprite = baseSlicedSpr != null ? baseSlicedSpr : CreateRoundedRectSprite();
        mImg.type = Image.Type.Sliced;

        var artRT = MakeRectChild(maskRT.gameObject, "DetailArt");
        StretchFull(artRT);
        var artImg = artRT.gameObject.AddComponent<Image>();
        artImg.preserveAspect = true;

        // Name
        var nameTxt = MakeTextChild(popupRT.gameObject, "DetailName", "Card Name", 32, Color.white);
        nameTxt.anchoredPosition = new Vector2(0, -25);
        nameTxt.sizeDelta = new Vector2(640, 44);
        var nTMP = nameTxt.GetComponent<TMP_Text>();
        nTMP.alignment = TextAlignmentOptions.Center;
        nTMP.fontStyle = FontStyles.Bold;

        // Level
        var lvlTxt = MakeTextChild(popupRT.gameObject, "DetailLevel", "Level 1", 22, new Color(0.40f, 0.85f, 1f));
        lvlTxt.anchoredPosition = new Vector2(0, -70);
        lvlTxt.sizeDelta = new Vector2(640, 32);
        var lTMP = lvlTxt.GetComponent<TMP_Text>();
        lTMP.alignment = TextAlignmentOptions.Center;
        lTMP.fontStyle = FontStyles.Bold;

        // Copies Progress
        var copiesTxt = MakeTextChild(popupRT.gameObject, "DetailCopies", "0/4 to upgrade", 18, new Color(0.70f, 0.82f, 0.95f));
        copiesTxt.anchoredPosition = new Vector2(0, -108);
        copiesTxt.sizeDelta = new Vector2(640, 28);
        var cpTMP = copiesTxt.GetComponent<TMP_Text>();
        cpTMP.alignment = TextAlignmentOptions.Center;

        // Description
        var descTxt = MakeTextChild(popupRT.gameObject, "DetailDesc", "Deploys a mighty warrior onto the battlefield.", 18, new Color(0.78f, 0.88f, 0.98f));
        descTxt.anchoredPosition = new Vector2(0, -170);
        descTxt.sizeDelta = new Vector2(600, 80);
        var dTMP = descTxt.GetComponent<TMP_Text>();
        dTMP.alignment = TextAlignmentOptions.Center;

        // Action Buttons Row (Add to Deck / Use / Upgrade / Remove)
        // 1. USE / SWAP Button (Left side, Royal Blue)
        var addBtnRT = MakeRectChild(popupRT.gameObject, "AddToDeckBtn", 280, 72);
        addBtnRT.anchoredPosition = new Vector2(-155, -300);
        var addBtnImg = addBtnRT.gameObject.AddComponent<Image>();
        addBtnImg.sprite = CreateRoundedRectSprite();
        addBtnImg.type = Image.Type.Sliced;
        addBtnImg.color = new Color(0.12f, 0.52f, 0.95f); // 3D Royal Blue Use/Swap button
        var addBtn = addBtnRT.gameObject.AddComponent<Button>();
        addBtn.targetGraphic = addBtnImg;

        // 3D bottom bevel shadow on button
        var btnShadow = MakeRectChild(addBtnRT.gameObject, "BShadow");
        btnShadow.anchorMin = new Vector2(0.04f, 0); btnShadow.anchorMax = new Vector2(0.96f, 0.12f);
        btnShadow.offsetMin = Vector2.zero; btnShadow.offsetMax = Vector2.zero;
        var bsImg = btnShadow.gameObject.AddComponent<Image>();
        bsImg.color = new Color(0.06f, 0.28f, 0.58f, 0.95f);
        bsImg.sprite = CreateRoundedRectSprite();
        bsImg.type = Image.Type.Sliced;
        bsImg.raycastTarget = false;

        var addLbl = MakeTextChild(addBtnRT.gameObject, "Label", "USE", 26, Color.white);
        StretchFull(addLbl);
        var alTMP = addLbl.GetComponent<TMP_Text>();
        alTMP.alignment = TextAlignmentOptions.Center;
        alTMP.fontStyle = FontStyles.Bold;

        // 2. UPGRADE Button (Right side, Vibrant Emerald Green with Gold Coin & Cost)
        var upgBtnRT = MakeRectChild(popupRT.gameObject, "UpgradeBtn", 280, 72);
        upgBtnRT.anchoredPosition = new Vector2(155, -300);
        var upgBtnImg = upgBtnRT.gameObject.AddComponent<Image>();
        upgBtnImg.sprite = CreateRoundedRectSprite();
        upgBtnImg.type = Image.Type.Sliced;
        upgBtnImg.color = new Color(0.18f, 0.82f, 0.35f); // 3D Emerald Green Upgrade button
        var upgBtn = upgBtnRT.gameObject.AddComponent<Button>();
        upgBtn.targetGraphic = upgBtnImg;

        var upgShadow = MakeRectChild(upgBtnRT.gameObject, "BShadow");
        upgShadow.anchorMin = new Vector2(0.04f, 0); upgShadow.anchorMax = new Vector2(0.96f, 0.12f);
        upgShadow.offsetMin = Vector2.zero; upgShadow.offsetMax = Vector2.zero;
        var usImg = upgShadow.gameObject.AddComponent<Image>();
        usImg.color = new Color(0.08f, 0.45f, 0.18f, 0.95f);
        usImg.sprite = CreateRoundedRectSprite();
        usImg.type = Image.Type.Sliced;
        usImg.raycastTarget = false;

        var upgLblRT = MakeTextChild(upgBtnRT.gameObject, "Label", "UPGRADE", 18, Color.white);
        upgLblRT.anchorMin = new Vector2(0, 0.45f); upgLblRT.anchorMax = new Vector2(1, 0.95f);
        upgLblRT.offsetMin = Vector2.zero; upgLblRT.offsetMax = Vector2.zero;
        var ulTMP = upgLblRT.GetComponent<TMP_Text>();
        ulTMP.alignment = TextAlignmentOptions.Center;
        ulTMP.fontStyle = FontStyles.Bold;

        var costRow = MakeRectChild(upgBtnRT.gameObject, "CostRow", 160, 30);
        costRow.anchorMin = new Vector2(0.5f, 0.08f); costRow.anchorMax = new Vector2(0.5f, 0.48f);
        costRow.anchoredPosition = Vector2.zero;

        var coinImgRT = MakeRectChild(costRow.gameObject, "CoinIcon", 26, 26);
        coinImgRT.anchorMin = new Vector2(0, 0.5f); coinImgRT.anchorMax = new Vector2(0, 0.5f);
        coinImgRT.anchoredPosition = new Vector2(18, 0);
        var cImg = coinImgRT.gameObject.AddComponent<Image>();
        var coinSpr = LoadAsset<Sprite>("Assets/UIPACK/Chest_UI_Pack/Icons/Coin.png");
        if (coinSpr == null) coinSpr = LoadAsset<Sprite>("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/coin.png");
        if (coinSpr != null) { cImg.sprite = coinSpr; cImg.preserveAspect = true; }

        var costTxtRT = MakeTextChild(costRow.gameObject, "CostText", "50", 22, Color.white);
        costTxtRT.anchorMin = new Vector2(0, 0); costTxtRT.anchorMax = new Vector2(1, 1);
        costTxtRT.offsetMin = new Vector2(48, 0); costTxtRT.offsetMax = Vector2.zero;
        var ucTMP = costTxtRT.GetComponent<TMP_Text>();
        ucTMP.alignment = TextAlignmentOptions.MidlineLeft;
        ucTMP.fontStyle = FontStyles.Bold;

        // 3. REMOVE Button (Small subtle pill at y = -385)
        var remBtnRT = MakeRectChild(popupRT.gameObject, "RemoveBtn", 200, 48);
        remBtnRT.anchoredPosition = new Vector2(0, -385);
        var remBtnImg = remBtnRT.gameObject.AddComponent<Image>();
        remBtnImg.sprite = CreateRoundedRectSprite();
        remBtnImg.type = Image.Type.Sliced;
        remBtnImg.color = new Color(0.85f, 0.22f, 0.22f, 0.90f);
        var remBtn = remBtnRT.gameObject.AddComponent<Button>();
        remBtn.targetGraphic = remBtnImg;
        remBtnRT.gameObject.SetActive(false);

        var remLbl = MakeTextChild(remBtnRT.gameObject, "Label", "REMOVE", 18, Color.white);
        StretchFull(remLbl);
        var rlTMP = remLbl.GetComponent<TMP_Text>();
        rlTMP.alignment = TextAlignmentOptions.Center;
        rlTMP.fontStyle = FontStyles.Bold;

        return (popupRT, cg, artImg, nTMP, lTMP, cpTMP, dTMP,
                addBtn, remBtn, closeBtn, alTMP, dimmerImg,
                upgBtn, ulTMP, ucTMP, cImg);
    }

    private static CardUpgradeCelebrationOverlay BuildUpgradeCelebrationOverlay(GameObject parent)
    {
        // 1. Overlay Root (Fullscreen on top of popup and HUD)
        var overlayRT = MakeUIGameObject("UpgradeCelebrationOverlay", parent.transform);
        StretchFull(overlayRT);
        overlayRT.transform.SetAsLastSibling();
        var cg = overlayRT.gameObject.AddComponent<CanvasGroup>();
        var overlayComp = overlayRT.gameObject.AddComponent<CardUpgradeCelebrationOverlay>();
        var aSrc = overlayRT.gameObject.AddComponent<AudioSource>();
        aSrc.playOnAwake = false;

        // 2. Fullscreen Vignette / Dimmer (Screen_Dim)
        var dimmerRT = MakeUIGameObject("BackdropDimmer", overlayRT);
        StretchFull(dimmerRT);
        var dimmerImg = dimmerRT.gameObject.AddComponent<Image>();
        var screenDimSpr = LoadAsset<Sprite>("Assets/UIPACK/Chest_UI_Pack/Overlays/Screen_Dim.png");
        if (screenDimSpr != null) dimmerImg.sprite = screenDimSpr;
        dimmerImg.color = new Color(0.01f, 0.04f, 0.12f, 0.96f);

        // 3. Rays Container + Chest_Gold_Glow (Sunburst)
        var raysRT = MakeRectChild(overlayRT.gameObject, "RaysContainer", 960, 960);
        raysRT.anchoredPosition = new Vector2(0f, 75f);
        var raysImg = raysRT.gameObject.AddComponent<Image>();
        var chestGlowSpr = LoadAsset<Sprite>("Assets/UIPACK/Chest_UI_Pack/Overlays/Chest_Gold_Glow.png");
        if (chestGlowSpr != null) raysImg.sprite = chestGlowSpr;
        else raysImg.sprite = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Image_Light.png");
        raysImg.color = new Color(1f, 0.82f, 0.28f, 0.85f);
        raysImg.raycastTarget = false;

        // 4. Radial Soft Glow
        var glowRT = MakeRectChild(overlayRT.gameObject, "RadialGlow", 720, 720);
        glowRT.anchoredPosition = new Vector2(0f, 75f);
        var glowImg = glowRT.gameObject.AddComponent<Image>();
        var glowSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png");
        if (glowSpr != null) glowImg.sprite = glowSpr;
        glowImg.color = new Color(1f, 0.72f, 0.18f, 0.50f);
        glowImg.raycastTarget = false;

        // 5. Confetti / Particles Container
        var partRT = MakeRectChild(overlayRT.gameObject, "ParticlesContainer", 0, 0);
        partRT.anchoredPosition = new Vector2(0f, 75f);

        // 6. Card Hero Container (Grand Size: 360x480)
        var heroRT = MakeRectChild(overlayRT.gameObject, "CardHero", 360, 480);
        heroRT.anchoredPosition = new Vector2(0f, 75f);
        var heroFrameImg = heroRT.gameObject.AddComponent<Image>();
        var frameRareSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        heroFrameImg.sprite = frameRareSpr != null ? frameRareSpr : CreateRoundedRectSprite();
        heroFrameImg.type = Image.Type.Sliced;

        // Mask & Base
        var maskRT = MakeRectChild(heroRT.gameObject, "Mask");
        maskRT.anchorMin = new Vector2(0.04f, 0.14f); maskRT.anchorMax = new Vector2(0.96f, 0.96f);
        maskRT.offsetMin = Vector2.zero; maskRT.offsetMax = Vector2.zero;
        var mask = maskRT.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        var mImg = maskRT.gameObject.AddComponent<Image>();
        var baseSlicedSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Base_Sliced.png");
        mImg.sprite = baseSlicedSpr != null ? baseSlicedSpr : CreateRoundedRectSprite();
        mImg.type = Image.Type.Sliced;

        // Card Art
        var artRT = MakeRectChild(maskRT.gameObject, "CardArt");
        StretchFull(artRT);
        var artImg = artRT.gameObject.AddComponent<Image>();
        artImg.preserveAspect = true;

        // Energy Flash Overlay (starts alpha = 0)
        var flashRT = MakeRectChild(maskRT.gameObject, "FlashOverlay");
        StretchFull(flashRT);
        var flashImg = flashRT.gameObject.AddComponent<Image>();
        flashImg.color = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;

        // Elixir Cost badge in top-left
        var costBadgeRT = MakeRectChild(heroRT.gameObject, "CostBadge", 60, 60);
        costBadgeRT.anchorMin = new Vector2(0f, 1f); costBadgeRT.anchorMax = new Vector2(0f, 1f);
        costBadgeRT.anchoredPosition = new Vector2(32f, -32f);
        var costImg = costBadgeRT.gameObject.AddComponent<Image>();
        var dropletSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ElixirDroplet.png");
        if (dropletSpr != null) costImg.sprite = dropletSpr;
        costImg.preserveAspect = true;

        var costTxtRT = MakeTextChild(costBadgeRT.gameObject, "CostText", "3", 28, Color.white);
        StretchFull(costTxtRT);
        var costTMP = costTxtRT.GetComponent<TMP_Text>();
        costTMP.alignment = TextAlignmentOptions.Center;
        costTMP.fontStyle = FontStyles.Bold;

        // Card Level Ribbon at bottom of card
        var cardLvlGO = MakeRectChild(heroRT.gameObject, "CardLevelBanner");
        cardLvlGO.anchorMin = new Vector2(0.04f, 0.02f); cardLvlGO.anchorMax = new Vector2(0.96f, 0.14f);
        cardLvlGO.offsetMin = Vector2.zero; cardLvlGO.offsetMax = Vector2.zero;
        var clImg = cardLvlGO.gameObject.AddComponent<Image>();
        var lvlBannerSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_LevelBanner_Sliced.png");
        if (lvlBannerSpr != null) clImg.sprite = lvlBannerSpr;
        clImg.type = Image.Type.Sliced;
        clImg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);

        var cardLvlTxt = MakeTextChild(cardLvlGO.gameObject, "Text", "Level 2", 22, Color.white);
        StretchFull(cardLvlTxt);
        var clTMP = cardLvlTxt.GetComponent<TMP_Text>();
        clTMP.alignment = TextAlignmentOptions.Center;
        clTMP.fontStyle = FontStyles.Bold;

        // 7. Crown (Perched on top of card)
        var crownRT = MakeRectChild(overlayRT.gameObject, "Crown", 110, 110);
        crownRT.anchoredPosition = new Vector2(0f, 345f);
        var crownImg = crownRT.gameObject.AddComponent<Image>();
        var crownSpr = LoadAsset<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crown.png");
        if (crownSpr == null) crownSpr = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Image_Crown.png");
        if (crownSpr != null) crownImg.sprite = crownSpr;
        crownImg.preserveAspect = true;

        // 8. Grand Ribbon Announcement Banner
        var bannerRT = MakeRectChild(overlayRT.gameObject, "BannerAnnouncement", 520, 96);
        bannerRT.anchoredPosition = new Vector2(0f, 440f);
        var bannerImg = bannerRT.gameObject.AddComponent<Image>();
        var ribbonSpr = LoadAsset<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/blue_name_ribbon.png");
        if (ribbonSpr != null) bannerImg.sprite = ribbonSpr;
        bannerImg.preserveAspect = true;

        var titleTxtRT = MakeTextChild(bannerRT.gameObject, "Title", "LEVEL UP!", 38, new Color(1f, 0.90f, 0.20f));
        StretchFull(titleTxtRT);
        var titleTMP = titleTxtRT.GetComponent<TMP_Text>();
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.fontStyle = FontStyles.Bold;

        // 9. Level Transition Row
        var lvlRowRT = MakeRectChild(overlayRT.gameObject, "LevelTransitionRow", 480, 60);
        lvlRowRT.anchoredPosition = new Vector2(0f, -205f);

        var lvlTransRT = MakeTextChild(lvlRowRT.gameObject, "Text", "Level 1  ➜  Level 2", 32, Color.white);
        StretchFull(lvlTransRT);
        var lvlTMP = lvlTransRT.GetComponent<TMP_Text>();
        lvlTMP.alignment = TextAlignmentOptions.Center;
        lvlTMP.fontStyle = FontStyles.Bold;

        // 10. Stat Boost Comparison Card (Clash Royale Style: HP & Damage)
        var statCardRT = MakeRectChild(overlayRT.gameObject, "StatShowcaseCard", 500, 92);
        statCardRT.anchoredPosition = new Vector2(0f, -288f);
        var scImg = statCardRT.gameObject.AddComponent<Image>();
        scImg.sprite = CreateRoundedRectSprite();
        scImg.type = Image.Type.Sliced;
        scImg.color = new Color(0.05f, 0.14f, 0.28f, 0.96f);

        // Left Stat (Hitpoints)
        var hpRT = MakeRectChild(statCardRT.gameObject, "StatHP");
        hpRT.anchorMin = new Vector2(0.04f, 0.08f); hpRT.anchorMax = new Vector2(0.48f, 0.92f);
        hpRT.offsetMin = Vector2.zero; hpRT.offsetMax = Vector2.zero;

        var hpLbl = MakeTextChild(hpRT.gameObject, "Label", "<color=#6EDAFF>HITPOINTS</color>", 18, Color.white);
        hpLbl.anchorMin = new Vector2(0f, 0.50f); hpLbl.anchorMax = new Vector2(1f, 1f);
        hpLbl.offsetMin = Vector2.zero; hpLbl.offsetMax = Vector2.zero;
        var hlTMP = hpLbl.GetComponent<TMP_Text>();
        hlTMP.alignment = TextAlignmentOptions.Center;
        hlTMP.fontStyle = FontStyles.Bold;

        var hpVal = MakeTextChild(hpRT.gameObject, "Value", "<color=#2ECC71><b>+10%</b> ▲</color>", 24, Color.white);
        hpVal.anchorMin = new Vector2(0f, 0f); hpVal.anchorMax = new Vector2(1f, 0.50f);
        hpVal.offsetMin = Vector2.zero; hpVal.offsetMax = Vector2.zero;
        var hvTMP = hpVal.GetComponent<TMP_Text>();
        hvTMP.alignment = TextAlignmentOptions.Center;
        hvTMP.fontStyle = FontStyles.Bold;

        // Divider Line
        var divRT = MakeRectChild(statCardRT.gameObject, "Divider", 2, 60);
        divRT.anchorMin = new Vector2(0.50f, 0.50f); divRT.anchorMax = new Vector2(0.50f, 0.50f);
        divRT.anchoredPosition = Vector2.zero;
        var divImg = divRT.gameObject.AddComponent<Image>();
        divImg.color = new Color(0.20f, 0.35f, 0.55f, 0.6f);

        // Right Stat (Damage)
        var dmgRT = MakeRectChild(statCardRT.gameObject, "StatDamage");
        dmgRT.anchorMin = new Vector2(0.52f, 0.08f); dmgRT.anchorMax = new Vector2(0.96f, 0.92f);
        dmgRT.offsetMin = Vector2.zero; dmgRT.offsetMax = Vector2.zero;

        var dmgLbl = MakeTextChild(dmgRT.gameObject, "Label", "<color=#FF7675>DAMAGE</color>", 18, Color.white);
        dmgLbl.anchorMin = new Vector2(0f, 0.50f); dmgLbl.anchorMax = new Vector2(1f, 1f);
        dmgLbl.offsetMin = Vector2.zero; dmgLbl.offsetMax = Vector2.zero;
        var dlTMP = dmgLbl.GetComponent<TMP_Text>();
        dlTMP.alignment = TextAlignmentOptions.Center;
        dlTMP.fontStyle = FontStyles.Bold;

        var dmgVal = MakeTextChild(dmgRT.gameObject, "Value", "<color=#2ECC71><b>+10%</b> ▲</color>", 24, Color.white);
        dmgVal.anchorMin = new Vector2(0f, 0f); dmgVal.anchorMax = new Vector2(1f, 0.50f);
        dmgVal.offsetMin = Vector2.zero; dmgVal.offsetMax = Vector2.zero;
        var dvTMP = dmgVal.GetComponent<TMP_Text>();
        dvTMP.alignment = TextAlignmentOptions.Center;
        dvTMP.fontStyle = FontStyles.Bold;

        // 11. 3D Continue Button (Green_Empty)
        var continueBtnRT = MakeRectChild(overlayRT.gameObject, "ContinueButton", 340, 84);
        continueBtnRT.anchoredPosition = new Vector2(0f, -395f);
        var continueBtnImg = continueBtnRT.gameObject.AddComponent<Image>();
        var greenBtnSpr = LoadAsset<Sprite>("Assets/UIPACK/Chest_UI_Pack/Buttons/Green_Empty.png");
        if (greenBtnSpr != null) continueBtnImg.sprite = greenBtnSpr;
        else continueBtnImg.sprite = CreateRoundedRectSprite();
        continueBtnImg.type = Image.Type.Sliced;
        var continueBtn = continueBtnRT.gameObject.AddComponent<Button>();
        continueBtn.targetGraphic = continueBtnImg;

        var continueLbl = MakeTextChild(continueBtnRT.gameObject, "Label", "CONTINUE", 28, Color.white);
        StretchFull(continueLbl);
        var contTMP = continueLbl.GetComponent<TMP_Text>();
        contTMP.alignment = TextAlignmentOptions.Center;
        contTMP.fontStyle = FontStyles.Bold;

        // Wire SerializedObject on CardUpgradeCelebrationOverlay
        var so = new SerializedObject(overlayComp);
        AssignField(so, "overlayRoot",            overlayRT);
        AssignField(so, "overlayCanvasGroup",     cg);
        AssignField(so, "backdropDimmer",         dimmerImg);
        AssignField(so, "raysContainer",          raysRT);
        AssignField(so, "raysImage",              raysImg);
        AssignField(so, "radialGlowImage",        glowImg);
        AssignField(so, "cardHeroContainer",      heroRT);
        AssignField(so, "cardHeroFrame",          heroFrameImg);
        AssignField(so, "cardHeroBase",           mImg);
        AssignField(so, "cardHeroArt",            artImg);
        AssignField(so, "cardHeroFlash",          flashImg);
        AssignField(so, "costDroplet",            costImg);
        AssignField(so, "costText",               costTMP);
        AssignField(so, "cardLevelBanner",        clImg);
        AssignField(so, "cardLevelText",          clTMP);
        AssignField(so, "crownRect",              crownRT);
        AssignField(so, "crownImage",             crownImg);
        AssignField(so, "bannerRect",             bannerRT);
        AssignField(so, "bannerImage",            bannerImg);
        AssignField(so, "bannerTitleText",        titleTMP);
        AssignField(so, "levelTransitionRow",     lvlRowRT);
        AssignField(so, "levelTransitionText",    lvlTMP);
        AssignField(so, "statShowcaseCard",       statCardRT);
        AssignField(so, "statHpLabel",            hlTMP);
        AssignField(so, "statHpValue",            hvTMP);
        AssignField(so, "statDmgLabel",           dlTMP);
        AssignField(so, "statDmgValue",           dvTMP);
        AssignField(so, "particlesContainer",     partRT);
        AssignField(so, "continueButton",         continueBtn);
        AssignField(so, "continueButtonImage",    continueBtnImg);
        AssignField(so, "continueButtonLabel",    contTMP);
        AssignField(so, "audioSource",            aSrc);

        var sparkleSpr = LoadAsset<Sprite>("Assets/UIPACK/Chest_UI_Pack/Effects/Sparkle.png");
        if (sparkleSpr != null) AssignField(so, "sparkleParticleSprite", sparkleSpr);

        // Load SFX
        var whoosh = LoadAsset<AudioClip>("Assets/Resources/Sounds/arrowrelease.mp3");
        var impact = LoadAsset<AudioClip>("Assets/Resources/Sounds/dragon-studio-hammer-smash-effect-382731.mp3");
        var chime  = LoadAsset<AudioClip>("Assets/Resources/Sounds/arrowhit.mp3");
        if (whoosh != null) AssignField(so, "whooshSound", whoosh);
        if (impact != null) AssignField(so, "impactSound", impact);
        if (chime  != null) AssignField(so, "chimeSound",  chime);

        // Load Rarity Frames
        var fComm = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Common.png");
        var fRare = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        var fEpic = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Epic.png");
        var fLeg  = LoadAsset<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Legendary.png");
        if (fComm != null) AssignField(so, "frameCommon",    fComm);
        if (fRare != null) AssignField(so, "frameRare",      fRare);
        if (fEpic != null) AssignField(so, "frameEpic",      fEpic);
        if (fLeg  != null) AssignField(so, "frameLegendary", fLeg);

        so.ApplyModifiedProperties();
        overlayRT.gameObject.SetActive(false);

        return overlayComp;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Utility Helpers

    private static RectTransform MakeRectChild(GameObject parent, string name, float w = 0, float h = 0)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        if (w > 0 && h > 0) rt.sizeDelta = new Vector2(w, h);
        else rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static RectTransform MakeUIGameObject(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static RectTransform MakeTextChild(GameObject parent, string name, string text, float size, Color color)
    {
        var rt = MakeRectChild(parent, name);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AssignField(SerializedObject so, string propertyName, Object targetValue)
    {
        var prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = targetValue;
        }
        else
        {
            Debug.LogWarning($"[CardsPageSetupTool] Field '{propertyName}' not found on {so.targetObject.GetType().Name}");
        }
    }

    private static T LoadAsset<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static Sprite CreateRoundedRectSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Button/Button_SquareSolid01_White.png");
        if (existing != null) return existing;
        return null;
    }

    private static Sprite CreateCircleSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Frame/BasicFrame_CircleSolid01_White.png");
        if (existing != null) return existing;
        return null;
    }

    private static CardLibrary CreateAndPopulateCardLibrary()
    {
        string assetPath = "Assets/UIPACK/ResourcesData/CardLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<CardLibrary>(assetPath);
        if (lib != null) return lib;

        lib = ScriptableObject.CreateInstance<CardLibrary>();
        AssetDatabase.CreateAsset(lib, assetPath);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    private static CardInventoryManager EnsureInventoryManager(CardLibrary lib)
    {
        var existing = Object.FindObjectOfType<CardInventoryManager>();
        if (existing != null)
        {
            var soExisting = new SerializedObject(existing);
            AssignField(soExisting, "cardLibrary", lib);
            soExisting.ApplyModifiedProperties();
            existing.EnsureLoaded();
            return existing;
        }

        var go = new GameObject("[CardInventoryManager]");
        var mgr = go.AddComponent<CardInventoryManager>();
        var so = new SerializedObject(mgr);
        AssignField(so, "cardLibrary", lib);
        so.ApplyModifiedProperties();
        mgr.EnsureLoaded();
        return mgr;
    }

    #endregion
}
#endif
