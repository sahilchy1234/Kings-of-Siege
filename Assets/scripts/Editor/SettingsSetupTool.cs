using Siege.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kings of Siege > Settings > Build Settings Panel (MainMenu)
/// Adds [SettingsPanel] as the last child of the Canvas, wires fonts and sprites from Assets/UIPACK,
/// and saves the scene. The panel opens from the gear button in the top bar. Safe to run again.
/// </summary>
public static class SettingsSetupTool
{
    private const string PanelName = "[SettingsPanel]";
    private const string UI = "Assets/UIPACK/";
    private const string HOME = UI + "Kings_of_Siege_Home_UI_PNGs/";
    private const string VS = UI + "battlevs/arena_matchmaking_transparent_sprites/";
    private const string COMP = UI + "ResourcesData/Sprites/Components/";

    [MenuItem("Kings of Siege/Settings/Build Settings Panel (MainMenu)")]
    public static void BuildSettingsPanel()
    {
        var canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Settings", "No Canvas found in the open scene. Open MainMenu first.", "OK");
            return;
        }

        var existing = canvas.transform.Find(PanelName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(PanelName, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var c in go.GetComponents<Component>())
        {
            if (c is RectTransform) continue;
            Object.DestroyImmediate(c);
        }

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        go.transform.SetAsLastSibling();

        var ctrl = go.AddComponent<SettingsController>();
        ctrl.titleFont = LoadFont("TMP_TiltWarp");
        ctrl.outlineFont = LoadFont("TMP_TiltWarp_Outline");
        ctrl.hookSettingsButton = true;
        ctrl.settingsButtonName = "settings";
        ctrl.art = BuildArt(out int missing);

        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);

        Debug.Log("[Settings] Settings panel built on " + go.scene.name +
                  ". Fonts: " + (ctrl.titleFont != null ? ctrl.titleFont.name : "default") + " / " + (ctrl.outlineFont != null ? ctrl.outlineFont.name : "default") +
                  ". Missing sprites: " + missing);
    }

    private static SettingsArt BuildArt(out int missing)
    {
        int miss = 0;
        Sprite L(string path, Vector4 border = default)
        {
            var s = LoadSprite(path, border);
            if (s == null) { miss++; Debug.LogWarning("[Settings] Sprite not found: " + path); }
            return s;
        }

        var art = new SettingsArt
        {
            panel = L(VS + "Panels/wide_status_panel.png", new Vector4(46, 42, 46, 42)),
            ribbon = L(COMP + "Label/Label_Ribbon01_Yellow.png"),
            gear = L(HOME + "Icons/settings.png"),
            buttonBlue = L(COMP + "Button/Button01_Demo_Blue.png", new Vector4(28, 50, 28, 100)),
            buttonRed = L(COMP + "Button/Button01_Demo_Red.png", new Vector4(28, 50, 28, 100)),

            iconMusic = L(COMP + "IconMisc/Icon_Music.png"),
            iconSound = L(COMP + "IconMisc/Icon_Sound.png"),
            iconVibration = L(COMP + "IconMisc/Icon_Bell.png"),
            iconGraphics = L(COMP + "IconMisc/Icon_Picture.png"),
            iconFps = L(COMP + "IconMisc/Icon_Timer.png"),
            iconName = L(COMP + "IconMisc/Icon_Body.png"),
            iconId = L(COMP + "IconMisc/Icon_Ticket.png"),
            iconLanguage = L(COMP + "IconMisc/Icon_Flag.png"),
            iconPencil = L(COMP + "IconMisc/Icon_Pencil.png"),
            iconClose = L(COMP + "IconMisc/Icon_Close01.png"),
            iconCredits = L(COMP + "IconMisc/Icon_Book.png"),
            iconReset = L(COMP + "IconMisc/Icon_Arrow_Back.png"),
            iconCheck = L(COMP + "IconMisc/Icon_Check.png"),
            flagEnglish = L(COMP + "Icon_Flag/Icon_Flag_Eng.png"),
        };
        missing = miss;
        return art;
    }

    /// <summary>Loads a sprite, switching the texture to Sprite import (and setting 9-slice borders) when needed.</summary>
    private static Sprite LoadSprite(string path, Vector4 border = default)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            changed = true;
        }
        if (border.sqrMagnitude > 0f && importer.spriteBorder.sqrMagnitude <= 0f)
        {
            importer.spriteBorder = border;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TMP_FontAsset LoadFont(string name)
    {
        var path = UI + "ResourcesData/Fonts/" + name + ".asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:TMP_FontAsset"))
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null && f.name == name) return f;
        }
        return null;
    }

    private static Canvas FindMainCanvas()
    {
        Canvas first = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.transform.parent != null) continue;
            if (c.name == "Canvas") return c;
            if (first == null) first = c;
        }
        return first;
    }
}
