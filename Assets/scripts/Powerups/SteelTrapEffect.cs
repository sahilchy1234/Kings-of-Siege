using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spike Trap Power-up.
/// Deploys a mechanical spike trap (spiketrap prefab) flush onto the ground.
/// When any enemy unit steps over the trap zone, the steel spikes snap up,
/// playing the spike animation, dealing heavy damage, applying a slow effect, and screen shake.
/// Can trigger multiple times or has a lifetime before disarming.
/// </summary>
public class SteelTrapEffect : MonoBehaviour
{
    [Header("Trap Parameters")]
    [SerializeField] private float trapRadius = 2.4f;
    [SerializeField] private float trapLifetime = 45f;
    [SerializeField] private float snapDamage = 130f; // Good heavy damage (~70-130% of unit HP)
    [SerializeField, Range(0.1f, 0.9f)] private float slowFactor = 0.35f; // 65% slow
    [SerializeField, Min(1f)] private float slowDuration = 4.0f;
    [SerializeField] private int maxTriggers = 5;
    [SerializeField] private float triggerCooldown = 0.8f;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject spikesPrefab;
    [SerializeField] private AudioClip snapAudioClip;
    [SerializeField] private AudioClip strikeAudioClip;
    [SerializeField] private GameObject impactVFXPrefab;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    private readonly HashSet<PeasantUnitController> triggeredUnits = new HashSet<PeasantUnitController>();
    private int triggerCount = 0;
    private bool isExpired = false;
    private float nextAllowedTriggerTime = 0f;
    private float _scanTimer = 0f;
    private const float ScanInterval = 0.1f; // 10 Hz is plenty for trap detection

    private GameObject trapInstance;
    private Animator trapAnimator;

    public void Initialize(Vector3 position, float radius, int playerTeam = 0,
        GameObject customSpikesPrefab = null, AudioClip customSnapClip = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        // Align to ground surface
        Vector3 groundPos = position;
        if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundPos = hit.point;
        }
        transform.position = groundPos;

        this.playerTeamId = playerTeam;
        if (radius > 1f) this.trapRadius = radius;
        if (customDamage > 0f) this.snapDamage = customDamage;
        if (customDuration > 0f) this.trapLifetime = customDuration;
        if (customQuantity > 0) this.maxTriggers = customQuantity;

        if (customSpikesPrefab != null) this.spikesPrefab = customSpikesPrefab;
        if (customSnapClip != null) this.snapAudioClip = customSnapClip;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        if (spikesPrefab == null)
        {
            spikesPrefab = Resources.Load<GameObject>("Powerups/aspiketrap");
            if (spikesPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("aspiketrap t:Prefab");
                if (guids == null || guids.Length == 0)
                    guids = RuntimeAssets.FindAssets("aspiketrap t:Prefab");

                if (guids != null && guids.Length > 0)
                {
                    spikesPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }
        if (snapAudioClip == null)
        {
            snapAudioClip = Resources.Load<AudioClip>("Sounds/dragon-studio-sword-slice-393847") ?? Resources.Load<AudioClip>("Sounds/arrowhit");
            if (snapAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-sword-slice-393847 t:AudioClip");
                if (guids == null || guids.Length == 0)
                    guids = RuntimeAssets.FindAssets("arrowhit t:AudioClip");

                if (guids != null && guids.Length > 0)
                {
                    snapAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }
        if (strikeAudioClip == null)
        {
            strikeAudioClip = Resources.Load<AudioClip>("Sounds/dragon-studio-hammer-smash-effect-382731");
            if (strikeAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-hammer-smash-effect-382731 t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    strikeAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }
        if (impactVFXPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Impact_Metal_01 t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                impactVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }

        BuildTrapVisual();
        StartCoroutine(LifetimeRoutine());
    }

    private void BuildTrapVisual()
    {
        if (spikesPrefab != null)
        {
            trapInstance = Instantiate(spikesPrefab, transform.position, transform.rotation, transform);
            trapInstance.transform.localPosition = Vector3.zero;
            trapInstance.transform.localRotation = Quaternion.identity;

            trapAnimator = trapInstance.GetComponentInChildren<Animator>();
            if (trapAnimator != null)
            {
                // Pause animation at frame 0 until stepped on
                trapAnimator.speed = 0f;
                trapAnimator.Play(0, 0, 0f);
            }
        }
        else
        {
            // Fallback primitive
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            plate.transform.localScale = new Vector3(trapRadius * 1.5f, 0.1f, trapRadius * 1.5f);
            Collider c = plate.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = plate.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.2f, 0.22f, 0.25f);
        }
    }

    private void Update()
    {
        if (isExpired) return;

        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = ScanInterval;

        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units == null) return;

        Vector3 trapPos = transform.position;

        for (int i = 0; i < units.Count; i++)
        {
            PeasantUnitController u = units[i];
            if (u == null || u.IsDead || u.TeamId == playerTeamId) continue;

            // Check if player or enemy unit steps onto the trap
            Vector3 diff = u.transform.position - trapPos;
            diff.y = 0f;
            if (diff.sqrMagnitude <= (trapRadius * trapRadius))
            {
                if (triggeredUnits.Contains(u)) continue;

                TriggerTrapOnUnit(u);

                if (triggerCount >= maxTriggers)
                {
                    DisarmTrap();
                    break;
                }
            }
        }
    }

    private void TriggerTrapOnUnit(PeasantUnitController unit)
    {
        triggeredUnits.Add(unit);
        triggerCount++;

        // 1. Play animation (spikes shoot up)
        if (trapAnimator != null)
        {
            trapAnimator.speed = 1.4f;
            trapAnimator.Play(0, 0, 0f);
        }

        // 2. Deal heavy damage
        unit.TakeDamage(snapDamage, null);

        // 3. Apply strong slow / cripple effect
        unit.ApplySlow(slowFactor, slowDuration);

        // 4. Play audio effects (slice + mechanical smash)
        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.15f);
            if (snapAudioClip != null)
            {
                KoSAudio.PlayOneShot(audioSource, snapAudioClip, 0.9f, "trap_snap");
            }
            if (strikeAudioClip != null)
            {
                KoSAudio.PlayOneShot(audioSource, strikeAudioClip, 0.65f, "trap_strike");
            }
        }

        // 5. Spawn Impact VFX at unit's feet
        if (impactVFXPrefab != null)
        {
            Vector3 fxPos = unit.transform.position + Vector3.up * 0.2f;
            GameObject fx = Instantiate(impactVFXPrefab, fxPos, Quaternion.identity);
            Destroy(fx, 2.5f);
        }

        // 6. Camera punch / screen shake
        if (CameraSwipeController.Instance != null)
        {
            CameraSwipeController.Instance.TriggerShake(0.22f, 0.16f);
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(trapLifetime);
        DisarmTrap();
    }

    private void DisarmTrap()
    {
        if (isExpired) return;
        isExpired = true;

        // Shrink or fade out then destroy
        StartCoroutine(DisarmFadeRoutine());
    }

    private IEnumerator DisarmFadeRoutine()
    {
        float elapsed = 0f;
        float duration = 0.5f;
        Vector3 initialScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
