using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight per-prefab GameObject pool. Use Get/Return instead of
/// Instantiate/Destroy for frequently spawned temporary objects (arrows, VFX).
/// </summary>
public static class SimpleGameObjectPool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> s_pools =
        new Dictionary<GameObject, Stack<GameObject>>();

    private static Transform s_root;

    private static Transform Root
    {
        get
        {
            if (s_root == null)
            {
                var go = new GameObject("[ObjectPool]");
                Object.DontDestroyOnLoad(go);
                s_root = go.transform;
            }
            return s_root;
        }
    }

    /// <summary>
    /// Get a pooled instance of <paramref name="prefab"/> at <paramref name="position"/> / <paramref name="rotation"/>.
    /// </summary>
    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!s_pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>(8);
            s_pools[prefab] = stack;
        }

        GameObject obj;
        while (stack.Count > 0)
        {
            obj = stack.Pop();
            if (obj != null)
            {
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
                return obj;
            }
        }

        obj = Object.Instantiate(prefab, position, rotation);
        // Tag with which prefab this came from so Return can dispatch it correctly.
        var tag = obj.AddComponent<PoolReturnTag>();
        tag.SourcePrefab = prefab;
        return obj;
    }

    /// <summary>
    /// Get a pooled instance parented to <paramref name="parent"/>.
    /// </summary>
    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        var obj = Get(prefab, position, rotation);
        if (obj != null) obj.transform.SetParent(parent, true);
        return obj;
    }

    /// <summary>
    /// Return an object to its pool. Call instead of Destroy.
    /// </summary>
    public static void Return(GameObject obj)
    {
        if (obj == null) return;
        var tag = obj.GetComponent<PoolReturnTag>();
        if (tag == null || tag.SourcePrefab == null)
        {
            Object.Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(Root, false);
        if (!s_pools.TryGetValue(tag.SourcePrefab, out var stack))
        {
            stack = new Stack<GameObject>(8);
            s_pools[tag.SourcePrefab] = stack;
        }
        stack.Push(obj);
    }

    /// <summary>
    /// Return after a delay (mirrors Destroy(obj, delay) pattern).
    /// </summary>
    public static void ReturnDelayed(GameObject obj, float delay)
    {
        if (obj == null) return;
        var tag = obj.GetComponent<PoolReturnTag>();
        if (tag == null || tag.SourcePrefab == null)
        {
            Object.Destroy(obj, delay);
            return;
        }
        tag.ReturnToPool(delay);
    }

    /// <summary>Warm the pool with <paramref name="count"/> pre-instantiated instances.</summary>
    public static void Prewarm(GameObject prefab, int count, Transform parent = null)
    {
        if (prefab == null || count <= 0) return;
        var objs = new GameObject[count];
        for (int i = 0; i < count; i++)
            objs[i] = Get(prefab, Vector3.zero, Quaternion.identity);
        for (int i = 0; i < count; i++)
            Return(objs[i]);
    }

    /// <summary>Clear all pools (call on scene unload if prefab assets change).</summary>
    public static void Clear()
    {
        foreach (var stack in s_pools.Values)
            while (stack.Count > 0) { var o = stack.Pop(); if (o != null) Object.Destroy(o); }
        s_pools.Clear();
    }
}
