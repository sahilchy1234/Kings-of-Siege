using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Big Fireball Power-up (Clash Royale Fireball style).
/// Launches a massive molten meteor in a high, burning parabolic ballistic arc from the friendly backline,
/// featuring expanding fiery ground anticipation decals, violent tumbling rotation, trailing fire/smoke streaks,
/// a cataclysmic impact squash-and-stretch explosion, radial launched ragdoll knockbacks via DOTween,
/// and heavy ground scorched-crack craters.
/// </summary>
public class BigFireballEffect : MonoBehaviour
{
    [Header("Fireball Parameters")]
    [Tooltip("Visual scale multiplier for the fireball sphere/particle.")]
    [SerializeField] private float fireballScale = 2.4f;
    [Tooltip("Visual scale multiplier for the explosion VFX on impact.")]
    [SerializeField] private float explosionScale = 1.35f;
    [SerializeField] private float blastRadius = 6.5f;
    [SerializeField] private float damage = 220f;
    [SerializeField] private float flightDuration = 1.15f;
    [SerializeField] private float arcHeight = 15f;
    [SerializeField] private float launchDistanceMin = 28f;
    [SerializeField] private float launchDistanceMax = 36f;
    [SerializeField] private float knockbackForce = 8.5f;

    [Header("Visuals & Audio")]
    [SerializeField] private GameObject customBallPrefab;
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private AudioClip launchAudio;
    [SerializeField] private AudioClip explosionAudio;

    [Header("Juicy Particle Prefabs (PolygonParticles)")]
    [SerializeField] private GameObject fireExplosionPrefab;
    [SerializeField] private GameObject groundCrackPrefab;
    [SerializeField] private GameObject fireTrailPrefab;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    private Material fireballMat;

    public void Initialize(Vector3 targetCenter, float radius, int playerTeam = 0,
        GameObject customPrefab = null, AudioClip customExplosionClip = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        transform.position = targetCenter;
        this.playerTeamId = playerTeam;
        if (radius > 1f) this.blastRadius = radius;
        if (customDamage > 0f) this.damage = customDamage;
        if (customPrefab != null) this.customBallPrefab = customPrefab;
        if (customExplosionClip != null) this.explosionAudio = customExplosionClip;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        CreateFireballMaterial();

        if (explosionVFXPrefab == null)
        {
            explosionVFXPrefab = Resources.Load<GameObject>("Powerups/CFXR2 WW Explosion");
            if (explosionVFXPrefab == null)
            {
                string[] fxGuids = RuntimeAssets.FindAssets("CFXR2 WW Explosion t:Prefab");
                if (fxGuids != null && fxGuids.Length > 0)
                {
                    explosionVFXPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(fxGuids[0]));
                }
            }
        }

        if (explosionAudio == null)
        {
            explosionAudio = Resources.Load<AudioClip>("Sounds/dragon-studio-hammer-smash-effect-382731");
            if (explosionAudio == null)
            {
                string[] guids = RuntimeAssets.FindAssets("dragon-studio-hammer-smash-effect-382731 t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    explosionAudio = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

        if (launchAudio == null)
        {
            launchAudio = Resources.Load<AudioClip>("Sounds/arrowrelease");
            if (launchAudio == null)
            {
                string[] guids = RuntimeAssets.FindAssets("arrowrelease t:AudioClip");
                if (guids != null && guids.Length > 0)
                {
                    launchAudio = RuntimeAssets.LoadAssetAtPath<AudioClip>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }

#if UNITY_EDITOR
        if (fireExplosionPrefab == null)
            fireExplosionPrefab = LoadPrefabByName("FX_Fire_Explosion_01");
        if (groundCrackPrefab == null)
            groundCrackPrefab = LoadPrefabByName("FX_GroundCrack_Blast_01");
        if (fireTrailPrefab == null)
            fireTrailPrefab = LoadPrefabByName("FX_Trail_Fire_01");
#endif

        StartCoroutine(LaunchFireballRoutine(targetCenter));
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

    private void CreateFireballMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        fireballMat = new Material(s);
        fireballMat.color = new Color(1f, 0.22f, 0.04f);
        if (fireballMat.HasProperty("_EmissionColor"))
        {
            fireballMat.EnableKeyword("_EMISSION");
            fireballMat.SetColor("_EmissionColor", new Color(1f, 0.38f, 0.05f) * 3.5f);
        }
    }

    private IEnumerator LaunchFireballRoutine(Vector3 targetPos)
    {
        // 1. Calculate ground landing elevation
        Vector3 groundTarget = targetPos;
        if (Physics.Raycast(targetPos + Vector3.up * 20f, Vector3.down, out RaycastHit gHit, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundTarget.y = gHit.point.y;
        }

        // 2. Parabolic launch origin from friendly backline
        float launchDirX = (playerTeamId == 1) ? 1f : -1f;
        Vector3 startPos = groundTarget + new Vector3(
            launchDirX * Random.Range(launchDistanceMin, launchDistanceMax),
            Random.Range(14f, 18f),
            Random.Range(-3f, 3f));

        // 3. Catapult launch whoosh audio
        if (launchAudio != null && audioSource != null)
        {
            audioSource.pitch = 0.58f;
            KoSAudio.PlayOneShot(audioSource, launchAudio, 0.85f, "fireball_launch");
        }

        // 4. Instantiate Fireball Sphere
        GameObject ballObj;
        if (customBallPrefab != null)
        {
            ballObj = Instantiate(customBallPrefab, startPos, Quaternion.identity, transform);
            ballObj.transform.localScale = Vector3.one * fireballScale;
        }
        else
        {
            ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObj.transform.SetParent(transform, false);
            ballObj.transform.position = startPos;
            ballObj.transform.localScale = Vector3.one * fireballScale;
            Collider c = ballObj.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = ballObj.GetComponent<Renderer>();
            if (r != null && fireballMat != null) r.material = fireballMat;
        }

        // 6. Attach fiery flame & smoke trail
        GameObject trailInstance = null;
        if (fireTrailPrefab != null)
        {
            trailInstance = Instantiate(fireTrailPrefab, ballObj.transform);
            trailInstance.transform.localPosition = Vector3.zero;
        }

        TrailRenderer ribbonTrail = CreateFireballRibbonTrail(ballObj);

        // Dynamic radiant point light
        Light fireLight = ballObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.48f, 0.12f);
        fireLight.range = fireballScale * 7.5f;
        fireLight.intensity = 2.6f;
        fireLight.shadows = LightShadows.None;

        CinematicCameraController.TriggerBigFireball(ballObj.transform, groundTarget, flightDuration, this.playerTeamId);

        // 7. DOTWEEN BALLISTIC FLIGHT ANIMATION
        ballObj.transform.position = startPos;
        ballObj.transform.DOJump(groundTarget, arcHeight, 1, flightDuration).SetEase(Ease.Linear);

        // Heavy violent 3D tumble rotation & volatile pulsing core
        ballObj.transform.DORotate(new Vector3(720f, 360f, 180f), flightDuration, RotateMode.WorldAxisAdd).SetEase(Ease.Linear);
        ballObj.transform.DOScale(fireballScale * 1.15f, 0.11f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        // Wait for flight duration
        yield return new WaitForSeconds(flightDuration);

        if (ballObj == null)
        {
            yield break;
        }

        // 8. THE CATACLYSMIC EXPLOSION (THE GRAND IMPACT)
        ballObj.transform.DOKill();
        ballObj.transform.position = groundTarget;

        if (ribbonTrail != null) ribbonTrail.emitting = false;
        if (trailInstance != null)
        {
            ParticleSystem[] ps = trailInstance.GetComponentsInChildren<ParticleSystem>();
            for (int p = 0; p < ps.Length; p++) ps[p].Stop();
            Destroy(trailInstance, 1.5f);
        }

        // Detach and burst the radiant light
        if (fireLight != null)
        {
            fireLight.transform.SetParent(transform, true);
            fireLight.intensity = 9.0f;
            fireLight.range = blastRadius * 3.5f;
            DOTween.To(() => fireLight.intensity, x => fireLight.intensity = x, 0f, 0.45f)
                .OnComplete(() => Destroy(fireLight.gameObject));
        }

        // Instant impact squash before detonating
        ballObj.transform.DOScale(
            new Vector3(fireballScale * 1.8f, fireballScale * 0.3f, fireballScale * 1.8f),
            0.05f).SetEase(Ease.OutQuad);

        yield return new WaitForSeconds(0.05f);
        Destroy(ballObj);

        // 9. Spawn Particle Explosions
        // A. Primary WW Explosion
        if (explosionVFXPrefab != null)
        {
            GameObject exp = Instantiate(explosionVFXPrefab, groundTarget, Quaternion.identity);
            exp.transform.localScale = Vector3.one * explosionScale;

            ParticleSystem[] psList = exp.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in psList)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            Destroy(exp, 4.0f);
        }

        // B. Secondary Massive Fireball Blast
        if (fireExplosionPrefab != null)
        {
            GameObject fExp = Instantiate(fireExplosionPrefab, groundTarget + Vector3.up * 0.2f, Quaternion.identity);
            fExp.transform.localScale = Vector3.one * (explosionScale * 1.25f);
            Destroy(fExp, 3.0f);
        }

        // C. Radial Scorched Ground Crack
        if (groundCrackPrefab != null)
        {
            GameObject crack = Instantiate(groundCrackPrefab, groundTarget + Vector3.up * 0.03f, Quaternion.identity);
            crack.transform.localScale = Vector3.one * 1.5f;
            Destroy(crack, 5.0f);
        }

        // 10. Heavy camera shake punch
        if (CameraSwipeController.Instance != null)
        {
            CameraSwipeController.Instance.TriggerShake(0.75f, 0.45f);
        }

        // 11. Earth-shattering explosion audio
        if (explosionAudio != null && audioSource != null)
        {
            audioSource.pitch = 0.88f;
            KoSAudio.PlayOneShot(audioSource, explosionAudio, 1.0f, "fireball_explode");
        }

        // 12. Devastating AOE Damage & DOTween Airborne Ragdoll Knockback
        ApplyExplosionDamage(groundTarget);

        yield return new WaitForSeconds(2.0f);
        Destroy(gameObject);
    }

    private TrailRenderer CreateFireballRibbonTrail(GameObject ballObj)
    {
        TrailRenderer trail = ballObj.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = fireballScale * 0.9f;
        trail.endWidth = 0.05f;
        trail.minVertexDistance = 0.2f;
        trail.autodestruct = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = new Color(1f, 0.35f, 0.05f, 0.7f);
            trail.material = mat;
        }

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0f),
                new GradientColorKey(new Color(0.8f, 0.15f, 0.02f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;

        return trail;
    }

    private void ApplyExplosionDamage(Vector3 center)
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

                Vector3 diff = unit.transform.position - center;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist <= blastRadius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        // Falloff damage: maximum at epicenter
                        float falloff = 1f - (dist / blastRadius) * 0.3f;
                        unit.TakeDamage(damage * falloff, null);

                        // DOTween launched airborne ragdoll knockback jump with violent tumble cartwheel
                        if (dist > 0.05f)
                        {
                            Vector3 pushDir = diff.normalized;
                            Vector3 knockTarget = unit.transform.position + pushDir * Mathf.Min(5.2f, knockbackForce * (1f - dist / blastRadius));
                            unit.transform.DOJump(knockTarget, jumpPower: 1.85f, numJumps: 1, duration: 0.38f).SetEase(Ease.OutQuad);
                            unit.transform.DORotate(new Vector3(Random.Range(360f, 720f) * (Random.value > 0.5f ? 1f : -1f), Random.Range(-180f, 180f), Random.Range(-180f, 180f)), 0.38f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad);
                            unit.transform.DOPunchScale(new Vector3(0.35f, -0.25f, 0.35f), 0.22f, 8, 0.5f);
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

                Vector3 diff = unit.transform.position - center;
                diff.y = 0f;
                float dist = diff.magnitude;

                if (dist <= blastRadius)
                {
                    if (!unit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(unit.transform.position, unit.TeamId))
                    {
                        float falloff = 1f - (dist / blastRadius) * 0.3f;
                        unit.TakeDamage(damage * falloff, null);

                        if (dist > 0.05f)
                        {
                            Vector3 pushDir = diff.normalized;
                            Vector3 knockTarget = unit.transform.position + pushDir * Mathf.Min(5.2f, knockbackForce * (1f - dist / blastRadius));
                            unit.transform.DOJump(knockTarget, jumpPower: 1.85f, numJumps: 1, duration: 0.38f).SetEase(Ease.OutQuad);
                            unit.transform.DORotate(new Vector3(Random.Range(360f, 720f) * (Random.value > 0.5f ? 1f : -1f), Random.Range(-180f, 180f), Random.Range(-180f, 180f)), 0.38f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad);
                            unit.transform.DOPunchScale(new Vector3(0.35f, -0.25f, 0.35f), 0.22f, 8, 0.5f);
                        }
                    }
                }
            }
        }

        // Damage enemy buildings or objective castles in blast radius
        Collider[] nearby = Physics.OverlapSphere(center, blastRadius);
        for (int c = 0; c < nearby.Length; c++)
        {
            PeasantObjectiveTarget obj = nearby[c].GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && obj.TeamId != playerTeamId)
            {
                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint != null ? obj.TargetPoint.position : obj.transform.position, obj.TeamId))
                {
                    obj.TakeDamage(damage * 0.6f, null); // Siege blast damage
                    break;
                }
            }
        }
    }

}
