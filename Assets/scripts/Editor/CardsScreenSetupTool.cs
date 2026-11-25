#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Siege.Cards;
using Siege.CardsScreen;
using Siege.Chests;
using Siege.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click builder for the redesigned Cards screen (Decks + Collection).
/// Menu: Kings of Siege > Cards > Build Cards Screen (MainMenu)
///
/// - Opens MainMenu.unity if another scene is active.
/// - Makes sure the PNGs it uses are imported as UI sprites (only touches textures that are not sprites yet).
/// - Creates the grayscale material for "Not found" cards.
/// - Turns the old [CardsPage] off (renamed [CardsPage_Legacy], nothing deleted) and moves its
///   level-up celebration overlay onto the new screen so upgrades keep the same celebration.
/// - Adds [CardsScreen] under the main Canvas, just below the bottom nav, with a CardsScreenController
///   that has every sprite, the font, the card library, the nav bar and the menu sounds wired.
///   The page itself is generated at runtime.
///
/// Kings of Siege > Cards > Restore Old Cards Page undoes the swap.
/// </summary>
public static class CardsScreenSetupTool
{
    private const string MAIN_MENU_SCENE = "Assets/Scenes/MainMenu.unity";
    private const string STORE = "Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/";
    private const string CHEST_UI = "Assets/UIPACK/Chest_UI_Pack/";
    private const string HOME = "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/";
    private const string GUI_CARDS = "Assets/UIPACK/Kings_of_Siege_GUI_Cards/";
    private const string COMPONENTS = "Assets/UIPACK/ResourcesData/Sprites/Components/";
    private const string DEMO = "Assets/UIPACK/ResourcesData/Sprites/Demo/";
    private const string SHADER_NAME = "Kings of Siege/UI Grayscale";
    private const string MATERIAL_PATH = "Assets/scripts/CardsScreen/Shaders/KoS_UIGrayscale.mat";
    private const string ROOT_NAME = "[CardsScreen]";
    private const string LEGACY_NAME = "[CardsPage]";
    private const string LEGACY_DISABLED_NAME = "[CardsPage_Legacy]";

    [MenuItem("Kings of Siege/Cards/Build Cards Screen (MainMenu)", priority = 20)]
    public static void BuildCardsScreen()
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
            EditorUtility.DisplayDialog("Cards Screen Builder", "No Canvas found in the open scene.", "OK");
            return;
        }

        // ── Sprites ──
        var s = new Dictionary<string, Sprite>
        {
            { "background", LoadSprite(STORE + "Backgrounds/Shop_Blurred_Blue.png") },
            { "rounded", LoadSprite(COMPONENTS + "Button/Button_SquareSolid01_White.png") },
            { "circle", LoadSprite(COMPONENTS + "Frame/BasicFrame_CircleSolid01_White.png") },
            { "cardsIcon", LoadSprite(STORE + "Navigation/Cards.png") },
            { "coin", LoadSprite(STORE + "Currency/Coin.png") },
            { "gem", LoadSprite(STORE + "Currency/Gem.png") },
            { "elixir", LoadSprite(COMPONENTS + "Icon_ElixirDroplet.png") },
            { "track", LoadSprite(CHEST_UI + "Progress/Track.png") },
            { "arrowUp", LoadSprite(COMPONENTS + "Slider/Slider_Basic01_Icon_Arrow.png") },
            { "newRibbon", LoadSprite(COMPONENTS + "Label/Label_Ribbon01_Red.png") },
            { "levelUpRibbon", LoadSprite(COMPONENTS + "Label/Label_Ribbon01_Yellow.png") },
            { "popup", LoadSprite(CHEST_UI + "Panels/Popup_Empty.png") },
            { "close", LoadSprite(CHEST_UI + "Icons/Close.png") },
            { "goldButton", LoadSprite(CHEST_UI + "Buttons/Gold_Empty.png") },
            { "greenButton", LoadSprite(CHEST_UI + "Buttons/Green_Empty.png") },
            { "greyButton", LoadSprite(CHEST_UI + "Buttons/Grey_Empty.png") },
            { "glow", LoadSprite(CHEST_UI + "Overlays/Chest_Gold_Glow.png") },
            { "rays", LoadSprite(DEMO + "Demo_Image/Image_Light.png") },
            { "sparkle", LoadSprite(CHEST_UI + "Effects/Sparkle.png") },
            { "king", LoadSprite(GUI_CARDS + "King_Blue.png") },
            { "tower", LoadSprite(COMPONENTS + "Pedestal_Turret_Stone.png") },
            { "army", LoadSprite(HOME + "Icons/swords.png") },
            { "power", LoadSprite(COMPONENTS + "IconMisc/Icon_Fire01.png") },
            { "sort", LoadSprite(COMPONENTS + "Icon_TuningSliders.png") },
            { "lock", LoadSprite(COMPONENTS + "IconMisc/Icon_Lock01.png") },
        };
        if (s["coin"] == null) s["coin"] = LoadSprite(HOME + "Icons/coin.png");
        if (s["gem"] == null) s["gem"] = LoadSprite(HOME + "Icons/gem.png");
        if (s["cardsIcon"] == null) s["cardsIcon"] = LoadSprite(HOME + "Icons/cards.png");
        if (s["king"] == null) s["king"] = LoadSprite(HOME + "Characters/king_avatar.png");
        if (s["army"] == null) s["army"] = LoadSprite(COMPONENTS + "IconMisc/Icon_Sword04.png");
        if (s["newRibbon"] == null) s["newRibbon"] = LoadSprite(COMPONENTS + "Label/Label_Ribbon01_Pink.png");
        if (s["levelUpRibbon"] == null) s["levelUpRibbon"] = s["newRibbon"];

        var missing = new List<string>();
        foreach (var kv in s) if (kv.Value == null) missing.Add(kv.Key);

        // ── Grayscale material for "Not found" cards ──
        var grayMat = EnsureGrayscaleMaterial();
        if (grayMat == null) missing.Add("grayscale material (cards will be darkened instead)");

        // ── Card data ──
        var library = FindCardLibrary();
        var inventory = Object.FindFirstObjectByType<CardInventoryManager>(FindObjectsInactive.Include);

        // ── Retire the old cards page (kept, just switched off) ──
        CardUpgradeCelebrationOverlay overlay = null;
        var legacy = FindDeep(canvas.transform, LEGACY_NAME) ?? FindDeep(canvas.transform, LEGACY_DISABLED_NAME);
        if (legacy != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(legacy.gameObject, "Retire old cards page");
            overlay = legacy.GetComponentInChildren<CardUpgradeCelebrationOverlay>(true);

            // The inventory manager must stay active (BattleDeckLoader and the chests rely on it).
            if (inventory != null && inventory.transform.IsChildOf(legacy))
            {
                Undo.SetTransformParent(inventory.transform, null, "Move CardInventoryManager");
                inventory.gameObject.SetActive(true);
            }
            legacy.name = LEGACY_DISABLED_NAME;
            legacy.gameObject.SetActive(false);
        }

        if (inventory == null)
        {
            var invGo = new GameObject("[CardInventoryManager]");
            Undo.RegisterCreatedObjectUndo(invGo, "Create CardInventoryManager");
            inventory = invGo.AddComponent<CardInventoryManager>();
        }
        if (library != null)
        {
            var so = new SerializedObject(inventory);
            var prop = so.FindProperty("cardLibrary");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = library;
                so.ApplyModifiedProperties();
            }
        }

        // ── Root object ──
        var old = canvas.transform.Find(ROOT_NAME);
        if (old != null)
        {
            // keep the celebration overlay if a previous build already moved it here
            var prevOverlay = old.GetComponentInChildren<CardUpgradeCelebrationOverlay>(true);
            if (overlay == null && prevOverlay != null) overlay = prevOverlay;
            if (overlay != null && overlay.transform.IsChildOf(old)) Undo.SetTransformParent(overlay.transform, canvas.transform, "Keep overlay");
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var go = new GameObject(ROOT_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Cards Screen");
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var nav = FindDeep(canvas.transform, "downpanel") as RectTransform;
        if (nav != null && nav.parent == canvas.transform) rt.SetSiblingIndex(nav.GetSiblingIndex());
        else rt.SetAsLastSibling();

        var ctrl = go.AddComponent<CardsScreenController>();
        ctrl.backgroundSprite = s["background"];
        ctrl.roundedSprite = s["rounded"];
        ctrl.circleSprite = s["circle"];
        ctrl.cardsIconSprite = s["cardsIcon"];
        ctrl.coinSprite = s["coin"];
        ctrl.gemSprite = s["gem"];
        ctrl.elixirSprite = s["elixir"];
        ctrl.barTrackSprite = s["track"];
        ctrl.barFillSprite = null; // generated glossy white capsule so state colours stay true
        ctrl.arrowUpSprite = s["arrowUp"];
        ctrl.newRibbonSprite = s["newRibbon"];
        ctrl.popupPanelSprite = s["popup"];
        ctrl.closeSprite = s["close"];
        ctrl.goldButtonSprite = s["goldButton"];
        ctrl.greenButtonSprite = s["greenButton"];
        ctrl.greyButtonSprite = s["greyButton"];
        ctrl.glowSprite = s["glow"];
        ctrl.raysSprite = s["rays"];
        ctrl.levelUpRibbonSprite = s["levelUpRibbon"];
        ctrl.sparkleSprite = s["sparkle"];
        ctrl.kingPortraitSprite = s["king"];
        ctrl.towerSprite = s["tower"];
        ctrl.armyIconSprite = s["army"];
        ctrl.powerIconSprite = s["power"];
        ctrl.sortIconSprite = s["sort"];
        ctrl.lockSprite = s["lock"];
        ctrl.grayscaleMaterial = grayMat;
        ctrl.cardLibraryOverride = library;
        ctrl.bottomNav = nav;
        ctrl.font = FindMenuFont(canvas.transform);

        ctrl.spriteTrims = new List<ShopSpriteTrim>();
        foreach (var key in new[] { "coin", "gem", "cardsIcon", "army", "power", "arrowUp", "close" })
        {
            var sp = s[key];
            if (sp == null) continue;
            ctrl.spriteTrims.Add(new ShopSpriteTrim { sprite = sp, visible = ComputeVisibleRect(AssetDatabase.GetAssetPath(sp)) });
        }

        if (overlay != null)
        {
            Undo.SetTransformParent(overlay.transform, go.transform, "Move celebration overlay");
            overlay.gameObject.SetActive(false);
            ctrl.celebrationOverlay = overlay;
        }

        // ── Sounds the menu already uses ──
        var menu = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            ctrl.clickSound = so.FindProperty("buttonClickSound")?.objectReferenceValue as AudioClip;
        }
        var chest = Object.FindFirstObjectByType<ChestSystemController>(FindObjectsInactive.Include);
        if (chest != null)
        {
            var so = new SerializedObject(chest);
            ctrl.upgradeSound = so.FindProperty("collectSound")?.objectReferenceValue as AudioClip;
            ctrl.swapSound = so.FindProperty("coinTickSound")?.objectReferenceValue as AudioClip;
        }
        if (ctrl.swapSound == null) ctrl.swapSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/arrowrelease.mp3");
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        ctrl.audioSource = audio;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = go;

        string msg = "Cards screen added to '" + canvas.gameObject.scene.name + "' under " + canvas.name +
                     (nav != null ? " (wired to the bottom nav's Cards tab)." : " (bottom nav 'downpanel' not found: call CardsScreenController.Instance.Open() from your own button).");
        if (legacy != null) msg += "\n\nThe old " + LEGACY_NAME + " is switched off and renamed " + LEGACY_DISABLED_NAME + " (not deleted).";
        if (library == null) msg += "\n\nNo CardLibrary asset found: build the old cards page once so the card list exists.";
        if (missing.Count > 0) msg += "\n\nMissing (screen still works, these parts are blank): " + string.Join(", ", missing);
        Debug.Log("<color=#7CFC00>[CardsScreenSetupTool]</color> " + msg);
        EditorUtility.DisplayDialog("Cards Screen Builder", msg + "\n\nPress Play and tap the Cards tab.", "Nice");
    }

    [MenuItem("Kings of Siege/Cards/Restore Old Cards Page", priority = 21)]
    public static void RestoreOldCardsPage()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) return;
        var legacy = FindDeep(canvas.transform, LEGACY_DISABLED_NAME);
        var screen = canvas.transform.Find(ROOT_NAME);
        if (legacy == null)
        {
            EditorUtility.DisplayDialog("Cards Screen Builder", "No " + LEGACY_DISABLED_NAME + " found in this scene.", "OK");
            return;
        }
        if (screen != null)
        {
            var overlay = screen.GetComponentInChildren<CardUpgradeCelebrationOverlay>(true);
            if (overlay != null) Undo.SetTransformParent(overlay.transform, legacy, "Return overlay");
            Undo.DestroyObjectImmediate(screen.gameObject);
        }
        Undo.RegisterFullObjectHierarchyUndo(legacy.gameObject, "Restore old cards page");
        legacy.name = LEGACY_NAME;
        legacy.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Debug.Log("[CardsScreenSetupTool] Old cards page restored.");
    }

    [MenuItem("Kings of Siege/Cards/Debug: +20 Copies To Every Found Card")]
    public static void GiveCopies()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Cards Screen Builder", "Enter Play mode first (the card inventory lives in the running game).", "OK");
            return;
        }
        var inv = CardInventoryManager.Instance;
        if (inv == null) return;
        foreach (var e in inv.GetFoundCards()) inv.AddCardCopies(e.cardId, 20);
        Debug.Log("[CardsScreenSetupTool] +20 copies to every found card (they are flagged New! too).");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static Material EnsureGrayscaleMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (mat != null) return mat;
        var shader = Shader.Find(SHADER_NAME);
        if (shader == null)
        {
            foreach (var guid in AssetDatabase.FindAssets("KoS_UIGrayscale t:Shader"))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader != null) break;
            }
        }
        if (shader == null) return null;
        mat = new Material(shader) { name = "KoS_UIGrayscale" };
        mat.SetFloat("_Desaturate", 1f);
        mat.SetFloat("_Brightness", 0.85f);
        string dir = Path.GetDirectoryName(MATERIAL_PATH);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
        AssetDatabase.SaveAssets();
        return mat;
    }

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
