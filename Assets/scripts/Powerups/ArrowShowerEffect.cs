using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Handles the Arrow Shower power-up effect.
/// Launches a majestic ballistic volley of arrows in high-arching parabolic trajectories
/// from the friendly backline, showering the target circular area with dynamic velocity-aligned
/// arrows with wind trails, ground-sticking physics, shaft quiver twang, dust puffs, and area damage.
/// </summary>
public class ArrowShowerEffect : MonoBehaviour
{
    [Header("Shower Parameters")]
    [SerializeField] private float radius = 5.5f;
    [SerializeField] private float duration = 2.4f;
    [SerializeField] private int arrowCount = 38;
    [SerializeField] private float damagePerArrow = 24f;
    [SerializeField] private float arrowSpeed = 28f;
    [SerializeField] private float minArcHeight = 11f;
    [SerializeField] private float maxArcHeight = 16f;
    [SerializeField] private float launchDistanceMin = 24f;
    [SerializeField] private float launchDistanceMax = 34f;
    [SerializeField] private float hitRadius = 1.6f;

    [Header("Camera Vibration")]
    [Tooltip("Subtle screen vibration/tremor intensity as arrows pepper the ground.")]
    [SerializeField] private float vibrationIntensity = 0.05f;
    [SerializeField] private float vibrationDuration = 0.06f;

    [Header("Visual & Audio Assets")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private GameObject bloodVFXPrefab;
    [Tooltip("Particle prefab spawned when arrows strike a machine (wood splinters / sparks).")]
    [SerializeField] private GameObject machineVFXPrefab;
    [SerializeField] private AudioClip hitAudioClip;
    [SerializeField] private AudioClip releaseAudioClip;

    [Header("Trail Visuals")]
    [SerializeField] private bool enableTrails = true;
    [SerializeField] private float trailWidth = 0.08f;
    [SerializeField] private float trailLifetime = 0.16f;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    private static Material sharedTrailMaterial;

    public void Initialize(Vector3 center, float showerRadius, int playerTeam = 0,
        GameObject customArrowPrefab = null, AudioClip customHitAudio = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        transform.position = center;
        this.radius = showerRadius;
        this.playerTeamId = playerTeam;

        if (customQuantity > 0) this.arrowCount = customQuantity;
        if (customDamage > 0f) this.damagePerArrow = customDamage;
        if (customDuration > 0f) this.duration = customDuration;

        if (customArrowPrefab != null) arrowPrefab = customArrowPrefab;
        if (customHitAudio != null) hitAudioClip = customHitAudio;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

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

        if (hitAudioClip == null)
        {
            hitAudioClip = Resources.Load<AudioClip>("Sounds/arrowhit");
            if (hitAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("arrowhit t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    hitAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (releaseAudioClip == null)
        {
            releaseAudioClip = Resources.Load<AudioClip>("Sounds/arrowrelease");
            if (releaseAudioClip == null)
            {
                string[] guids = RuntimeAssets.FindAssets("arrowrelease t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    releaseAudioClip = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (bloodVFXPrefab == null)
        {
            bloodVFXPrefab = Resources.Load<GameObject>("Effects/ArrowHit_Blood");
        }

        if (machineVFXPrefab == null)
        {
            machineVFXPrefab = Resources.Load<GameObject>("Effects/ArrowHit_Wood");
        }

        if (bloodVFXPrefab == null)
        {
            string[] bloodGuids = RuntimeAssets.FindAssets("FX_BloodSplat_01 t:Prefab");
            if (bloodGuids != null && bloodGuids.Length > 0)
            {
                bloodVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(bloodGuids[0]));
            }
        }

        if (machineVFXPrefab == null)
        {
            string[] machineGuids = RuntimeAssets.FindAssets("FX_Impact_Wood_01 t:Prefab");
            if (machineGuids == null || machineGuids.Length == 0)
                machineGuids = RuntimeAssets.FindAssets("FX_Impact_Metal_01 t:Prefab");
            if (machineGuids == null || machineGuids.Length == 0)
                machineGuids = RuntimeAssets.FindAssets("FX_Sparks_01 t:Prefab");

            if (machineGuids != null && machineGuids.Length > 0)
            {
                machineVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(machineGuids[0]));
            }
        }

        if (impactVFXPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Impact_Dirt_01 t:Prefab");
            if (guids == null || guids.Length == 0)
                guids = RuntimeAssets.FindAssets("CFXR2 Ground Hit t:Prefab");

            if (guids != null && guids.Length > 0)
            {
                impactVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }

        CinematicCameraController.TriggerArrowShower(center, this.duration, this.playerTeamId);

        StartCoroutine(ExecuteShowerRoutine());
    }

    private static Material GetOrCreateTrailMaterial()
    {
        if (sharedTrailMaterial != null) return sharedTrailMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            sharedTrailMaterial = new Material(shader);
            sharedTrailMaterial.name = "ArrowShower_SharedTrailMat";
        }
        return sharedTrailMaterial;
    }

    private IEnumerator ExecuteShowerRoutine()
    {
        if (releaseAudioClip != null && audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, releaseAudioClip, 0.95f, "volley_release");
        }

        // Concentrated volley launch window so all arrows land within duration
        float launchWindow = duration * 0.72f;
        float interval = launchWindow / Mathf.Max(1, arrowCount);
        float groundY = transform.position.y;
        float launchDirX = (playerTeamId == 1) ? 1f : -1f;

        for (int i = 0; i < arrowCount; i++)
        {
            // Pick a random landing spot distributed across the circular target area
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 targetLandingPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Raycast ground elevation if available
            if (Physics.Raycast(targetLandingPos + Vector3.up * 15f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                targetLandingPos.y = hit.point.y;
            }
            else
            {
                targetLandingPos.y = groundY;
            }

            // Launch from friendly lines with dynamic spread and elevation
            float launchDistX = launchDirX * Random.Range(launchDistanceMin, launchDistanceMax);
            float launchElevation = Random.Range(8f, 13f);
            float lateralScatter = Random.Range(-radius * 0.75f, radius * 0.75f);
            Vector3 spawnPos = targetLandingPos + new Vector3(launchDistX, launchElevation, lateralScatter);

            float arcHeight = Random.Range(minArcHeight, maxArcHeight);
            float speedVariation = arrowSpeed * Random.Range(0.92f, 1.08f);

            // Layered release audio twangs during the barrage
            if (i > 0 && i % 7 == 0 && releaseAudioClip != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.18f);
                KoSAudio.PlayOneShot(audioSource, releaseAudioClip, 0.45f, "bow_release");
            }

            StartCoroutine(BallisticArrowRoutine(spawnPos, targetLandingPos, arcHeight, speedVariation));

            yield return new WaitForSeconds(interval);
        }

        // Wait for stuck arrows to finish their lifetime before cleaning up the controller
        yield return new WaitForSeconds(5.0f);
        Destroy(gameObject);
    }

    private IEnumerator BallisticArrowRoutine(Vector3 startPos, Vector3 targetPos, float arcHeight, float speed)
    {
        GameObject arrowObj;
        if (arrowPrefab != null)
        {
            arrowObj = Instantiate(arrowPrefab, startPos, Quaternion.identity, transform);
            ArrowProjectile ap = arrowObj.GetComponent<ArrowProjectile>();
            if (ap != null) Destroy(ap);

            // Remove any colliders so arrows don't collide or push units in mid-air
            Collider[] colliders = arrowObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++)
            {
                Destroy(colliders[c]);
            }
        }
        else
        {
            // Fallback procedural cylinder arrow
            arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowObj.transform.SetParent(transform);
            arrowObj.transform.position = startPos;
            arrowObj.transform.localScale = new Vector3(0.06f, 0.45f, 0.06f);
            Collider col = arrowObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // Attach aerodynamic wind streak trail
        TrailRenderer trail = CreateArrowTrail(arrowObj);

        float horizontalDist = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(targetPos.x, 0f, targetPos.z));
        float flightTime = Mathf.Max(0.65f, horizontalDist / speed) + Random.Range(-0.04f, 0.04f);
        float elapsed = 0f;
        Vector3 prevPos = startPos;

        // Set initial orientation toward launch trajectory
        Vector3 initialDirection = (targetPos - startPos);
        initialDirection.y += arcHeight * 1.5f;
        if (initialDirection.sqrMagnitude > 0.001f)
        {
            Vector3 initDir = initialDirection.normalized;
            arrowObj.transform.rotation = (arrowPrefab != null)
                ? Quaternion.FromToRotation(Vector3.down, initDir)
                : Quaternion.FromToRotation(Vector3.up, initDir);
        }

        // Ballistic flight simulation
        while (elapsed < flightTime && arrowObj != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flightTime);

            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, progress);
            nextPos.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;
            arrowObj.transform.position = nextPos;

            // Continually pitch and orient arrow along its instantaneous flight velocity
            Vector3 moveDelta = nextPos - prevPos;
            if (moveDelta.sqrMagnitude > 0.00005f)
            {
                Vector3 flightDir = moveDelta.normalized;
                arrowObj.transform.rotation = (arrowPrefab != null)
                    ? Quaternion.FromToRotation(Vector3.down, flightDir)
                    : Quaternion.FromToRotation(Vector3.up, flightDir);
            }

            prevPos = nextPos;
            yield return null;
        }

        if (arrowObj == null) yield break;

        // Final impact calculations
        Vector3 finalDir = (targetPos - prevPos).normalized;
        if (finalDir.sqrMagnitude < 0.001f) finalDir = Vector3.down;

        // Embed arrow tip into the target surface
        arrowObj.transform.position = targetPos + finalDir * 0.12f;

        Quaternion impactRot = (arrowPrefab != null)
            ? Quaternion.FromToRotation(Vector3.down, finalDir)
            : Quaternion.FromToRotation(Vector3.up, finalDir);
        arrowObj.transform.rotation = impactRot;

        // Turn off trail emission so the streak catches up and dissipates cleanly
        if (trail != null) trail.emitting = false;

        // Spawn impact ground dust VFX
        SpawnImpactFX(targetPos);

        // Subtle camera vibration on arrow impact
        if (CameraSwipeController.Instance != null && vibrationIntensity > 0f)
        {
            CameraSwipeController.Instance.TriggerShake(vibrationIntensity, vibrationDuration);
        }

        // Play randomized hit sound as arrows strike ground and armor
        if (hitAudioClip != null && audioSource != null && Random.value < 0.45f)
        {
            audioSource.pitch = Random.Range(0.85f, 1.25f);
            KoSAudio.PlayOneShot(audioSource, hitAudioClip, Random.Range(0.45f, 0.72f), "arrow_hit");
        }

        // Apply area damage to enemies
        ApplyAreaDamage(targetPos);

        // Damped harmonic quiver animation (shaft vibration twang upon hard ground impact)
        Vector3 quiverAxis = Vector3.Cross(finalDir, Vector3.up);
        if (quiverAxis.sqrMagnitude < 0.01f) quiverAxis = Vector3.right;
        quiverAxis.Normalize();

        float quiverDuration = 0.32f;
        float quiverElapsed = 0f;
        float quiverFrequency = 34f;
        float quiverDamping = 9.5f;
        float twangAngle = Random.Range(8f, 13f);

        while (quiverElapsed < quiverDuration && arrowObj != null)
        {
            quiverElapsed += Time.deltaTime;
            float decay = Mathf.Exp(-quiverDamping * quiverElapsed);
            float angle = Mathf.Sin(quiverElapsed * quiverFrequency) * twangAngle * decay;
            arrowObj.transform.rotation = impactRot * Quaternion.AngleAxis(angle, quiverAxis);
            yield return null;
        }

        if (arrowObj != null) arrowObj.transform.rotation = impactRot;

        // Stick in ground for a few seconds before smoothly sinking and despawning
        yield return new WaitForSeconds(Random.Range(2.5f, 3.8f));

        if (arrowObj != null)
        {
            float sinkDuration = 0.5f;
            float sinkElapsed = 0f;
            Vector3 sinkStartPos = arrowObj.transform.position;
            Vector3 sinkEndPos = sinkStartPos - Vector3.up * 0.9f;

            while (sinkElapsed < sinkDuration && arrowObj != null)
            {
                sinkElapsed += Time.deltaTime;
                arrowObj.transform.position = Vector3.Lerp(sinkStartPos, sinkEndPos, sinkElapsed / sinkDuration);
                yield return null;
            }

            if (arrowObj != null) Destroy(arrowObj);
        }
    }

    private TrailRenderer CreateArrowTrail(GameObject arrowObj)
    {
        if (!enableTrails || arrowObj == null) return null;

        TrailRenderer trail = arrowObj.AddComponent<TrailRenderer>();
        trail.time = trailLifetime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.12f;
        trail.autodestruct = false;

        Material mat = GetOrCreateTrailMaterial();
        if (mat != null) trail.material = mat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.92f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.85f, 0.9f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.55f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        return trail;
    }

    private void SpawnImpactFX(Vector3 point)
    {
        if (impactVFXPrefab != null)
        {
            GameObject fx = SimpleGameObjectPool.Get(impactVFXPrefab, point + Vector3.up * 0.05f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 0.45f;
            SimpleGameObjectPool.ReturnDelayed(fx, 1.2f);
        }
        else
        {
            CreateGroundImpactFX(point);
        }

        CreateGroundPunctureMark(point);
    }

    private void CreateGroundPunctureMark(Vector3 point)
    {
        GameObject puncture = GameObject.CreatePrimitive(PrimitiveType.Quad);
        puncture.name = "ArrowPuncture";
        puncture.transform.SetParent(transform);
        puncture.transform.position = point + Vector3.up * 0.015f;
        puncture.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        puncture.transform.localScale = Vector3.one * Random.Range(0.14f, 0.22f);

        Collider col = puncture.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = puncture.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            Material mat = GetOrCreateTrailMaterial();
            if (mat != null) rend.material = mat;
            rend.material.color = new Color(0.18f, 0.14f, 0.10f, 0.65f); // Dark earth puncture hole
        }

        StartCoroutine(FadeAndDestroyImpact(puncture, 3.2f));
    }

    private void CreateGroundImpactFX(Vector3 point)
    {
        GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dust.transform.SetParent(transform);
        dust.transform.position = point + Vector3.up * 0.03f;
        dust.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        dust.transform.localScale = Vector3.one * Random.Range(0.25f, 0.42f);

        Collider col = dust.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = dust.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            Material mat = GetOrCreateTrailMaterial();
            if (mat != null) rend.material = mat;
            rend.material.color = new Color(0.48f, 0.42f, 0.32f, 0.45f);
        }

        StartCoroutine(FadeAndDestroyImpact(dust, 0.45f));
    }

    private IEnumerator FadeAndDestroyImpact(GameObject dust, float fadeDuration = 0.45f)
    {
        if (dust == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = dust.transform.localScale;
        Vector3 targetScale = startScale * 1.35f;
        Renderer rend = dust.GetComponent<Renderer>();
        Color startCol = rend != null ? rend.material.color : Color.clear;

        while (elapsed < fadeDuration && dust != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            dust.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (rend != null)
            {
                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, t);
                rend.material.color = c;
            }
            yield return null;
        }

        if (dust != null) Destroy(dust);
    }

    private void ApplyAreaDamage(Vector3 impactPoint)
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
        {
            units = BattleAIManager.Instance.GetAllLivingUnits();
        }

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                PeasantUnitController unit = units[i];
                if (unit == null || unit.IsDead) continue;
                if (unit.TeamId == playerTeamId) continue; // friendly

                float dist = Vector3.Distance(impactPoint, unit.transform.position);
                if (dist <= hitRadius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        unit.TakeDamage(damagePerArrow, null);

                        GameObject hitVfx = null;
                        if (unit.HitVfxPrefab != null)
                        {
                            hitVfx = unit.HitVfxPrefab;
                        }
                        else if (unit.IsMachine)
                        {
                            hitVfx = machineVFXPrefab != null ? machineVFXPrefab : (impactVFXPrefab != null ? impactVFXPrefab : bloodVFXPrefab);
                        }
                        else
                        {
                            hitVfx = bloodVFXPrefab != null ? bloodVFXPrefab : impactVFXPrefab;
                        }

                        if (hitVfx != null)
                        {
                            GameObject uHit = SimpleGameObjectPool.Get(hitVfx, unit.transform.position + Vector3.up * 0.65f, Quaternion.identity);
                            uHit.transform.localScale = Vector3.one * 0.32f;
                            SimpleGameObjectPool.ReturnDelayed(uHit, 1.2f);
                        }

                        // Tactile hit flinch & mini recoil jump (squash removed)
                        Vector3 push = (unit.transform.position - impactPoint);
                        push.y = 0f;
                        if (push.sqrMagnitude > 0.001f)
                        {
                            Vector3 targetHop = unit.transform.position + push.normalized * 0.45f;
                            unit.transform.DOJump(targetHop, 0.35f, 1, 0.18f).SetEase(Ease.OutQuad);
                        }
                    }
                }
            }
        }
        else
        {
            // Fallback unit scan
            PeasantUnitController[] allUnits = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                PeasantUnitController unit = allUnits[i];
                if (unit == null || unit.IsDead) continue;
                if (unit.TeamId == playerTeamId) continue;

                float dist = Vector3.Distance(impactPoint, unit.transform.position);
                if (dist <= hitRadius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        unit.TakeDamage(damagePerArrow, null);

                        GameObject hitVfx = null;
                        if (unit.HitVfxPrefab != null)
                        {
                            hitVfx = unit.HitVfxPrefab;
                        }
                        else if (unit.IsMachine)
                        {
                            hitVfx = machineVFXPrefab != null ? machineVFXPrefab : (impactVFXPrefab != null ? impactVFXPrefab : bloodVFXPrefab);
                        }
                        else
                        {
                            hitVfx = bloodVFXPrefab != null ? bloodVFXPrefab : impactVFXPrefab;
                        }

                        if (hitVfx != null)
                        {
                            GameObject uHit = SimpleGameObjectPool.Get(hitVfx, unit.transform.position + Vector3.up * 0.65f, Quaternion.identity);
                            uHit.transform.localScale = Vector3.one * 0.32f;
                            SimpleGameObjectPool.ReturnDelayed(uHit, 1.2f);
                        }

                        Vector3 push = (unit.transform.position - impactPoint);
                        push.y = 0f;
                        if (push.sqrMagnitude > 0.001f)
                        {
                            Vector3 targetHop = unit.transform.position + push.normalized * 0.45f;
                            unit.transform.DOJump(targetHop, 0.35f, 1, 0.18f).SetEase(Ease.OutQuad);
                        }
                    }
                }
            }
        }

        // Damage enemy objective towers or castles if within impact radius
        Collider[] nearbyColliders = Physics.OverlapSphere(impactPoint, hitRadius);
        for (int c = 0; c < nearbyColliders.Length; c++)
        {
            PeasantObjectiveTarget obj = nearbyColliders[c].GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && obj.TeamId != playerTeamId)
            {
                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint != null ? obj.TargetPoint.position : obj.transform.position, obj.TeamId))
                {
                    obj.TakeDamage(damagePerArrow, null);
                    break;
                }
            }
        }
    }
}
