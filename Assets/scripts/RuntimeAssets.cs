using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drop-in replacement for the UnityEditor.AssetDatabase lookups used by runtime scripts
/// (FindAssets / GUIDToAssetPath / LoadAssetAtPath).
///
/// In the editor it calls AssetDatabase directly, so behaviour is unchanged.
/// In player builds (Android) AssetDatabase does not exist, so it reads the same assets from
/// Resources/RuntimeAssetRegistry, which the editor bakes automatically before every build
/// (see Editor/RuntimeAssetRegistryBaker.cs). Any query string or "Assets/..." path that appears
/// in a runtime script is baked, so new lookups work on device without extra setup.
/// </summary>
public static class RuntimeAssets
{
    const string KeySep = "\n#";

#if !UNITY_EDITOR
    static RuntimeAssetRegistry s_registry;
    static Dictionary<string, RuntimeAssetRegistry.Entry> s_map;

    static Dictionary<string, RuntimeAssetRegistry.Entry> Map
    {
        get
        {
            if (s_map != null) return s_map;
            s_map = new Dictionary<string, RuntimeAssetRegistry.Entry>(StringComparer.Ordinal);
            s_registry = Resources.Load<RuntimeAssetRegistry>(RuntimeAssetRegistry.ResourcePath);
            if (s_registry == null)
            {
                Debug.LogWarning("[RuntimeAssets] RuntimeAssetRegistry not found in Resources; editor-only lookups will return nothing.");
                return s_map;
            }
            foreach (var e in s_registry.entries)
                if (e != null && !string.IsNullOrEmpty(e.key)) s_map[e.key] = e;
            return s_map;
        }
    }
#endif

    /// <summary>Same contract as AssetDatabase.FindAssets: returns ids to pass to GUIDToAssetPath.</summary>
    public static string[] FindAssets(string filter)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.FindAssets(filter);
#else
        var list = new List<string>();
        for (int i = 0; ; i++)
        {
            string k = filter + KeySep + i;
            if (!Map.ContainsKey(k)) break;
            list.Add(k);
        }
        return list.ToArray();
#endif
    }

    public static string[] FindAssets(string filter, string[] searchInFolders)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.FindAssets(filter, searchInFolders);
#else
        return FindAssets(filter);
#endif
    }

    /// <summary>In builds the "guid" is already the registry key, so it is returned unchanged.</summary>
    public static string GUIDToAssetPath(string guid)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
#else
        return guid;
#endif
    }

    public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return LoadAssetAtPath(path, typeof(T)) as T;
#endif
    }

    public static UnityEngine.Object LoadAssetAtPath(string path, Type type)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
#else
        if (string.IsNullOrEmpty(path) || !Map.TryGetValue(path, out var e) || e.objects == null) return null;
        foreach (var o in e.objects)
        {
            if (o == null) continue;
            if (type.IsInstanceOfType(o)) return o;
            if (o is GameObject go && typeof(Component).IsAssignableFrom(type))
            {
                var c = go.GetComponent(type);
                if (c != null) return c;
            }
        }
        return null;
#endif
    }

    public static string MakeFindKey(string filter, int index) => filter + KeySep + index;
}
