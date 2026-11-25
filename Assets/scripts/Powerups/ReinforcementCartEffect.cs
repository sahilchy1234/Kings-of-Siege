using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Horse-drawn troop wagon powerup.
/// - Spawns at the caster's castle, drives to the targeted point, drops troops, returns to the castle.
/// - Movement goes through a NavMeshAgent (agent.Move), so the cart always stays on the NavMesh,
///   slides along walls instead of sticking, and can never drive through towers or barriers.
/// - Path corners are pushed away from NavMesh edges so the whole wagon body keeps clear of walls.
/// - Speed is capped on sharp turns and the cart can pivot when slow, so it never orbits a point.
/// - Polished motion: pop-in/out, body lean into turns, pitch on accelerate/brake, horse animation
///   speed and gallop audio follow the cart speed, dust on spawn/brake/despawn.
/// </summary>
public class ReinforcementCartEffect : MonoBehaviour
{
    [Header("--- SPEED & DRIVING PHYSICS ---")]
    [Tooltip("Cruising speed of the wagon in m/s.")]
    [SerializeField] public float travelSpeed = 13.5f;

    [Tooltip("Acceleration rate (m/s²) when speeding up from a standstill.")]
    [SerializeField] public float accelerationRate = 12f;

    [Tooltip("Braking deceleration rate (m/s²) when slowing down to a halt.")]
    [SerializeField] public float brakingRate = 15f;

    [Tooltip("Turning rate in degrees per second for arcing maneuvers.")]
    [SerializeField] public float turnSpeed = 140f;

    [Tooltip("Arrival distance tolerance to begin full halt.")]
    [SerializeField] public float arrivalThreshold = 0.9f;

    [Header("--- TERRAIN & OBSTACLE CONFORMANCE ---")]
    [Tooltip("Layer mask used for ground surface raycasting.")]
    [SerializeField] public LayerMask groundLayerMask = ~0;

    [Tooltip("How fast the wagon pitch/roll tilts to match uneven terrain slope.")]
    [SerializeField] public float terrainTiltSpeed = 12f;

    [Tooltip("Height of wagon pivot above ground surface.")]
    [SerializeField] public float groundYOffset = 0.05f;

    [Tooltip("Space (m) kept between the cart's path and walls, towers and barriers.")]
    [SerializeField] public float bodyClearance = 1.9f;

    [Header("--- WHEEL PHYSICS & SUSPENSION ---")]
    [Tooltip("Radius of the cart wheels in meters (used to calculate angular spin omega = v / r).")]
    [SerializeField] public float wheelRadius = 0.45f;

    [Tooltip("Transforms of wheels on the cart to spin dynamically with ground distance.")]
    [SerializeField] public Transform[] wheels;

    [Tooltip("Axis around which wheels rotate in their local space.")]
    [SerializeField] public Vector3 wheelRotationAxis = Vector3.right;

    [Tooltip("Suspension bump intensity when rolling on battlefield terrain.")]
    [SerializeField] public float suspensionBounciness = 0.035f;

    [Tooltip("Suspension bump frequency.")]
    [SerializeField] public float suspensionFrequency = 16f;

    [Tooltip("Max body lean (degrees) into turns.")]
    [SerializeField] public float maxTurnLean = 6f;

    [Tooltip("Max body pitch (degrees) when accelerating or braking.")]
    [SerializeField] public float maxAccelPitch = 4f;

    [Tooltip("Max degrees the horse turns ahead of the wagon in curves (horse leads, wagon follows).")]
    [SerializeField] public float horseLeadAngle = 12f;

    [Tooltip("Seconds between dust puffs kicked up behind the wheels at speed (0 = off).")]
    [SerializeField] public float wheelDustInterval = 0.3f;

    [Header("--- VISUAL COMPONENTS ---")]
    [Tooltip("Visual Cart parent object.")]
    [SerializeField] public Transform cartRoot;

    [Tooltip("Visual Horse parent object.")]
    [SerializeField] public Transform horseRoot;

    [Tooltip("Animator on the horse (cavalry run/idle).")]
    [SerializeField] public Animator horseAnimator;

    [Header("--- TROOP SEATING & DEPLOYMENT ---")]
    [Tooltip("Number of soldiers transported in the wagon.")]
    [SerializeField] public int soldierCount = 4;

    [Tooltip("Prefab of the soldier spawned if not using pre-seated rider transforms.")]
    [SerializeField] public GameObject soldierPrefab;

    [Tooltip("Transforms of the seated riders attached inside the wagon.")]
    [SerializeField] public Transform[] seatedSoldiers;

    [Tooltip("Side offsets for soldier dismount jumping points.")]
    [SerializeField] public Vector3[] dismountOffsets = new Vector3[]
    {
        new Vector3(-1.8f, 0f, 0.4f),
        new Vector3(1.8f, 0f, 0.4f),
        new Vector3(-1.9f, 0f, -0.6f),
        new Vector3(1.9f, 0f, -0.6f)
    };

    [Header("--- AUDIO & HORN ---")]
    [SerializeField] public AudioClip gallopClip;
    [SerializeField] public AudioClip dropHornClip;
    [SerializeField] public AudioSource audioSource;

    private int playerTeamId = 0;
    private Vector3 destinationPoint;
    private Vector3 spawnStartPoint;
    private float currentSpeed = 0f;
    private float totalDistanceTraveled = 0f;
    private Vector3 originalCartLocalPos;
    private Quaternion originalCartLocalRot = Quaternion.identity;
    private Vector3 baseScale = Vector3.one;

    private NavMeshAgent driveAgent;
    private bool agentOk;
    private float currentLean;
    private float currentPitch;
    private GameObject dustPrefab;
    private Quaternion originalHorseLocalRot = Quaternion.identity;
    private float horseYaw;
    private float dustTimer;
    private bool horseHasRunning;
    private bool horseHasSpeed;

    private void Awake()
    {
        if (cartRoot != null)
        {
            originalCartLocalPos = cartRoot.localPosition;
            originalCartLocalRot = cartRoot.localRotation;
        }
        baseScale = transform.localScale;
        if (horseRoot != null) originalHorseLocalRot = horseRoot.localRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        // Auto-configure ground layer mask if default/unconfigured
        if (groundLayerMask.value == 0 || groundLayerMask.value == ~0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int envLayer = LayerMask.NameToLayer("Environment");
            int defaultLayer = LayerMask.NameToLayer("Default");
            int mask = 0;
            if (groundLayer != -1) mask |= (1 << groundLayer);
            if (envLayer != -1) mask |= (1 << envLayer);
            if (defaultLayer != -1) mask |= (1 << defaultLayer);
            if (mask != 0) groundLayerMask = mask;
        }

        FindChildReferencesIfMissing();
        PrepareVisualRiders();
    }

    /// <summary>
    /// Ensure seated riders don't run AI or physics while riding inside the moving cart.
    /// </summary>
    private void PrepareVisualRiders()
    {
        if (seatedSoldiers == null || seatedSoldiers.Length == 0) return;

        for (int i = 0; i < seatedSoldiers.Length; i++)
        {
            if (seatedSoldiers[i] == null) continue;
            GameObject soldier = seatedSoldiers[i].gameObject;

            NavMeshAgent agent = soldier.GetComponentInChildren<NavMeshAgent>(true);
            if (agent != null) agent.enabled = false;

            PeasantUnitController puc = soldier.GetComponentInChildren<PeasantUnitController>(true);
            if (puc != null) puc.enabled = false;

            Collider[] cols = soldier.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < cols.Length; c++) cols[c].enabled = false;

            Rigidbody rb = soldier.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Hide the riders' own health bars while seated: four of them stacked inside the
            // wagon showed up as one huge blue bar over the cart. Shown again on landing.
            SetRiderHealthBarsVisible(soldier, false);
        }
    }

    private static void SetRiderHealthBarsVisible(GameObject soldier, bool visible)
    {
        if (soldier == null) return;
        JuicyUnitHealthBar[] bars = soldier.GetComponentsInChildren<JuicyUnitHealthBar>(true);
        for (int b = 0; b < bars.Length; b++)
        {
            if (bars[b] != null) bars[b].gameObject.SetActive(visible);
        }
    }

    public void Initialize(Vector3 dropPoint, int playerTeam = 0,
        GameObject customCartPrefab = null, AudioClip customArrivalSound = null,
        int customQuantity = 0, float customDamage = 0f, float customDuration = 0f)
    {
        this.destinationPoint = dropPoint;
        this.playerTeamId = playerTeam;
        if (customQuantity > 0) this.soldierCount = customQuantity;
        if (customArrivalSound != null) this.dropHornClip = customArrivalSound;

        FindChildReferencesIfMissing();
        PrepareVisualRiders();
        CacheHorseParameters();

        if (dustPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("FX_Dust_Big_01 t:Prefab");
            if (guids != null && guids.Length > 0)
                dustPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(guids[0]));
        }

        // 1. Target on the NavMesh, with room for the wagon body
        if (NavMesh.SamplePosition(dropPoint, out NavMeshHit destHit, 6f, NavMesh.AllAreas))
        {
            this.destinationPoint = destHit.position;
        }
        this.destinationPoint = PushAwayFromEdges(this.destinationPoint);

        // 2. Spawn at the caster's own castle, just outside it on the side facing the target.
        //    Fallback (no castle found): behind the caster's lines (team 0 on -X, team 1 on +X).
        Vector3 rawSpawn = this.destinationPoint + new Vector3(BackSign * 28f, 0f, Random.Range(-2f, 2f));
        PeasantObjectiveTarget ownCastle = FindOwnCastle();
        if (ownCastle != null)
        {
            Vector3 castlePos = ownCastle.transform.position;
            Vector3 toDest = this.destinationPoint - castlePos;
            toDest.y = 0f;
            Vector3 outDir = toDest.sqrMagnitude > 0.01f ? toDest.normalized : new Vector3(-BackSign, 0f, 0f);

            float clearance = 4f;
            Collider[] castleCols = ownCastle.GetComponentsInChildren<Collider>();
            for (int i = 0; i < castleCols.Length; i++)
            {
                if (castleCols[i] == null || castleCols[i].isTrigger) continue;
                Vector3 ext = castleCols[i].bounds.extents;
                clearance = Mathf.Max(clearance, Mathf.Max(ext.x, ext.z) + 3.5f);
            }
            clearance = Mathf.Min(clearance, Mathf.Max(3f, toDest.magnitude - 3f)); // never past the target

            rawSpawn = castlePos + outDir * clearance;
        }

        if (NavMesh.SamplePosition(rawSpawn, out NavMeshHit spawnHit, 8f, NavMesh.AllAreas))
        {
            spawnStartPoint = PushAwayFromEdges(spawnHit.position);
        }
        else
        {
            spawnStartPoint = rawSpawn;
            spawnStartPoint.y = GetGroundHeight(spawnStartPoint);
        }

        transform.position = spawnStartPoint;

        // Face along the first leg of the route, not straight at the target (avoids an instant U-turn)
        Vector3 faceDir = this.destinationPoint - spawnStartPoint;
        NavMeshPath firstPath = new NavMeshPath();
        if (NavMesh.CalculatePath(spawnStartPoint, this.destinationPoint, NavMesh.AllAreas, firstPath) && firstPath.corners.Length > 1)
        {
            faceDir = firstPath.corners[1] - spawnStartPoint;
        }
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(faceDir);

        SetupDriveAgent();
        AlignWithTerrainSurface(100f);

        // Pop in at the castle gate
        transform.localScale = baseScale * 0.01f;
        transform.DOScale(baseScale, 0.45f).SetEase(Ease.OutBack);
        SpawnDust(spawnStartPoint, 0.8f);

        if (gallopClip != null && audioSource != null)
        {
            audioSource.clip = gallopClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        StartCoroutine(RealisticWagonMissionRoutine());
    }

    private void SetupDriveAgent()
    {
        driveAgent = GetComponent<NavMeshAgent>();
        if (driveAgent == null) driveAgent = gameObject.AddComponent<NavMeshAgent>();

        driveAgent.enabled = true;
        driveAgent.updatePosition = false;   // we place the transform ourselves (terrain height, tilt)
        driveAgent.updateRotation = false;
        driveAgent.updateUpAxis = false;
        driveAgent.radius = 0.6f;
        driveAgent.height = 2f;
        driveAgent.baseOffset = 0f;
        driveAgent.autoBraking = false;
        driveAgent.speed = travelSpeed * 2f;
        driveAgent.acceleration = 1000f;
        driveAgent.angularSpeed = 0f;
        driveAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // units never block it

        agentOk = driveAgent.Warp(transform.position) && driveAgent.isOnNavMesh;
    }

    private void FindChildReferencesIfMissing()
    {
        if (cartRoot == null)
        {
            Transform foundCart = transform.Find("Cart") ?? transform.Find("Cart_1 Variant") ?? transform.Find("Cart_2 Variant");
            if (foundCart != null)
            {
                cartRoot = foundCart;
                originalCartLocalPos = cartRoot.localPosition;
                originalCartLocalRot = cartRoot.localRotation;
            }
        }

        if (horseRoot == null)
        {
            Transform foundHorse = transform.Find("Horse") ?? transform.Find("Horse Variant");
            if (foundHorse != null)
            {
                horseRoot = foundHorse;
                originalHorseLocalRot = horseRoot.localRotation;
            }
        }

        if (horseAnimator == null && horseRoot != null)
        {
            horseAnimator = horseRoot.GetComponentInChildren<Animator>();
        }

        if (horseAnimator != null && horseAnimator.runtimeAnimatorController == null)
        {
            horseAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Powerups/horse_cavalry");
            if (horseAnimator.runtimeAnimatorController == null)
            {
                string[] animGuids = RuntimeAssets.FindAssets("horse_cavalry t:AnimatorController");
                if (animGuids != null && animGuids.Length > 0)
                {
                    horseAnimator.runtimeAnimatorController = RuntimeAssets.LoadAssetAtPath<RuntimeAnimatorController>(
                        RuntimeAssets.GUIDToAssetPath(animGuids[0]));
                }
            }
        }

        if (seatedSoldiers == null || seatedSoldiers.Length == 0)
        {
            Transform seatsRoot = transform.Find("TroopSeats");
            if (seatsRoot != null)
            {
                List<Transform> seatsList = new List<Transform>();
                for (int i = 0; i < seatsRoot.childCount; i++)
                {
                    seatsList.Add(seatsRoot.GetChild(i));
                }
                seatedSoldiers = seatsList.ToArray();
            }
        }

        if (wheels == null || wheels.Length == 0)
        {
            List<Transform> wheelList = new List<Transform>();
            Transform searchTarget = cartRoot != null ? cartRoot : transform;
            foreach (Transform t in searchTarget.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().Contains("wheel"))
                {
                    wheelList.Add(t);
                }
            }
            if (wheelList.Count > 0) wheels = wheelList.ToArray();
        }

        if (soldierPrefab == null)
        {
            soldierPrefab = Resources.Load<GameObject>("Powerups/TT_Halberdier");
            if (soldierPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("TT_Halberdier t:Prefab");
                if (guids == null || guids.Length == 0) guids = RuntimeAssets.FindAssets("TT_Peasant t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    soldierPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
        }
    }

    private void CacheHorseParameters()
    {
        horseHasRunning = false;
        horseHasSpeed = false;
        if (horseAnimator == null || horseAnimator.runtimeAnimatorController == null) return;

        AnimatorControllerParameter[] ps = horseAnimator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].name == "running" && ps[i].type == AnimatorControllerParameterType.Bool) horseHasRunning = true;
            if (ps[i].name == "speed" && ps[i].type == AnimatorControllerParameterType.Float) horseHasSpeed = true;
        }
    }

    private IEnumerator RealisticWagonMissionRoutine()
    {
        CinematicCameraController.TriggerReinforcementCart(transform, destinationPoint, this.playerTeamId);

        // Let the pop-in read before rolling off
        yield return new WaitForSeconds(0.2f);

        // -------------------------------------------------------------
        // PHASE 1: DRIVE FROM THE CASTLE TO THE DROP POINT
        // -------------------------------------------------------------
        currentSpeed = 2f;
        SetHorseRunAnimation(true);

        float expectedTime = Vector3.Distance(transform.position, destinationPoint) * 1.4f / Mathf.Max(1f, travelSpeed);
        yield return StartCoroutine(DriveToRoutine(destinationPoint, travelSpeed, true, expectedTime * 2.5f + 4f));
        CinematicCameraController.NotifyReinforcementCartArrived();

        // -------------------------------------------------------------
        // PHASE 2: ARRIVAL SKID, SETTLE & HORN
        // -------------------------------------------------------------
        currentSpeed = 0f;
        SetHorseRunAnimation(false);
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == gallopClip)
        {
            audioSource.Stop();
        }
        if (dropHornClip != null && audioSource != null)
        {
            audioSource.pitch = 1f;
            audioSource.volume = 1f;
            KoSAudio.PlayOneShot(audioSource, dropHornClip, 0.95f, "cart_horn");
        }

        SpawnDust(transform.position + transform.forward * 1.2f, 0.55f);
        ResetBodyPose();
        yield return StartCoroutine(BrakingSettleRoutine());

        // -------------------------------------------------------------
        // PHASE 3: DEPLOY SOLDIERS
        // -------------------------------------------------------------
        yield return StartCoroutine(DeployTroopsWithDismount());
        yield return new WaitForSeconds(0.3f);

        // -------------------------------------------------------------
        // PHASE 4: RETURN TO THE CASTLE
        // -------------------------------------------------------------
        SetHorseRunAnimation(true);
        if (gallopClip != null && audioSource != null)
        {
            audioSource.clip = gallopClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        float returnTime = Vector3.Distance(transform.position, spawnStartPoint) * 1.4f / Mathf.Max(1f, travelSpeed * 1.2f);
        yield return StartCoroutine(DriveToRoutine(spawnStartPoint, travelSpeed * 1.2f, true, returnTime * 2.5f + 4f));

        // -------------------------------------------------------------
        // PHASE 5: DESPAWN AT THE CASTLE
        // -------------------------------------------------------------
        SetHorseRunAnimation(false);
        if (audioSource != null) audioSource.Stop();
        SpawnDust(transform.position, 0.8f);

        Tween shrink = transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack);
        yield return shrink.WaitForCompletion();

        Destroy(gameObject);
    }

    /// <summary>+1 or -1 along X: the direction towards the caster's own base.</summary>
    private float BackSign => playerTeamId == 1 ? 1f : -1f;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    /// <summary>
    /// Drives the wagon to a target.
    /// - Follows NavMesh corners pushed away from walls (re-planned every 0.5 s).
    /// - Moves with agent.Move, so it slides along walls and never leaves the NavMesh.
    /// - Slows for sharp turns and pivots faster when slow or blocked, so it never orbits or wedges.
    /// </summary>
    private IEnumerator DriveToRoutine(Vector3 target, float cruiseSpeed, bool brakeToStop, float maxTime)
    {
        NavMeshPath path = new NavMeshPath();
        List<Vector3> corners = new List<Vector3>(16);
        int cornerIdx = 1;
        float repathTimer = 0f;
        float elapsed = 0f;
        float bestDist = float.MaxValue;
        float noProgressTime = 0f;
        float blockedTime = 0f;
        float reverseTimer = 0f;
        float turnRateRad = Mathf.Max(10f, turnSpeed) * Mathf.Deg2Rad;

        while (true)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                yield return null;
                continue;
            }

            elapsed += dt;
            repathTimer -= dt;

            if (repathTimer <= 0f || corners.Count < 2)
            {
                repathTimer = 0.5f;
                BuildCorners(target, path, corners);
                cornerIdx = 1;
            }

            Vector3 pos = transform.position;
            Vector3 toFinal = Flat(target - pos);
            float distToFinal = toFinal.magnitude;

            if (distToFinal <= Mathf.Max(arrivalThreshold, 1.2f) || elapsed >= maxTime) break;

            // Orbit watchdog, only near the target (detours far away legitimately add distance)
            if (distToFinal < bestDist - 0.3f)
            {
                bestDist = distToFinal;
                noProgressTime = 0f;
            }
            else
            {
                noProgressTime += dt;
                // Near the target: accept arrival. Anywhere else: never stall forever
                if (distToFinal < 8f && noProgressTime > 1.5f) break;
                if (noProgressTime > 7f) break;
            }

            Vector3 fwd = Flat(transform.forward);
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : (toFinal.sqrMagnitude > 0.001f ? toFinal.normalized : Vector3.forward);

            // Skip corners reached or already behind us, but only when the next corner is in
            // clear line of sight on the NavMesh. Skipping early made the cart cut across fence
            // tips and wall corners and grind against them.
            Vector3 navPos = (agentOk && driveAgent != null) ? driveAgent.nextPosition : pos;
            while (cornerIdx < corners.Count - 1)
            {
                Vector3 toCorner = Flat(corners[cornerIdx] - pos);
                float cd = toCorner.magnitude;
                bool done = cd < 2.5f || (cd < 6f && Vector3.Dot(fwd, toCorner) < 0f);
                if (done && !NavMesh.Raycast(navPos, corners[cornerIdx + 1], out NavMeshHit _, NavMesh.AllAreas)) cornerIdx++;
                else break;
            }

            int ci = Mathf.Clamp(cornerIdx, 0, corners.Count - 1);
            Vector3 steerDir = Flat(corners[ci] - pos);
            float steerDist = steerDir.magnitude;

            // Overshot the final point: arrive rather than loop back
            if (ci >= corners.Count - 1 && distToFinal < 3f && Vector3.Dot(fwd, toFinal) < 0f) break;

            float headingError = steerDist > 0.01f ? Vector3.Angle(fwd, steerDir) : 0f;

            // ---- speed ----
            float desiredSpeed = cruiseSpeed;
            if (headingError > 15f) desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(2f, turnRateRad * steerDist * 0.5f));
            if (headingError > 75f) desiredSpeed = Mathf.Min(desiredSpeed, 2.5f);
            if (blockedTime > 0.3f) desiredSpeed = Mathf.Min(desiredSpeed, 2f);
            if (brakeToStop)
            {
                float stopDist = (currentSpeed * currentSpeed) / (2f * brakingRate) + 1.2f;
                if (distToFinal <= stopDist)
                {
                    desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(1.5f, cruiseSpeed * (distToFinal / stopDist)));
                }
            }

            float prevSpeed = currentSpeed;
            if (reverseTimer > 0f)
            {
                // Unstick: back up briefly while turning towards the route
                reverseTimer -= dt;
                currentSpeed = Mathf.MoveTowards(currentSpeed, -3f, brakingRate * dt);
            }
            else
            {
                // A horse pulling a loaded wagon starts slowly and builds up to a gallop
                float pullFactor = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(currentSpeed / Mathf.Max(1f, cruiseSpeed)));
                float rate = desiredSpeed > currentSpeed ? accelerationRate * pullFactor : brakingRate;
                currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * dt);
            }

            // ---- steering: faster pivot when slow or blocked ----
            float yawBefore = transform.eulerAngles.y;
            if (steerDir.sqrMagnitude > 0.05f)
            {
                float slowBoost = Mathf.Lerp(1.8f, 1f, Mathf.Clamp01(currentSpeed / Mathf.Max(1f, cruiseSpeed)));
                float blockBoost = (blockedTime > 0.6f || reverseTimer > 0f) ? 2f : 1f;
                Quaternion targetRot = Quaternion.LookRotation(steerDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * slowBoost * blockBoost * dt);
            }

            Vector3 moveDir = Flat(transform.forward);
            moveDir = moveDir.sqrMagnitude > 0.001f ? moveDir.normalized : fwd;
            Vector3 delta = moveDir * (currentSpeed * dt);

            // ---- move, constrained to the NavMesh ----
            float moved;
            if (agentOk && driveAgent != null && driveAgent.isOnNavMesh)
            {
                Vector3 before = driveAgent.nextPosition;
                driveAgent.Move(delta);
                Vector3 after = driveAgent.nextPosition;
                moved = Flat(after - before).magnitude;
                transform.position = new Vector3(after.x, transform.position.y, after.z);

                // Blocked = little progress in the intended direction (sliding along a wall counts)
                float progress = delta.sqrMagnitude > 0.000001f ? Vector3.Dot(Flat(after - before), delta.normalized) : 0f;
                if (delta.magnitude > 0.01f && progress < delta.magnitude * 0.5f) blockedTime += dt;
                else blockedTime = Mathf.Max(0f, blockedTime - dt * 2f);

                if (blockedTime > 0.6f && repathTimer > 0.1f) repathTimer = 0.1f; // re-plan from where we are
                if (blockedTime > 1.0f && reverseTimer <= 0f)
                {
                    reverseTimer = 0.6f;
                    blockedTime = 0f;
                    repathTimer = 0f;
                }
            }
            else
            {
                transform.position += delta;
                moved = delta.magnitude;
            }

            totalDistanceTraveled += moved;

            AlignWithTerrainSurface(terrainTiltSpeed * dt);
            UpdateWheelRotation(currentSpeed >= 0f ? moved : -moved);

            // Dust kicked up behind the wheels at speed
            if (wheelDustInterval > 0f && currentSpeed > 5f)
            {
                dustTimer -= dt;
                if (dustTimer <= 0f)
                {
                    dustTimer = wheelDustInterval;
                    SpawnDust(transform.position - transform.forward * 1.3f + transform.right * Random.Range(-0.6f, 0.6f), 0.22f);
                }
            }

            float yawRate = Mathf.DeltaAngle(yawBefore, transform.eulerAngles.y) / dt;
            float accel = (currentSpeed - prevSpeed) / dt;
            ApplyBodyMotion(yawRate, accel);
            UpdateHorseAndAudio();

            yield return null;
        }
    }

    private void BuildCorners(Vector3 target, NavMeshPath path, List<Vector3> corners)
    {
        corners.Clear();
        Vector3 from = (agentOk && driveAgent != null) ? driveAgent.nextPosition : transform.position;

        if (NavMesh.CalculatePath(from, target, NavMesh.AllAreas, path) &&
            path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 1)
        {
            // Drop duplicate / near-duplicate corners. NavMesh often returns the start point twice
            // when the cart sits at a fence tip; pushing that duplicate away from the wall put the
            // steering target BEHIND the cart, so it crawled back and forth at the fence end.
            corners.Add(path.corners[0]);
            for (int i = 1; i < path.corners.Length; i++)
            {
                bool isLast = i == path.corners.Length - 1;
                if (!isLast && Flat(path.corners[i] - corners[corners.Count - 1]).magnitude < 0.75f) continue;
                corners.Add(path.corners[i]);
            }
            if (corners.Count < 2) corners.Add(target);

            for (int i = 1; i < corners.Count - 1; i++)
            {
                // Corners right next to the cart are steered past, never pushed (would point backwards)
                if (Flat(corners[i] - from).magnitude < 3f) continue;

                // Keep the push only if both legs stay clear: a pushed corner must never hop
                // across a thin wall (that made the cart drive into the wall and circle).
                Vector3 pushed = PushAwayFromEdges(corners[i]);
                if (!NavMesh.Raycast(corners[i - 1], pushed, out NavMeshHit _, NavMesh.AllAreas) &&
                    !NavMesh.Raycast(pushed, corners[i + 1], out NavMeshHit _, NavMesh.AllAreas))
                {
                    corners[i] = pushed;
                }
            }
        }
        else
        {
            corners.Add(from);
            corners.Add(target);
        }
    }

    /// <summary>Moves a point away from nearby NavMesh edges (walls, towers, barriers) by bodyClearance.</summary>
    private Vector3 PushAwayFromEdges(Vector3 p)
    {
        for (int iter = 0; iter < 2; iter++)
        {
            if (!NavMesh.FindClosestEdge(p, out NavMeshHit edge, NavMesh.AllAreas)) break;
            if (edge.distance >= bodyClearance) break;

            Vector3 away = Flat(p - edge.position);
            if (away.sqrMagnitude < 0.0001f) away = Flat(edge.normal);
            if (away.sqrMagnitude < 0.0001f) break;

            Vector3 candidate = p + away.normalized * (bodyClearance - edge.distance + 0.1f);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit h, 1.5f, NavMesh.AllAreas)) p = h.position;
            else break;
        }
        return p;
    }

    /// <summary>The caster's main castle (not a capturable tower), furthest back on their side.</summary>
    private PeasantObjectiveTarget FindOwnCastle()
    {
        PeasantObjectiveTarget best = null;
        float bestScore = float.MaxValue;
        PeasantObjectiveTarget[] all = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            PeasantObjectiveTarget o = all[i];
            if (o == null || o.IsDestroyed || o.TeamId != playerTeamId) continue;
            bool isTower = o.GetComponentInParent<TowerCombatController>() != null ||
                           o.GetComponentInChildren<TowerCombatController>() != null;
            float score = (isTower ? 100000f : 0f) - BackSign * o.transform.position.x;
            if (score < bestScore)
            {
                bestScore = score;
                best = o;
            }
        }
        return best;
    }

    /// <summary>
    /// Ground raycasts must only hit real ground, never units, towers, castles, fences or boulders.
    /// </summary>
    private bool IsIgnoredGroundHit(RaycastHit h)
    {
        if (h.collider == null) return true;
        if (h.collider.transform.IsChildOf(transform)) return true;
        if (h.collider.attachedRigidbody != null) return true;
        if (h.collider.GetComponentInParent<PeasantUnitController>() != null) return true;
        if (h.collider.GetComponentInParent<PeasantObjectiveTarget>() != null) return true;
        if (h.collider.GetComponentInParent<TowerCombatController>() != null) return true;
        if (h.collider.GetComponentInParent<FenceBarrier>() != null) return true;
        if (h.collider.GetComponentInParent<NavMeshObstacle>() != null) return true;
        return false;
    }

    private void AlignWithTerrainSurface(float tiltDelta)
    {
        Vector3 pos = transform.position;
        Ray ray = new Ray(new Vector3(pos.x, pos.y + 25f, pos.z), Vector3.down);

        bool foundGround = false;
        Vector3 surfaceNormal = Vector3.up;
        float groundHeight = pos.y;

        RaycastHit[] hits = Physics.RaycastAll(ray, 60f, groundLayerMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            float highestY = float.MinValue;
            RaycastHit bestHit = default;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsIgnoredGroundHit(hits[i])) continue;
                if (hits[i].point.y > highestY)
                {
                    highestY = hits[i].point.y;
                    bestHit = hits[i];
                }
            }

            if (highestY > float.MinValue)
            {
                groundHeight = bestHit.point.y;
                surfaceNormal = bestHit.normal;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
            {
                groundHeight = navHit.position.y;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            float targetY = groundHeight + groundYOffset;
            // Snap on spawn or big drops; otherwise glide smoothly (no popping on small bumps)
            if (tiltDelta >= 50f || Mathf.Abs(pos.y - targetY) > 1.0f)
            {
                pos.y = targetY;
            }
            else
            {
                pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            transform.position = pos;

            Vector3 forwardProjected = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
            if (forwardProjected.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(forwardProjected, surfaceNormal);
                Vector3 euler = targetRot.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                float roll = euler.z > 180f ? euler.z - 360f : euler.z;
                pitch = Mathf.Clamp(pitch, -30f, 30f);
                roll = Mathf.Clamp(roll, -25f, 25f);
                targetRot = Quaternion.Euler(pitch, euler.y, roll);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(tiltDelta * 2f));
            }
        }
    }

    private float GetGroundHeight(Vector3 pos)
    {
        Ray ray = new Ray(new Vector3(pos.x, pos.y + 30f, pos.z), Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 70f, groundLayerMask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            float highestY = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsIgnoredGroundHit(hits[i])) continue;
                if (hits[i].point.y > highestY)
                {
                    highestY = hits[i].point.y;
                }
            }
            if (highestY > float.MinValue)
            {
                return highestY + groundYOffset;
            }
        }

        if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
        {
            return navHit.position.y + groundYOffset;
        }
        return pos.y;
    }

    private void UpdateWheelRotation(float moveDelta)
    {
        if (wheels == null || wheels.Length == 0) return;

        float degrees = (moveDelta / Mathf.Max(0.1f, wheelRadius)) * Mathf.Rad2Deg;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] != null)
            {
                wheels[i].Rotate(wheelRotationAxis, degrees, Space.Self);
            }
        }
    }

    /// <summary>Suspension bounce + lean into turns + pitch on accelerate/brake, all smoothed.</summary>
    private void ApplyBodyMotion(float yawRate, float accel)
    {
        if (cartRoot == null) return;

        float dt = Time.deltaTime;
        float targetLean = Mathf.Clamp(-yawRate * 0.04f, -maxTurnLean, maxTurnLean);
        float targetPitch = Mathf.Clamp(-accel * 0.35f, -maxAccelPitch, maxAccelPitch);
        currentLean = Mathf.Lerp(currentLean, targetLean, 1f - Mathf.Exp(-8f * dt));
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, 1f - Mathf.Exp(-6f * dt));

        // Suspension: rhythmic wheel bounce plus irregular road bumps (not a perfect sine)
        float speedT = Mathf.Clamp01(Mathf.Abs(currentSpeed) / Mathf.Max(1f, travelSpeed));
        float bumpNoise = (Mathf.PerlinNoise(totalDistanceTraveled * 0.9f, 0.37f) - 0.5f) * 2f;
        float bounce = (Mathf.Sin(totalDistanceTraveled * suspensionFrequency) * 0.6f + bumpNoise * 0.8f) *
                       suspensionBounciness * speedT;
        // Uneven ground under the left/right wheels rocks the wagon a little
        float sideRock = (Mathf.PerlinNoise(totalDistanceTraveled * 1.3f, 5.1f) - 0.5f) * 2.5f * speedT;

        cartRoot.localPosition = originalCartLocalPos + Vector3.up * bounce;
        cartRoot.localRotation = originalCartLocalRot * Quaternion.Euler(currentPitch, 0f, currentLean + sideRock);

        // The horse leads into turns and the wagon follows behind it
        if (horseRoot != null)
        {
            float targetHorseYaw = Mathf.Clamp(yawRate * 0.15f, -horseLeadAngle, horseLeadAngle);
            horseYaw = Mathf.Lerp(horseYaw, targetHorseYaw, 1f - Mathf.Exp(-6f * dt));
            horseRoot.localRotation = originalHorseLocalRot * Quaternion.Euler(0f, horseYaw, 0f);
        }
    }

    private void ResetBodyPose()
    {
        currentLean = 0f;
        currentPitch = 0f;
        horseYaw = 0f;
        if (horseRoot != null) horseRoot.localRotation = originalHorseLocalRot;
        if (cartRoot == null) return;
        cartRoot.localPosition = originalCartLocalPos;
        cartRoot.localRotation = originalCartLocalRot;
    }

    private void UpdateHorseAndAudio()
    {
        float t = Mathf.Clamp01(currentSpeed / Mathf.Max(1f, travelSpeed));

        if (horseAnimator != null)
        {
            if (horseHasRunning) horseAnimator.SetBool("running", currentSpeed > 0.6f);
            if (horseHasSpeed) horseAnimator.SetFloat("speed", currentSpeed);
            horseAnimator.speed = Mathf.Lerp(0.7f, 1.25f, t);
        }

        if (audioSource != null && audioSource.isPlaying && audioSource.clip == gallopClip)
        {
            audioSource.pitch = Mathf.Lerp(0.85f, 1.15f, t);
            audioSource.volume = Mathf.Lerp(0.5f, 1f, t);
        }
    }

    private void SpawnDust(Vector3 pos, float scale)
    {
        if (dustPrefab == null) return;
        GameObject d = Instantiate(dustPrefab, pos, Quaternion.identity);
        d.transform.localScale = Vector3.one * scale;
        Destroy(d, 2f);
    }

    private IEnumerator BrakingSettleRoutine()
    {
        if (cartRoot == null) yield break;

        float settleTimer = 0f;
        float settleDuration = 0.4f;
        Quaternion initRot = cartRoot.localRotation;
        Quaternion pitchForward = initRot * Quaternion.Euler(3f, 0f, 0f);

        while (settleTimer < 0.12f)
        {
            settleTimer += Time.deltaTime;
            cartRoot.localRotation = Quaternion.Slerp(initRot, pitchForward, settleTimer / 0.12f);
            yield return null;
        }

        settleTimer = 0f;
        while (settleTimer < settleDuration)
        {
            settleTimer += Time.deltaTime;
            float damping = Mathf.Exp(-5.5f * (settleTimer / settleDuration));
            float osc = Mathf.Cos(settleTimer * 20f) * damping;
            cartRoot.localRotation = Quaternion.SlerpUnclamped(initRot, pitchForward, osc);
            yield return null;
        }

        cartRoot.localRotation = initRot;
        cartRoot.localPosition = originalCartLocalPos;
    }

    private IEnumerator DeployTroopsWithDismount()
    {
        if (seatedSoldiers != null && seatedSoldiers.Length > 0)
        {
            for (int i = 0; i < seatedSoldiers.Length; i++)
            {
                if (seatedSoldiers[i] == null) continue;

                Vector3 landTarget = transform.position + transform.TransformDirection(
                    dismountOffsets[i % dismountOffsets.Length]);

                landTarget.y = GetGroundHeight(landTarget);

                if (NavMesh.SamplePosition(landTarget, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    landTarget = hit.position;
                }

                StartCoroutine(SoldierDismountJumpRoutine(seatedSoldiers[i].gameObject, landTarget));
                yield return new WaitForSeconds(0.08f);
            }
        }
        else
        {
            for (int i = 0; i < soldierCount; i++)
            {
                Vector3 dropPos = transform.position + transform.TransformDirection(
                    dismountOffsets[i % dismountOffsets.Length]);

                dropPos.y = GetGroundHeight(dropPos);

                if (NavMesh.SamplePosition(dropPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    dropPos = hit.position;
                }

                if (soldierPrefab != null)
                {
                    GameObject soldier = Instantiate(soldierPrefab, dropPos, transform.rotation);
                    Vector3 finalScale = new Vector3(0.77f, 0.77f, 0.77f);
                    soldier.transform.localScale = finalScale * 0.2f;
                    soldier.transform.DOScale(finalScale, 0.25f).SetEase(Ease.OutBack);

                    NavMeshAgent agent = soldier.GetComponentInChildren<NavMeshAgent>(true);
                    if (agent != null)
                    {
                        agent.enabled = false;
                        soldier.transform.position = dropPos;
                        agent.enabled = true;
                        if (agent.isOnNavMesh) agent.Warp(dropPos);
                    }
                    PeasantUnitController puc = soldier.GetComponentInChildren<PeasantUnitController>();
                    if (puc != null) puc.SetTeamId(playerTeamId);
                }
                yield return new WaitForSeconds(0.08f);
            }
        }
    }

    private IEnumerator SoldierDismountJumpRoutine(GameObject riderObj, Vector3 groundTarget)
    {
        riderObj.transform.SetParent(null, true);

        Vector3 startPos = riderObj.transform.position;
        float jumpElapsed = 0f;
        float jumpDuration = 0.36f;

        while (jumpElapsed < jumpDuration)
        {
            jumpElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(jumpElapsed / jumpDuration);
            float eased = 1f - (1f - t) * (1f - t); // ease out

            Vector3 currentPos = Vector3.Lerp(startPos, groundTarget, eased);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.6f;
            riderObj.transform.position = currentPos;

            yield return null;
        }

        riderObj.transform.position = groundTarget;
        riderObj.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f), 0.2f, 6, 0.5f);

        Vector3 forwardFlat = riderObj.transform.forward;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude > 0.01f)
        {
            riderObj.transform.rotation = Quaternion.LookRotation(forwardFlat, Vector3.up);
        }

        Collider[] cols = riderObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;

        NavMeshAgent agent = riderObj.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null)
        {
            agent.enabled = false;
            if (NavMesh.SamplePosition(groundTarget, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
            {
                riderObj.transform.position = navHit.position;
            }
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(riderObj.transform.position);
            }
        }

        PeasantUnitController puc = riderObj.GetComponentInChildren<PeasantUnitController>(true);
        if (puc != null)
        {
            puc.enabled = true;
            puc.SetTeamId(playerTeamId);
        }

        Animator anim = riderObj.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.enabled = true;

        SetRiderHealthBarsVisible(riderObj, true);
    }

    private void SetHorseRunAnimation(bool isRunning)
    {
        if (horseAnimator == null) return;
        if (horseHasRunning) horseAnimator.SetBool("running", isRunning);
        if (horseHasSpeed) horseAnimator.SetFloat("speed", isRunning ? Mathf.Max(currentSpeed, 2f) : 0f);
        if (!isRunning) horseAnimator.speed = 1f;
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
