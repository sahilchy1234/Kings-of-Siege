using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The indicator creates itself at runtime in any scene that has a BattleSceneSetup, so the game needs
/// no setup at all. This tool only adds the component to the open battle scene, which is the way to see
/// and tune its thresholds in the Inspector (timings, how big a shift has to be, sound on/off).
/// </summary>
public static class TerritoryShiftIndicatorSetupTool
{
    [MenuItem("Kings of Siege/Battle/Add Territory Border Indicator (tunable)", false, 1)]
    private static void AddToScene()
    {
        var arena = Object.FindFirstObjectByType<BattleSceneSetup>();
        if (arena == null)
        {
            EditorUtility.DisplayDialog(
                "Territory Border Indicator",
                "No BattleSceneSetup in the open scene.\n\nOpen the battle scene first. You only need this tool to tune the " +
                "indicator in the Inspector: in play mode it creates itself with its default settings.",
                "OK");
            return;
        }

        var existing = Object.FindFirstObjectByType<TerritoryShiftIndicator>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[TerritoryShiftIndicator] Already in this scene on '" + existing.gameObject.name + "'. Selected it for you.");
            return;
        }

        var added = Undo.AddComponent<TerritoryShiftIndicator>(arena.gameObject);
        Selection.activeGameObject = arena.gameObject;
        EditorGUIUtility.PingObject(added);
        EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);

        Debug.Log("[TerritoryShiftIndicator] Added to '" + arena.gameObject.name +
                  "'. Tune it in the Inspector, then save the scene. Leave it off and it still runs with defaults.");
    }

    [MenuItem("Kings of Siege/Battle/Remove Territory Border Indicator", false, 2)]
    private static void RemoveFromScene()
    {
        var existing = Object.FindFirstObjectByType<TerritoryShiftIndicator>();
        if (existing == null)
        {
            Debug.Log("[TerritoryShiftIndicator] Nothing to remove: no instance in the open scene.");
            return;
        }

        var scene = existing.gameObject.scene;
        if (existing.gameObject.name == "[TerritoryShiftIndicator]") Undo.DestroyObjectImmediate(existing.gameObject);
        else Undo.DestroyObjectImmediate(existing);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[TerritoryShiftIndicator] Removed from the scene. It will still create itself at runtime with defaults; " +
                  "to switch it off entirely, add it back and untick Show Ground Sweep and Show Floating Label.");
    }
}

