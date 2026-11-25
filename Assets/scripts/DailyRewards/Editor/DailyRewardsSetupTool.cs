#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Siege.DailyRewards;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Daily Rewards page as real scene objects (no runtime layout code).
/// Menu: Kings of Siege > Daily Rewards > Build Rewards Page (MainMenu)
///
/// Creates [DailyRewardsScreen] under the main Canvas:
///   [DailyRewardsScreen]            DailyRewardsScreenController (always active)
///     DailyRewardsPage              hidden until Open()
///       Dimmer, PanelFrame, Panel, Title, Subtitle, CloseButton
///       Day1 .. Day7                 tile containers (Glow, Frame, Body, Icon, DayLabel, Amount, Checkmark, LockOverlay)
///       ClaimButton
/// Wires the side-panel "button_reward" (already in MainMenu) to open it.
/// Edit any element in the Scene view or Inspector. Re-running the tool rebuilds the page.
/// </summary>
public static class DailyRewardsSetupTool
{
    private const string MAIN_MENU_SCENE = "Assets/Scenes/MainMenu.unity";
    private const string STORE = "Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/";
    private const string CHEST_UI = "Assets/UIPACK/Chest_UI_Pack/";
    private const string HOME = "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/";
    private const string COMPONENTS = "Assets/UIPACK/ResourcesData/Sprites/Components/";
    private const string FONT_PATH = "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp.asset";
    private const string ROOT_NAME = "[DailyRewardsScreen]";
    private const string BUTTON_NAME = "button_reward";

    private static readonly Color Gold = new Color(1f, 0.80f, 0.28f, 1f);
    private static readonly Color RoyalBlue = new Color(0.09f, 0.20f, 0.52f, 1f);
    private static readonly Color TileBlue = new Color(0.13f, 0.30f, 0.68f, 1f);
    private static readonly Color White = Color.white;
    private static readonly Color DarkBrown = new Color(0.22f, 0.11f, 0.02f, 1f);
    private static readonly Color Green = new Color(0.35f, 0.9f, 0.35f, 1f);

    [MenuItem("Kings of Siege/Daily Rewards/Build Rewards Page (MainMenu)", priority = 50)]
    public static void BuildRewardsPage()
    {
        if (!EditorUtility.DisplayDialog("Daily Rewards Builder",
                "This replaces [DailyRewardsScreen] with the basic generated layout and discards any hand-made design changes. Continue?",
                "Rebuild", "Cancel")) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MAIN_MENU_SCENE && File.Exists(MAIN_MENU_SCENE))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE, OpenSceneMode.Single);
        }

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Daily Rewards Builder", "No Canvas found in the open scene.", "OK");
            return;
        }

        var missing = new List<string>();
        Sprite S(string path) { var sp = LoadSprite(path); if (sp == null) missing.Add(path); return sp; }
        Sprite First(params string[] paths) { foreach (var p in paths) { var sp = LoadSprite(p); if (sp != null) return sp; } missing.Add(paths[0]); return null; }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH) ?? FindMenuFont(canvas.transform);

        // Sprites
        Sprite rounded = S(COMPONENTS + "Button/Button_SquareSolid01_White.png");
        Sprite circle = S(COMPONENTS + "Frame/BasicFrame_CircleSolid01_White.png");
        Sprite check = First(COMPONENTS + "IconMisc/Icon_Check.png", COMPONENTS + "IconMisc/Icon_Checkboard.Png");
        Sprite closeIcon = LoadSprite(CHEST_UI + "Icons/Close.png");
        Sprite coin = First(HOME + "Icons/coin.png", STORE + "Currency/Coin.png");
        Sprite gem = First(HOME + "Icons/gem.png", STORE + "Currency/Gem.png");
        Sprite key = First(COMPONENTS + "Icon_ItemIcons/512/ItemIcon_Key.png", COMPONENTS + "Icon_ItemIcons/256/ItemIcon_Key.Png");
        Sprite silverChest = S(STORE + "Products/Silver_Chest.png");
        Sprite goldChest = S(STORE + "Products/Gold_Chest.png");
        Sprite background = S(STORE + "Backgrounds/Shop_Blurred_Blue.png");

        // Remove old page
        var old = canvas.transform.Find(ROOT_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Root (always active so its Awake hooks the buttons)
        var root = NewRect(canvas.transform, ROOT_NAME, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Rewards Page");
        root.SetAsLastSibling();
        var ctrl = root.gameObject.AddComponent<DailyRewardsScreenController>();
        var audio = root.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // Page
        var page = NewRect(root, "DailyRewardsPage", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        page.gameObject.SetActive(false);

        var dimmer = Img(page, "Dimmer", null, new Color(0f, 0f, 0f, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dimmerCg = dimmer.gameObject.AddComponent<CanvasGroup>();
        dimmer.raycastTarget = true;

        // Blue backdrop from the shop art, faded behind the panel
        if (background != null)
        {
            var bg = Img(page, "Backdrop", background, new Color(1f, 1f, 1f, 0.35f), new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), Vector2.zero, Vector2.zero);
            bg.preserveAspect = false;
        }

        Img(page, "PanelFrame", rounded, Gold, new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.91f), Vector2.zero, Vector2.zero);
        Img(page, "Panel", rounded, RoyalBlue, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero);

        var title = Txt(page, "Title", "DAILY REWARDS", 64, Gold, font, new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.93f));
        var subtitle = Txt(page, "Subtitle", "Streak: 0 / 7", 36, White, font, new Vector2(0.1f, 0.79f), new Vector2(0.9f, 0.84f));

        // Close button
        var closeRt = NewRect(page, "CloseButton", new Vector2(0.83f, 0.85f), new Vector2(0.93f, 0.95f), Vector2.zero, Vector2.zero);
        var closeImg = closeRt.gameObject.AddComponent<Image>();
        closeImg.sprite = circle;
        closeImg.color = Gold;
        closeImg.preserveAspect = true;
        var closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        if (closeIcon != null)
        {
            var ci = Img(closeRt, "Icon", closeIcon, DarkBrown, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Vector2.zero, Vector2.zero);
            ci.preserveAspect = true;
        }
        else
        {
            Txt(closeRt, "Icon", "X", 48, DarkBrown, font, Vector2.zero, Vector2.one);
        }

        // 7 day tiles: row 1 = days 1-4, row 2 = days 5-7
        var tiles = new List<Transform>();
        for (int i = 0; i < DailyRewardsManager.DayCount; i++)
        {
            bool row0 = i < 4;
            int col = row0 ? i : i - 4;
            int n = row0 ? 4 : 3;
            float yMin = row0 ? 0.50f : 0.22f;
            float yMax = row0 ? 0.76f : 0.48f;
            float span = 0.9f / n;
            float xMin = 0.05f + col * span + 0.012f;
            float xMax = 0.05f + (col + 1) * span - 0.012f;

            var tile = NewRect(page, "Day" + (i + 1), new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);
            tiles.Add(tile);

            var glowSprite = circle != null ? circle : rounded;
            Img(tile, "Glow", glowSprite, new Color(1f, 0.85f, 0.3f, 0.5f), Vector2.zero, Vector2.one, new Vector2(-14, -14), new Vector2(14, 14));
            Img(tile, "Frame", rounded, Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Img(tile, "Body", rounded, TileBlue, Vector2.zero, Vector2.one, new Vector2(7, 7), new Vector2(-7, -7));
            Txt(tile, "DayLabel", "Day " + (i + 1), 34, Gold, font, new Vector2(0f, 0.82f), new Vector2(1f, 0.97f));
            Img(tile, "Icon", SpriteForDay(i, coin, gem, key, silverChest, goldChest), White, new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero).preserveAspect = true;
            Txt(tile, "Amount", "", 36, White, font, new Vector2(0f, 0.22f), new Vector2(1f, 0.38f));
            var checkImg = Img(tile, "Checkmark", check, Green, new Vector2(0.28f, 0.30f), new Vector2(0.72f, 0.70f), Vector2.zero, Vector2.zero);
            checkImg.preserveAspect = true;
            checkImg.gameObject.SetActive(false);
            var lockImg = Img(tile, "LockOverlay", rounded, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one, new Vector2(7, 7), new Vector2(-7, -7));
            lockImg.gameObject.SetActive(false);
        }

        // Claim button
        var claimRt = NewRect(page, "ClaimButton", new Vector2(0.28f, 0.08f), new Vector2(0.72f, 0.17f), Vector2.zero, Vector2.zero);
        var claimImg = claimRt.gameObject.AddComponent<Image>();
        claimImg.sprite = rounded;
        claimImg.color = Gold;
        var claimBtn = claimRt.gameObject.AddComponent<Button>();
        claimBtn.targetGraphic = claimImg;
        var claimLabel = Txt(claimRt, "Label", "CLAIM", 44, DarkBrown, font, Vector2.zero, Vector2.one);

        // Wire controller fields through SerializedObject so they persist
        var so = new SerializedObject(ctrl);
        so.FindProperty("pageRoot").objectReferenceValue = page.gameObject;
        so.FindProperty("dimmer").objectReferenceValue = dimmerCg;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
        so.FindProperty("claimButton").objectReferenceValue = claimBtn;
        so.FindProperty("claimLabel").objectReferenceValue = claimLabel;
        so.FindProperty("audioSource").objectReferenceValue = audio;

        var tilesProp = so.FindProperty("tiles");
        tilesProp.arraySize = tiles.Count;
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var el = tilesProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("frame").objectReferenceValue = t.Find("Frame").GetComponent<Image>();
            el.FindPropertyRelative("body").objectReferenceValue = t.Find("Body").GetComponent<Image>();
            el.FindPropertyRelative("icon").objectReferenceValue = t.Find("Icon").GetComponent<Image>();
            el.FindPropertyRelative("dayLabel").objectReferenceValue = t.Find("DayLabel").GetComponent<TMP_Text>();
            el.FindPropertyRelative("amountLabel").objectReferenceValue = t.Find("Amount").GetComponent<TMP_Text>();
            el.FindPropertyRelative("checkmark").objectReferenceValue = t.Find("Checkmark").gameObject;
            el.FindPropertyRelative("claimedOverlay").objectReferenceValue = t.Find("LockOverlay").gameObject;
            el.FindPropertyRelative("glow").objectReferenceValue = t.Find("Glow").GetComponent<Image>();
        }

        // Default 7-day reward table (edit in the Inspector on [DailyRewardsScreen])
        var rewards = new (string label, DailyRewardKind kind, int amount, Sprite icon)[]
        {
            ("Gems",        DailyRewardKind.Gems,        5,  gem),
            ("Coins",       DailyRewardKind.Coins,       500, coin),
            ("Chest Key",   DailyRewardKind.ChestKey,    1,  key),
            ("Gems",        DailyRewardKind.Gems,        10, gem),
            ("Coins",       DailyRewardKind.Coins,       1000, coin),
            ("Silver Chest",DailyRewardKind.SilverChest, 1,  silverChest),
            ("Gold Chest + 25 Gems", DailyRewardKind.GoldChest, 1, goldChest),
        };
        var rewardsProp = so.FindProperty("rewards");
        rewardsProp.arraySize = rewards.Length;
        for (int i = 0; i < rewards.Length; i++)
        {
            var el = rewardsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("label").stringValue = rewards[i].label;
            el.FindPropertyRelative("kind").enumValueIndex = (int)rewards[i].kind;
            el.FindPropertyRelative("amount").intValue = rewards[i].amount;
            el.FindPropertyRelative("icon").objectReferenceValue = rewards[i].icon;
        }
        // Day 7 also gives 25 gems (bonusGems on that entry)
        rewardsProp.GetArrayElementAtIndex(6).FindPropertyRelative("bonusGems").intValue = 25;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Wire the side-panel button
        bool wired = WireOpenButton(canvas.transform, ctrl);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;

        string msg = "Daily Rewards page built under '" + canvas.name + "' in '" + root.gameObject.scene.name + "'." +
                     "\nSelect [DailyRewardsScreen] to edit tiles, rewards and colours.";
        if (!wired) msg += "\n\nSide-panel '" + BUTTON_NAME + "' not found: wire a button's OnClick to DailyRewardsScreenController.Open.";
        if (missing.Count > 0) msg += "\n\nMissing sprites (these fall back to plain colour): " + string.Join(", ", missing);
        Debug.Log("<color=#7CFC00>[DailyRewardsSetupTool]</color> " + msg);
        EditorUtility.DisplayDialog("Daily Rewards Builder", msg, "Nice");
    }

    [MenuItem("Kings of Siege/Daily Rewards/Remove Rewards Page", priority = 51)]
    public static void RemoveRewardsPage()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) return;
        var t = canvas.transform.Find(ROOT_NAME);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Debug.Log("[DailyRewardsSetupTool] Daily Rewards page removed.");
    }

    [MenuItem("Kings of Siege/Daily Rewards/Debug: Reset Streak")]
    public static void DebugResetStreak()
    {
        DailyRewardsManager.Reset();
        Debug.Log("[DailyRewardsSetupTool] Daily streak reset.");
    }

    [MenuItem("Kings of Siege/Daily Rewards/Debug: Advance One Day")]
    public static void DebugAdvanceDay()
    {
        DailyRewardsManager.DebugAdvanceOneDay();
        Debug.Log("[DailyRewardsSetupTool] Pretended one day passed; tomorrow's reward should be claimable.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static Sprite SpriteForDay(int i, Sprite coin, Sprite gem, Sprite key, Sprite silver, Sprite gold)
    {
        switch (i)
        {
            case 0: return gem;
            case 1: return coin;
            case 2: return key;
            case 3: return gem;
            case 4: return coin;
            case 5: return silver;
            default: return gold;
        }
    }

    private static bool WireOpenButton(Transform canvas, DailyRewardsScreenController ctrl)
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
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (btn.onClick.GetPersistentTarget(i) is DailyRewardsScreenController)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEventTools.AddVoidPersistentListener(btn.onClick, ctrl.Open);
        EditorUtility.SetDirty(btn);
        return true;
    }

    private static RectTransform NewRect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return rt;
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var rt = NewRect(parent, name, aMin, aMax, offMin, offMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Txt(Transform parent, string name, string text, float size, Color color, TMP_FontAsset font, Vector2 aMin, Vector2 aMax)
    {
        var rt = NewRect(parent, name, aMin, aMax, Vector2.zero, Vector2.zero);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
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
