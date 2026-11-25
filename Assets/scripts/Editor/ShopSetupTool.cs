#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Siege.Cards;
using Siege.Chests;
using Siege.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click builder for the Kings of Siege shop.
/// Menu: Kings of Siege > Shop > Build Shop Page (MainMenu)
///
/// - Opens MainMenu.unity if another scene is active.
/// - Makes sure the Store PNGs are imported as UI sprites (only touches textures that are not sprites yet).
/// - Adds a [ShopPage] object under the main Canvas, just below the bottom navigation bar,
///   with a ShopController that has every sprite, the font, the card library, the nav bar and
///   the existing UI sounds wired up. The page itself is generated at runtime.
/// </summary>
public static class ShopSetupTool
{
    private const string MAIN_MENU_SCENE = "Assets/Scenes/MainMenu.unity";
    private const string STORE = "Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/";
    private const string CHEST_UI = "Assets/UIPACK/Chest_UI_Pack/";
    private const string CHEST_PNGS = "Assets/UIPACK/Kings_of_Siege_Chest_Individual_PNGs/";
    private const string COMPONENTS = "Assets/UIPACK/ResourcesData/Sprites/";

    [MenuItem("Kings of Siege/Shop/Build Shop Page (MainMenu)", priority = 10)]
    public static void BuildShopPage()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MAIN_MENU_SCENE && File.Exists(MAIN_MENU_SCENE))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE, OpenSceneMode.Single);
        }

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Shop Builder", "No Canvas found in the open scene.", "OK");
            return;
        }

        // ── Sprites ──
        var s = new Dictionary<string, Sprite>
        {
            { "background", LoadSprite(STORE + "Backgrounds/Shop_Blurred_Blue.png") },
            { "offerPanel", LoadSprite(STORE + "Panels/Offer_Panel.png") },
            { "cardPanel", LoadSprite(STORE + "Panels/Card_Panel.png") },
            { "coinPanel", LoadSprite(STORE + "Panels/Coin_Panel.png") },
            { "buyButton", LoadSprite(STORE + "Buttons/Buy_Button.png") },
            { "plusButton", LoadSprite(STORE + "Buttons/Plus_Button.png") },
            { "plusGlyph", LoadSprite(STORE + "Buttons/Plus_Glyph.png") },
            { "chevron", LoadSprite(STORE + "Buttons/Chevron_Down.png") },
            { "coin", LoadSprite(STORE + "Currency/Coin.png") },
            { "gem", LoadSprite(STORE + "Currency/Gem.png") },
            { "shopIcon", LoadSprite(STORE + "Navigation/Shop.png") },
            { "calendar", LoadSprite(STORE + "Heading_Icons/Daily_Offers_Calendar.png") },
            { "cardsIcon", LoadSprite(STORE + "Navigation/Cards.png") },
            { "silverChest", LoadSprite(STORE + "Products/Silver_Chest.png") },
            { "goldChest", LoadSprite(STORE + "Products/Gold_Chest.png") },
            { "silverOpen", LoadSprite(CHEST_PNGS + "Silver/Silver_Open_With_Light.png") },
            { "goldOpen", LoadSprite(CHEST_PNGS + "Gold/Gold_Open_With_Light.png") },
            { "coinStack", LoadSprite(STORE + "Products/Coin_Stack.png") },
            { "coinBag", LoadSprite(STORE + "Products/Coin_Bag.png") },
            { "divider", LoadSprite(STORE + "Overlays/Header_Divider.png") },
            { "circle", LoadSprite(COMPONENTS + "Components/Frame/BasicFrame_CircleSolid01_White.png") },
            { "rounded", LoadSprite(COMPONENTS + "Components/Button/Button_SquareSolid01_White.png") },
            { "popup", LoadSprite(CHEST_UI + "Panels/Popup_Empty.png") },
            { "close", LoadSprite(CHEST_UI + "Icons/Close.png") },
            { "rays", LoadSprite(COMPONENTS + "Demo/Demo_Image/Image_Light.png") },
            { "glow", LoadSprite(CHEST_UI + "Overlays/Chest_Gold_Glow.png") },
            { "sparkle", LoadSprite(CHEST_UI + "Effects/Sparkle.png") },
        };
        // Fallbacks to sprites that are already in use elsewhere in the project
        if (s["coin"] == null) s["coin"] = LoadSprite("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/coin.png");
        if (s["gem"] == null) s["gem"] = LoadSprite("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/gem.png");
        if (s["coinStack"] == null) s["coinStack"] = LoadSprite(CHEST_UI + "Icons/Coin_Stack.png");
        if (s["silverChest"] == null) s["silverChest"] = LoadSprite(CHEST_PNGS + "Silver/Silver_Closed.png");
        if (s["goldChest"] == null) s["goldChest"] = LoadSprite(CHEST_PNGS + "Gold/Gold_Closed.png");
        if (s["shopIcon"] == null) s["shopIcon"] = LoadSprite("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/shop.png");
        if (s["cardsIcon"] == null) s["cardsIcon"] = LoadSprite("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/cards.png");

        var missing = new List<string>();
        foreach (var kv in s) if (kv.Value == null) missing.Add(kv.Key);

        // ── Root object ──
        var old = canvas.transform.Find("[ShopPage]");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("[ShopPage]", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Shop Page");
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var nav = FindDeep(canvas.transform, "downpanel") as RectTransform;
        if (nav != null && nav.parent == canvas.transform) rt.SetSiblingIndex(nav.GetSiblingIndex());
        else rt.SetAsLastSibling();

        var shop = go.AddComponent<ShopController>();
        shop.backgroundSprite = s["background"];
        shop.offerPanelSprite = s["offerPanel"];
        shop.cardPanelSprite = s["cardPanel"];
        shop.coinPanelSprite = s["coinPanel"];
        shop.buyButtonSprite = s["buyButton"];
        shop.plusButtonSprite = s["plusButton"];
        shop.plusGlyphSprite = s["plusGlyph"];
        shop.chevronSprite = s["chevron"];
        shop.coinSprite = s["coin"];
        shop.gemSprite = s["gem"];
        shop.shopIconSprite = s["shopIcon"];
        shop.calendarSprite = s["calendar"];
        shop.cardsIconSprite = s["cardsIcon"];
        shop.silverChestSprite = s["silverChest"];
        shop.goldChestSprite = s["goldChest"];
        shop.silverChestOpenSprite = s["silverOpen"];
        shop.goldChestOpenSprite = s["goldOpen"];
        shop.coinStackSprite = s["coinStack"];
        shop.coinBagSprite = s["coinBag"];
        shop.dividerSprite = s["divider"];
        shop.circleSprite = s["circle"];
        shop.roundedSprite = s["rounded"];
        shop.popupPanelSprite = s["popup"];
        shop.closeSprite = s["close"];
        shop.raysSprite = s["rays"];
        shop.glowSprite = s["glow"];
        shop.sparkleSprite = s["sparkle"];
        shop.bottomNav = nav;
        shop.cardLibraryOverride = FindCardLibrary();
        shop.font = FindMenuFont(canvas.transform);

        // Visible-pixel trims so padded PNGs (chests, coins, gems) fill their boxes properly
        shop.spriteTrims = new List<ShopSpriteTrim>();
        foreach (var key in new[] { "coin", "gem", "shopIcon", "calendar", "cardsIcon", "silverChest", "goldChest",
                                    "silverOpen", "goldOpen", "coinStack", "coinBag", "close", "plusGlyph", "chevron" })
        {
            var sp = s[key];
            if (sp == null) continue;
            shop.spriteTrims.Add(new ShopSpriteTrim { sprite = sp, visible = ComputeVisibleRect(AssetDatabase.GetAssetPath(sp)) });
        }

        // Re-use the UI sounds the menu and chest system already use
        var chest = Object.FindFirstObjectByType<ChestSystemController>(FindObjectsInactive.Include);
        if (chest != null)
        {
            var so = new SerializedObject(chest);
            shop.chestOpenSound = so.FindProperty("openChestSound")?.objectReferenceValue as AudioClip;
            shop.coinSound = so.FindProperty("coinTickSound")?.objectReferenceValue as AudioClip;
            shop.purchaseSound = so.FindProperty("collectSound")?.objectReferenceValue as AudioClip;
        }
        var menu = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            shop.clickSound = so.FindProperty("buttonClickSound")?.objectReferenceValue as AudioClip;
        }
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        shop.audioSource = audio;

        EditorUtility.SetDirty(shop);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = go;

        string msg = "Shop page added to '" + canvas.gameObject.scene.name + "' under " + canvas.name +
                     (nav != null ? " (wired to the bottom nav's Shop tab)." : " (bottom nav 'downpanel' not found: call ShopController.Instance.Open() from your own button).");
        if (missing.Count > 0) msg += "\n\nMissing sprites (shop still works, these parts are blank): " + string.Join(", ", missing);
        Debug.Log("<color=#7CFC00>[ShopSetupTool]</color> " + msg);
        EditorUtility.DisplayDialog("Shop Builder", msg + "\n\nPress Play and tap the Shop tab.", "Nice");
    }

    [MenuItem("Kings of Siege/Shop/Debug: Reset Daily Offers Timer")]
    public static void ResetDaily()
    {
        PlayerPrefs.DeleteKey("KoS_Shop_DailyCycleStartUtc");
        PlayerPrefs.DeleteKey("KoS_Shop_DailyCycleIndex");
        PlayerPrefs.DeleteKey("KoS_Shop_PurchasedThisCycle");
        PlayerPrefs.Save();
        Debug.Log("[ShopSetupTool] Daily offers reset (sold-out flags cleared, new 24h cycle starts on next launch).");
    }

    [MenuItem("Kings of Siege/Shop/Debug: Give 5,000 Gems")]
    public static void GiveGems()
    {
        PlayerWallet.AddGems(5000);
        Debug.Log("[ShopSetupTool] +5,000 gems. Gems now: " + PlayerWallet.Gems.ToString("N0"));
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static Canvas FindMainCanvas()
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.name == "Canvas") return c;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas) return c;
        return null;
    }

    /// <summary>Loads a texture as a UI sprite, switching its importer to Sprite only if it isn't one yet.</summary>
    private static Sprite LoadSprite(string path)
    {
        if (!File.Exists(path)) return null;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) return sp;
        }
        return sprite;
    }

    private static CardLibrary FindCardLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CardLibrary>("Assets/UIPACK/ResourcesData/CardLibrary.asset");
        if (lib != null) return lib;
        var guids = AssetDatabase.FindAssets("t:CardLibrary");
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<CardLibrary>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    private static TMP_FontAsset FindMenuFont(Transform canvas)
    {
        var top = FindDeep(canvas, "toppanel");
        if (top != null)
        {
            var t = top.GetComponentInChildren<TMP_Text>(true);
            if (t != null && t.font != null) return t.font;
        }
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (t.font != null) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>Reads the PNG from disk and returns the non-transparent area as UV fractions (origin bottom-left).</summary>
    private static Rect ComputeVisibleRect(string assetPath)
    {
        var full = new Rect(0f, 0f, 1f, 1f);
        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return full;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(assetPath))) return full;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a <= 20) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return full;
            return new Rect(minX / (float)w, minY / (float)h, (maxX - minX + 1) / (float)w, (maxY - minY + 1) / (float)h);
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
#endif
