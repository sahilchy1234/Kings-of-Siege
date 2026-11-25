#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Siege.Missions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click builder for the Missions page, matching the Profile / Cards workflow.
/// Menu: Kings of Siege > Missions > Build Missions Page (MainMenu)
///
/// - Opens MainMenu.unity if another scene is active.
/// - Imports the PNGs it uses as UI sprites (only touches textures that are not sprites yet).
/// - Adds [MissionsScreen] under the main Canvas with a MissionsScreenController that has every sprite,
///   the TMP_TiltWarp font and the menu sounds wired.
/// - Wires the side-panel "button_mission" so it opens the page.
/// - Bakes the page in Edit mode (BuildInEditor), so you can select and tweak any element in the Scene view.
///   The baked page is [SerializeField]-held, so it carries into Play mode instead of rebuilding.
///
/// Re-run it any time to rebuild from the current sprites. Kings of Siege > Missions > Remove Missions Page takes it out.
/// </summary>
public static class MissionsSetupTool
{
    private const string MAIN_MENU_SCENE = "Assets/Scenes/MainMenu.unity";
    private const string STORE = "Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/";
    private const string CHEST_UI = "Assets/UIPACK/Chest_UI_Pack/";
    private const string HOME = "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/";
    private const string GUI_CARDS = "Assets/UIPACK/Kings_of_Siege_GUI_Cards/";
    private const string COMPONENTS = "Assets/UIPACK/ResourcesData/Sprites/Components/";
    private const string BATTLEVS = "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/";
    private const string FONT_PATH = "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp.asset";
    private const string ROOT_NAME = "[MissionsScreen]";
    private const string LEGACY_NAME = "MissionsScreen";
    private const string BUTTON_NAME = "button_mission";

    [MenuItem("Kings of Siege/Missions/Build Missions Page (MainMenu)", priority = 40)]
    public static void BuildMissionsPage()
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
            EditorUtility.DisplayDialog("Missions Builder", "No Canvas found in the open scene.", "OK");
            return;
        }

        var missing = new List<string>();
        Sprite S(string path) { var sp = LoadSprite(path); if (sp == null) missing.Add(path); return sp; }
        Sprite First(params string[] paths) { foreach (var p in paths) { var sp = LoadSprite(p); if (sp != null) return sp; } missing.Add(paths[0]); return null; }

        // ── Remove old roots (both the editor root and the earlier runtime object) ──
        foreach (var name in new[] { ROOT_NAME, LEGACY_NAME })
        {
            var old = canvas.transform.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // ── Root object ──
        var go = new GameObject(ROOT_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Missions Page");
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var ctrl = go.AddComponent<MissionsScreenController>();
        ctrl.backgroundSprite = S(STORE + "Backgrounds/Shop_Blurred_Blue.png");
        ctrl.roundedSprite = S(COMPONENTS + "Button/Button_SquareSolid01_White.png");
        ctrl.circleSprite = S(COMPONENTS + "Frame/BasicFrame_CircleSolid01_White.png");
        ctrl.coinSprite = First(HOME + "Icons/coin.png", STORE + "Currency/Coin.png");
        ctrl.gemSprite = First(HOME + "Icons/gem.png", STORE + "Currency/Gem.png");
        ctrl.headerIcon = S(HOME + "Icons/missions.png");
        ctrl.kingSprite = First(GUI_CARDS + "King_Blue.png", HOME + "Characters/king_avatar.png");
        ctrl.levelShieldSprite = First(HOME + "Icons/level_badge.png", COMPONENTS + "IconMisc/Icon_Shield01.png");
        ctrl.ribbonSprite = S(COMPONENTS + "Label/Label_Ribbon01_Blue.png");
        ctrl.clockSprite = First(CHEST_UI + "Icons/Clock.png", COMPONENTS + "IconMisc/Icon_Timer.png");
        ctrl.silverChestSprite = S(STORE + "Products/Silver_Chest.png");
        ctrl.goldChestSprite = S(STORE + "Products/Gold_Chest.png");
        ctrl.tabIconSprites = new[]
        {
            First(STORE + "Heading_Icons/Daily_Offers_Calendar.png", COMPONENTS + "IconMisc/Icon_Check.png"),
            LoadSprite(COMPONENTS + "IconMisc/Icon_Checkboard.Png"),
            LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Trophy.png"),
        };
        // statIcons order: Matches, Wins, Draws, ThreeStarWins, TrophiesGained, TowersDestroyed, BasesDestroyed
        ctrl.statIcons = new[]
        {
            First(BATTLEVS + "crossed_swords.png", COMPONENTS + "IconMisc/Icon_Sword05.png"),
            LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Crown.png"),
            LoadSprite(COMPONENTS + "IconMisc/Icon_Shield01.png"),
            LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Star.png"),
            LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Trophy.png"),
            LoadSprite(COMPONENTS + "IconMisc/Icon_Shield01.png"),
            LoadSprite(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Badge_Crown.png"),
        };

        ctrl.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH) ?? FindMenuFont(canvas.transform);

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        ctrl.audioSource = audio;
        var menu = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var mso = new SerializedObject(menu);
            var click = mso.FindProperty("buttonClickSound")?.objectReferenceValue as AudioClip;
            ctrl.tabSound = click;
            ctrl.claimSound = click;
        }

        // ── Wire the side-panel Missions button ──
        bool buttonWired = WireOpenButton(canvas.transform, ctrl);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Selection.activeGameObject = go;

        // Defer the bake one editor frame so canvas layout is initialized.
        var captured = ctrl;
        EditorApplication.delayCall += () =>
        {
            if (captured == null) return;
            captured.BuildInEditor();
            EditorUtility.SetDirty(captured.gameObject);
            EditorSceneManager.MarkSceneDirty(captured.gameObject.scene);
            EditorSceneManager.SaveScene(captured.gameObject.scene);
        };

        string msg = "Missions page added to '" + canvas.gameObject.scene.name + "' under " + canvas.name + " and baked in Edit mode." +
                     " Select any element under [MissionsScreen]/MissionsPage to tweak it.";
        if (!buttonWired) msg += "\n\nSide-panel '" + BUTTON_NAME + "' not found: wire a button's OnClick to MissionsScreenController.Open to open the page.";
        if (missing.Count > 0) msg += "\n\nMissing sprites (the page still works, these parts fall back): " + string.Join(", ", missing);
        Debug.Log("<color=#7CFC00>[MissionsSetupTool]</color> " + msg);
        EditorUtility.DisplayDialog("Missions Builder", msg, "Nice");
    }

    [MenuItem("Kings of Siege/Missions/Remove Missions Page", priority = 41)]
    public static void RemoveMissionsPage()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) return;
        foreach (var name in new[] { ROOT_NAME, LEGACY_NAME })
        {
            var t = canvas.transform.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Debug.Log("[MissionsSetupTool] Missions page removed.");
    }

    [MenuItem("Kings of Siege/Missions/Debug: Complete All Dailies")]
    public static void CompleteDailies()
    {
        // Big one-off bump so every daily/weekly shows as claimable (progress = total - baseline).
        MissionsManager.RecordMatchResult(true, false, 3, 50, 5, true);
        Debug.Log("[MissionsSetupTool] Recorded a large match result; daily/weekly missions should be claimable.");
    }

    [MenuItem("Kings of Siege/Missions/Debug: Reset Mission Progress")]
    public static void ResetProgress()
    {
        var now = System.DateTime.Now;
        foreach (MissionStat st in System.Enum.GetValues(typeof(MissionStat)))
        {
            PlayerPrefs.DeleteKey("KoS_Mission_Total_" + st);
            PlayerPrefs.DeleteKey("KoS_Mission_Base_" + st + "_" + MissionsManager.PeriodKey(MissionType.Daily, now));
            PlayerPrefs.DeleteKey("KoS_Mission_Base_" + st + "_" + MissionsManager.PeriodKey(MissionType.Weekly, now));
        }
        PlayerPrefs.DeleteKey("KoS_Season_Points");
        foreach (var t in new[] { MissionType.Daily, MissionType.Weekly, MissionType.Achievement })
            foreach (var m in MissionsManager.GetMissions(t))
                PlayerPrefs.DeleteKey("KoS_Mission_Claim_" + m.Id + "_" + MissionsManager.PeriodKey(t, now));
        PlayerPrefs.Save();
        Debug.Log("[MissionsSetupTool] Mission progress reset.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static bool WireOpenButton(Transform canvas, MissionsScreenController ctrl)
    {
        var t = FindDeep(canvas, BUTTON_NAME);
        if (t == null) return false;
        var btn = t.GetComponent<Button>();
        if (btn == null)
        {
            var g = t.GetComponent<Graphic>();
            if (g == null) { var img = t.gameObject.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0f); g = img; }
            btn = t.gameObject.AddComponent<Button>();
            btn.targetGraphic = g;
        }
        // Remove any persistent listeners that already target a MissionsScreenController, then add ours.
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (btn.onClick.GetPersistentTarget(i) is MissionsScreenController)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEventTools.AddIntPersistentListener(btn.onClick, ctrl.Open, 0);
        EditorUtility.SetDirty(btn);
        return true;
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
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) return sp;
        return sprite;
    }

    private static TMP_FontAsset FindMenuFont(Transform canvas)
    {
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
