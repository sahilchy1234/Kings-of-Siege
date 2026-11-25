using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Wood Rolling Power-up (The Log).
/// Spawns with a physical drop slam bounce, then rolls forward in a guaranteed straight line
/// aligned with the camera's viewing perspective across the battlefield.
/// Features DOTween drop squash-and-stretch, continuous rolling rotation,
/// impact punch squashing on enemy crush, DOTween launched knockback jumps,
/// wood splinter impact particles, and a final splintering breakup animation.
/// </summary>
public class WoodRollingEffect : MonoBehaviour
{
    [Header("Rolling Properties")]
    [SerializeField] private float rollDistance = 18f;
    [SerializeField] private float rollSpeed = 10f;
    [SerializeField] private float rollWidth = 3.6f;
    [SerializeField] private float rollDamage = 95f;
    [SerializeField] private float knockbackForce = 3.8f;
    [SerializeField] private float rollRotationSpeed = 520f;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject logPrefab;
    [SerializeField] private AudioClip rollAudioClip;
    [SerializeField] private AudioClip smashAudioClip;

    [Header("Juicy Particle Prefabs (PolygonParticles)")]
    [SerializeField] private GameObject woodImpactVFXPrefab;
    [SerializeField] private GameObject dustTrailPrefab;
    [SerializeField] private GameObject dustExplosionPrefab;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    private readonly HashSet<PeasantUnitController> hitUnits = new HashSet<PeasantUnitController>();

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;
    }

    public void Initialize(Vector3 startPoint, Vector3 forwardDirection, int playerTeam = 0,
        GameObject customLogPrefab = null, AudioClip customRollAudio = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // 1. Calculate straight forward direction aligned with camera perspective
        forwardDirection.y = 0f;
        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                forwardDirection = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            }
            else if (CameraSwipeController.Instance != null)
            {
                forwardDirection = CameraSwipeController.Instance.ForwardDirection;
            }

            if (forwardDirection.sqrMagnitude < 0.001f)
            {
                forwardDirection = (playerTeam == 0) ? Vector3.right : Vector3.left;
            }
        }
        Vector3 straightForward = forwardDirection.normalized;

        this.playerTeamId = playerTeam;
        if (customDamage > 0f) this.rollDamage = customDamage;
        if (customLogPrefab != null) this.logPrefab = customLogPrefab;
        if (customRollAudio != null) this.rollAudioClip = customRollAudio;

        // Auto-load prefabs & audio
        if (logPrefab == null)
        {
            logPrefab = Resources.Load<GameObject>("Powerups/WoodRollFinal");
            if (logPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("WoodRollFinal t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    logPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (smashAudioClip == null)
        {
            smashAudioClip = Resources.Load<AudioClip>("Sounds/dragon-studio-hammer-smash-effect-382731");
            if (smashAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-hammer-smash-effect-382731 t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    smashAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

#if UNITY_EDITOR
        if (woodImpactVFXPrefab == null)
            woodImpactVFXPrefab = LoadPrefabByName("FX_Impact_Wood_01");
        if (dustTrailPrefab == null)
            dustTrailPrefab = LoadPrefabByName("FX_Trail_Dust_01");
        if (dustExplosionPrefab == null)
            dustExplosionPrefab = LoadPrefabByName("FX_Dust_Big_01");
#endif

        // Position on ground elevation
        Vector3 spawnPos = startPoint;
        if (Physics.Raycast(startPoint + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos.y = hit.point.y;
        }

        transform.position = spawnPos;
        transform.rotation = Quaternion.LookRotation(straightForward);

        StartCoroutine(RollRoutine(spawnPos, straightForward));
    }

    private static GameObject LoadPrefabByName(string prefabName)
    {
        string[] guids = RuntimeAssets.FindAssets($"{prefabName} t:Prefab");
        if (guids != null && guids.Length > 0)
        {
            return RuntimeAssets.LoadAssetAtPath<GameObject>(
                RuntimeAssets.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    private IEnumerator RollRoutine(Vector3 spawnPos, Vector3 straightDir)
    {
        // Setup visual log object
        GameObject logVisual = null;
        if (logPrefab != null)
        {
            logVisual = Instantiate(logPrefab, transform.position, transform.rotation, transform);
            logVisual.transform.localPosition = Vector3.zero;
            logVisual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            logVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logVisual.transform.SetParent(transform, false);
            logVisual.transform.localPosition = Vector3.zero;
            logVisual.transform.localScale = new Vector3(0.9f, rollWidth * 0.5f, 0.9f);
            logVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Collider c = logVisual.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = logVisual.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.42f, 0.28f, 0.16f);
        }

        // Disable child NavMeshAgent if present
        NavMeshAgent childAgent = logVisual.GetComponent<NavMeshAgent>();
        if (childAgent != null) Destroy(childAgent);

        Vector3 baseScale = logVisual.transform.localScale;

        // 1. JUICY DROP-IN SLAM WITH DOTWEEN
        Vector3 dropStart = spawnPos + Vector3.up * 3.8f;
        transform.position = dropStart;
        transform.DOMove(spawnPos, 0.22f).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(0.22f);

        // Ground slam squash and elastic bounce
        Sequence dropSquash = DOTween.Sequence();
        dropSquash.Append(logVisual.transform.DOScale(
            new Vector3(baseScale.x * 1.25f, baseScale.y * 0.75f, baseScale.z * 1.25f),
            0.08f).SetEase(Ease.OutQuad));
        dropSquash.Append(logVisual.transform.DOScale(baseScale, 0.2f).SetEase(Ease.OutBounce));

        if (smashAudioClip != null && audioSource != null)
        {
            audioSource.pitch = 1.15f;
            KoSAudio.PlayOneShot(audioSource, smashAudioClip, 0.55f, "wood_smash");
        }

        if (CameraSwipeController.Instance != null)
        {
            CameraSwipeController.Instance.TriggerShake(0.18f, 0.12f);
        }

        SpawnWoodSplat(spawnPos);

        // 2. Attach rolling dust trail
        GameObject dustTrailInstance = null;
        if (dustTrailPrefab != null)
        {
            dustTrailInstance = Instantiate(dustTrailPrefab, transform);
            dustTrailInstance.transform.localPosition = Vector3.back * 0.5f;
        }

        if (rollAudioClip != null && audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, rollAudioClip, 0.85f, "log_roll");
        }

        // 3. Straight Rolling Navigation
        Vector3 startOrigin = spawnPos;
        Vector3 straightTarget = startOrigin + (straightDir * rollDistance);
        float estimatedDuration = rollDistance / Mathf.Max(1f, rollSpeed);

        CinematicCameraController.TriggerWoodLog(transform, straightDir, estimatedDuration, this.playerTeamId);

        float elapsed = 0f;
        while (elapsed < estimatedDuration)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { yield return null; continue; }

            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / estimatedDuration);

            // Perfectly straight horizontal path aligned with camera perspective
            Vector3 currentFlatPos = Vector3.Lerp(startOrigin, straightTarget, t);

            // Stop at towers / castles / buildings instead of rolling into or onto them
            if (IsBuildingAhead(currentFlatPos, straightDir))
            {
                CheckCollisionsAlongPath(straightDir, logVisual, baseScale);
                break;
            }

            // Hug terrain elevation, ignoring buildings and units so the log never climbs them
            currentFlatPos.y = GetGroundHeight(currentFlatPos, transform.position.y);

            transform.position = currentFlatPos;
            transform.rotation = Quaternion.LookRotation(straightDir);

            // Continuous rolling rotation around transverse axis
            if (logVisual != null)
            {
                logVisual.transform.Rotate(Vector3.right, rollRotationSpeed * dt, Space.Self);
            }

            CheckCollisionsAlongPath(straightDir, logVisual, baseScale);
            yield return null;
        }

        // 4. JUICY END-OF-ROLL SHATTER BREAKUP WITH DOTWEEN
        CinematicCameraController.NotifyWoodLogFinished();

        if (dustTrailInstance != null)
        {
            ParticleSystem[] ps = dustTrailInstance.GetComponentsInChildren<ParticleSystem>();
            for (int p = 0; p < ps.Length; p++) ps[p].Stop();
            Destroy(dustTrailInstance, 1.2f);
        }

        // Final ground shatter audio and dust burst
        if (smashAudioClip != null && audioSource != null)
        {
            audioSource.pitch = 0.92f;
            KoSAudio.PlayOneShot(audioSource, smashAudioClip, 0.95f, "wood_smash");
        }

        if (CameraSwipeController.Instance != null)
        {
            CameraSwipeController.Instance.TriggerShake(0.38f, 0.25f);
        }

        SpawnWoodSplat(transform.position);
        SpawnLogSplinters(transform.position, straightDir);

        if (dustExplosionPrefab != null)
        {
            GameObject de = Instantiate(dustExplosionPrefab, transform.position, Quaternion.identity);
            de.transform.localScale = Vector3.one * 1.15f;
            Destroy(de, 2.0f);
        }

        // DOTween shatter animation
        if (logVisual != null)
        {
            Sequence breakSeq = DOTween.Sequence();
            breakSeq.Append(logVisual.transform.DOShakePosition(0.2f, 0.28f, 28));
            breakSeq.Join(logVisual.transform.DOScale(Vector3.zero, 0.24f).SetEase(Ease.InBack));
            yield return breakSeq.WaitForCompletion();
        }

        yield return new WaitForSeconds(0.6f);
        Destroy(gameObject);
    }

    private void SpawnLogSplinters(Vector3 center, Vector3 forwardDir)
    {
        int count = 6;
        for (int i = 0; i < count; i++)
        {
            GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            splinter.name = "[LogSplinter]";
            splinter.transform.SetParent(transform, true);
            splinter.transform.position = center + Vector3.up * 0.35f;
            splinter.transform.localScale = new Vector3(Random.Range(0.2f, 0.35f), Random.Range(0.5f, 1.1f), Random.Range(0.2f, 0.35f));

            Collider col = splinter.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = splinter.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh != null)
                {
                    Material m = new Material(sh);
                    m.color = new Color(0.42f, 0.28f, 0.16f);
                    rend.material = m;
                }
            }

            Vector3 scatterDir = (forwardDir * Random.Range(0.8f, 1.8f) + Random.insideUnitSphere * 1.2f).normalized;
            scatterDir.y = 0f;
            Vector3 target = center + scatterDir * Random.Range(2.2f, 4.8f);
            float flyTime = Random.Range(0.48f, 0.68f);

            splinter.transform.DOJump(target, Random.Range(1.8f, 3.2f), 1, flyTime).SetEase(Ease.OutQuad);
            splinter.transform.DORotate(Random.insideUnitSphere * 720f, flyTime, RotateMode.WorldAxisAdd);
            splinter.transform.DOScale(Vector3.zero, 0.22f).SetDelay(flyTime * 0.72f).OnComplete(() => Destroy(splinter));
        }
    }

    private void CheckCollisionsAlongPath(Vector3 forwardDir, GameObject logVisual, Vector3 baseScale)
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units != null)
        {
            Vector3 logPos = transform.position;
            Vector3 logRight = Vector3.Cross(Vector3.up, forwardDir).normalized;

            for (int i = 0; i < units.Count; i++)
            {
                PeasantUnitController unit = units[i];
                if (unit == null || unit.IsDead || unit.TeamId == playerTeamId) continue;
                if (hitUnits.Contains(unit)) continue;

                Vector3 toUnit = unit.transform.position - logPos;
                toUnit.y = 0f;

                float forwardDist = Vector3.Dot(toUnit, forwardDir);
                float lateralDist = Mathf.Abs(Vector3.Dot(toUnit, logRight));

                // Hitbox along the rolling path
                if (forwardDist >= -0.8f && forwardDist <= 1.6f && lateralDist <= rollWidth * 0.5f)
                {
                    hitUnits.Add(unit);

                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        unit.TakeDamage(rollDamage, null);

                        // DOTween squash punch reaction on the rolling log
                        if (logVisual != null)
                        {
                            logVisual.transform.DOPunchScale(new Vector3(0.18f, -0.22f, 0.18f), 0.14f, 10, 0.5f);
                        }

                        // DOTween launched bowling-pin knockback jump on hit enemy with violent tumble
                        Vector3 push = (forwardDir * 1.5f + (unit.transform.position - logPos).normalized * 0.5f).normalized;
                        push.y = 0f;
                        Vector3 knockTarget = unit.transform.position + push * (knockbackForce * 1.25f);
                        unit.transform.DOJump(knockTarget, jumpPower: 1.6f, numJumps: 1, duration: 0.35f).SetEase(Ease.OutQuad);
                        unit.transform.DORotate(new Vector3(Random.Range(360f, 720f), Random.Range(-180f, 180f), 0f), 0.35f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad);
                        unit.transform.DOPunchScale(new Vector3(0.35f, -0.25f, 0.35f), 0.2f, 8, 0.5f);

                        // Wood splinter particle impact
                        SpawnWoodSplat(unit.transform.position + Vector3.up * 0.5f);

                        if (smashAudioClip != null && audioSource != null)
                        {
                            audioSource.pitch = Random.Range(0.92f, 1.25f);
                            KoSAudio.PlayOneShot(audioSource, smashAudioClip, 0.7f, "wood_smash");
                        }

                        if (CameraSwipeController.Instance != null)
                        {
                            CameraSwipeController.Instance.TriggerShake(0.2f, 0.1f);
                        }
                    }
                }
            }
        }

        // Damage enemy buildings or towers encountered along the path
        Collider[] nearby = Physics.OverlapSphere(transform.position, rollWidth * 0.55f);
        for (int c = 0; c < nearby.Length; c++)
        {
            PeasantObjectiveTarget obj = nearby[c].GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && obj.TeamId != playerTeamId)
            {
                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint != null ? obj.TargetPoint.position : obj.transform.position, obj.TeamId))
                {
                    obj.TakeDamage(rollDamage, null);
                    break;
                }
            }
        }
    }

    private static bool IsBuildingCollider(Collider c)
    {
        if (c == null || c.isTrigger) return false;
        return c.GetComponentInParent<PeasantObjectiveTarget>() != null
            || c.GetComponentInParent<TowerCombatController>() != null;
    }

    private bool IsBuildingAhead(Vector3 flatPos, Vector3 dir)
    {
        // Any solid obstacle at log height in front (towers, stone barriers, walls, rocks, castles)
        float groundY = transform.position.y;
        Vector3 probe = new Vector3(flatPos.x, groundY + 0.9f, flatPos.z) + dir * 0.8f;
        Collider[] hits = Physics.OverlapSphere(probe, 0.5f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c.transform.IsChildOf(transform)) continue;
            if (c is TerrainCollider) continue;
            if (c.GetComponentInParent<PeasantUnitController>() != null) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            if (c.bounds.max.y < groundY + 0.6f) continue;
            // Skip the ground mesh itself: if this collider's surface under the probe is low, it's floor
            RaycastHit gh;
            if (c.Raycast(new Ray(probe + Vector3.up * 5f, Vector3.down), out gh, 20f) && gh.point.y < groundY + 0.5f) continue;
            return true;
        }
        return false;
    }

    private float GetGroundHeight(Vector3 flatPos, float fallbackY)
    {
        RaycastHit[] hits = Physics.RaycastAll(flatPos + Vector3.up * 10f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (IsBuildingCollider(c)) continue;
            if (c.GetComponentInParent<PeasantUnitController>() != null) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            // never step up more than a small bump in one frame
            if (hits[i].point.y > fallbackY + 0.5f) continue;
            if (hits[i].point.y > best) best = hits[i].point.y;
        }
        return float.IsNegativeInfinity(best) ? fallbackY : best;
    }

    private void SpawnWoodSplat(Vector3 pos)
    {
        if (woodImpactVFXPrefab != null)
        {
            GameObject fx = Instantiate(woodImpactVFXPrefab, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 0.8f;
            Destroy(fx, 1.5f);
        }
    }
}
