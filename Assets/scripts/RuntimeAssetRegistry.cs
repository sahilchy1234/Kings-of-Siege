using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baked table of assets that runtime scripts look up through RuntimeAssets.
/// Lives at Resources/RuntimeAssetRegistry.asset and is regenerated before every build.
/// </summary>
public class RuntimeAssetRegistry : ScriptableObject
{
    public const string ResourcePath = "RuntimeAssetRegistry";

    [Serializable]
    public class Entry
    {
        public string key;
        public UnityEngine.Object[] objects;
    }

    public List<Entry> entries = new List<Entry>();
}
