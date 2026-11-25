using Siege.Tutorial;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Siege.Tutorial.EditorTools
{
    /// <summary>
    /// Kings of Siege > Tutorial menu. The tutorial starts itself at runtime (no scene objects), so the only
    /// setup is the art asset in Resources. The other items help test each part of the tutorial.
    /// </summary>
    public static class TutorialSetupTool
    {
        private const string Folder = "Assets/Resources/Tutorial";
        private const string ArtPath = Folder + "/KoSTutorialArt.asset";
        private const string HandPath = Folder + "/kos_tutorial_hand.png";

        [MenuItem("Kings of Siege/Tutorial/Build Tutorial Assets", false, 1)]
        public static void BuildAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Tutorial");

            // Hand pointer: sprite import with the pivot on the fingertip.
            var ti = AssetImporter.GetAtPath(HandPath) as TextureImporter;
            if (ti != null)
            {
                bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || !ti.alphaIsTransparency;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                if (settings.spriteAlignment != (int)SpriteAlignment.Custom) dirty = true;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.45f, 0.95f);
                ti.SetTextureSettings(settings);
                if (dirty) ti.SaveAndReimport();
            }
            else Debug.LogWarning("[Tutorial] Hand sprite missing at " + HandPath + ". A plain dot is used instead.");

            var art = AssetDatabase.LoadAssetAtPath<KoSTutorialArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<KoSTutorialArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            int missing = 0;
            art.hand = AssetDatabase.LoadAssetAtPath<Sprite>(HandPath);
            if (art.hand == null) missing++;
            art.advisor = Load<Sprite>(ref missing, "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Characters/king.png", "king t:Sprite");
            art.nameRibbon = Load<Sprite>(ref missing, "Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Yellow.png", "Label_Ribbon01_Yellow t:Sprite");
            art.font = Load<TMP_FontAsset>(ref missing, "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp.asset", "TMP_TiltWarp t:TMP_FontAsset");
            art.outlineFont = Load<TMP_FontAsset>(ref missing, "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset", "TMP_TiltWarp_Outline t:TMP_FontAsset");
            const string VS = "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/";
            const string COMP = "Assets/UIPACK/ResourcesData/Sprites/Components/";
            var pill = new Vector4(28, 50, 28, 100);
            art.dialogPanel = LoadSprite(ref missing, VS + "Panels/wide_status_panel.png", new Vector4(46, 42, 46, 42));
            art.sparkle = LoadSprite(ref missing, VS + "Effects/gold_glow_orb.png", Vector4.zero);
            art.actionRibbon = LoadSprite(ref missing, COMP + "Label/Label_Ribbon01_Green.png", Vector4.zero);
            art.buttonYellow = LoadSprite(ref missing, COMP + "Button/Button01_Demo_Yellow.png", pill);
            art.buttonGreen = LoadSprite(ref missing, COMP + "Button/Button01_Demo_Green.png", pill);
            art.buttonRed = LoadSprite(ref missing, COMP + "Button/Button01_Demo_Red.png", pill);
            art.buttonNavy = LoadSprite(ref missing, COMP + "Button/Button01_Demo_Navy.png", pill);
            if (string.IsNullOrEmpty(art.advisorName)) art.advisorName = "The King";

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Selection.activeObject = art;
            Debug.Log("<color=#00FFAA>[Tutorial] Tutorial art ready at " + ArtPath + (missing > 0 ? " (missing: " + missing + ", generated shapes are used instead)" : "") + ".</color>");
        }

        private static T Load<T>(ref int missing, string path, string search) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                var g = AssetDatabase.FindAssets(search);
                if (g.Length > 0) a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g[0]));
            }
            if (a == null) missing++;
            return a;
        }

        /// <summary>Loads a UIPACK sprite, switching it to Sprite import and setting 9-slice borders when they are missing.</summary>
        private static Sprite LoadSprite(ref int missing, string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { missing++; Debug.LogWarning("[Tutorial] Sprite not found: " + path); return null; }
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
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) missing++;
            return s;
        }

        [MenuItem("Kings of Siege/Tutorial/Reset Tutorial (play it again from the start)", false, 20)]
        public static void ResetTutorial()
        {
            PlayerPrefs.SetInt(KoSTutorial.StageKey, KoSTutorial.StageNotStarted);
            PlayerPrefs.DeleteKey(KoSTutorial.RewardKey);
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] Reset. It starts the next time the MainMenu loads.");
            if (Application.isPlaying) KoSTutorial.Replay();
        }

        [MenuItem("Kings of Siege/Tutorial/Jump To Battle Part", false, 21)]
        public static void JumpBattle() => SetStage(KoSTutorial.StageBattlePending, "Next battle vs AI runs the battle tutorial (the menu points at BATTLE).");

        [MenuItem("Kings of Siege/Tutorial/Jump To Reward Part", false, 22)]
        public static void JumpOutro() => SetStage(KoSTutorial.StageOutroPending, "Next MainMenu load shows the welcome gift.");

        [MenuItem("Kings of Siege/Tutorial/Mark Tutorial Complete", false, 23)]
        public static void Complete() => SetStage(KoSTutorial.StageComplete, "Tutorial will not show again.");

        [MenuItem("Kings of Siege/Tutorial/Advance Step (Play Mode) %#t", false, 30)]
        public static void Advance()
        {
            var o = Object.FindFirstObjectByType<TutorialOverlay>();
            if (o != null) o.ForceAdvance();
        }

        [MenuItem("Kings of Siege/Tutorial/Advance Step (Play Mode) %#t", true)]
        public static bool AdvanceValid() => Application.isPlaying;

        [MenuItem("Kings of Siege/Tutorial/Log Current Stage", false, 40)]
        public static void LogStage()
        {
            int s = PlayerPrefs.GetInt(KoSTutorial.StageKey, 0);
            string[] names = { "Not started", "Menu done, battle pending", "Battle done, reward pending", "Complete" };
            Debug.Log("[Tutorial] Stage " + s + ": " + names[Mathf.Clamp(s, 0, 3)] + ", rewarded=" + PlayerPrefs.GetInt(KoSTutorial.RewardKey, 0));
        }

        private static void SetStage(int stage, string msg)
        {
            PlayerPrefs.SetInt(KoSTutorial.StageKey, stage);
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] Stage set to " + stage + ". " + msg);
        }
    }
}
