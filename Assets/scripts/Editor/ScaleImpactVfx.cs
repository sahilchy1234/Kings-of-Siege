using UnityEditor;
using UnityEngine;

// The Resources impact prefabs (ArrowHit_*) spawn correctly on device but their particles are
// ~0.15 world units, which is a few pixels from the battle camera. This scales their start size.
public static class ScaleImpactVfx
{
    const float Factor = 5f;
    static readonly string[] Prefabs =
    {
        "Assets/Resources/Effects/ArrowHit_Dust.prefab",
        "Assets/Resources/Effects/ArrowHit_Wood.prefab",
    };

    [MenuItem("Tools/Build/Scale Up Impact VFX")]
    public static void Run()
    {
        foreach (var path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { Debug.LogWarning("[ScaleImpactVfx] missing " + path); continue; }
            int n = 0;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startSizeMultiplier *= Factor;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.renderMode == ParticleSystemRenderMode.Mesh) { /* mesh chips: size already applied via startSize */ }
                n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[ScaleImpactVfx] " + path + ": scaled " + n + " systems x" + Factor);
        }
    }
}
