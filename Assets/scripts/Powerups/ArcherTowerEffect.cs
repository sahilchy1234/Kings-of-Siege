using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Deployable Archer Tower Power-up.
/// Spawns a fortified wooden watchtower with an elite archer stationed at the top.
/// Has its own health bar (enemies can target & attack it), duration timer,
/// and continuously fires arrows at enemy units within its defensive perimeter.
/// The tower drops in from above with a squash-and-stretch landing and a dust puff.
/// It cannot be damaged, targeted or fire until the spawn animation ends.
/// </summary>
[DisallowMultipleComponent]
public class ArcherTowerEffect : MonoBehaviour
{
    [Header("Tower Durability & Lifespan")]
    [SerializeField, Min(50f)] private float maxHealth = 400f;
    [SerializeField, Min(5f)] private float lifetime = 30f;
    [SerializeField] private float currentHealth;

    [Header("Archer Combat Stats")]
    [SerializeField, Min(5f)] private float attackRange = 18f;
    [SerializeField, Min(1f)] private float damagePerShot = 32f;
    [SerializeField, Min(0.2f)] private float attackCooldown = 0.85f;
    [SerializeField] private float towerHeight = 4.2f;
    [Tooltip("Extra vertical offset for the archer stationed atop the tower.")]
    [SerializeField] private float archerHeightOffset = 1.35f;
    [Tooltip("Overall size of the tower, its archer and its hit box (1 = original size).")]
    [SerializeField, Range(0.2f, 1.5f)] private float sizeScale = 0.65f; // +30% (was 0.5)

    [Header("Spawn Animation")]
    [Tooltip("How high above the ground the tower starts before dropping in.")]
    [SerializeField, Min(0f)] private float spawnDropHeight = 3.5f;
    [Tooltip("Seconds for the drop-in (before the landing squash).")]
    [SerializeField, Min(0.05f)] private float spawnDropDuration = 0.45f;
    [Tooltip("Seconds for the elastic settle after landing.")]
    [SerializeField, Min(0.05f)] private float spawnSettleDuration = 0.5f;
    [Tooltip("Number of dust puffs released on landing.")]
    [SerializeField, Range(0, 20)] private int spawnDustCount = 10;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip smashSound;

    private NavMeshObstacle navObstacle;
    private BoxCollider boxCollider;
    private AudioSource audioSource;
    private Transform healthBarTransform;
    private Vector3 initialHealthBarScale;
    private Transform towerArcherTransform;

    private int playerTeamId = 0;
    private float nextFireTime;
    private bool isDestroyed = false;
    private bool isSpawning = false;
    private Tween spawnTween;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => isDestroyed;
    public bool IsSpawning => isSpawning;

    public void Initialize(Vector3 position, int playerTeam = 0,
        GameObject customTowerPrefab = null, AudioClip customPlacementSound = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        // Snap directly and accurately to the ground surface
        Vector3 groundSnappedPos = position;
        if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundSnappedPos = hit.point;
        }
        else if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
        {
            groundSnappedPos = navHit.position;
        }

        transform.position = groundSnappedPos;
        this.playerTeamId = playerTeam;
        this.currentHealth = maxHealth;

        if (customDamage > 0f) this.damagePerShot = customDamage;
        if (customDuration > 0f) this.lifetime = customDuration;
        if (customTowerPrefab != null) this.towerPrefab = customTowerPrefab;

        // Scale every height used below (archer perch, health bar, collider, arrow origin) with the tower
        towerHeight *= sizeScale;
        archerHeightOffset *= sizeScale;

        SetupComponents();
        BuildTowerVisuals();
        BuildFloatingHealthBar();

        if (audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, customPlacementSound, 0.8f, "tower_build");
        }

        PlaySpawnAnimation();
    }

    private void SetupComponents()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null) navObstacle = gameObject.AddComponent<NavMeshObstacle>();
        navObstacle.shape = NavMeshObstacleShape.Box;
        float footprint = 2.5f * sizeScale;
        navObstacle.size = new Vector3(footprint, towerHeight, footprint);
        navObstacle.center = new Vector3(0f, towerHeight * 0.5f, 0f);
        navObstacle.carving = true;

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(footprint, towerHeight, footprint);
        boxCollider.center = new Vector3(0f, towerHeight * 0.5f, 0f);

        if (towerPrefab == null)
        {
            towerPrefab = Resources.Load<GameObject>("Powerups/Tower_1 Variant");
            if (towerPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("Tower_1 Variant t:Prefab");
                if (guids == null || guids.Length == 0)
                    guids = RuntimeAssets.FindAssets("Tower_2 Variant t:Prefab");

                if (guids != null && guids.Length > 0)
                {
                    towerPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (archerPrefab == null)
        {
            archerPrefab = Resources.Load<GameObject>("Powerups/TT_Archer");
            if (archerPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("TT_Archer t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    archerPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (arrowPrefab == null)
        {
            arrowPrefab = Resources.Load<GameObject>("Powerups/Arrow_B");
            if (arrowPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("Arrow_B t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    arrowPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (shootSound == null)
        {
            shootSound = Resources.Load<AudioClip>("Sounds/arrowrelease");
            if (shootSound == null)
            {
                string[] guids = RuntimeAssets.FindAssets("arrowrelease t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    shootSound = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }
    }

    private void BuildTowerVisuals()
    {
        if (towerPrefab != null)
        {
            GameObject towerInstance = Instantiate(towerPrefab, transform.position, transform.rotation, transform);
            towerInstance.transform.localScale = Vector3.one * (0.9f * sizeScale);
        }
        else
        {
            // Procedural tower column
            GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.transform.SetParent(transform, false);
            col.transform.localPosition = new Vector3(0f, towerHeight * 0.5f, 0f);
            col.transform.localScale = new Vector3(2.2f * sizeScale, towerHeight * 0.5f, 2.2f * sizeScale);
            Collider c = col.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        // Spawn archer on top platform (with upwards offset so he stands clearly above the battlements)
        Vector3 archerPos = transform.position + Vector3.up * (towerHeight + archerHeightOffset);
        if (archerPrefab != null)
        {
            GameObject archerInstance = Instantiate(archerPrefab, archerPos, transform.rotation, transform);
            // Disable its standard controller/NavMeshAgent since it's stationed on the tower
            PeasantUnitController puc = archerInstance.GetComponent<PeasantUnitController>();
            if (puc != null) Destroy(puc);
            NavMeshAgent nma = archerInstance.GetComponent<NavMeshAgent>();
            if (nma != null) Destroy(nma);
            archerInstance.transform.localScale *= sizeScale;
            towerArcherTransform = archerInstance.transform;
        }
    }

    private void BuildFloatingHealthBar()
    {
        GameObject barRoot = new GameObject("HealthBar");
        barRoot.transform.SetParent(transform, false);
        // Bar keeps its readable size; it just sits above the (scaled) archer
        barRoot.transform.localPosition = new Vector3(0f, towerHeight + archerHeightOffset + 2.2f * sizeScale + 0.5f, 0f);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.transform.SetParent(barRoot.transform, false);
        bg.transform.localScale = new Vector3(1.4f, 0.14f, 0.08f);
        Collider bgCol = bg.GetComponent<Collider>();
        if (bgCol != null) Destroy(bgCol);
        Renderer bgR = bg.GetComponent<Renderer>();
        if (bgR != null) bgR.material.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.transform.SetParent(barRoot.transform, false);
        fill.transform.localScale = new Vector3(1.36f, 0.11f, 0.09f);
        Collider fillCol = fill.GetComponent<Collider>();
        if (fillCol != null) Destroy(fillCol);
        Renderer fillR = fill.GetComponent<Renderer>();
        if (fillR != null) fillR.material.color = new Color(0.15f, 0.5f, 1f, 0.95f);

        healthBarTransform = fill.transform;
        initialHealthBarScale = fill.transform.localScale;
    }

    // ------------------------------------------------------------------
    // Spawn animation (DOTween)
    // ------------------------------------------------------------------

    /// <summary>
    /// Drops the tower in from above while it grows, lands with a squash,
    /// releases dust, then settles with an elastic overshoot. Shooting, damage
    /// and targeting stay disabled until the sequence completes.
    /// </summary>
    private void PlaySpawnAnimation()
    {
        isSpawning = true;

        // Not targetable or blocking pathing while it is animating in
        if (navObstacle != null) navObstacle.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;

        Vector3 groundPos = transform.position;
        transform.position = groundPos + Vector3.up * spawnDropHeight;
        transform.localScale = new Vector3(0.6f, 1.4f, 0.6f); // stretched while falling

        Sequence seq = DOTween.Sequence().SetLink(gameObject);

        // Fall and grow back to normal with a little overshoot
        seq.Append(transform.DOMove(groundPos, spawnDropDuration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.one, spawnDropDuration).SetEase(Ease.OutBack));

        // Landing: squash flat, release dust, punch back up
        seq.AppendCallback(() =>
        {
            SpawnDust(groundPos);
            if (audioSource != null && smashSound != null)
                KoSAudio.PlayOneShot(audioSource, smashSound, 0.6f, "wood_smash");
        });
        seq.Append(transform.DOScale(new Vector3(1.25f, 0.7f, 1.25f), 0.08f).SetEase(Ease.OutQuad));

        // Elastic settle back to full size
        seq.Append(transform.DOScale(Vector3.one, spawnSettleDuration).SetEase(Ease.OutElastic));

        seq.OnComplete(FinishSpawn);
        spawnTween = seq;
    }

    private void FinishSpawn()
    {
        isSpawning = false;
        if (isDestroyed) return;

        transform.localScale = Vector3.one;
        if (navObstacle != null) navObstacle.enabled = true;
        if (boxCollider != null) boxCollider.enabled = true;

        StartCoroutine(LifetimeRoutine());
    }

    /// <summary>Procedural dust puff: small sand-coloured spheres that spread out and fade by shrinking.</summary>
    private void SpawnDust(Vector3 groundPos)
    {
        if (spawnDustCount <= 0) return;

        GameObject root = new GameObject("SpawnDust");
        root.transform.position = groundPos + Vector3.up * 0.15f;
        Color dustColor = new Color(0.78f, 0.70f, 0.56f, 1f);

        for (int i = 0; i < spawnDustCount; i++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.transform.SetParent(root.transform, false);
            Collider c = puff.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = puff.GetComponent<Renderer>();
            if (r != null) r.material.color = dustColor;

            float ang = (360f / spawnDustCount) * i * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0.2f, Mathf.Sin(ang)).normalized;
            float size = Random.Range(0.25f, 0.4f);
            float travel = Random.Range(1.0f, 1.8f);

            puff.transform.localScale = Vector3.one * size;
            puff.transform.DOMove(puff.transform.position + dir * travel, 0.6f)
                .SetEase(Ease.OutCubic).SetLink(puff);
            puff.transform.DOScale(0.02f, 0.6f)
                .SetEase(Ease.InQuad).SetLink(puff);
        }

        Destroy(root, 0.8f);
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (isDestroyed || isSpawning) return;

        // In multiplayer, tower shooting and damage calculation belongs to MasterClient authority
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
            return;

        if (Time.time >= nextFireTime)
        {
            PeasantUnitController target = AcquireTarget();
            if (target != null)
            {
                FireAtTarget(target);
                nextFireTime = Time.time + attackCooldown;
            }
        }
    }

    private PeasantUnitController AcquireTarget()
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units == null) return null;

        PeasantUnitController closest = null;
        float closestDistSq = attackRange * attackRange;

        for (int i = 0; i < units.Count; i++)
        {
            PeasantUnitController u = units[i];
            if (u == null || u.IsDead || u.TeamId == playerTeamId) continue;

            float distSq = (u.transform.position - transform.position).sqrMagnitude;
            if (distSq <= closestDistSq)
            {
                closestDistSq = distSq;
                closest = u;
            }
        }

        return closest;
    }

    private void FireAtTarget(PeasantUnitController target)
    {
        Vector3 fireOrigin = transform.position + Vector3.up * (towerHeight + archerHeightOffset + 1.2f);
        if (towerArcherTransform != null)
        {
            Vector3 aimDir = target.transform.position - towerArcherTransform.position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.01f)
            {
                towerArcherTransform.rotation = Quaternion.LookRotation(aimDir);
            }
            fireOrigin = towerArcherTransform.position + Vector3.up * (1.0f * sizeScale);
        }

        if (shootSound != null && audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, shootSound, 0.7f, "bow_release");
        }

        StartCoroutine(ArrowFlightRoutine(fireOrigin, target));
    }

    private IEnumerator ArrowFlightRoutine(Vector3 start, PeasantUnitController target)
    {
        GameObject arrowObj;
        if (arrowPrefab != null)
        {
            arrowObj = Instantiate(arrowPrefab, start, Quaternion.identity);
            ArrowProjectile ap = arrowObj.GetComponent<ArrowProjectile>();
            if (ap != null) Destroy(ap);
        }
        else
        {
            arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowObj.transform.position = start;
            arrowObj.transform.localScale = new Vector3(0.06f, 0.4f, 0.06f);
            Collider c = arrowObj.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        float speed = 28f;
        Vector3 lastTargetPos = target != null ? target.transform.position + Vector3.up * 1f : start + transform.forward * 5f;

        while (arrowObj != null)
        {
            if (target != null && !target.IsDead)
            {
                lastTargetPos = target.transform.position + Vector3.up * 1f;
            }

            Vector3 dir = (lastTargetPos - arrowObj.transform.position);
            float dist = dir.magnitude;

            if (dist < 0.6f)
            {
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(damagePerShot, null);
                }
                Destroy(arrowObj);
                yield break;
            }

            arrowObj.transform.rotation = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(90f, 0f, 0f);
            arrowObj.transform.position += dir.normalized * speed * Time.deltaTime;
            yield return null;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDestroyed || isSpawning || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
            DestroyTower();
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarTransform == null) return;
        float pct = Mathf.Clamp01(currentHealth / maxHealth);
        Vector3 s = initialHealthBarScale;
        s.x = initialHealthBarScale.x * pct;
        healthBarTransform.localScale = s;
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
            DestroyTower();
        }
    }

    private void DestroyTower()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (spawnTween != null && spawnTween.IsActive()) spawnTween.Kill();
        isSpawning = false;

        if (navObstacle != null) navObstacle.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;

        StartCoroutine(SinkAndDestroyRoutine());
    }

    private IEnumerator SinkAndDestroyRoutine()
    {
        float elapsed = 0f;
        Vector3 start = transform.position;
        Vector3 target = start - Vector3.up * (towerHeight + 1f);

        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, elapsed / 0.8f);
            yield return null;
        }

        Destroy(gameObject);
    }
}
