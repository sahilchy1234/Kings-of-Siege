using System;
using UnityEngine;

/// <summary>
/// Serializable configuration data for a battlefield power-up.
/// </summary>
[Serializable]
public class PowerupData
{
    public PowerupType powerupType;
    public PowerupCategory category = PowerupCategory.AirAssault;
    public string displayName;
    [TextArea(1, 3)] public string description;

    [Header("UI Visuals")]
    [Tooltip("The main full button image / card sprite for this powerup. You can set this manually in Inspector.")]
    public Sprite buttonImage;

    [Tooltip("Small icon sprite for this powerup (optional if buttonImage is assigned).")]
    public Sprite icon;

    [Tooltip("Segmented energy cost required to deploy this powerup.")]
    [Min(0.1f)] public float energyCost = 2f;

    [Min(0.5f)] public float cooldown = 10f;
    [Min(1f)] public float targetingRadius = 5f;
    public Color themeColor = Color.cyan;

    [Header("Quantity & Combat Values")]
    [Tooltip("Number of items spawned: Arrows count (Arrow Shower), Stones count (Stone Shower), or Fence sections (Fence).")]
    [Min(1)] public int quantity = 35;
    [Tooltip("Damage dealt per arrow or per stone impact, or contact damage for fence.")]
    [Min(0f)] public float damage = 25f;
    [Tooltip("Duration of the effect in seconds.")]
    [Min(0.5f)] public float duration = 3f;

    [Header("Optional Prefabs & Audio")]
    public GameObject customPrefab;
    public AudioClip activationSound;
}

