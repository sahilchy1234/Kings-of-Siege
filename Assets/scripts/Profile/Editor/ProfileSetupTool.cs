#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Siege.Chests;
using Siege.Profile;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click builder for the Player Profile page.
/// Menu: Kings of Siege > Profile > Build Profile Page (MainMenu)
///
/// - Opens MainMenu.unity if another scene is active.
/// - Makes sure the PNGs it uses are imported as UI sprites (only touches textures that are not sprites yet).
/// - Adds [ProfileScreen] under the main Canvas, just below the bottom nav, with a ProfileController that has
///   every sprite, the font, the card library, the nav bar and the menu sounds wired. The page itself is generated at runtime.
/// - Nothing existing is changed or removed: the controller finds the avatar in the top bar at runtime and
///   opens the profile when it is tapped.
///
/// Kings of Siege > Profile > Remove Profile Page takes it out again.
/// </summary>
public static class ProfileSetupTool
{
    private const string MAIN_MENU_SCENE = "Assets/Scenes/MainMenu.unity";
    private const string STORE = "Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/";
    private const string CHEST_UI = "Assets/UIPACK/Chest_UI_Pack/";
    private const string HOME = "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/";
    private const string GUI_CARDS = "Assets/UIPACK/Kings_of_Siege_GUI_Cards/";
    private const string COMPONENTS = "Assets/UIPACK/ResourcesData/Sprites/Components/";
    private const string ROOT_NAME = "[ProfileScreen]";

    // Extra sprites used by the league-style rank banner and level badge
    private static readonly string[] LeaguePanelPaths =
    {
        HOME + "Panels/league_panel.png",
        HOME + "league_panel.png",
        HOME + "UI/league_panel.png",
        HOME + "Icons/league_panel.png",
    };
    private const string BADGES = "Assets/UIPACK/badges/";
    // Slot order matches PlayerProfileData.RankTier.badgeIndex (9 leagues x 3 divisions, then Kings).
    private static readonly string[] RankBadgePaths =
    {
        BADGES + "Wood 1.png",
        HOME + "Icons/gold_badge_clean.png",
        HOME + "Icons/gold_badge.png",
        COMPONENTS + "IconMisc/Icon_Trophy_s.png",
    };
    private static readonly string[] LevelShieldPaths =
    {
        HOME + "Icons/level_badge.png",
        HOME + "Icons/level_shield.png",
        HOME + "Icons/shield_blue.png",
        COMPONENTS + "IconMisc/Icon_Shield01.png",
    };
    private static readonly string[] SwordsPaths =
    {
        HOME + "Icons/swords.png",
        HOME + "Icons/battle.png",
        HOME + "Icons/sword.png",
        COMPONENTS + "IconMisc/Icon_Sword.png",
        COMPONENTS + "IconMisc/Icon_Shield01.png",
    };
    private static readonly string[] StarPaths =
    {
        HOME + "Icons/star.png",
        HOME + "Icons/star_filled.png",
        COMPONENTS + "IconMisc/Icon_Star.png",
        COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Crown.png",
    };

    private static readonly string[] AvatarPaths =
    {
        HOME + "Characters/king_avatar.png",
        HOME + "Avatars/Premium/01_royal_king.png",
        HOME + "Avatars/Premium/02_archer.png",
        HOME + "Avatars/Premium/03_viking.png",
        HOME + "Avatars/Premium/04_ice_mage.png",
        HOME + "Avatars/Premium/05_desert_warrior.png",
        HOME + "Avatars/Premium/06_paladin.png",
        HOME + "Avatars/Premium/07_rogue.png",
        HOME + "Avatars/Premium/08_blacksmith.png",
        HOME + "Avatars/Premium/09_forest_guardian.png",
        HOME + "Avatars/Premium/10_dark_knight.png",
        HOME + "Avatars/Blue_Troops/01_swordsman.png",
        HOME + "Avatars/Blue_Troops/02_horseman.png",
        HOME + "Avatars/Blue_Troops/03_archer.png",
        HOME + "Avatars/Blue_Troops/04_spearman.png",
    };

    [MenuItem("Kings of Siege/Profile/Build Profile Page (MainMenu)", priority = 30)]
    public static void BuildProfilePage()
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
            EditorUtility.DisplayDialog("Profile Builder", "No Canvas found in the open scene.", "OK");
            return;
        }

        var s = new Dictionary<string, Sprite>
        {
            { "background", LoadSprite(STORE + "Backgrounds/Shop_Blurred_Blue.png") },
            { "rounded", LoadSprite(COMPONENTS + "Button/Button_SquareSolid01_White.png") },
            { "circle", LoadSprite(COMPONENTS + "Frame/BasicFrame_CircleSolid01_White.png") },
            { "coin", LoadSprite(STORE + "Currency/Coin.png") },
            { "gem", LoadSprite(STORE + "Currency/Gem.png") },
            { "elixir", LoadSprite(COMPONENTS + "Icon_ElixirDroplet.png") },
            { "trophy", LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Trophy.png") },
            { "crown", LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Crown.png") },
            { "shield", LoadSprite(COMPONENTS + "IconMisc/Icon_Shield01.png") },
            { "cardsIcon", LoadSprite(STORE + "Navigation/Cards.png") },
            { "back", LoadSprite(COMPONENTS + "IconMisc/Icon_Arrow_Back.png") },
            { "glow", LoadSprite(CHEST_UI + "Overlays/Chest_Gold_Glow.png") },
            { "sparkle", LoadSprite(CHEST_UI + "Effects/Sparkle.png") },
            { "lock", LoadSprite(COMPONENTS + "IconMisc/Icon_Lock01.png") },
            { "king", LoadSprite(GUI_CARDS + "King_Blue.png") },
        };
        if (s["coin"] == null) s["coin"] = LoadSprite(HOME + "Icons/coin.png");
        if (s["gem"] == null) s["gem"] = LoadSprite(HOME + "Icons/gem.png");
        if (s["cardsIcon"] == null) s["cardsIcon"] = LoadSprite(HOME + "Icons/cards.png");
        if (s["trophy"] == null) s["trophy"] = LoadSprite(COMPONENTS + "IconMisc/Icon_Trophy_s.png");
        if (s["trophy"] == null) s["trophy"] = LoadSprite(HOME + "Icons/rankings.png");
        if (s["king"] == null) s["king"] = LoadSprite(HOME + "Characters/king_avatar.png");

        var missing = new List<string>();
        foreach (var kv in s) if (kv.Value == null) missing.Add(kv.Key);

        var avatars = new List<Sprite>();
        foreach (var p in AvatarPaths)
        {
            var sp = LoadSprite(p, 512);
            if (sp != null) avatars.Add(sp);
        }
        if (avatars.Count == 0 && s["king"] != null) avatars.Add(s["king"]);

        var library = FindCardLibrary();

        // ── Root object ──
        var old = canvas.transform.Find(ROOT_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject(ROOT_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Profile Page");
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var nav = FindDeep(canvas.transform, "downpanel") as RectTransform;
        if (nav != null && nav.parent == canvas.transform) rt.SetSiblingIndex(nav.GetSiblingIndex());
        else rt.SetAsLastSibling();

        var ctrl = go.AddComponent<ProfileController>();
        ctrl.backgroundSprite = s["background"];
        ctrl.roundedSprite = s["rounded"];
        ctrl.circleSprite = s["circle"];
        ctrl.coinSprite = s["coin"];
        ctrl.gemSprite = s["gem"];
        ctrl.elixirSprite = s["elixir"];
        ctrl.trophySprite = s["trophy"];
        ctrl.crownSprite = s["crown"];
        ctrl.shieldSprite = s["shield"];
        ctrl.cardsIconSprite = s["cardsIcon"];
        ctrl.backSprite = s["back"];
        ctrl.glowSprite = s["glow"];
        ctrl.sparkleSprite = s["sparkle"];
        ctrl.lockSprite = s["lock"];
        ctrl.kingPortraitSprite = s["king"];
        ctrl.leaguePanelSprite = LoadFirstExisting(LeaguePanelPaths);
        ctrl.rankBadgeSprite   = LoadFirstExisting(RankBadgePaths);
        ctrl.rankBadgeByDivision = new Sprite[Siege.Profile.PlayerProfileData.RANK_BADGE_COUNT];
        for (int i = 0; i < ctrl.rankBadgeByDivision.Length; i++)
            ctrl.rankBadgeByDivision[i] = LoadSprite(BADGES + Siege.Profile.PlayerProfileData.RankBadgeFileNames[i] + ".png");
        ctrl.levelShieldSprite = LoadFirstExisting(LevelShieldPaths);
        ctrl.swordsSprite      = LoadFirstExisting(SwordsPaths);
        ctrl.starSprite        = LoadFirstExisting(StarPaths);
        ctrl.avatarSprites = avatars.ToArray();
        ctrl.cardLibraryOverride = library;
        ctrl.bottomNav = nav;
        ctrl.font = FindMenuFont(canvas.transform);

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
            ctrl.successSound = so.FindProperty("collectSound")?.objectReferenceValue as AudioClip;
        }
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        ctrl.audioSource = audio;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = go;

        // Defer the UI build to the next editor frame so canvas layout is fully initialized.
        var capturedCtrl = ctrl;
        EditorApplication.delayCall += () =>
        {
            if (capturedCtrl == null) return;
            capturedCtrl.BuildInEditor();
            EditorUtility.SetDirty(capturedCtrl.gameObject);
            EditorSceneManager.MarkSceneDirty(capturedCtrl.gameObject.scene);
            EditorSceneManager.SaveScene(capturedCtrl.gameObject.scene);
        };

        string msg = "Profile page added to '" + canvas.gameObject.scene.name + "' under " + canvas.name +
                     ". Tap the avatar in the top bar to open it (back button or any bottom tab closes it).";
        if (nav == null) msg += "\n\nBottom nav 'downpanel' not found: the page still works, but bottom tabs will not close it.";
        if (library == null) msg += "\n\nNo CardLibrary asset found: the Battle Deck will show empty slots.";
        if (missing.Count > 0) msg += "\n\nMissing sprites (the page still works, these parts are blank): " + string.Join(", ", missing);
        Debug.Log("<color=#7CFC00>[ProfileSetupTool]</color> " + msg);
        EditorUtility.DisplayDialog("Profile Builder", msg, "Nice");
    }

    [MenuItem("Kings of Siege/Profile/Remove Profile Page", priority = 31)]
    public static void RemoveProfilePage()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) return;
        var t = canvas.transform.Find(ROOT_NAME);
        if (t == null) return;
        Undo.DestroyObjectImmediate(t.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Debug.Log("[ProfileSetupTool] Profile page removed.");
    }

    // ── Debug helpers (work in and out of Play mode) ──

    [MenuItem("Kings of Siege/Profile/Debug: +500 Trophies")]
    public static void AddTrophies()
    {
        PlayerPrefs.SetInt(PlayerProfileData.TROPHIES_KEY, PlayerProfileData.Trophies + 500);
        PlayerPrefs.Save();
        Debug.Log("[ProfileSetupTool] Trophies = " + PlayerProfileData.Trophies);
    }

    [MenuItem("Kings of Siege/Profile/Debug: Reset Trophies")]
    public static void ResetTrophies()
    {
        PlayerPrefs.SetInt(PlayerProfileData.TROPHIES_KEY, 0);
        PlayerPrefs.SetInt(PlayerProfileData.BEST_TROPHIES_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[ProfileSetupTool] Trophies reset.");
    }

    [MenuItem("Kings of Siege/Profile/Debug: Join Demo Clan")]
    public static void JoinDemoClan()
    {
        PlayerPrefs.SetString(PlayerProfileData.CLAN_NAME_KEY, "Iron Wolves");
        PlayerPrefs.SetString(PlayerProfileData.CLAN_ROLE_KEY, "Elder");
        PlayerPrefs.SetInt(PlayerProfileData.CLAN_TROPHIES_KEY, 18450);
        PlayerPrefs.Save();
        Debug.Log("[ProfileSetupTool] Demo clan set.");
    }

    [MenuItem("Kings of Siege/Profile/Debug: Leave Clan")]
    public static void LeaveClan()
    {
        PlayerPrefs.DeleteKey(PlayerProfileData.CLAN_NAME_KEY);
        PlayerPrefs.DeleteKey(PlayerProfileData.CLAN_ROLE_KEY);
        PlayerPrefs.DeleteKey(PlayerProfileData.CLAN_TROPHIES_KEY);
        PlayerPrefs.Save();
        Debug.Log("[ProfileSetupTool] Left clan.");
    }

    [MenuItem("Kings of Siege/Profile/Debug: Reset Battle Record")]
    public static void ResetBattleRecord()
    {
        PlayerPrefs.DeleteKey(PlayerProfileData.WINS_KEY);
        PlayerPrefs.DeleteKey(PlayerProfileData.LOSSES_KEY);
        PlayerPrefs.DeleteKey(PlayerProfileData.DRAWS_KEY);
        PlayerPrefs.DeleteKey(PlayerProfileData.THREE_STARS_KEY);
        PlayerPrefs.Save();
        Debug.Log("[ProfileSetupTool] Battle record reset.");
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
    private static Sprite LoadSprite(string path, int maxSize = 0)
    {
        if (!File.Exists(path)) return null;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (maxSize > 0) importer.maxTextureSize = maxSize;
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

    /// <summary>Returns the first sprite that exists on disk from an ordered candidate list.</summary>
    private static Sprite LoadFirstExisting(string[] candidates)
    {
        foreach (var p in candidates)
        {
            var sp = LoadSprite(p);
            if (sp != null) return sp;
        }
        return null;
    }

    private static Siege.Cards.CardLibrary FindCardLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<Siege.Cards.CardLibrary>("Assets/UIPACK/ResourcesData/CardLibrary.asset");
        if (lib != null) return lib;
        var guids = AssetDatabase.FindAssets("t:CardLibrary");
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Siege.Cards.CardLibrary>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
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
