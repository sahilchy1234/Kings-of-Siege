using System.Collections.Generic;
using Siege.Profile;
using Siege.Rankings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kings of Siege > Rankings > Build Rankings Page (MainMenu)
/// Adds [RankingsPage] under the Canvas, just below the bottom nav (downpanel), wires fonts and all sprites
/// from Assets/UIPACK, and saves the scene. Safe to run again: it re-adds the controller each time.
/// </summary>
public static class RankingsSetupTool
{
    private const string PageName = "[RankingsPage]";
    private const string UI = "Assets/UIPACK/";
    private const string HOME = UI + "Kings_of_Siege_Home_UI_PNGs/";
    private const string STORE = UI + "Kings_of_Siege_Store_PNG_Elements/";
    private const string VS = UI + "battlevs/arena_matchmaking_transparent_sprites/";
    private const string VS_PNG = UI + "battlevs/png/";
    private const string COMP = UI + "ResourcesData/Sprites/Components/";

    // Same order as ProfileSetupTool, so PlayerProfileData.AvatarIndex picks the same picture
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

    private static readonly string[] FlagCodes =
    {
        "Chn", "Deu", "Eng", "Esp", "Fra", "Gre", "Ina", "Ita", "Jpn", "Kor", "Pol", "Prt", "Rus", "Swe", "Tha", "Tur"
    };

    [MenuItem("Kings of Siege/Rankings/Build Rankings Page (MainMenu)")]
    public static void BuildRankingsPage()
    {
        var canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Rankings", "No Canvas found in the open scene. Open MainMenu first.", "OK");
            return;
        }

        var existing = canvas.transform.Find(PageName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(PageName, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        // Remove anything an older version left behind
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var c in go.GetComponents<Component>())
        {
            if (c is RectTransform) continue;
            if (c is RankingsController) continue;
            Object.DestroyImmediate(c);
        }

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        // Re-add the controller so a stale instance can't keep its old state
        var old = go.GetComponent<RankingsController>();
        if (old != null) Object.DestroyImmediate(old);
        var ctrl = go.AddComponent<RankingsController>();
        ctrl.titleFont = LoadFont("TMP_TiltWarp");
        ctrl.outlineFont = LoadFont("TMP_TiltWarp_Outline");
        ctrl.rankingsTabIndex = 3;   // downpanel order: Shop, Cards, Home, Rankings
        ctrl.hookBottomNav = true;
        ctrl.listSize = 50;
        ctrl.art = BuildArt(out int missing);

        // Sit below the nav bar so the tabs stay tappable
        var nav = FindDeep(canvas.transform, "downpanel");
        if (nav != null && nav.parent == canvas.transform) go.transform.SetSiblingIndex(nav.GetSiblingIndex());

        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);

        Debug.Log("[Rankings] Rankings page built on " + go.scene.name +
                  ". Fonts: " + (ctrl.titleFont != null ? ctrl.titleFont.name : "default") + " / " + (ctrl.outlineFont != null ? ctrl.outlineFont.name : "default") +
                  ". Missing sprites: " + missing);
    }

    private static RankingsArt BuildArt(out int missing)
    {
        int miss = 0;
        Sprite L(string path, Vector4 border = default)
        {
            var s = LoadSprite(path, border);
            if (s == null) { miss++; Debug.LogWarning("[Rankings] Sprite not found: " + path); }
            return s;
        }

        var art = new RankingsArt
        {
            background = L(STORE + "Backgrounds/Shop_Blurred_Blue.png"),
            ribbon = L(COMP + "Label/Label_Ribbon01_Yellow.png"),
            backButton = L(HOME + "Panels/shortcut_button.png"),
            backIcon = L(COMP + "IconMisc/Icon_Arrow_Back.png"),
            seasonPanel = L(VS + "Panels/wide_status_panel.png", new Vector4(46, 42, 46, 42)),
            timerIcon = L(COMP + "IconMisc/Icon_Timer.png"),
            kingsBadge = L(UI + "badges/Kings.png"),

            tabOn = L(COMP + "Button/Button01_Demo_Yellow.png", new Vector4(28, 50, 28, 100)),
            tabOff = L(COMP + "Button/Button01_Demo_Blue.png", new Vector4(28, 50, 28, 100)),
            tabGlobalIcon = L(COMP + "IconMisc/Icon_Trophy_l.png"),
            tabLocalIcon = L(COMP + "IconMisc/Icon_Flag.png"),
            tabClansIcon = L(COMP + "IconMisc/Icon_Shield01.png"),

            trophy = L(VS + "Icons/trophy.png"),
            crown = L(VS + "Icons/crown.png"),
            laurel = L(VS + "Icons/laurel_wreath.png"),
            glow = L(VS + "Effects/gold_glow_orb.png"),
            medalGold = L(VS_PNG + "level_medallion_gold_blank.png"),
            medalSilver = L(VS_PNG + "level_medallion_silver_blank.png"),

            rowPanel = L(VS + "Panels/wide_status_panel.png", new Vector4(46, 42, 46, 42)),
            selfPanel = L(COMP + "Frame/ListFrame01_White1.png", new Vector4(42, 42, 42, 42)),
            trophyPill = L(VS + "Panels/inner_status_slot.png"),
            clanShield = L(COMP + "IconMisc/Icon_Shield01.png"),
        };

        var avatars = new List<Sprite>();
        foreach (var p in AvatarPaths) avatars.Add(L(p));
        art.avatars = avatars.ToArray();

        var badges = new List<Sprite>();
        foreach (var n in PlayerProfileData.RankBadgeFileNames) badges.Add(L(UI + "badges/" + n + ".png"));
        art.rankBadges = badges.ToArray();

        var flags = new List<Sprite>();
        foreach (var code in FlagCodes) flags.Add(L(COMP + "Icon_Flag/Icon_Flag_" + code + ".png"));
        art.flags = flags.ToArray();

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

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
