using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Scans every runtime script (outside Editor folders) for the asset lookups that go through
/// RuntimeAssets (FindAssets("..."), LoadAssetAtPath&lt;T&gt;("..."), "Assets/..." path literals and
/// $"...{x}..." query templates) and bakes the matching assets into
/// Resources/RuntimeAssetRegistry.asset, so those lookups also work in Android builds.
/// Runs automatically before every build; can also be run from Tools/Build.
/// </summary>
public class RuntimeAssetRegistryBaker : IPreprocessBuildWithReport
{
    const string AssetPath = "Assets/Resources/RuntimeAssetRegistry.asset";
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Bake();

    static readonly Regex FindLiteral = new Regex(@"FindAssets\(\s*""((?:[^""\\]|\\.)*)""");
    static readonly Regex FindTemplate = new Regex(@"\$""((?:[^""\\]|\\.)*\{[^""]*\}(?:[^""\\]|\\.)*)""");
    static readonly Regex LoadLiteral = new Regex(@"LoadAssetAtPath<\s*([A-Za-z_][\w\.]*)\s*>\(\s*""((?:[^""\\]|\\.)*)""");
    static readonly Regex AnyLiteral = new Regex(@"""((?:[^""\\]|\\.)*)""");
    static readonly Regex Placeholder = new Regex(@"\{[^}]*\}");

    [MenuItem("Tools/Build/Bake Runtime Asset Registry")]
    public static void Bake()
    {
        s_nameIndex.Clear();
        var scripts = Directory.GetFiles("Assets/scripts", "*.cs", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/Editor/"))
            .ToList();

        var map = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.Ordinal);
        var enumNames = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name == "Assembly-CSharp")
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
            .Where(t => t.IsEnum)
            .SelectMany(t => Enum.GetNames(t))
            .Distinct().ToList();

        foreach (var file in scripts)
        {
            string src = File.ReadAllText(file);
            if (!src.Contains("AssetDatabase") && !src.Contains("RuntimeAssets")) continue;

            var literals = AnyLiteral.Matches(src).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

            // FindAssets("literal query")
            foreach (Match m in FindLiteral.Matches(src)) AddFind(map, m.Groups[1].Value);

            // $"...{x}..." query templates: try every identifier-like string literal in the file and
            // every enum name, matched against an index of asset file names (no FindAssets per guess).
            foreach (Match m in FindTemplate.Matches(src))
            {
                string tpl = m.Groups[1].Value;
                var tm = Regex.Match(tpl, @"t:(\w+)");
                if (!tm.Success) continue;
                string typeName = tm.Groups[1].Value;
                var parts = Placeholder.Split(tpl);
                if (parts.Length != 2) continue; // one placeholder only
                // name part = template with the "t:Type" token removed
                string pre = parts[0].Replace("t:" + typeName, "").Trim();
                string post = parts[1].Replace("t:" + typeName, "").Trim();
                var index = NameIndex(typeName);
                foreach (var cand in literals.Concat(enumNames))
                {
                    if (string.IsNullOrEmpty(cand) || cand.Length > 80 || !SafeName.IsMatch(cand)) continue;
                    if (!index.TryGetValue(pre + cand + post, out var assetPath)) continue;
                    string key = RuntimeAssets.MakeFindKey(parts[0] + cand + parts[1], 0);
                    if (!map.ContainsKey(key)) map[key] = ObjectsAt(assetPath, null);
                }
            }

            // LoadAssetAtPath<T>("Assets/...")
            foreach (Match m in LoadLiteral.Matches(src))
            {
                var type = ResolveType(m.Groups[1].Value);
                AddPath(map, m.Groups[2].Value, type);
            }

            // Any other "Assets/..." literal (paths stored in variables first).
            foreach (var lit in literals)
                if (lit.StartsWith("Assets/") && Path.HasExtension(lit)) AddPath(map, lit, null);
        }

        var reg = AssetDatabase.LoadAssetAtPath<RuntimeAssetRegistry>(AssetPath);
        if (reg == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            reg = ScriptableObject.CreateInstance<RuntimeAssetRegistry>();
            AssetDatabase.CreateAsset(reg, AssetPath);
        }
        reg.entries = map.Where(kv => kv.Value.Count > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new RuntimeAssetRegistry.Entry { key = kv.Key, objects = kv.Value.Distinct().ToArray() })
            .ToList();
        EditorUtility.SetDirty(reg);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RuntimeAssetRegistry] Baked {reg.entries.Count} lookups from {scripts.Count} runtime scripts.");
    }

    static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9_][A-Za-z0-9_ \-\(\)\.]*$");
    static readonly Dictionary<string, Dictionary<string, string>> s_nameIndex = new Dictionary<string, Dictionary<string, string>>();

    // file name (no extension) -> first asset path, for one asset type, built with a single FindAssets call
    static Dictionary<string, string> NameIndex(string typeName)
    {
        if (s_nameIndex.TryGetValue(typeName, out var idx)) return idx;
        idx = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in AssetDatabase.FindAssets("t:" + typeName))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.StartsWith("Assets/")) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            if (!idx.ContainsKey(n)) idx[n] = p;
        }
        s_nameIndex[typeName] = idx;
        return idx;
    }

    static void AddFind(Dictionary<string, List<UnityEngine.Object>> map, string query, bool requireHit = false)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        if (map.ContainsKey(RuntimeAssets.MakeFindKey(query, 0))) return;
        string[] guids;
        try { guids = AssetDatabase.FindAssets(query); } catch { return; }
        if (guids.Length == 0)
        {
            if (!requireHit) Debug.LogWarning($"[RuntimeAssetRegistry] No asset for query \"{query}\"");
            return;
        }
        // Keep the same order AssetDatabase returns, capped so broad queries don't pull in half the project.
        for (int i = 0; i < guids.Length && i < 8; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            map[RuntimeAssets.MakeFindKey(query, i)] = ObjectsAt(path, null);
        }
    }

    static void AddPath(Dictionary<string, List<UnityEngine.Object>> map, string path, Type type)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        if (!map.TryGetValue(path, out var list)) map[path] = list = new List<UnityEngine.Object>();
        foreach (var o in ObjectsAt(path, type)) if (!list.Contains(o)) list.Add(o);
    }

    static List<UnityEngine.Object> ObjectsAt(string path, Type type)
    {
        var list = new List<UnityEngine.Object>();
        if (type != null)
        {
            var typed = AssetDatabase.LoadAssetAtPath(path, type);
            if (typed != null) list.Add(typed);
        }
        var main = AssetDatabase.LoadMainAssetAtPath(path);
        if (main != null && !list.Contains(main)) list.Add(main);
        // Textures: include their Sprite sub-assets so LoadAssetAtPath<Sprite> works.
        if (main is Texture2D)
            foreach (var s in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>())
                if (!list.Contains(s)) list.Add(s);
        return list;
    }

    static Type ResolveType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name) ?? asm.GetType("UnityEngine." + name);
            if (t != null) return t;
        }
        return null;
    }
}
