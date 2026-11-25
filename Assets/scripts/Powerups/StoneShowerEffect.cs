using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Handles the Stone Throw / Stone Shower power-up effect.
/// Hurls colossal catapult boulders in majestic parabolic ballistic arcs from the friendly backline,
/// featuring expanding ground impact anticipation decals, tumbling physics, heavy aerodynamic dust trails,
/// massive DOTween squash-and-stretch ground slam impacts, radial shockwave knockback,
/// polygon particle explosions (dust + rock shards + ground cracks), and NavMeshObstacle carving.
/// </summary>
public class StoneShowerEffect : MonoBehaviour
{
    [Header("Stone Throw Parameters")]
    [SerializeField] private float damage = 999f;
    [SerializeField] private float impactRadius = 4.8f;
    [SerializeField] private float flightDuration = 1.15f;
    [SerializeField] private float arcHeight = 14f;
    [SerializeField] private float launchDistanceMin = 28f;
    [SerializeField] private float launchDistanceMax = 36f;
    [SerializeField] private float knockbackForce = 7.5f;
    [SerializeField] private int boulderCount = 1;

    [Header("Camera Vibration")]
    [Tooltip("Heavy camera tremor intensity when the massive boulder impacts the ground.")]
    [SerializeField] private float vibrationIntensity = 0.65f;
    [SerializeField] private float vibrationDuration = 0.42f;

    [Header("Visual & Audio Assets")]
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private AudioClip impactAudioClip;
    [SerializeField] private AudioClip throwAudioClip;

    [Header("Juicy Particle Prefabs (PolygonParticles)")]
    [SerializeField] private GameObject rockShardExplosionPrefab;
    [SerializeField] private GameObject dustExplosionPrefab;
    [SerializeField] private GameObject groundCrackPrefab;
    [SerializeField] private GameObject trailDustPrefab;

    private AudioSource audioSource;
    private int playerTeamId = 0;

    public void Initialize(Vector3 center, float showerRadius, int playerTeam = 0,
        GameObject customRockPrefab = null, AudioClip customImpactAudio = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        transform.position = center;
        this.playerTeamId = playerTeam;
        if (showerRadius > 0f) this.impactRadius = showerRadius;

        if (customDamage > 0f) this.damage = customDamage;
        else this.damage = 999f; // Lethal siege crush

        if (customQuantity > 0) this.boulderCount = customQuantity;
        if (customRockPrefab != null) rockPrefab = customRockPrefab;
        if (customImpactAudio != null) impactAudioClip = customImpactAudio;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // Auto-load audio and prefabs if null
        if (rockPrefab == null)
        {
            rockPrefab = Resources.Load<GameObject>("Powerups/Rock_1 Variant 1");
            if (rockPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("Rock_1 Variant 1 t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    rockPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (impactAudioClip == null)
        {
            impactAudioClip = Resources.Load<AudioClip>("Sounds/dragon-studio-hammer-smash-effect-382731");
            if (impactAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-hammer-smash-effect-382731 t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    impactAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (throwAudioClip == null)
        {
            throwAudioClip = Resources.Load<AudioClip>("Sounds/arrowrelease");
            if (throwAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("arrowrelease t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    throwAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        // Particle prefabs. These used to be loaded only in the editor, so Android builds had no
        // stone shards, dust, ground cracks or trail dust. Filters are literal strings so the
        // RuntimeAssetRegistry baker picks them up for device builds.
        if (rockShardExplosionPrefab == null)
            rockShardExplosionPrefab = LoadPrefabByFilter("FX_ShardRock_Explosion_01 t:Prefab");
        if (dustExplosionPrefab == null)
            dustExplosionPrefab = LoadPrefabByFilter("FX_Dust_Big_01 t:Prefab");
        if (groundCrackPrefab == null)
            groundCrackPrefab = LoadPrefabByFilter("FX_GroundCrack_Blast_01 t:Prefab");
        if (trailDustPrefab == null)
            trailDustPrefab = LoadPrefabByFilter("FX_Trail_Dust_01 t:Prefab");

        StartCoroutine(ExecuteStoneThrowRoutine());
    }

    private static GameObject LoadPrefabByFilter(string filter)
    {
        string[] guids = RuntimeAssets.FindAssets(filter);
        if (guids != null && guids.Length > 0)
        {
            return RuntimeAssets.LoadAssetAtPath<GameObject>(
                RuntimeAssets.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    private IEnumerator ExecuteStoneThrowRoutine()
    {
        Vector3 targetCenter = transform.position;
        if (Physics.Raycast(targetCenter + Vector3.up * 25f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetCenter.y = hit.point.y;
        }

        float launchDirX = (playerTeamId == 1) ? 1f : -1f;

        // If boulderCount is 1: throw 1 Colossal Main Siege Boulder + 2 Companion Debris Rocks
        if (boulderCount <= 1)
        {
            // 1. Launch Primary Colossal Boulder
            Vector3 mainLaunchPos = targetCenter + new Vector3(
                launchDirX * Random.Range(launchDistanceMin, launchDistanceMax),
                Random.Range(12f, 16f),
                Random.Range(-3f, 3f));

            StartCoroutine(ThrowSingleBoulderRoutine(
                mainLaunchPos, targetCenter,
                scale: Vector3.one * 2.7f,
                customArc: arcHeight,
                customDuration: flightDuration,
                isPrimary: true));

            // 2. Launch 2 Accompanying Cluster Rocks (Trebuchet Shatter payload)
            yield return new WaitForSeconds(0.06f);

            Vector2 offsetA = Random.insideUnitCircle.normalized * (impactRadius * 0.45f);
            Vector3 targetA = targetCenter + new Vector3(offsetA.x, 0f, offsetA.y);
            Vector3 launchA = targetA + new Vector3(launchDirX * (launchDistanceMin + 4f), 13.5f, -3.5f);

            StartCoroutine(ThrowSingleBoulderRoutine(
                launchA, targetA,
                scale: Vector3.one * 1.35f,
                customArc: arcHeight * 0.88f,
                customDuration: flightDuration + 0.08f,
                isPrimary: false));

            yield return new WaitForSeconds(0.08f);

            Vector2 offsetB = -offsetA * 0.8f;
            Vector3 targetB = targetCenter + new Vector3(offsetB.x, 0f, offsetB.y);
            Vector3 launchB = targetB + new Vector3(launchDirX * (launchDistanceMin + 2f), 14f, 3.8f);

            StartCoroutine(ThrowSingleBoulderRoutine(
                launchB, targetB,
                scale: Vector3.one * 1.2f,
                customArc: arcHeight * 0.92f,
                customDuration: flightDuration + 0.12f,
                isPrimary: false));
        }
        else
        {
            // Salvo throw for custom multi-boulder quantities
            for (int i = 0; i < boulderCount; i++)
            {
                Vector2 scatter = (i == 0) ? Vector2.zero : Random.insideUnitCircle * (impactRadius * 0.6f);
                Vector3 targetPoint = targetCenter + new Vector3(scatter.x, 0f, scatter.y);
                Vector3 launchPoint = targetPoint + new Vector3(
                    launchDirX * Random.Range(launchDistanceMin, launchDistanceMax),
                    Random.Range(12f, 16f),
                    Random.Range(-4f, 4f));

                StartCoroutine(ThrowSingleBoulderRoutine(
                    launchPoint, targetPoint,
                    scale: Vector3.one * 2.3f,
                    customArc: arcHeight + Random.Range(-1.5f, 1.5f),
                    customDuration: flightDuration + Random.Range(-0.05f, 0.05f),
                    isPrimary: (i == 0)));

                yield return new WaitForSeconds(0.22f);
            }
        }

        // Wait until all boulders settle and despawn
        yield return new WaitForSeconds(7.5f);
        Destroy(gameObject);
    }

    private IEnumerator ThrowSingleBoulderRoutine(Vector3 spawnPos, Vector3 targetGround, Vector3 scale, float customArc, float customDuration, bool isPrimary)
    {
        // 1. Play catapult launch whoosh
        if (throwAudioClip != null && audioSource != null)
        {
            audioSource.pitch = isPrimary ? 0.62f : Random.Range(0.75f, 0.95f);
            KoSAudio.PlayOneShot(audioSource, throwAudioClip, isPrimary ? 0.85f : 0.45f, "stone_throw");
        }

        // 3. Instantiate Boulder Object
        GameObject rockObj;
        if (rockPrefab != null)
        {
            rockObj = Instantiate(rockPrefab, spawnPos, Random.rotation, transform);
            rockObj.transform.localScale = scale;

            // Make child colliders convex
            MeshCollider[] mcs = rockObj.GetComponentsInChildren<MeshCollider>();
            for (int i = 0; i < mcs.Length; i++) mcs[i].convex = true;
        }
        else
        {
            rockObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rockObj.transform.SetParent(transform);
            rockObj.transform.position = spawnPos;
            rockObj.transform.localScale = scale;
            Renderer r = rockObj.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.42f, 0.38f, 0.35f);
        }

        // Remove any rigidbodies during DOTween flight to avoid physics conflicts
        Rigidbody oldRb = rockObj.GetComponent<Rigidbody>();
        if (oldRb != null) Destroy(oldRb);

        // 4. Attach aerodynamic wind streak and dust trail
        TrailRenderer trail = CreateRockTrail(rockObj, isPrimary ? 0.45f : 0.25f);
        GameObject dustTrailInstance = null;
        if (trailDustPrefab != null)
        {
            dustTrailInstance = Instantiate(trailDustPrefab, rockObj.transform);
            dustTrailInstance.transform.localPosition = Vector3.zero;
            dustTrailInstance.transform.localScale = Vector3.one * (isPrimary ? 1.0f : 0.5f);
        }

        // 5. DOTWEEN FLIGHT ANIMATION: Heavy Parabolic Catapult Throw
        // DOJump handles X/Z linear movement and Y parabolic gravity curve seamlessly!
        rockObj.transform.position = spawnPos;
        rockObj.transform.DOJump(targetGround, customArc, 1, customDuration).SetEase(Ease.Linear);

        // Heavy violent 3D tumble rotation
        Vector3 tumbleAxis = new Vector3(Random.Range(0.6f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.8f)).normalized;
        rockObj.transform.DORotate(tumbleAxis * (360f * Random.Range(2.2f, 3.8f)), customDuration, RotateMode.WorldAxisAdd).SetEase(Ease.Linear);

        // Wait for flight to complete
        yield return new WaitForSeconds(customDuration);

        if (rockObj == null)
        {
            yield break;
        }

        // 6. THE GRAND SLAM: IMPACT!
        rockObj.transform.DOKill();
        rockObj.transform.position = targetGround;

        // Turn off trails
        if (trail != null) trail.emitting = false;
        if (dustTrailInstance != null)
        {
            ParticleSystem[] ps = dustTrailInstance.GetComponentsInChildren<ParticleSystem>();
            for (int p = 0; p < ps.Length; p++) ps[p].Stop();
            Destroy(dustTrailInstance, 1.5f);
        }

        // JUICY DOTWEEN SQUASH & STRETCH SLAM
        Sequence slamSeq = DOTween.Sequence();
        // High-impact pancake squash into ground
        slamSeq.Append(rockObj.transform.DOScale(
            new Vector3(scale.x * 1.55f, scale.y * 0.48f, scale.z * 1.55f),
            0.06f).SetEase(Ease.OutQuad));
        // Elastic rebound bounce & settle
        slamSeq.Append(rockObj.transform.DOScale(scale, 0.32f).SetEase(Ease.OutBounce));

        // Micro ground hop / settle into crater
        rockObj.transform.DOJump(targetGround, 0.36f, 1, 0.28f).SetEase(Ease.OutBounce);

        // 7. Spawn Polygon Particle Explosions
        SpawnImpactVFX(targetGround, isPrimary);

        // 8. Heavy Camera Tremor Punch
        if (isPrimary && CameraSwipeController.Instance != null && vibrationIntensity > 0f)
        {
            CameraSwipeController.Instance.TriggerShake(vibrationIntensity, vibrationDuration);
        }

        // 9. Earth-Shattering Impact Audio
        if (impactAudioClip != null && audioSource != null)
        {
            audioSource.pitch = isPrimary ? Random.Range(0.72f, 0.86f) : Random.Range(0.92f, 1.15f);
            KoSAudio.PlayOneShot(audioSource, impactAudioClip, isPrimary ? 1.0f : 0.65f, "stone_impact");
        }

        // 10. Lethal Blast AOE Damage & Radial Ragdoll Knockback
        float effectiveRadius = isPrimary ? impactRadius : (impactRadius * 0.65f);
        float effectiveDamage = isPrimary ? damage : (damage * 0.35f);
        float effectiveKnockback = isPrimary ? knockbackForce : (knockbackForce * 0.5f);
        ApplyBlastDamageAndKnockback(targetGround, effectiveRadius, effectiveDamage, effectiveKnockback);

        // 11. NavMesh Obstacle Carving (Primary boulder blocks battlefield path)
        NavMeshObstacle navObstacle = null;
        if (isPrimary)
        {
            navObstacle = rockObj.AddComponent<NavMeshObstacle>();
            navObstacle.shape = NavMeshObstacleShape.Box;
            navObstacle.size = new Vector3(3.2f, 2.8f, 3.2f);
            navObstacle.carving = true;
            navObstacle.carveOnlyStationary = false;
        }

        // 12. Rest firmly on the battlefield as physical obstacle
        yield return new WaitForSeconds(isPrimary ? 4.8f : 3.2f);

        // 13. JUICY CRUMBLE & SINK DESPAWN WITH DOTWEEN
        if (rockObj != null)
        {
            if (navObstacle != null) navObstacle.enabled = false;

            // Pre-sink crack shudder
            rockObj.transform.DOShakePosition(0.35f, 0.12f, 22);
            yield return new WaitForSeconds(0.35f);

            if (rockObj != null)
            {
                Sequence sinkSeq = DOTween.Sequence();
                sinkSeq.Append(rockObj.transform.DOMoveY(targetGround.y - 3.2f, 0.9f).SetEase(Ease.InBack));
                sinkSeq.Join(rockObj.transform.DOScale(Vector3.zero, 0.9f).SetEase(Ease.InBack));
                sinkSeq.OnComplete(() => Destroy(rockObj));
            }
        }
    }

    private TrailRenderer CreateRockTrail(GameObject rockObj, float width)
    {
        TrailRenderer trail = rockObj.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.startWidth = width;
        trail.endWidth = 0.02f;
        trail.minVertexDistance = 0.18f;
        trail.autodestruct = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(0.48f, 0.42f, 0.35f, 0.45f);
            trail.material = mat;
        }

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.65f, 0.58f, 0.48f), 0f),
                new GradientColorKey(new Color(0.38f, 0.35f, 0.32f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.48f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        return trail;
    }

    private void SpawnImpactVFX(Vector3 point, bool isPrimary)
    {
        float scaleMul = isPrimary ? 1.0f : 0.55f;

        // 1. Radial Ground Crack Blast
        if (groundCrackPrefab != null && isPrimary)
        {
            GameObject crack = Instantiate(groundCrackPrefab, point + Vector3.up * 0.025f, Quaternion.identity);
            crack.transform.localScale = Vector3.one * 1.25f;
            Destroy(crack, 4.5f);
        }

        // 2. Giant Billowing Dust Explosion
        if (dustExplosionPrefab != null)
        {
            GameObject dust = Instantiate(dustExplosionPrefab, point, Quaternion.identity);
            dust.transform.localScale = Vector3.one * scaleMul * 1.35f;
            Destroy(dust, 2.5f);
        }

        // 3. Shard Rock & Flying Stone Debris Burst
        if (rockShardExplosionPrefab != null)
        {
            GameObject shards = Instantiate(rockShardExplosionPrefab, point + Vector3.up * 0.2f, Quaternion.identity);
            shards.transform.localScale = Vector3.one * scaleMul * 1.2f;
            Destroy(shards, 2.5f);
        }
    }

    private void ApplyBlastDamageAndKnockback(Vector3 point, float radius, float dmg, float knockback)
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                PeasantUnitController unit = units[i];
                if (unit == null || unit.IsDead || unit.TeamId == playerTeamId) continue;

                Vector3 diff = unit.transform.position - point;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist <= radius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        unit.TakeDamage(dmg, null);

                        if (dist > 0.05f)
                        {
                            Vector3 pushDir = diff.normalized;
                            Vector3 targetPos = unit.transform.position + pushDir * Mathf.Min(4.8f, knockback * (1f - dist / radius));
                            unit.transform.DOJump(targetPos, jumpPower: 1.75f, numJumps: 1, duration: 0.36f).SetEase(Ease.OutQuad);
                            unit.transform.DORotate(new Vector3(Random.Range(360f, 720f) * (Random.value > 0.5f ? 1f : -1f), Random.Range(-180f, 180f), Random.Range(-180f, 180f)), 0.36f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad);
                            unit.transform.DOPunchScale(new Vector3(0.32f, -0.22f, 0.32f), 0.2f, 8, 0.5f);
                        }
                    }
                }
            }
        }
        else
        {
            PeasantUnitController[] allUnits = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                PeasantUnitController unit = allUnits[i];
                if (unit == null || unit.IsDead || unit.TeamId == playerTeamId) continue;

                Vector3 diff = unit.transform.position - point;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist <= radius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        unit.TakeDamage(dmg, null);

                        if (dist > 0.05f)
                        {
                            Vector3 pushDir = diff.normalized;
                            Vector3 targetPos = unit.transform.position + pushDir * Mathf.Min(4.8f, knockback * (1f - dist / radius));
                            unit.transform.DOJump(targetPos, jumpPower: 1.75f, numJumps: 1, duration: 0.36f).SetEase(Ease.OutQuad);
                            unit.transform.DORotate(new Vector3(Random.Range(360f, 720f) * (Random.value > 0.5f ? 1f : -1f), Random.Range(-180f, 180f), Random.Range(-180f, 180f)), 0.36f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad);
                            unit.transform.DOPunchScale(new Vector3(0.32f, -0.22f, 0.32f), 0.2f, 8, 0.5f);
                        }
                    }
                }
            }
        }

        // Damage enemy objective castles and towers caught in the blast radius
        Collider[] nearbyColliders = Physics.OverlapSphere(point, radius);
        for (int c = 0; c < nearbyColliders.Length; c++)
        {
            PeasantObjectiveTarget obj = nearbyColliders[c].GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && obj.TeamId != playerTeamId)
            {
                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint != null ? obj.TargetPoint.position : obj.transform.position, obj.TeamId))
                {
                    obj.TakeDamage(dmg * 0.5f, null); // Heavy siege damage against fortifications
                    break;
                }
            }
        }
    }

}
