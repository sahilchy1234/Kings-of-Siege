using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Optimised "preset" for the editor-only asset lookups in runtime scripts.
///
/// Many runtime scripts fill an empty serialized field at runtime with
///     field = RuntimeAssets/AssetDatabase.LoadAssetAtPath&lt;T&gt;(...)  (after FindAssets("query"))
/// That only works in the editor. This tool reads those scripts, works out which asset each field
/// gets, and writes the reference straight into the serialized field on every prefab and build
/// scene that uses the component - exactly like dragging it in the Inspector. On device there is
/// then no lookup and no extra memory; the runtime fallback code only runs if a field is still empty.
///
/// Only fields that are empty are touched; anything you assigned by hand is left alone.
/// </summary>
public static class BakeEditorLookupsIntoFields
{
    class Rule { public string field; public string query; public string path; public string type; }

    static readonly Regex RxPathAssign = new Regex(@"\b(\w+)\s*=\s*(?:RuntimeAssets|UnityEditor\.AssetDatabase)\.LoadAssetAtPath<\s*(\w+)\s*>\(\s*""([^""]+)""");
    static readonly Regex RxGuidAssign = new Regex(@"\b(\w+)\s*=\s*(?:RuntimeAssets|UnityEditor\.AssetDatabase)\.LoadAssetAtPath<\s*(\w+)\s*>\(");
    static readonly Regex RxFind = new Regex(@"FindAssets\(\s*""([^""]+)""");
    static readonly Regex RxHelperAssign = new Regex(@"\b(\w+)\s*=\s*(\w+)\(\s*""([^""]+)""\s*\)");
    static readonly Regex RxClass = new Regex(@"\bclass\s+(\w+)");
    static readonly Regex RxTemplate = new Regex(@"(\w+)\s*\(\s*string\s+(\w+)\s*\)[^{]*\{(?:[^{}]|\{[^{}]*\})*?FindAssets\(\s*\$""([^""]*)""", RegexOptions.Singleline);

    [MenuItem("Tools/Build/Bake Editor Lookups Into Fields")]
    public static void Run()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("[BakeLookups] Save your open scene first; nothing was changed.");
                return;
            }

        var rulesByClass = CollectRules(out var log);
        var types = rulesByClass.Keys.Select(FindType).Where(t => t != null && typeof(MonoBehaviour).IsAssignableFrom(t)).ToList();
        int assigned = 0;
        var cache = new Dictionary<string, UnityEngine.Object>();

        // 1) Prefab assets
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            bool any = types.Any(t => go.GetComponentInChildren(t, true) != null);
            if (!any) continue;

            var root = PrefabUtility.LoadPrefabContents(p);
            int n = 0;
            foreach (var t in types)
                foreach (var c in root.GetComponentsInChildren(t, true))
                {
                    // skip components that come from a nested prefab: they are baked on their own asset
                    if (PrefabUtility.IsPartOfPrefabInstance(c) && PrefabUtility.GetCorrespondingObjectFromSource(c) != null) continue;
                    n += Apply(c, rulesByClass[t.Name], cache, log, p);
                }
            if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, p); assigned += n; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Scenes in the build
        string active = SceneManager.GetActiveScene().path;
        foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int n = 0;
            foreach (var rootGo in scene.GetRootGameObjects())
                foreach (var t in types)
                    foreach (var c in rootGo.GetComponentsInChildren(t, true))
                        n += Apply(c, rulesByClass[t.Name], cache, log, s.path);
            if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); assigned += n; }
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/BakeEditorLookups.txt", log.ToString());
        Debug.Log($"[BakeLookups] Assigned {assigned} empty fields across prefabs and build scenes. Details: Logs/BakeEditorLookups.txt");
    }

    static int Apply(Component c, List<Rule> rules, Dictionary<string, UnityEngine.Object> cache, StringBuilder log, string where)
    {
        int n = 0;
        var so = new SerializedObject(c);
        foreach (var r in rules)
        {
            var prop = so.FindProperty(r.field);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.objectReferenceValue != null) continue;
            var asset = Resolve(r, cache);
            if (asset == null) continue;
            prop.objectReferenceValue = asset;
            if (prop.objectReferenceValue == null) continue; // wrong type for the field
            log.AppendLine($"{where} :: {c.GetType().Name}.{r.field} = {AssetDatabase.GetAssetPath(asset)}");
            n++;
        }
        if (n > 0) so.ApplyModifiedPropertiesWithoutUndo();
        return n;
    }

    static UnityEngine.Object Resolve(Rule r, Dictionary<string, UnityEngine.Object> cache)
    {
        string key = (r.path ?? r.query) + "|" + r.type;
        if (cache.TryGetValue(key, out var hit)) return hit;
        UnityEngine.Object result = null;
        Type t = FindType(r.type) ?? typeof(UnityEngine.Object);
        string path = r.path;
        if (path == null && r.query != null)
        {
            var guids = AssetDatabase.FindAssets(r.query);
            if (guids.Length > 0) path = AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        if (path != null) result = AssetDatabase.LoadAssetAtPath(path, t);
        cache[key] = result;
        return result;
    }

    static Dictionary<string, List<Rule>> CollectRules(out StringBuilder log)
    {
        log = new StringBuilder();
        var result = new Dictionary<string, List<Rule>>();
        foreach (var file in Directory.GetFiles("Assets/scripts", "*.cs", SearchOption.AllDirectories))
        {
            string f = file.Replace('\\', '/');
            if (f.Contains("/Editor/")) continue;
            string src = File.ReadAllText(f);
            if (!src.Contains("LoadAssetAtPath")) continue;
            var cm = RxClass.Match(src);
            if (!cm.Success) continue;
            var rules = new List<Rule>();

            // field = LoadAssetAtPath<T>("Assets/...")
            foreach (Match m in RxPathAssign.Matches(src))
                rules.Add(new Rule { field = m.Groups[1].Value, type = m.Groups[2].Value, path = m.Groups[3].Value });

            // FindAssets("query") ... field = LoadAssetAtPath<T>(GUIDToAssetPath(...)) within the next lines
            var lines = src.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var fm = RxFind.Match(lines[i]);
                if (!fm.Success) continue;
                for (int j = i; j < Math.Min(lines.Length, i + 8); j++)
                {
                    var am = RxGuidAssign.Match(lines[j]);
                    if (am.Success && !lines[j].Contains("\"Assets/"))
                    {
                        rules.Add(new Rule { field = am.Groups[1].Value, type = am.Groups[2].Value, query = fm.Groups[1].Value });
                        break;
                    }
                }
            }

            // field = LoadPrefabByName("Name") where the helper builds $"{name} t:Prefab"
            foreach (Match tm in RxTemplate.Matches(src))
            {
                string helper = tm.Groups[1].Value, arg = tm.Groups[2].Value, tpl = tm.Groups[3].Value;
                foreach (Match hm in RxHelperAssign.Matches(src))
                {
                    if (hm.Groups[2].Value != helper) continue;
                    string q = tpl.Replace("{" + arg + "}", hm.Groups[3].Value);
                    var tt = Regex.Match(q, @"t:(\w+)");
                    rules.Add(new Rule { field = hm.Groups[1].Value, query = q, type = tt.Success ? tt.Groups[1].Value : "Object" });
                }
            }

            // first rule per field wins (it is the primary choice in the script's fallback chain)
            var distinct = rules.GroupBy(r => r.field).Select(g => g.First()).ToList();
            if (distinct.Count == 0) continue;
            result[cm.Groups[1].Value] = distinct;
            foreach (var r in distinct) log.AppendLine($"rule {cm.Groups[1].Value}.{r.field} <- {(r.path ?? r.query)} ({r.type})");
        }
        return result;
    }

    static Type FindType(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name) ?? asm.GetType("UnityEngine." + name);
            if (t != null) return t;
        }
        return null;
    }
}
