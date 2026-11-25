using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Fire Arrows Power-up.
/// Rains blazing fire arrows onto the battlefield.
/// Features:
/// - Parabolic ballistic flight with burning fire trails and glowing flame heads.
/// - Explosive fiery ground impacts with fiery burst VFX, sparks, and crackling embers.
/// - Lingering ground fire flames burning at the base of stuck arrows.
/// - Charred scorch marks on the terrain that fade over time.
/// - Damped harmonic quiver twang upon burying into the ground.
/// - Tactical knockback hops, fiery impact bursts, and burning DoT on struck enemy units.
/// </summary>
public class FireArrowsEffect : MonoBehaviour
{
    [Header("Shower Parameters")]
    [SerializeField] private float radius = 5.5f;
    [SerializeField] private float duration = 2.4f;
    [SerializeField] private int arrowCount = 40;
    [SerializeField] private float directDamage = 35f;
    [SerializeField] private float burnDamagePerTick = 12f;
    [SerializeField] private float burnDuration = 3f;
    [SerializeField] private float arrowFallSpeed = 30f;
    [SerializeField] private float hitRadius = 1.8f;

    [Header("Camera Vibration")]
    [SerializeField] private float vibrationIntensity = 0.08f;
    [SerializeField] private float vibrationDuration = 0.07f;

    [Header("Visual & Audio Assets")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private GameObject impactFireVFXPrefab;
    [SerializeField] private GameObject lingeringFirePrefab;
    [SerializeField] private AudioClip hitAudioClip;
    [SerializeField] private AudioClip releaseAudioClip;

    [Header("Trail Visuals")]
    [SerializeField] private bool enableTrails = true;
    [SerializeField] private float trailWidth = 0.10f;
    [SerializeField] private float trailLifetime = 0.18f;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    private Material fireArrowMaterial;
    private static Material sharedFireTrailMaterial;
    private static Material sharedScorchMaterial;

    public void Initialize(Vector3 center, float showerRadius, int playerTeam = 0,
        GameObject customArrowPrefab = null, AudioClip customHitAudio = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        transform.position = center;
        this.radius = showerRadius;
        this.playerTeamId = playerTeam;

        if (customQuantity > 0) this.arrowCount = customQuantity;
        if (customDamage > 0f) this.directDamage = customDamage;
        if (customDuration > 0f) this.duration = customDuration;

        if (customArrowPrefab != null) arrowPrefab = customArrowPrefab;
        if (customHitAudio != null) hitAudioClip = customHitAudio;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        CreateFireMaterial();

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

        if (impactFireVFXPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Fire_Explosion_01 t:Prefab");
            if (guids == null || guids.Length == 0)
                guids = RuntimeAssets.FindAssets("CFXR3 Hit Fire B (Air) t:Prefab");

            if (guids != null && guids.Length > 0)
            {
                impactFireVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }

        if (lingeringFirePrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Fire_Small_01 t:Prefab");
            if (guids == null || guids.Length == 0)
                guids = RuntimeAssets.FindAssets("CFXR Fire t:Prefab");

            if (guids != null && guids.Length > 0)
            {
                lingeringFirePrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }

        StartCoroutine(ExecuteShowerRoutine());
    }

    private void CreateFireMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        fireArrowMaterial = new Material(s) { name = "FireArrowHeadMat" };
        fireArrowMaterial.color = new Color(1f, 0.40f, 0.05f);
        if (fireArrowMaterial.HasProperty("_EmissionColor"))
        {
            fireArrowMaterial.EnableKeyword("_EMISSION");
            fireArrowMaterial.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 2.5f);
        }
    }

    private static Material GetOrCreateFireTrailMaterial()
    {
        if (sharedFireTrailMaterial != null) return sharedFireTrailMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        if (shader != null)
        {
            sharedFireTrailMaterial = new Material(shader) { name = "FireArrows_TrailMat" };
        }
        return sharedFireTrailMaterial;
    }

    private static Material GetOrCreateScorchMaterial()
    {
        if (sharedScorchMaterial != null) return sharedScorchMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            sharedScorchMaterial = new Material(shader) { name = "FireArrows_ScorchMat" };
            if (shader.name.StartsWith("Universal Render Pipeline"))
            {
                if (sharedScorchMaterial.HasProperty("_Surface")) sharedScorchMaterial.SetFloat("_Surface", 1f);
                if (sharedScorchMaterial.HasProperty("_Blend")) sharedScorchMaterial.SetFloat("_Blend", 0f);
                if (sharedScorchMaterial.HasProperty("_ZWrite")) sharedScorchMaterial.SetFloat("_ZWrite", 0f);
                sharedScorchMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedScorchMaterial.renderQueue = 3000;
                sharedScorchMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sharedScorchMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            sharedScorchMaterial.color = new Color(0.12f, 0.08f, 0.05f, 0.65f);
        }
        return sharedScorchMaterial;
    }

    private IEnumerator ExecuteShowerRoutine()
    {
        if (releaseAudioClip != null && audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, releaseAudioClip, 0.95f, "volley_release");
        }

        float launchWindow = duration * 0.75f;
        float interval = launchWindow / Mathf.Max(1, arrowCount);
        float groundY = transform.position.y;
        float launchDirX = (playerTeamId == 1) ? 1f : -1f;

        for (int i = 0; i < arrowCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 targetLandingPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (Physics.Raycast(targetLandingPos + Vector3.up * 15f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                targetLandingPos.y = hit.point.y;
            }
            else
            {
                targetLandingPos.y = groundY;
            }

            bool isHeroArrow = (i == 0);
            float launchDistX = launchDirX * Random.Range(24f, 32f);
            float launchElevation = Random.Range(9f, 14f);
            float lateralScatter = Random.Range(-radius * 0.75f, radius * 0.75f);
            Vector3 spawnPos = targetLandingPos + new Vector3(launchDistX, launchElevation, lateralScatter);

            float arcHeight = isHeroArrow ? 13.5f : Random.Range(10f, 15f);
            float speedVariation = arrowFallSpeed * Random.Range(0.92f, 1.08f);

            if (i > 0 && i % 7 == 0 && releaseAudioClip != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.20f);
                KoSAudio.PlayOneShot(audioSource, releaseAudioClip, 0.45f, "bow_release");
            }

            StartCoroutine(FallingArrowRoutine(spawnPos, targetLandingPos, arcHeight, speedVariation, isHeroArrow));
            yield return new WaitForSeconds(interval);
        }

        yield return new WaitForSeconds(5.0f);
        Destroy(gameObject);
    }

    private IEnumerator FallingArrowRoutine(Vector3 startPos, Vector3 targetPos, float arcHeight, float speed, bool isHeroArrow)
    {
        GameObject arrowObj;
        if (arrowPrefab != null)
        {
            arrowObj = Instantiate(arrowPrefab, startPos, Quaternion.identity, transform);
            ArrowProjectile ap = arrowObj.GetComponent<ArrowProjectile>();
            if (ap != null) Destroy(ap);

            Collider[] colliders = arrowObj.GetComponentsInChildren<Collider>();
            for (int c = 0; c < colliders.Length; c++)
            {
                Destroy(colliders[c]);
            }
        }
        else
        {
            arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowObj.transform.SetParent(transform);
            arrowObj.transform.position = startPos;
            arrowObj.transform.localScale = new Vector3(0.06f, 0.45f, 0.06f);
            Collider col = arrowObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // Apply emissive fire material to arrow renderers
        if (fireArrowMaterial != null)
        {
            Renderer[] rends = arrowObj.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < rends.Length; r++)
            {
                rends[r].material = fireArrowMaterial;
            }
        }

        // Attach burning fire trail
        TrailRenderer trail = CreateFireArrowTrail(arrowObj);

        float horizontalDist = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(targetPos.x, 0f, targetPos.z));
        float flightTime = Mathf.Max(0.65f, horizontalDist / speed) + Random.Range(-0.04f, 0.04f);

        if (isHeroArrow)
        {
            CinematicCameraController.TriggerFireArrowProjectile(arrowObj.transform, targetPos, flightTime, this.playerTeamId);
        }

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

        // Parabolic ballistic flight
        while (elapsed < flightTime && arrowObj != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flightTime);

            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, progress);
            nextPos.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;
            arrowObj.transform.position = nextPos;

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

        // Embed arrow tip into target ground surface
        arrowObj.transform.position = targetPos + finalDir * 0.14f;

        Quaternion impactRot = (arrowPrefab != null)
            ? Quaternion.FromToRotation(Vector3.down, finalDir)
            : Quaternion.FromToRotation(Vector3.up, finalDir);
        arrowObj.transform.rotation = impactRot;

        if (trail != null) trail.emitting = false;

        // 1. Spawn explosive fire impact VFX
        SpawnFireImpactFX(targetPos);

        // 2. Spawn lingering fire flame at arrow base
        SpawnLingeringFire(targetPos, arrowObj.transform);

        // 3. Spawn charred ground scorch mark
        CreateGroundScorchMark(targetPos);

        // 4. Camera tremor vibration
        if (CameraSwipeController.Instance != null && vibrationIntensity > 0f)
        {
            CameraSwipeController.Instance.TriggerShake(vibrationIntensity, vibrationDuration);
        }

        // 5. Impact audio
        if (hitAudioClip != null && audioSource != null && Random.value < 0.65f)
        {
            audioSource.pitch = Random.Range(0.85f, 1.25f);
            KoSAudio.PlayOneShot(audioSource, hitAudioClip, Random.Range(0.55f, 0.85f), "arrow_hit");
        }

        // 6. Apply area fire damage to enemy units & towers
        ApplyAreaFireDamage(targetPos);

        // 7. Damped harmonic quiver animation (shaft vibration twang upon hard ground impact)
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

        // Arrow sticks and burns on the ground for 2.4 - 3.4s before smoothly sinking
        yield return new WaitForSeconds(Random.Range(2.4f, 3.4f));

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

    private TrailRenderer CreateFireArrowTrail(GameObject arrowObj)
    {
        if (!enableTrails || arrowObj == null) return null;

        TrailRenderer trail = arrowObj.AddComponent<TrailRenderer>();
        trail.time = trailLifetime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.10f;
        trail.autodestruct = false;

        Material mat = GetOrCreateFireTrailMaterial();
        if (mat != null) trail.material = mat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.40f), 0f),   // Bright yellow-white head
                new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.45f), // Searing flame orange body
                new GradientColorKey(new Color(0.95f, 0.12f, 0.02f), 1f)  // Ruby flame tail
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.60f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        return trail;
    }

    private void SpawnFireImpactFX(Vector3 point)
    {
        if (impactFireVFXPrefab != null)
        {
            GameObject fx = Instantiate(impactFireVFXPrefab, point + Vector3.up * 0.05f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 0.50f;
            Destroy(fx, 1.2f);
        }
        else
        {
            CreateProceduralFireImpact(point);
        }
    }

    private void SpawnLingeringFire(Vector3 point, Transform parentArrow)
    {
        if (lingeringFirePrefab != null)
        {
            GameObject fire = Instantiate(lingeringFirePrefab, point + Vector3.up * 0.03f, Quaternion.identity);
            fire.transform.localScale = Vector3.one * Random.Range(0.35f, 0.45f);
            if (parentArrow != null) fire.transform.SetParent(parentArrow);
            Destroy(fire, 2.0f);
        }
    }

    private void CreateGroundScorchMark(Vector3 point)
    {
        GameObject scorch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        scorch.name = "FireArrowScorch";
        scorch.transform.SetParent(transform);
        scorch.transform.position = point + Vector3.up * 0.015f;
        scorch.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        scorch.transform.localScale = Vector3.one * Random.Range(0.32f, 0.46f);

        Collider col = scorch.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = scorch.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.sharedMaterial = GetOrCreateScorchMaterial();
        }

        StartCoroutine(FadeAndDestroyScorch(scorch, 2.8f));
    }

    private IEnumerator FadeAndDestroyScorch(GameObject scorch, float fadeDuration)
    {
        if (scorch == null) yield break;

        yield return new WaitForSeconds(fadeDuration * 0.6f);

        float elapsed = 0f;
        float fadeTime = fadeDuration * 0.4f;
        Renderer rend = scorch != null ? scorch.GetComponent<Renderer>() : null;
        Color startCol = rend != null ? rend.material.color : Color.clear;

        while (elapsed < fadeTime && scorch != null)
        {
            elapsed += Time.deltaTime;
            if (rend != null)
            {
                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, elapsed / fadeTime);
                rend.material.color = c;
            }
            yield return null;
        }

        if (scorch != null) Destroy(scorch);
    }

    private void CreateProceduralFireImpact(Vector3 point)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Quad);
        burst.transform.SetParent(transform);
        burst.transform.position = point + Vector3.up * 0.04f;
        burst.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        burst.transform.localScale = Vector3.one * Random.Range(0.40f, 0.60f);

        Collider col = burst.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = burst.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            Material mat = GetOrCreateFireTrailMaterial();
            if (mat != null) rend.material = mat;
            rend.material.color = new Color(1f, 0.45f, 0.05f, 0.75f);
        }

        StartCoroutine(FadeAndDestroyImpact(burst, 0.35f));
    }

    private IEnumerator FadeAndDestroyImpact(GameObject fx, float duration)
    {
        if (fx == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = fx.transform.localScale;
        Vector3 targetScale = startScale * 1.5f;
        Renderer rend = fx.GetComponent<Renderer>();
        Color startCol = rend != null ? rend.material.color : Color.clear;

        while (elapsed < duration && fx != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            fx.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (rend != null)
            {
                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, t);
                rend.material.color = c;
            }
            yield return null;
        }

        if (fx != null) Destroy(fx);
    }

    private void ApplyAreaFireDamage(Vector3 impactPoint)
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
                if (unit == null || unit.IsDead || unit.TeamId == playerTeamId) continue;

                float dist = Vector3.Distance(impactPoint, unit.transform.position);
                if (dist <= hitRadius)
                {
                    DamageUnitWithFire(unit, impactPoint);
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

                float dist = Vector3.Distance(impactPoint, unit.transform.position);
                if (dist <= hitRadius)
                {
                    DamageUnitWithFire(unit, impactPoint);
                }
            }
        }

        // Damage enemy objective towers or castles within impact radius
        Collider[] nearbyColliders = Physics.OverlapSphere(impactPoint, hitRadius);
        for (int c = 0; c < nearbyColliders.Length; c++)
        {
            PeasantObjectiveTarget obj = nearbyColliders[c].GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && obj.TeamId != playerTeamId)
            {
                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint != null ? obj.TargetPoint.position : obj.transform.position, obj.TeamId))
                {
                    obj.TakeDamage(directDamage, null);
                    break;
                }
            }
        }
    }

    private void DamageUnitWithFire(PeasantUnitController unit, Vector3 impactPoint)
    {
        if (unit.IsShieldImmune || ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
            return;

        unit.TakeDamage(directDamage, null);

        // Fiery hit burst on unit
        if (impactFireVFXPrefab != null)
        {
            GameObject uHit = Instantiate(impactFireVFXPrefab, unit.transform.position + Vector3.up * 0.65f, Quaternion.identity);
            uHit.transform.localScale = Vector3.one * 0.35f;
            Destroy(uHit, 0.8f);
        }

        // Tactical fiery knockback hop (squash removed)
        Vector3 push = (unit.transform.position - impactPoint);
        push.y = 0f;
        if (push.sqrMagnitude > 0.001f)
        {
            Vector3 targetHop = unit.transform.position + push.normalized * 0.55f;
            unit.transform.DOJump(targetHop, 0.4f, 1, 0.2f).SetEase(Ease.OutQuad);
        }

        StartCoroutine(BurnOverTimeRoutine(unit));
    }

    private IEnumerator BurnOverTimeRoutine(PeasantUnitController unit)
    {
        float elapsed = 0f;
        GameObject attachedFire = null;

        if (lingeringFirePrefab != null && unit != null)
        {
            attachedFire = Instantiate(lingeringFirePrefab, unit.transform.position + Vector3.up * 0.5f, Quaternion.identity, unit.transform);
            attachedFire.transform.localScale = Vector3.one * 0.35f;
        }

        while (elapsed < burnDuration && unit != null && !unit.IsDead)
        {
            yield return new WaitForSeconds(0.6f);
            elapsed += 0.6f;
            if (unit != null && !unit.IsDead)
            {
                unit.TakeDamage(burnDamagePerTick, null);
            }
        }

        if (attachedFire != null) Destroy(attachedFire);
    }
}
