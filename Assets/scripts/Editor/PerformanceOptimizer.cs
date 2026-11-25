#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Siege.Performance
{
    /// <summary>
    /// One-click editor passes for performance. Menu: Kings of Siege > Performance.
    /// Every pass asks for confirmation, edits the scene file and saves it.
    /// </summary>
    public static class PerformanceOptimizer
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string BattleScenePath = "Assets/Scenes/SampleScene 1/BattleScene.unity";

        // Root-level UI panels that were baked into the scene more than once.
        static readonly string[] DuplicatePanelNames = { "[KingdomRoadScreen]" };

        [MenuItem("Kings of Siege/Performance/Optimize MainMenu Scene", false, 100)]
        public static void OptimizeMainMenu()
        {
            if (!EditorUtility.DisplayDialog("Optimize MainMenu",
                "This removes unused duplicate UI panels and turns off raycast targets on decorative UI in MainMenu.unity, then saves the scene. Continue?",
                "Optimize", "Cancel"))
                return;

            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            int removed = RemoveInactiveDuplicatePanels(scene);
            int raycasts = DisableRaycastOnDecorativeGraphics(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Performance] MainMenu: removed {removed} duplicate panel(s), disabled raycast on {raycasts} decorative graphic(s).");
        }

        [MenuItem("Kings of Siege/Performance/Report Scene Hotspots (MainMenu + BattleScene)", false, 101)]
        public static void ReportHotspots()
        {
            ReportScene(MainMenuScenePath);
            ReportScene(BattleScenePath);
        }

        [MenuItem("Kings of Siege/Performance/Android: Compress Automatic Textures (ASTC 6x6)", false, 102)]
        public static void CompressAndroidTextures()
        {
            if (!EditorUtility.DisplayDialog("Compress Textures",
                "For every texture in Assets/ whose Android override is still Automatic, this sets ASTC 6x6 and caps the size at 2048. Reimporting can take a long time. Continue?",
                "Compress", "Cancel"))
                return;

            int changed = 0, skipped = 0;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Compressing textures", path, (float)i / guids.Length);

                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) { skipped++; continue; }

                    var android = imp.GetPlatformTextureSettings("Android");
                    // Respect textures someone already set on purpose.
                    if (android.overridden && android.format != TextureImporterFormat.Automatic) { skipped++; continue; }

                    android.overridden = true;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    android.maxTextureSize = Mathf.Min(android.maxTextureSize, 2048);
                    android.compressionQuality = 50;
                    imp.SetPlatformTextureSettings(android);
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[Performance] Android textures: {changed} set to ASTC 6x6, {skipped} skipped.");
        }

        // ---------------------------------------------------------------

        static int RemoveInactiveDuplicatePanels(Scene scene)
        {
            int removed = 0;
            var transforms = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .ToList();

            foreach (var parent in transforms.Select(t => t.parent).Where(p => p != null).Distinct().ToList())
            {
                foreach (var name in DuplicatePanelNames)
                {
                    var same = Enumerable.Range(0, parent.childCount)
                        .Select(i => parent.GetChild(i).gameObject)
                        .Where(g => g.name == name)
                        .ToList();

                    if (same.Count < 2 || !same.Any(g => g.activeSelf)) continue;

                    foreach (var dup in same.Where(g => !g.activeSelf))
                    {
                        Undo.DestroyObjectImmediate(dup);
                        removed++;
                    }
                }
            }
            return removed;
        }

        static int DisableRaycastOnDecorativeGraphics(Scene scene)
        {
            int changed = 0;
            var graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var g in graphics)
            {
                if (g.gameObject.scene != scene || !g.raycastTarget) continue;
                if (IsInteractive(g.transform)) continue;

                Undo.RecordObject(g, "Disable raycast target");
                g.raycastTarget = false;
                changed++;
            }
            return changed;
        }

        // A graphic is interactive if it, or any ancestor, can receive pointer events.
        static bool IsInteractive(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
            {
                foreach (var c in cur.GetComponents<Component>())
                {
                    if (c is Selectable || c is IEventSystemHandler) return true;
                }
            }
            return false;
        }

        static void ReportScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene == scene).ToList();
            int inactive = all.Count(t => !t.gameObject.activeInHierarchy);
            int graphics = all.Count(t => t.GetComponent<Graphic>() != null);
            int raycasting = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(g => g.gameObject.scene == scene && g.raycastTarget);
            int animators = all.Count(t => t.GetComponent<Animator>() != null);
            int particles = all.Count(t => t.GetComponent<ParticleSystem>() != null);
            int canvases = all.Count(t => t.GetComponent<Canvas>() != null);

            Debug.Log($"[Performance] {scenePath}: objects={all.Count}, inactive={inactive}, canvases={canvases}, graphics={graphics}, raycastTargets={raycasting}, animators={animators}, particleSystems={particles}");
        }
    }
}
#endif
