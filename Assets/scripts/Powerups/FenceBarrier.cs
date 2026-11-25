using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Deployable defensive barricade / fence.
/// Dynamically carves NavMesh to block enemy navigation, provides solid physical collision,
/// hurts nearby enemy attackers on spikes, and possesses a health pool and duration timer.
/// Enhanced with juicy DOTween placement bounce-in, hit punch recoil, and splinter shatter animations.
/// </summary>
[DisallowMultipleComponent]
public class FenceBarrier : MonoBehaviour, IDamageable
{
    private static readonly List<FenceBarrier> allBarriers = new List<FenceBarrier>();
    public static IReadOnlyList<FenceBarrier> AllBarriers => allBarriers;

    /// <summary>Rotation a fence is placed with (aligned to the battle scene).</summary>
    public static Quaternion GetPlacementRotation()
    {
        return BattleSceneSetup.Instance != null ? BattleSceneSetup.Instance.transform.rotation : Quaternion.identity;
    }

    /// <summary>
    /// True if a fence footprint placed here would overlap a building (castle or tower, any team).
    /// Units, the ground and other fences are ignored.
    /// </summary>
    public static bool IsPlacementBlocked(Vector3 position, Quaternion rotation, float width, float depth, float height)
    {
        Vector3 center = position + Vector3.up * (height * 0.5f);
        Vector3 half = new Vector3(width * 0.5f + 0.3f, height * 0.5f + 0.5f, depth * 0.5f + 0.3f);

        Collider[] hits = Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;

            PeasantObjectiveTarget castle = c.GetComponentInParent<PeasantObjectiveTarget>();
            if (castle != null && !castle.IsDestroyed) return true;

            TowerCombatController tower = c.GetComponentInParent<TowerCombatController>();
            if (tower != null && !tower.IsDestroyed) return true;
        }
        return false;
    }

    /// <summary>Footprint values used for placement checks, matching the serialized defaults.</summary>
    public static float PlacementWidth => 5f;
    public static float PlacementDepth => 1.0f;
    public static float PlacementHeight => 1.8f;

    [Header("Durability & Lifetime")]
    [SerializeField, Min(10f)] private float maxHealth = 350f;
    [SerializeField, Min(5f)] private float lifetime = 25f;
    [SerializeField] private float currentHealth;

    [Header("Spike Contact Damage")]
    [Tooltip("Damage dealt per second to enemies colliding or crowding against the fence.")]
    [SerializeField, Min(0f)] private float spikeDamagePerSec = 12f;
    [SerializeField, Min(0.5f)] private float damageRadius = 1.8f;

    [Header("Dimensions")]
    [SerializeField] private float barrierWidth = 5f;
    [SerializeField] private float barrierHeight = 1.8f;
    [SerializeField] private float barrierDepth = 1.0f;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject fenceVisualPrefab;
    [SerializeField] private AudioClip placementClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip destroyClip;

    [Header("Juicy Particle Prefabs")]
    [SerializeField] private GameObject woodImpactVFXPrefab;
    [SerializeField] private GameObject dustExplosionPrefab;

    private NavMeshObstacle navObstacle;
    private BoxCollider boxCollider;
    private AudioSource audioSource;
    private Transform healthBarTransform;
    private Image healthFillImage;
    private Vector3 initialHealthBarScale;
    private GameObject visualRoot;
    private Vector3 targetVisualScale = Vector3.one;
    private int playerTeamId = 0;
    private int sectionQuantity = 2;
    private bool isDestroyed = false;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => isDestroyed;
    public int TeamId => playerTeamId;

    public void Initialize(Vector3 position, Quaternion rotation, int playerTeam = 0,
        GameObject customPrefab = null, AudioClip customPlacementSound = null,
        int customQuantity = 0, float customSpikeDamage = 0f, float customDuration = 0f)
    {
        transform.position = position;
        transform.rotation = rotation;
        this.playerTeamId = playerTeam;
        if (customQuantity > 0) this.sectionQuantity = customQuantity;

        if (customSpikeDamage > 0f) this.spikeDamagePerSec = customSpikeDamage;
        if (customDuration > 0f) this.lifetime = customDuration;

        this.currentHealth = maxHealth;

        if (customPrefab != null) fenceVisualPrefab = customPrefab;
        if (customPlacementSound != null) placementClip = customPlacementSound;

        SetupComponents();
        BuildVisualFence();
        BuildFloatingHealthBar();

        if (!allBarriers.Contains(this))
            allBarriers.Add(this);

        // JUICY DOTWEEN PLACEMENT SLAM
        PlayJuicyPlacementAnimation();

        StartCoroutine(LifetimeRoutine());
    }

    private void SetupComponents()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // NavMeshObstacle with immediate synchronous carving
        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null) navObstacle = gameObject.AddComponent<NavMeshObstacle>();
        navObstacle.shape = NavMeshObstacleShape.Box;
        navObstacle.size = new Vector3(barrierWidth, barrierHeight, barrierDepth);
        navObstacle.center = new Vector3(0f, barrierHeight * 0.5f, 0f);
        navObstacle.carving = true;
        navObstacle.carveOnlyStationary = false;
        navObstacle.carvingTimeToStationary = 0.05f;

        // Solid physical BoxCollider (sized appropriately so units physically cannot walk through it)
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(barrierWidth, barrierHeight, barrierDepth * 1.5f);
        boxCollider.center = new Vector3(0f, barrierHeight * 0.5f, 0f);
        boxCollider.isTrigger = false;

        if (fenceVisualPrefab == null)
        {
            fenceVisualPrefab = Resources.Load<GameObject>("Powerups/Fence_Spikes_1 Variant");
            if (fenceVisualPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("Fence_Spikes_1 Variant t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    fenceVisualPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (placementClip == null)
        {
            placementClip = Resources.Load<AudioClip>("Sounds/dragon-studio-hammer-smash-effect-382731");
            if (placementClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-hammer-smash-effect-382731 t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    placementClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (woodImpactVFXPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Impact_Wood_01 t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                woodImpactVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }
        if (dustExplosionPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Dust_Big_01 t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                dustExplosionPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }
    }

    private void BuildVisualFence()
    {
        Transform existing = transform.Find("VisualRoot");
        if (existing != null) Destroy(existing.gameObject);

        visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        if (fenceVisualPrefab != null)
        {
            int count = Mathf.Max(1, sectionQuantity);

            GameObject firstSection = Instantiate(fenceVisualPrefab, visualRoot.transform, false);
            firstSection.transform.localPosition = Vector3.zero;
            firstSection.transform.localRotation = Quaternion.identity;
            firstSection.transform.localScale = Vector3.one;
            RemoveChildColliders(firstSection);

            Bounds singleBounds = CalculateLocalBounds(firstSection, visualRoot.transform);
            float singleW = Mathf.Max(0.5f, singleBounds.size.x);

            float step = (count > 1) ? (barrierWidth - singleW) / (count - 1) : 0f;
            step = Mathf.Clamp(step, singleW * 0.45f, singleW * 0.95f);

            Vector3 pivotOffset = singleBounds.center;
            pivotOffset.y = 0f;

            float startX = -((count - 1) * step * 0.5f);
            firstSection.transform.localPosition = new Vector3(startX, 0f, 0f) - pivotOffset;

            for (int i = 1; i < count; i++)
            {
                GameObject nextSection = Instantiate(fenceVisualPrefab, visualRoot.transform, false);
                nextSection.transform.localPosition = new Vector3(startX + i * step, 0f, 0f) - pivotOffset;
                nextSection.transform.localRotation = Quaternion.identity;
                nextSection.transform.localScale = Vector3.one;
                RemoveChildColliders(nextSection);
            }

            Bounds totalBounds = CalculateLocalBounds(visualRoot, transform);

            if (totalBounds.size.x > 0.1f)
            {
                float targetScale = barrierWidth / totalBounds.size.x;
                targetVisualScale = new Vector3(targetScale, targetScale, targetScale);
                visualRoot.transform.localScale = targetVisualScale;
            }

            Bounds finalBounds = CalculateLocalBounds(visualRoot, transform);
            visualRoot.transform.localPosition = new Vector3(
                -finalBounds.center.x,
                -finalBounds.min.y,
                -finalBounds.center.z
            );

            if (finalBounds.size.y > 0.5f)
            {
                barrierHeight = finalBounds.size.y;
            }
            if (navObstacle != null)
            {
                navObstacle.size = new Vector3(barrierWidth, barrierHeight, barrierDepth);
                navObstacle.center = new Vector3(0f, barrierHeight * 0.5f, 0f);
            }
            if (boxCollider != null)
            {
                boxCollider.size = new Vector3(barrierWidth, barrierHeight, barrierDepth * 1.5f);
                boxCollider.center = new Vector3(0f, barrierHeight * 0.5f, 0f);
            }
        }
        else
        {
            // Procedural wooden barricade fallback
            int postCount = 5;
            float step = barrierWidth / (postCount - 1);
            for (int i = 0; i < postCount; i++)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.transform.SetParent(visualRoot.transform, false);
                post.transform.localPosition = new Vector3(-barrierWidth * 0.5f + i * step, barrierHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.2f, barrierHeight * 0.5f, 0.2f);
                post.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), 0f, Random.Range(-8f, 8f));
                Collider c = post.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            GameObject cross = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cross.transform.SetParent(visualRoot.transform, false);
            cross.transform.localPosition = new Vector3(0f, barrierHeight * 0.55f, 0f);
            cross.transform.localScale = new Vector3(barrierWidth, 0.16f, 0.16f);
            Collider cc = cross.GetComponent<Collider>();
            if (cc != null) Destroy(cc);
        }
    }

    private void PlayJuicyPlacementAnimation()
    {
        if (visualRoot == null) return;

        // Fortress Slam: drop from sky with high-impact squash and spring bounce
        Vector3 finalScale = targetVisualScale;
        Vector3 finalPos = visualRoot.transform.localPosition;
        visualRoot.transform.localPosition = finalPos + Vector3.up * 4.2f;
        visualRoot.transform.localScale = finalScale;

        Sequence placeSeq = DOTween.Sequence();
        placeSeq.Append(visualRoot.transform.DOLocalMoveY(finalPos.y, 0.22f).SetEase(Ease.InQuad));
        placeSeq.AppendCallback(() =>
        {
            // Ground dust puffs along the fence line
            SpawnWoodParticles(transform.position - transform.right * (barrierWidth * 0.4f));
            SpawnWoodParticles(transform.position);
            SpawnWoodParticles(transform.position + transform.right * (barrierWidth * 0.4f));

            if (dustExplosionPrefab != null)
            {
                GameObject dust = Instantiate(dustExplosionPrefab, transform.position, Quaternion.identity);
                dust.transform.localScale = Vector3.one * 0.65f;
                Destroy(dust, 1.8f);
            }

            // Placement sound & micro tremor
            if (placementClip != null && audioSource != null)
            {
                audioSource.pitch = 1.05f;
                KoSAudio.PlayOneShot(audioSource, placementClip, 0.95f, "fence_place");
            }

            if (CameraSwipeController.Instance != null)
            {
                CameraSwipeController.Instance.TriggerShake(0.24f, 0.16f);
            }
        });

        // High-impact pancake squash & elastic overshoot bounce
        placeSeq.Append(visualRoot.transform.DOScale(
            new Vector3(finalScale.x * 1.15f, finalScale.y * 0.68f, finalScale.z * 1.15f),
            0.06f).SetEase(Ease.OutQuad));
        placeSeq.Append(visualRoot.transform.DOScale(finalScale, 0.26f).SetEase(Ease.OutBounce));
        placeSeq.Join(visualRoot.transform.DOPunchRotation(new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-4f, 4f)), 0.22f, 10, 0.5f));
    }

    private void RemoveChildColliders(GameObject obj)
    {
        if (obj == null) return;
        Collider[] cols = obj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) Destroy(cols[i]);
        }
    }

    private Bounds CalculateLocalBounds(GameObject root, Transform relativeTo)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds localBounds = new Bounds();
        bool initialized = false;

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer rend = renderers[r];
            if (rend == null || !rend.enabled) continue;

            Bounds b = rend.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            Vector3[] corners = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y,  e.z)
            };

            for (int i = 0; i < 8; i++)
            {
                Vector3 localPt = relativeTo.InverseTransformPoint(corners[i]);
                if (!initialized)
                {
                    localBounds = new Bounds(localPt, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localPt);
                }
            }
        }

        return initialized ? localBounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private void BuildFloatingHealthBar()
    {
        Transform oldBar = transform.Find("HealthBar");
        if (oldBar != null) Destroy(oldBar.gameObject);

        // World-space UI canvas, the same approach the tower and unit health bars use.
        // The old 3D cube bar relied on primitive materials, which do not render in Android builds.
        GameObject barRoot = new GameObject("HealthBar", typeof(RectTransform));
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = new Vector3(0f, barrierHeight + 0.35f, 0f);

        Canvas canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rootRect = barRoot.GetComponent<RectTransform>();
        healthBarRect = rootRect;
        ApplyHealthBarSize();

        Sprite white = JuicyUnitHealthBar.GetDefaultWhiteSprite();

        // Background
        CreateHealthBarImage("Background", barRoot.transform, white, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Fill: pivot on the left so shrinking x drains it from the right
        Image fill = CreateHealthBarImage("Fill", barRoot.transform, white, new Color(0.2f, 0.8f, 0.3f, 0.95f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        healthFillImage = fill;
        healthBarTransform = fill.transform;
        initialHealthBarScale = fill.transform.localScale;
    }

    // Bar of about 1.3 x 0.14 world units. Re-applied every frame because a Canvas added at
    // runtime can drive its own RectTransform size/scale, which made the bar huge.
    private static readonly Vector2 HealthBarSize = new Vector2(130f, 14f);
    private const float HealthBarScale = 0.01f;
    private RectTransform healthBarRect;

    private void ApplyHealthBarSize()
    {
        if (healthBarRect == null) return;
        if (healthBarRect.sizeDelta != HealthBarSize) healthBarRect.sizeDelta = HealthBarSize;
        Vector3 s = Vector3.one * HealthBarScale;
        if (healthBarRect.localScale != s) healthBarRect.localScale = s;
    }

    private static Image CreateHealthBarImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void LateUpdate()
    {
        // Keep the world-space health bar facing the camera
        if (isDestroyed || healthBarTransform == null) return;
        ApplyHealthBarSize();
        Transform bar = healthBarTransform.parent;
        Camera cam = Camera.main;
        if (bar == null || cam == null) return;

        Vector3 dir = bar.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f) bar.rotation = Quaternion.LookRotation(dir);
    }

    private void Update()
    {
        if (isDestroyed) return;

        BlockAndRepelEnemyUnits();

        if (spikeDamagePerSec > 0f)
        {
            ApplySpikeDamage(Time.deltaTime);
        }
    }

    private void BlockAndRepelEnemyUnits()
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units == null) return;

        Vector3 fencePos = transform.position;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        float halfW = (barrierWidth * 0.5f) + 0.4f;
        float halfD = (barrierDepth * 0.5f) + 0.55f;
        float dt = Time.deltaTime;

        for (int i = 0; i < units.Count; i++)
        {
            PeasantUnitController unit = units[i];
            if (unit == null || unit.IsDead || unit.TeamId == playerTeamId || unit.IsStationary) continue;

            Vector3 toUnit = unit.transform.position - fencePos;
            toUnit.y = 0f;

            float alongRight = Vector3.Dot(toUnit, right);
            float alongForward = Vector3.Dot(toUnit, forward);

            if (Mathf.Abs(alongRight) <= halfW && Mathf.Abs(alongForward) <= halfD)
            {
                float sign = alongForward >= 0f ? 1f : -1f;
                float overlap = halfD - Mathf.Abs(alongForward);
                Vector3 push = forward * (sign * (overlap + 0.2f) * 12f * dt);

                if (unit.Agent != null && unit.Agent.enabled && unit.Agent.isOnNavMesh)
                {
                    unit.Agent.Move(push);
                }
                else
                {
                    unit.transform.position += push;
                }
            }
        }
    }

    private void ApplySpikeDamage(float dt)
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units != null)
        {
            float dmg = spikeDamagePerSec * dt;
            for (int i = 0; i < units.Count; i++)
            {
                PeasantUnitController unit = units[i];
                if (unit == null || unit.IsDead || unit.TeamId == playerTeamId) continue;

                float dist = Vector3.Distance(transform.position, unit.transform.position);
                if (dist <= damageRadius)
                {
                    unit.TakeDamage(dmg, null);
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
    }

    public void TakeDamage(float amount, PeasantUnitController attacker)
    {
        if (isDestroyed || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar();

        // JUICY HIT REACTION WITH DOTWEEN
        if (visualRoot != null)
        {
            visualRoot.transform.DOPunchRotation(
                new Vector3(Random.Range(-3f, 3f), Random.Range(-4f, 4f), Random.Range(-3f, 3f)),
                0.16f, 10, 0.5f);
        }

        if (healthBarTransform != null)
        {
            healthBarTransform.DOPunchScale(new Vector3(0.2f, 0.2f, 0.2f), 0.14f);
        }

        Vector3 hitPoint = attacker != null ? attacker.transform.position : transform.position;
        SpawnWoodParticles(hitPoint);

        if (audioSource != null && Random.value < 0.5f)
        {
            audioSource.pitch = Random.Range(0.85f, 1.25f);
            KoSAudio.PlayOneShot(audioSource, hitClip, 0.6f, "wood_hit");
        }

        if (currentHealth <= 0f)
        {
            DestroyFence();
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarTransform == null) return;
        float pct = Mathf.Clamp01(currentHealth / maxHealth);
        Vector3 s = initialHealthBarScale;
        s.x = initialHealthBarScale.x * pct;
        healthBarTransform.localScale = s;

        if (healthFillImage != null)
        {
            if (pct > 0.5f)
                healthFillImage.color = Color.Lerp(new Color(1f, 0.75f, 0.1f), new Color(0.2f, 0.85f, 0.25f), (pct - 0.5f) * 2f);
            else if (pct > 0.25f)
                healthFillImage.color = Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(1f, 0.75f, 0.1f), (pct - 0.25f) * 4f);
            else
                healthFillImage.color = new Color(0.95f, 0.15f, 0.1f, 0.95f);
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        float remaining = lifetime;
        while (remaining > 0f && !isDestroyed)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (!isDestroyed)
        {
            ExpireFence();
        }
    }

    /// <summary>
    /// Timer ran out: the fence groans, sinks into the ground with a dust puff, then is removed.
    /// Not used for destruction by enemies, which still uses the crumble/splinter animation.
    /// </summary>
    private void ExpireFence()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        allBarriers.Remove(this);

        if (navObstacle != null) navObstacle.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;

        StartCoroutine(ExpireAnimationRoutine());
    }

    private IEnumerator ExpireAnimationRoutine()
    {
        // 1. Warning wobble while the timer runs out
        if (visualRoot != null)
        {
            visualRoot.transform.DOShakePosition(0.4f, 0.09f, 22);
            visualRoot.transform.DOShakeRotation(0.4f, 5f, 22);
        }
        if (healthBarTransform != null && healthBarTransform.parent != null)
        {
            healthBarTransform.parent.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack);
        }

        yield return new WaitForSeconds(0.4f);

        // 2. Dust puff at the base, then sink into the ground while shrinking
        if (dustExplosionPrefab != null)
        {
            GameObject dust = Instantiate(dustExplosionPrefab, transform.position, Quaternion.identity);
            dust.transform.localScale = Vector3.one * 0.55f;
            Destroy(dust, 1.8f);
        }
        SpawnWoodParticles(transform.position);

        if (visualRoot != null)
        {
            Transform vr = visualRoot.transform;
            float startY = vr.localPosition.y;

            Sequence sinkSeq = DOTween.Sequence();
            sinkSeq.Append(vr.DOLocalMoveY(startY - 1.2f, 0.5f).SetEase(Ease.InBack));
            sinkSeq.Join(vr.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack));
            sinkSeq.Join(vr.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-8f, 8f)), 0.5f, RotateMode.LocalAxisAdd).SetEase(Ease.InQuad));
            yield return sinkSeq.WaitForCompletion();
        }

        Destroy(gameObject);
    }

    private void DestroyFence()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        allBarriers.Remove(this);

        if (navObstacle != null) navObstacle.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;

        StartCoroutine(JuicyCrumbleRoutine());
    }

    private IEnumerator JuicyCrumbleRoutine()
    {
        // 1. Violent pre-fracture shudder
        if (visualRoot != null)
        {
            visualRoot.transform.DOShakePosition(0.25f, 0.22f, 28);
            visualRoot.transform.DOShakeRotation(0.25f, 16f, 28);
        }

        // 2. Spawn wood splinter burst and dust explosion along barrier length
        SpawnWoodParticles(transform.position);
        SpawnWoodParticles(transform.position - transform.right * (barrierWidth * 0.35f));
        SpawnWoodParticles(transform.position + transform.right * (barrierWidth * 0.35f));

        if (dustExplosionPrefab != null)
        {
            GameObject dust = Instantiate(dustExplosionPrefab, transform.position, Quaternion.identity);
            dust.transform.localScale = Vector3.one * 0.95f;
            Destroy(dust, 2.0f);
        }

        if (CameraSwipeController.Instance != null)
        {
            CameraSwipeController.Instance.TriggerShake(0.25f, 0.2f);
        }

        yield return new WaitForSeconds(0.25f);

        // 3. Catastrophic Splinter Burst: Individual child sections detach and explode outward!
        if (visualRoot != null)
        {
            int childCount = visualRoot.transform.childCount;
            if (childCount > 0)
            {
                Transform[] pieces = new Transform[childCount];
                for (int i = 0; i < childCount; i++) pieces[i] = visualRoot.transform.GetChild(i);

                for (int i = 0; i < pieces.Length; i++)
                {
                    Transform piece = pieces[i];
                    piece.SetParent(transform, true);

                    Vector3 scatterDir = (piece.position - transform.position);
                    scatterDir.y = 0f;
                    if (scatterDir.sqrMagnitude < 0.01f) scatterDir = Random.insideUnitSphere;
                    scatterDir.Normalize();

                    Vector3 scatterTarget = piece.position + scatterDir * Random.Range(1.8f, 3.4f) + transform.forward * Random.Range(-1.2f, 1.2f);
                    float flyTime = Random.Range(0.48f, 0.65f);

                    piece.DOJump(scatterTarget, Random.Range(1.6f, 2.6f), 1, flyTime).SetEase(Ease.OutQuad);
                    piece.DORotate(Random.insideUnitSphere * 720f, flyTime, RotateMode.WorldAxisAdd);
                    piece.DOScale(Vector3.zero, 0.22f).SetDelay(flyTime * 0.7f);
                }

                yield return new WaitForSeconds(0.7f);
            }
            else
            {
                Sequence sinkSeq = DOTween.Sequence();
                sinkSeq.Append(visualRoot.transform.DOMoveY(transform.position.y - 1.4f, 0.28f).SetEase(Ease.InBack));
                sinkSeq.Join(visualRoot.transform.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InBack));
                yield return sinkSeq.WaitForCompletion();
            }
        }

        Destroy(gameObject);
    }

    private void SpawnWoodParticles(Vector3 pos)
    {
        if (woodImpactVFXPrefab != null)
        {
            GameObject fx = Instantiate(woodImpactVFXPrefab, pos + Vector3.up * 0.4f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 0.7f;
            Destroy(fx, 1.5f);
        }
    }

    private void OnDestroy()
    {
        allBarriers.Remove(this);
    }
}
