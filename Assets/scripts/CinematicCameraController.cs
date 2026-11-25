using System;
using UnityEngine;

/// <summary>
/// Choreographs distinct, film-grade cinematic camera sequences for battlefield power-ups:
/// - Arrow Shower: Curved aerial dive -> low-angle spiral dolly-in + crane rise + telephoto compression.
/// - Fire Arrows: Ground-skimming drop -> sky-catching whip-tilt -> Vertigo dolly-zoom (pull back + zoom in) + reverse sweep.
/// </summary>
[DisallowMultipleComponent]
public class CinematicCameraController : MonoBehaviour
{
    public enum CinematicStyle
    {
        ArrowShower,
        FireArrows,
        BigFireball,
        FireArrowProjectile,
        ReinforcementCart,
        WoodLog
    }

    [Serializable]
    public struct CameraChoreographyProfile
    {
        [Header("Timing (Seconds)")]
        [Min(0.2f)] public float entryDuration;
        [Min(0f)] public float holdPadding;
        [Min(0.2f)] public float exitDuration;

        [Header("Beat 1: Entry Curve & Speed FOV")]
        [Range(-25f, 25f)] public float entryArcSwing;
        [Range(-15f, 15f)] public float entryDiveDip;
        [Range(35f, 75f)] public float entryPeakFov;

        [Header("Beat 2: Orbit / Dolly / Crane")]
        [Range(8f, 40f)] public float orbitStartDistance;
        [Range(8f, 40f)] public float orbitEndDistance;
        [Range(3f, 25f)] public float orbitStartHeight;
        [Range(3f, 30f)] public float orbitEndHeight;
        [Range(-120f, 120f)] public float startAzimuthOffset;
        [Range(-120f, 120f)] public float orbitSweepAngle;

        [Header("Dynamic Aim & Lens")]
        [Range(0f, 16f)] public float startLookHeight;
        [Range(0f, 8f)] public float endLookHeight;
        [Range(18f, 65f)] public float startFov;
        [Range(18f, 65f)] public float endFov;
        [Range(-12f, 12f)] public float dutchRollAngle;

        [Header("Steadicam & Shake")]
        [Range(0.02f, 0.35f)] public float positionSmoothTime;
        [Range(4f, 25f)] public float rotationSmoothSpeed;
        [Range(0f, 1f)] public float handheldDriftAmplitude;
        [Range(0.1f, 3f)] public float handheldDriftFrequency;
        [Range(0f, 3f)] public float shakeMultiplier;
    }

    public static CinematicCameraController Instance { get; private set; }

    [Header("Master Activation Toggle")]
    [Tooltip("Global toggle to enable or disable cinematic camera choreography when powerups are released. Currently disabled as requested.")]
    [SerializeField] private bool enableCinematics = false;
    public static bool GlobalCinematicsEnabled = false;

    [Header("Activation Filter")]
    [Tooltip("If true, only triggers when the local player casts the power-up (prevents AI from hijacking the camera).")]
    [SerializeField] private bool triggerOnlyForLocalPlayer = true;
    [Tooltip("If true, casting another power-up mid-cinematic smoothly retargets to the new effect.")]
    [SerializeField] private bool allowRetriggerInterrupt = true;

    [Header("Arrow Shower Profile (Spiral Dive & Crane Rise)")]
    [SerializeField] private CameraChoreographyProfile arrowShowerProfile = new CameraChoreographyProfile
    {
        entryDuration = 0.72f,
        holdPadding = 0.2f,
        exitDuration = 0.95f,
        entryArcSwing = 11f,
        entryDiveDip = -6f,
        entryPeakFov = 53f,
        orbitStartDistance = 23f,
        orbitEndDistance = 15.5f,
        orbitStartHeight = 6.8f,
        orbitEndHeight = 11.5f,
        startAzimuthOffset = -34f,
        orbitSweepAngle = 64f,
        startLookHeight = 4.5f,
        endLookHeight = 1.4f,
        startFov = 34f,
        endFov = 31f,
        dutchRollAngle = 3.8f,
        positionSmoothTime = 0.11f,
        rotationSmoothSpeed = 11f,
        handheldDriftAmplitude = 0.18f,
        handheldDriftFrequency = 0.6f,
        shakeMultiplier = 1.35f
    };

    [Header("Fire Arrows Profile (Low-Skim & Vertigo Dolly-Zoom)")]
    [SerializeField] private CameraChoreographyProfile fireArrowsProfile = new CameraChoreographyProfile
    {
        entryDuration = 0.62f,
        holdPadding = 0.25f,
        exitDuration = 0.95f,
        entryArcSwing = -14f,
        entryDiveDip = -9f,
        entryPeakFov = 58f,
        orbitStartDistance = 14.5f,  // Starts close & low...
        orbitEndDistance = 25.5f,    // ...then pulls BACK while zooming IN (Vertigo Dolly-Zoom!)
        orbitStartHeight = 4.6f,     // Ground-skimming hero angle
        orbitEndHeight = 6.2f,
        startAzimuthOffset = 38f,    // Approaches from the opposite flank
        orbitSweepAngle = -56f,      // Reverse sweep
        startLookHeight = 9.5f,      // Looks UP into the burning sky first, then tilts down onto impacts
        endLookHeight = 1.2f,
        startFov = 50f,              // Wide close-up...
        endFov = 25f,                // ...compresses into tight telephoto as camera pulls back!
        dutchRollAngle = -4.6f,
        positionSmoothTime = 0.10f,
        rotationSmoothSpeed = 12f,
        handheldDriftAmplitude = 0.24f,
        handheldDriftFrequency = 0.95f,
        shakeMultiplier = 1.6f
    };

    [Header("Optional Slow Motion (Single-Player)")]
    [Tooltip("Subtly slows time during the barrage for dramatic effect. Keep off for online multiplayer.")]
    [SerializeField] private bool enableSlowMotion = false;
    [SerializeField, Range(0.4f, 1f)] private float slowMotionTimeScale = 0.75f;

    [Header("Big Fireball Profile (Doomsday Sky-Gaze & Thermal Whip-Arc)")]
    [Tooltip("Ground-level distance from target while staring up at the falling meteor.")]
    [SerializeField, Range(6f, 20f)] private float fireballGroundGazeDist = 11.5f;
    [Tooltip("Low ground-level height during the incoming sky-gaze.")]
    [SerializeField, Range(1.8f, 8f)] private float fireballGroundGazeHeight = 2.8f;
    [Tooltip("Dramatic Dutch cant/roll angle (degrees) while staring up at the falling meteor.")]
    [SerializeField, Range(-25f, 25f)] private float fireballSkyGazeRoll = -14f;
    [Tooltip("Tight telephoto FOV as the meteor plummets toward the ground.")]
    [SerializeField, Range(20f, 55f)] private float fireballChaseFov = 34f;
    [Tooltip("Explosive wide-angle FOV burst at the moment of fireball impact.")]
    [SerializeField, Range(45f, 85f)] private float fireballImpactBurstFov = 64f;
    [Tooltip("How many degrees the camera whips around the blast during the thermal updraft.")]
    [SerializeField, Range(-140f, 140f)] private float fireballBlastWhipAngle = 95f;
    [Tooltip("Distance from the blast center at the peak of the thermal updraft.")]
    [SerializeField, Range(12f, 35f)] private float fireballRecoilDistance = 22f;
    [Tooltip("Height the camera rises to during the explosion aftermath.")]
    [SerializeField, Range(8f, 28f)] private float fireballAftermathHeight = 16.5f;
    [Tooltip("How long the thermal whip-arc aftermath lasts before gliding back.")]
    [SerializeField, Range(0.4f, 2.2f)] private float fireballAftermathDuration = 1.05f;
    [Tooltip("Return glide duration after the fireball explosion.")]
    [SerializeField, Range(0.3f, 2f)] private float fireballExitDuration = 0.9f;

    [Header("Fire Arrow Projectile Cam (Single-Arrow Close-Up)")]
    [Tooltip("Close-up distance from the tracked hero arrow during its ballistic flight.")]
    [SerializeField, Range(1.2f, 8f)] private float arrowCamDistance = 2.6f;
    [Tooltip("Height offset relative to the flying arrow.")]
    [SerializeField, Range(-1f, 4f)] private float arrowCamHeightOffset = 0.65f;
    [Tooltip("Side angle sweep around the flying arrow (degrees) from behind-fletching to side-profile.")]
    [SerializeField, Range(-90f, 90f)] private float arrowCamStartAngle = -35f;
    [SerializeField, Range(-90f, 90f)] private float arrowCamEndAngle = 55f;
    [Tooltip("Tight telephoto FOV while locked onto the single flying arrow.")]
    [SerializeField, Range(18f, 50f)] private float arrowCamFov = 30f;
    [Tooltip("How far the camera pulls back on arrow impact to reveal the rest of the volley.")]
    [SerializeField, Range(6f, 24f)] private float arrowImpactPullbackDist = 13.5f;
    [Tooltip("Linger duration after the hero arrow lands before returning to gameplay view.")]
    [SerializeField, Range(0.4f, 2.5f)] private float arrowAftermathDuration = 1.25f;
    [Tooltip("Return glide duration after the Fire Arrow sequence.")]
    [SerializeField, Range(0.3f, 2f)] private float arrowExitDuration = 0.85f;

    [Header("Reinforcement Cart Cam (Rear-Wheel Chase & Dismount Sweep)")]
    [Tooltip("Distance behind the wagon during the low rear-wheel chase.")]
    [SerializeField, Range(2f, 10f)] private float cartRearDistance = 4.2f;
    [Tooltip("Side offset to frame the spinning rear wheel in the foreground.")]
    [SerializeField, Range(0.8f, 6f)] private float cartSideDistance = 2.1f;
    [Tooltip("Ultra-low wheel-level height while chasing from behind.")]
    [SerializeField, Range(0.6f, 4f)] private float cartTrackHeight = 1.2f;
    [Tooltip("How far forward along the wagon the camera creeps during the charge (0 = rear wheel, 1 = mid-side).")]
    [SerializeField, Range(0f, 6f)] private float cartCreepForward = 2.8f;
    [Tooltip("Wide-action FOV during the low rear-wheel chase.")]
    [SerializeField, Range(30f, 65f)] private float cartTrackFov = 46f;
    [Tooltip("Height the camera cranes up to while sweeping around for the troop dismount.")]
    [SerializeField, Range(2f, 10f)] private float cartDismountHeight = 3.8f;
    [Tooltip("Distance from the halted cart during the dismount sweep.")]
    [SerializeField, Range(4f, 14f)] private float cartDismountDistance = 7.2f;
    [Tooltip("How long to linger on the dismounting troops before returning.")]
    [SerializeField, Range(0.5f, 3f)] private float cartDismountHoldDuration = 1.45f;
    [Tooltip("Return glide duration after the Reinforcement Cart sequence.")]
    [SerializeField, Range(0.3f, 2f)] private float cartExitDuration = 0.85f;

    [Header("Wood Log Cam (Third-Person Left-to-Right Chase)")]
    [Tooltip("Distance behind the rolling log in third-person view.")]
    [SerializeField, Range(4f, 18f)] private float logFollowBackDistance = 10.5f;
    [Tooltip("Height above the rolling log in third-person view.")]
    [SerializeField, Range(2f, 12f)] private float logTrackHeight = 6.2f;
    [Tooltip("Starting lateral offset (left side of the log).")]
    [SerializeField, Range(-10f, 0f)] private float logLeftStartOffset = -5.5f;
    [Tooltip("Ending lateral offset (right side of the log).")]
    [SerializeField, Range(0f, 10f)] private float logRightEndOffset = 5.5f;
    [Tooltip("Wider FOV during the third-person log chase for a broader battlefield viewpoint.")]
    [SerializeField, Range(35f, 75f)] private float logCloseFov = 56f;
    [Tooltip("How long to linger after the log finishes rolling before returning.")]
    [SerializeField, Range(0.2f, 2f)] private float logAftermathDuration = 0.55f;
    [Tooltip("Return glide duration after the Wood Log sequence.")]
    [SerializeField, Range(0.3f, 2f)] private float logExitDuration = 0.8f;

    [Header("Obstacle Avoidance (Anti-Clipping)")]
    [Tooltip("Prevents castles, walls, towers, or terrain from blocking the camera during low-angle orbits.")]
    [SerializeField] private bool avoidObstacles = true;
    [Tooltip("Layers checked for camera line-of-sight blocking.")]
    [SerializeField] private LayerMask obstacleLayerMask = ~0;
    [Tooltip("SphereCast radius used to detect obstacles smoothly before clipping.")]
    [SerializeField, Range(0.2f, 1.5f)] private float collisionProbeRadius = 0.55f;
    [Tooltip("Distance kept in front of an obstacle surface.")]
    [SerializeField, Range(0.2f, 2.5f)] private float collisionPadding = 0.75f;
    [Tooltip("Minimum allowed distance from the impact center when pulled in by an obstacle.")]
    [SerializeField, Range(3f, 12f)] private float minCollisionDistance = 5.5f;
    [Tooltip("Minimum height above ground/terrain under the camera.")]
    [SerializeField, Range(1f, 8f)] private float minGroundClearance = 2.5f;
    [Tooltip("How quickly the camera pulls in front of an obstacle (higher = less clipping).")]
    [SerializeField, Range(5f, 35f)] private float obstaclePullInSpeed = 18f;
    [Tooltip("How smoothly the camera glides back out once clear of an obstacle.")]
    [SerializeField, Range(2f, 15f)] private float obstacleRecoverSpeed = 5f;

    private Camera targetCamera;
    private CameraSwipeController swipeController;

    private bool isPlaying;
    private CinematicStyle activeStyle;
    private float elapsed;
    private float activeHoldDuration;
    private float totalSequenceDuration;
    private CameraChoreographyProfile activeProfile;
    private Transform trackedProjectile;
    private Vector3 lastTrackedPos;
    private Vector3 prevTrackedPos;
    private Vector3 trackedVelocityDir;
    private float fireballFlightTime;
    private bool cartArrived;
    private float cartArrivalTime;

    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedFOV;
    private float savedTimeScale = 1f;
    private bool modifiedTimeScale;

    private Vector3 focusCenter;
    private Vector3 baseApproachDir;
    private Vector3 currentDampedPos;
    private Vector3 posVelocity;
    private Quaternion currentDampedRot;
    private float currentFov;
    private float currentClearanceFactor = 1f;
    private float noiseSeedX;
    private float noiseSeedY;

    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Triggers the Arrow Shower cinematic (curved aerial dive + spiral dolly-in + crane rise).
    /// </summary>
    public static void TriggerArrowShower(Vector3 center, float showerDuration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        TriggerStyle(CinematicStyle.ArrowShower, center, showerDuration, casterTeamId);
    }

    /// <summary>
    /// Triggers the Fire Arrows cinematic (low ground-skim + sky whip-tilt + Vertigo dolly-zoom).
    /// </summary>
    public static void TriggerFireArrows(Vector3 center, float showerDuration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        TriggerStyle(CinematicStyle.FireArrows, center, showerDuration, casterTeamId);
    }

    /// <summary>
    /// Triggers the Big Fireball cinematic (Doomsday Sky-Gaze -> Thermal Whip-Arc).
    /// </summary>
    public static void TriggerBigFireball(Transform fireballTransform, Vector3 targetCenter, float flightTime, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        CinematicCameraController controller = GetOrCreateController();
        if (controller != null && controller.enableCinematics)
        {
            controller.PlayBigFireballCinematic(fireballTransform, targetCenter, flightTime, casterTeamId);
        }
    }

    /// <summary>
    /// Triggers the Fire Arrow Projectile close-up cam tracking a single hero arrow along its ballistic arc.
    /// </summary>
    public static void TriggerFireArrowProjectile(Transform heroArrow, Vector3 targetCenter, float flightTime, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        CinematicCameraController controller = GetOrCreateController();
        if (controller != null && controller.enableCinematics)
        {
            controller.PlayFireArrowProjectileCinematic(heroArrow, targetCenter, flightTime, casterTeamId);
        }
    }

    /// <summary>
    /// Triggers the Reinforcement Cart chariot-style wheel-level tracking cam.
    /// </summary>
    public static void TriggerReinforcementCart(Transform cartTransform, Vector3 destinationPoint, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        CinematicCameraController controller = GetOrCreateController();
        if (controller != null && controller.enableCinematics)
        {
            controller.PlayReinforcementCartCinematic(cartTransform, destinationPoint, casterTeamId);
        }
    }

    /// <summary>
    /// Notifies the camera that the Reinforcement Cart has arrived and begun dismounting troops.
    /// </summary>
    public static void NotifyReinforcementCartArrived()
    {
        if (!GlobalCinematicsEnabled) return;
        if (Instance != null && Instance.isPlaying && Instance.activeStyle == CinematicStyle.ReinforcementCart && !Instance.cartArrived)
        {
            Instance.cartArrived = true;
            Instance.cartArrivalTime = Instance.elapsed;
            Instance.totalSequenceDuration = Instance.cartArrivalTime + Instance.cartDismountHoldDuration + Instance.cartExitDuration;
        }
    }

    /// <summary>
    /// Triggers the Wood Log front-lead "steamroller" cam looking back at the log barreling toward the lens.
    /// </summary>
    public static void TriggerWoodLog(Transform logTransform, Vector3 forwardDir, float estimatedDuration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        CinematicCameraController controller = GetOrCreateController();
        if (controller != null && controller.enableCinematics)
        {
            controller.PlayWoodLogCinematic(logTransform, forwardDir, estimatedDuration, casterTeamId);
        }
    }

    /// <summary>
    /// Notifies the camera that the Wood Log has finished its roll.
    /// </summary>
    public static void NotifyWoodLogFinished()
    {
        if (!GlobalCinematicsEnabled) return;
        if (Instance != null && Instance.isPlaying && Instance.activeStyle == CinematicStyle.WoodLog && !Instance.cartArrived)
        {
            Instance.cartArrived = true;
            Instance.cartArrivalTime = Instance.elapsed;
            Instance.totalSequenceDuration = Instance.cartArrivalTime + Instance.logAftermathDuration + Instance.logExitDuration;
        }
    }

    private static void TriggerStyle(CinematicStyle style, Vector3 center, float duration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled) return;
        CinematicCameraController controller = GetOrCreateController();
        if (controller != null && controller.enableCinematics)
        {
            controller.PlayCinematic(style, center, duration, casterTeamId);
        }
    }

    private static CinematicCameraController GetOrCreateController()
    {
        if (!GlobalCinematicsEnabled) return null;
        CinematicCameraController controller = Instance;
        if (controller == null)
        {
            controller = FindFirstObjectByType<CinematicCameraController>();
        }
        if (controller == null && Camera.main != null)
        {
            controller = Camera.main.gameObject.AddComponent<CinematicCameraController>();
        }
        return controller;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // If an instance was placed in the scene with enableCinematics explicitly true in inspector, sync flag
        if (enableCinematics)
        {
            GlobalCinematicsEnabled = true;
        }

        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;

        swipeController = GetComponent<CameraSwipeController>();
        if (swipeController == null) swipeController = CameraSwipeController.Instance;
    }

    private void OnValidate()
    {
        GlobalCinematicsEnabled = enableCinematics;
        if (!enableCinematics && isPlaying)
        {
            RestoreStateImmediate();
        }
    }

    private void OnDisable()
    {
        RestoreStateImmediate();
    }

    private void OnDestroy()
    {
        RestoreStateImmediate();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Backwards-compatible entry point for Arrow Shower.
    /// </summary>
    public void PlayArrowShowerCinematic(Vector3 center, float showerDuration, int casterTeamId)
    {
        PlayCinematic(CinematicStyle.ArrowShower, center, showerDuration, casterTeamId);
    }

    /// <summary>
    /// Starts (or smoothly retargets) a choreographed cinematic camera sequence for the given style.
    /// </summary>
    public void PlayCinematic(CinematicStyle style, Vector3 center, float showerDuration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            return;
        }

        if (triggerOnlyForLocalPlayer && casterTeamId != GameModeManager.LocalTeamId)
        {
            return;
        }

        if (isPlaying && !allowRetriggerInterrupt)
        {
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (swipeController == null) swipeController = CameraSwipeController.Instance;

        activeStyle = style;
        trackedProjectile = null;
        activeProfile = (style == CinematicStyle.FireArrows) ? fireArrowsProfile : arrowShowerProfile;

        if (!isPlaying)
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedFOV = targetCamera != null ? targetCamera.fieldOfView : 45f;

            currentDampedPos = savedPosition;
            currentDampedRot = savedRotation;
            currentFov = savedFOV;
            currentClearanceFactor = 1f;
            posVelocity = Vector3.zero;

            noiseSeedX = UnityEngine.Random.Range(0f, 100f);
            noiseSeedY = UnityEngine.Random.Range(0f, 100f);

            if (swipeController != null)
            {
                swipeController.IsCinematicOverride = true;
            }
            CameraSwipeController.SuppressSwipe = true;
        }
        else
        {
            currentDampedPos = transform.position;
            currentDampedRot = transform.rotation;
            if (targetCamera != null) currentFov = targetCamera.fieldOfView;
        }

        focusCenter = center;
        activeHoldDuration = Mathf.Max(0.5f, showerDuration + activeProfile.holdPadding);
        totalSequenceDuration = activeProfile.entryDuration + activeHoldDuration + activeProfile.exitDuration;
        elapsed = 0f;
        isPlaying = true;

        // Horizontal approach direction from impact center back toward gameplay camera
        Vector3 toCamXZ = savedPosition - focusCenter;
        toCamXZ.y = 0f;
        if (toCamXZ.sqrMagnitude < 0.01f)
        {
            toCamXZ = -(savedRotation * Vector3.forward);
            toCamXZ.y = 0f;
        }
        baseApproachDir = toCamXZ.sqrMagnitude > 0.001f ? toCamXZ.normalized : Vector3.left;

        if (enableSlowMotion && !modifiedTimeScale)
        {
            savedTimeScale = Time.timeScale;
            modifiedTimeScale = true;
        }
    }

    /// <summary>
    /// Starts the Big Fireball meteor-chase -> impact shockwave recoil cinematic.
    /// </summary>
    public void PlayBigFireballCinematic(Transform fireballTransform, Vector3 targetCenter, float flightTime, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            return;
        }

        if (triggerOnlyForLocalPlayer && casterTeamId != GameModeManager.LocalTeamId)
        {
            return;
        }

        if (isPlaying && !allowRetriggerInterrupt)
        {
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (swipeController == null) swipeController = CameraSwipeController.Instance;

        activeStyle = CinematicStyle.BigFireball;
        activeProfile = fireArrowsProfile; // Reuse smooth obstacle & shake settings
        trackedProjectile = fireballTransform;
        lastTrackedPos = fireballTransform != null ? fireballTransform.position : targetCenter + Vector3.up * 20f;
        fireballFlightTime = Mathf.Max(0.35f, flightTime);

        if (!isPlaying)
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedFOV = targetCamera != null ? targetCamera.fieldOfView : 45f;

            currentDampedPos = savedPosition;
            currentDampedRot = savedRotation;
            currentFov = savedFOV;
            currentClearanceFactor = 1f;
            posVelocity = Vector3.zero;

            noiseSeedX = UnityEngine.Random.Range(0f, 100f);
            noiseSeedY = UnityEngine.Random.Range(0f, 100f);

            if (swipeController != null)
            {
                swipeController.IsCinematicOverride = true;
            }
            CameraSwipeController.SuppressSwipe = true;
        }
        else
        {
            currentDampedPos = transform.position;
            currentDampedRot = transform.rotation;
            if (targetCamera != null) currentFov = targetCamera.fieldOfView;
        }

        focusCenter = targetCenter;
        elapsed = 0f;
        totalSequenceDuration = fireballFlightTime + fireballAftermathDuration + fireballExitDuration;
        isPlaying = true;

        Vector3 toCamXZ = savedPosition - focusCenter;
        toCamXZ.y = 0f;
        if (toCamXZ.sqrMagnitude < 0.01f)
        {
            toCamXZ = -(savedRotation * Vector3.forward);
            toCamXZ.y = 0f;
        }
        baseApproachDir = toCamXZ.sqrMagnitude > 0.001f ? toCamXZ.normalized : Vector3.left;

        if (enableSlowMotion && !modifiedTimeScale)
        {
            savedTimeScale = Time.timeScale;
            modifiedTimeScale = true;
        }
    }

    /// <summary>
    /// Starts the single-arrow close-up projectile tracking cinematic for Fire Arrows.
    /// </summary>
    public void PlayFireArrowProjectileCinematic(Transform heroArrow, Vector3 targetCenter, float flightTime, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            return;
        }

        if (triggerOnlyForLocalPlayer && casterTeamId != GameModeManager.LocalTeamId)
        {
            return;
        }

        if (isPlaying && !allowRetriggerInterrupt)
        {
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (swipeController == null) swipeController = CameraSwipeController.Instance;

        activeStyle = CinematicStyle.FireArrowProjectile;
        activeProfile = fireArrowsProfile;
        trackedProjectile = heroArrow;
        lastTrackedPos = heroArrow != null ? heroArrow.position : targetCenter + Vector3.up * 12f;
        prevTrackedPos = lastTrackedPos;
        trackedVelocityDir = (targetCenter - lastTrackedPos).sqrMagnitude > 0.01f
            ? (targetCenter - lastTrackedPos).normalized
            : Vector3.right;
        fireballFlightTime = Mathf.Max(0.45f, flightTime);

        if (!isPlaying)
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedFOV = targetCamera != null ? targetCamera.fieldOfView : 45f;

            currentDampedPos = savedPosition;
            currentDampedRot = savedRotation;
            currentFov = savedFOV;
            currentClearanceFactor = 1f;
            posVelocity = Vector3.zero;

            noiseSeedX = UnityEngine.Random.Range(0f, 100f);
            noiseSeedY = UnityEngine.Random.Range(0f, 100f);

            if (swipeController != null)
            {
                swipeController.IsCinematicOverride = true;
            }
            CameraSwipeController.SuppressSwipe = true;
        }
        else
        {
            currentDampedPos = transform.position;
            currentDampedRot = transform.rotation;
            if (targetCamera != null) currentFov = targetCamera.fieldOfView;
        }

        focusCenter = targetCenter;
        elapsed = 0f;
        totalSequenceDuration = fireballFlightTime + arrowAftermathDuration + arrowExitDuration;
        isPlaying = true;

        Vector3 toCamXZ = savedPosition - focusCenter;
        toCamXZ.y = 0f;
        if (toCamXZ.sqrMagnitude < 0.01f)
        {
            toCamXZ = -(savedRotation * Vector3.forward);
            toCamXZ.y = 0f;
        }
        baseApproachDir = toCamXZ.sqrMagnitude > 0.001f ? toCamXZ.normalized : Vector3.left;

        if (enableSlowMotion && !modifiedTimeScale)
        {
            savedTimeScale = Time.timeScale;
            modifiedTimeScale = true;
        }
    }

    /// <summary>
    /// Starts the Reinforcement Cart chariot-style low-angle tracking cinematic.
    /// </summary>
    public void PlayReinforcementCartCinematic(Transform cartTransform, Vector3 destinationPoint, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            return;
        }

        if (triggerOnlyForLocalPlayer && casterTeamId != GameModeManager.LocalTeamId)
        {
            return;
        }

        if (isPlaying && !allowRetriggerInterrupt)
        {
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (swipeController == null) swipeController = CameraSwipeController.Instance;

        activeStyle = CinematicStyle.ReinforcementCart;
        activeProfile = arrowShowerProfile;
        trackedProjectile = cartTransform;
        lastTrackedPos = cartTransform != null ? cartTransform.position : destinationPoint - Vector3.right * 20f;
        prevTrackedPos = lastTrackedPos;
        trackedVelocityDir = (destinationPoint - lastTrackedPos).sqrMagnitude > 0.01f
            ? (destinationPoint - lastTrackedPos).normalized
            : Vector3.right;

        cartArrived = false;
        cartArrivalTime = 0f;

        if (!isPlaying)
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedFOV = targetCamera != null ? targetCamera.fieldOfView : 45f;

            currentDampedPos = savedPosition;
            currentDampedRot = savedRotation;
            currentFov = savedFOV;
            currentClearanceFactor = 1f;
            posVelocity = Vector3.zero;

            noiseSeedX = UnityEngine.Random.Range(0f, 100f);
            noiseSeedY = UnityEngine.Random.Range(0f, 100f);

            if (swipeController != null)
            {
                swipeController.IsCinematicOverride = true;
            }
            CameraSwipeController.SuppressSwipe = true;
        }
        else
        {
            currentDampedPos = transform.position;
            currentDampedRot = transform.rotation;
            if (targetCamera != null) currentFov = targetCamera.fieldOfView;
        }

        focusCenter = destinationPoint;
        elapsed = 0f;
        // Max fallback duration in case cart never calls NotifyReinforcementCartArrived
        totalSequenceDuration = 5.0f + cartDismountHoldDuration + cartExitDuration;
        isPlaying = true;

        Vector3 toCamXZ = savedPosition - focusCenter;
        toCamXZ.y = 0f;
        if (toCamXZ.sqrMagnitude < 0.01f)
        {
            toCamXZ = -(savedRotation * Vector3.forward);
            toCamXZ.y = 0f;
        }
        baseApproachDir = toCamXZ.sqrMagnitude > 0.001f ? toCamXZ.normalized : Vector3.left;

        if (enableSlowMotion && !modifiedTimeScale)
        {
            savedTimeScale = Time.timeScale;
            modifiedTimeScale = true;
        }
    }

    /// <summary>
    /// Starts the Wood Log front-lead steamroller tracking cinematic.
    /// </summary>
    public void PlayWoodLogCinematic(Transform logTransform, Vector3 forwardDir, float estimatedDuration, int casterTeamId)
    {
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            return;
        }

        if (triggerOnlyForLocalPlayer && casterTeamId != GameModeManager.LocalTeamId)
        {
            return;
        }

        if (isPlaying && !allowRetriggerInterrupt)
        {
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (swipeController == null) swipeController = CameraSwipeController.Instance;

        activeStyle = CinematicStyle.WoodLog;
        activeProfile = arrowShowerProfile;
        trackedProjectile = logTransform;
        lastTrackedPos = logTransform != null ? logTransform.position : transform.position;
        prevTrackedPos = lastTrackedPos;

        Vector3 flatFwd = new Vector3(forwardDir.x, 0f, forwardDir.z);
        trackedVelocityDir = flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : Vector3.right;
        fireballFlightTime = Mathf.Max(0.8f, estimatedDuration);

        cartArrived = false;
        cartArrivalTime = 0f;

        if (!isPlaying)
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedFOV = targetCamera != null ? targetCamera.fieldOfView : 45f;

            currentDampedPos = savedPosition;
            currentDampedRot = savedRotation;
            currentFov = savedFOV;
            currentClearanceFactor = 1f;
            posVelocity = Vector3.zero;

            noiseSeedX = UnityEngine.Random.Range(0f, 100f);
            noiseSeedY = UnityEngine.Random.Range(0f, 100f);

            if (swipeController != null)
            {
                swipeController.IsCinematicOverride = true;
            }
            CameraSwipeController.SuppressSwipe = true;
        }
        else
        {
            currentDampedPos = transform.position;
            currentDampedRot = transform.rotation;
            if (targetCamera != null) currentFov = targetCamera.fieldOfView;
        }

        focusCenter = lastTrackedPos;
        elapsed = 0f;
        totalSequenceDuration = fireballFlightTime + logAftermathDuration + logExitDuration;
        isPlaying = true;

        Vector3 toCamXZ = savedPosition - focusCenter;
        toCamXZ.y = 0f;
        if (toCamXZ.sqrMagnitude < 0.01f)
        {
            toCamXZ = -(savedRotation * Vector3.forward);
            toCamXZ.y = 0f;
        }
        baseApproachDir = toCamXZ.sqrMagnitude > 0.001f ? toCamXZ.normalized : Vector3.left;

        if (enableSlowMotion && !modifiedTimeScale)
        {
            savedTimeScale = Time.timeScale;
            modifiedTimeScale = true;
        }
    }

    private void LateUpdate()
    {
        if (!isPlaying) return;
        if (!GlobalCinematicsEnabled || !enableCinematics)
        {
            RestoreStateImmediate();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        elapsed += dt;
        if (elapsed >= totalSequenceDuration)
        {
            FinishCinematic();
            return;
        }

        if (activeStyle == CinematicStyle.BigFireball)
        {
            UpdateBigFireballCinematic(dt);
            return;
        }

        if (activeStyle == CinematicStyle.FireArrowProjectile)
        {
            UpdateFireArrowProjectileCinematic(dt);
            return;
        }

        if (activeStyle == CinematicStyle.ReinforcementCart)
        {
            UpdateReinforcementCartCinematic(dt);
            return;
        }

        if (activeStyle == CinematicStyle.WoodLog)
        {
            UpdateWoodLogCinematic(dt);
            return;
        }

        float rawProgress = Mathf.Clamp01(elapsed / totalSequenceDuration);
        float orbitProgress = EaseInOutSine(rawProgress);

        Vector3 orbitPos = EvaluateOrbitPosition(orbitProgress, out Vector3 radialDir);
        Quaternion orbitRot = EvaluateOrbitRotation(orbitPos, orbitProgress);
        float orbitFov = Mathf.Lerp(activeProfile.startFov, activeProfile.endFov, orbitProgress);

        Vector3 desiredPos;
        Quaternion desiredRot;
        float desiredFov;
        float activeWeight;

        if (elapsed < activeProfile.entryDuration)
        {
            // BEAT 1: Curved Bezier dive with speed-FOV pulse
            float t = SmootherStep(elapsed / activeProfile.entryDuration);
            activeWeight = t;

            Vector3 sideDir = Vector3.Cross(Vector3.up, baseApproachDir).normalized;
            Vector3 midControl = Vector3.Lerp(savedPosition, orbitPos, 0.45f)
                + sideDir * activeProfile.entryArcSwing
                + Vector3.up * activeProfile.entryDiveDip;

            desiredPos = QuadraticBezier(savedPosition, midControl, orbitPos, t);
            desiredRot = Quaternion.Slerp(savedRotation, orbitRot, t);

            float speedPulse = Mathf.Sin(t * Mathf.PI);
            desiredFov = Mathf.Lerp(savedFOV, orbitFov, t) + (activeProfile.entryPeakFov - savedFOV) * speedPulse * 0.65f;
        }
        else if (elapsed < activeProfile.entryDuration + activeHoldDuration)
        {
            // BEAT 2: Full choreographed orbit / dolly-zoom / crane sweep
            activeWeight = 1f;
            desiredPos = orbitPos;
            desiredRot = orbitRot;
            desiredFov = orbitFov;
        }
        else
        {
            // BEAT 3: Graceful curved crane-out back to saved gameplay pose
            float exitT = SmootherStep((elapsed - (activeProfile.entryDuration + activeHoldDuration)) / activeProfile.exitDuration);
            activeWeight = 1f - exitT;

            Vector3 sideDir = Vector3.Cross(Vector3.up, radialDir).normalized;
            Vector3 midControl = Vector3.Lerp(orbitPos, savedPosition, 0.5f)
                - sideDir * (activeProfile.entryArcSwing * 0.6f)
                + Vector3.up * 4.5f;

            desiredPos = QuadraticBezier(orbitPos, midControl, savedPosition, exitT);
            desiredRot = Quaternion.Slerp(orbitRot, savedRotation, exitT);
            desiredFov = Mathf.Lerp(orbitFov, savedFOV, exitT);
        }

        // Subtle organic steadicam drift
        if (activeProfile.handheldDriftAmplitude > 0f && activeWeight > 0.01f)
        {
            float nx = (Mathf.PerlinNoise(noiseSeedX, elapsed * activeProfile.handheldDriftFrequency) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(noiseSeedY, elapsed * activeProfile.handheldDriftFrequency * 0.85f) - 0.5f) * 2f;
            Vector3 camRight = desiredRot * Vector3.right;
            Vector3 camUp = desiredRot * Vector3.up;
            desiredPos += (camRight * nx + camUp * ny) * (activeProfile.handheldDriftAmplitude * activeWeight);
        }

        // Smooth obstacle avoidance & ground clearance so walls/towers never block the shot
        if (avoidObstacles && activeWeight > 0.01f)
        {
            desiredPos = ResolveCameraObstacles(desiredPos, orbitProgress, dt);
        }

        // Sub-frame SmoothDamp & Slerp for butter-smooth motion
        float smoothTime = Mathf.Lerp(0.035f, activeProfile.positionSmoothTime, activeWeight);
        currentDampedPos = Vector3.SmoothDamp(currentDampedPos, desiredPos, ref posVelocity, smoothTime, Mathf.Infinity, dt);
        currentDampedRot = Quaternion.Slerp(currentDampedRot, desiredRot, 1f - Mathf.Exp(-activeProfile.rotationSmoothSpeed * dt));
        currentFov = Mathf.Lerp(currentFov, desiredFov, 1f - Mathf.Exp(-10f * dt));

        Vector3 shake = Vector3.zero;
        if (swipeController != null)
        {
            shake = swipeController.CurrentShakeOffset * activeProfile.shakeMultiplier;
        }

        transform.position = currentDampedPos + shake;
        transform.rotation = currentDampedRot;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = currentFov;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = Mathf.Lerp(savedTimeScale, slowMotionTimeScale, activeWeight);
        }
    }

    private Vector3 EvaluateOrbitPosition(float orbitProgress, out Vector3 radialDir)
    {
        float azimuth = activeProfile.startAzimuthOffset + (activeProfile.orbitSweepAngle * orbitProgress);
        radialDir = (Quaternion.Euler(0f, azimuth, 0f) * baseApproachDir).normalized;

        float dist = Mathf.Lerp(activeProfile.orbitStartDistance, activeProfile.orbitEndDistance, orbitProgress);
        float height = Mathf.Lerp(activeProfile.orbitStartHeight, activeProfile.orbitEndHeight, orbitProgress);

        return focusCenter + radialDir * dist + Vector3.up * height;
    }

    private Quaternion EvaluateOrbitRotation(Vector3 camPos, float orbitProgress)
    {
        float aimHeight = Mathf.Lerp(activeProfile.startLookHeight, activeProfile.endLookHeight, EaseInOutSine(orbitProgress));
        Vector3 aimTarget = focusCenter + Vector3.up * aimHeight;
        Vector3 lookDir = (aimTarget - camPos).normalized;

        Quaternion lookRot = Quaternion.LookRotation(lookDir, Vector3.up);

        if (Mathf.Abs(activeProfile.dutchRollAngle) > 0.01f)
        {
            float roll = activeProfile.dutchRollAngle * Mathf.Sin(orbitProgress * Mathf.PI * 2f) * 0.5f
                       + activeProfile.dutchRollAngle * Mathf.Sin(orbitProgress * Mathf.PI) * 0.5f;
            lookRot *= Quaternion.Euler(0f, 0f, roll);
        }

        return lookRot;
    }

    private Vector3 ResolveCameraObstacles(Vector3 desiredPos, float orbitProgress, float dt)
    {
        float aimHeight = Mathf.Lerp(activeProfile.startLookHeight, activeProfile.endLookHeight, EaseInOutSine(orbitProgress));
        Vector3 sightOrigin = focusCenter + Vector3.up * Mathf.Max(1.2f, aimHeight * 0.5f);
        Vector3 toCam = desiredPos - sightOrigin;
        float fullDist = toCam.magnitude;

        float targetFactor = 1f;
        if (fullDist > 0.5f)
        {
            Vector3 dir = toCam / fullDist;
            RaycastHit[] hits = Physics.SphereCastAll(
                sightOrigin,
                collisionProbeRadius,
                dir,
                fullDist,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore);

            float closestBlockingDist = fullDist;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;

                // Ignore moving soldiers and falling arrows so they don't cause camera pumping
                if (col.GetComponentInParent<PeasantUnitController>() != null) continue;
                if (col.GetComponentInParent<ArrowProjectile>() != null) continue;
                if (col.GetComponentInParent<ArrowShowerEffect>() != null) continue;
                if (col.GetComponentInParent<FireArrowsEffect>() != null) continue;

                float safeDist = Mathf.Max(minCollisionDistance, hits[i].distance - collisionPadding);
                if (safeDist < closestBlockingDist)
                {
                    closestBlockingDist = safeDist;
                }
            }

            targetFactor = Mathf.Clamp01(closestBlockingDist / fullDist);
        }

        // Asymmetric smoothing: pull in quickly to prevent clipping, glide back out smoothly
        float speed = (targetFactor < currentClearanceFactor) ? obstaclePullInSpeed : obstacleRecoverSpeed;
        currentClearanceFactor = Mathf.Lerp(currentClearanceFactor, targetFactor, 1f - Mathf.Exp(-speed * dt));

        Vector3 resolvedPos = sightOrigin + toCam * currentClearanceFactor;

        // Slight upward lift when pulled in tight so we peek over low walls/fences
        if (currentClearanceFactor < 0.98f)
        {
            resolvedPos.y += (1f - currentClearanceFactor) * 3.0f;
        }

        // Enforce minimum ground/terrain clearance under the camera
        if (Physics.Raycast(resolvedPos + Vector3.up * 15f, Vector3.down, out RaycastHit groundHit, 30f, obstacleLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (groundHit.collider.GetComponentInParent<PeasantUnitController>() == null)
            {
                float minY = groundHit.point.y + minGroundClearance;
                if (resolvedPos.y < minY)
                {
                    resolvedPos.y = minY;
                }
            }
        }

        return resolvedPos;
    }

    private void UpdateBigFireballCinematic(float dt)
    {
        if (trackedProjectile != null)
        {
            lastTrackedPos = trackedProjectile.position;
        }

        Vector3 desiredPos;
        Quaternion desiredRot;
        float desiredFov;
        float activeWeight;

        // Ground-gaze anchor near the target zone, looking UP at the plummeting fireball
        Vector3 gazeDir = (Quaternion.Euler(0f, -28f, 0f) * baseApproachDir).normalized;
        Vector3 gazeStartPos = focusCenter + gazeDir * fireballGroundGazeDist + Vector3.up * fireballGroundGazeHeight;
        Vector3 gazeCreepPos = focusCenter + gazeDir * (fireballGroundGazeDist + 3.5f) + Vector3.up * (fireballGroundGazeHeight + 0.6f);

        // End of the thermal whip-arc aftermath pose
        Vector3 whipEndDir = (Quaternion.Euler(0f, -28f + fireballBlastWhipAngle, 0f) * baseApproachDir).normalized;
        Vector3 whipEndPos = focusCenter + whipEndDir * fireballRecoilDistance + Vector3.up * fireballAftermathHeight;
        Quaternion whipEndRot = Quaternion.LookRotation(((focusCenter + Vector3.up * 1.4f) - whipEndPos).normalized, Vector3.up);

        if (elapsed < fireballFlightTime)
        {
            // BEAT 1: Doomsday Sky-Gaze — swoop down to ground level and stare UP as the meteor plummets
            float t = Mathf.Clamp01(elapsed / fireballFlightTime);
            float easeT = SmootherStep(t);
            activeWeight = easeT;

            Vector3 currentGazePos = Vector3.Lerp(gazeStartPos, gazeCreepPos, t);
            Vector3 sideDir = Vector3.Cross(Vector3.up, gazeDir).normalized;
            Vector3 midControl = Vector3.Lerp(savedPosition, currentGazePos, 0.5f) - sideDir * 9f + Vector3.up * -4f;
            desiredPos = QuadraticBezier(savedPosition, midControl, currentGazePos, easeT);

            // Track the falling fireball in the sky, then tilt down slightly right before impact
            Vector3 skyTarget = Vector3.Lerp(lastTrackedPos, focusCenter + Vector3.up * 2.5f, Mathf.Pow(t, 3f) * 0.45f);
            Quaternion gazeRot = Quaternion.LookRotation((skyTarget - desiredPos).normalized, Vector3.up);
            gazeRot *= Quaternion.Euler(0f, 0f, fireballSkyGazeRoll * easeT);

            desiredRot = Quaternion.Slerp(savedRotation, gazeRot, easeT);
            desiredFov = Mathf.Lerp(savedFOV, fireballChaseFov, easeT);
        }
        else if (elapsed < fireballFlightTime + fireballAftermathDuration)
        {
            // BEAT 2: Thermal Whip-Arc — blown upward & around the blast in a 95° sweeping arc with shockwave FOV punch
            float t = Mathf.Clamp01((elapsed - fireballFlightTime) / fireballAftermathDuration);
            float whipEase = 1f - Mathf.Pow(1f - t, 2.6f);
            activeWeight = 1f;

            float currentAngle = -28f + fireballBlastWhipAngle * whipEase;
            Vector3 arcDir = (Quaternion.Euler(0f, currentAngle, 0f) * baseApproachDir).normalized;
            float dist = Mathf.Lerp(fireballGroundGazeDist + 3.5f, fireballRecoilDistance, whipEase);
            float height = Mathf.Lerp(fireballGroundGazeHeight + 0.6f, fireballAftermathHeight, EaseInOutSine(t));

            desiredPos = focusCenter + arcDir * dist + Vector3.up * height;

            Vector3 blastLookPoint = focusCenter + Vector3.up * Mathf.Lerp(2.5f, 1.4f, whipEase);
            desiredRot = Quaternion.LookRotation((blastLookPoint - desiredPos).normalized, Vector3.up);
            float roll = Mathf.Lerp(fireballSkyGazeRoll, 0f, whipEase);
            desiredRot *= Quaternion.Euler(0f, 0f, roll);

            // Explosive wide-angle shockwave burst right on impact, then settling smoothly
            float shockwaveSpike = Mathf.Sin(Mathf.Clamp01(t * 2.4f) * Mathf.PI);
            desiredFov = Mathf.Lerp(fireballChaseFov, savedFOV, whipEase * 0.7f) + (fireballImpactBurstFov - fireballChaseFov) * shockwaveSpike * 0.8f;
        }
        else
        {
            // BEAT 3: Smooth curved return glide from the high thermal crater view back to gameplay camera
            float exitT = SmootherStep((elapsed - (fireballFlightTime + fireballAftermathDuration)) / fireballExitDuration);
            activeWeight = 1f - exitT;

            Vector3 sideDir = Vector3.Cross(Vector3.up, whipEndDir).normalized;
            Vector3 midControl = Vector3.Lerp(whipEndPos, savedPosition, 0.5f) + sideDir * 7f + Vector3.up * 6f;

            desiredPos = QuadraticBezier(whipEndPos, midControl, savedPosition, exitT);
            desiredRot = Quaternion.Slerp(whipEndRot, savedRotation, exitT);
            desiredFov = Mathf.Lerp(Mathf.Lerp(fireballChaseFov, savedFOV, 0.7f), savedFOV, exitT);
        }

        if (avoidObstacles && activeWeight > 0.01f)
        {
            desiredPos = ResolveCameraObstacles(desiredPos, Mathf.Clamp01(elapsed / totalSequenceDuration), dt);
        }

        float smoothTime = Mathf.Lerp(0.035f, 0.08f, activeWeight);
        currentDampedPos = Vector3.SmoothDamp(currentDampedPos, desiredPos, ref posVelocity, smoothTime, Mathf.Infinity, dt);
        currentDampedRot = Quaternion.Slerp(currentDampedRot, desiredRot, 1f - Mathf.Exp(-14f * dt));
        currentFov = Mathf.Lerp(currentFov, desiredFov, 1f - Mathf.Exp(-14f * dt));

        Vector3 shake = swipeController != null ? swipeController.CurrentShakeOffset * 1.55f : Vector3.zero;
        transform.position = currentDampedPos + shake;
        transform.rotation = currentDampedRot;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = currentFov;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = Mathf.Lerp(savedTimeScale, slowMotionTimeScale, activeWeight);
        }
    }

    private void UpdateFireArrowProjectileCinematic(float dt)
    {
        if (trackedProjectile != null)
        {
            prevTrackedPos = lastTrackedPos;
            lastTrackedPos = trackedProjectile.position;
            Vector3 delta = lastTrackedPos - prevTrackedPos;
            if (delta.sqrMagnitude > 0.0001f)
            {
                trackedVelocityDir = Vector3.Slerp(trackedVelocityDir, delta.normalized, 1f - Mathf.Exp(-16f * dt));
            }
        }

        Vector3 desiredPos;
        Quaternion desiredRot;
        float desiredFov;
        float activeWeight;

        // Horizontal flight forward axis for stable camera framing around the arrow
        Vector3 flightFwdXZ = new Vector3(trackedVelocityDir.x, 0f, trackedVelocityDir.z);
        if (flightFwdXZ.sqrMagnitude < 0.001f) flightFwdXZ = Vector3.right;
        flightFwdXZ.Normalize();

        // End-of-flight impact reveal pose (pulling back from the landed hero arrow)
        Vector3 impactDir = (Quaternion.Euler(0f, 35f, 0f) * baseApproachDir).normalized;
        Vector3 impactRevealPos = focusCenter + impactDir * arrowImpactPullbackDist + Vector3.up * 6.5f;
        Quaternion impactRevealRot = Quaternion.LookRotation(((focusCenter + Vector3.up * 1.2f) - impactRevealPos).normalized, Vector3.up);

        if (elapsed < fireballFlightTime)
        {
            // BEAT 1: Pure Arrow-Cam! Swoops tightly onto the single arrow and sweeps from fletching to side-profile
            float t = Mathf.Clamp01(elapsed / fireballFlightTime);
            float lockEase = SmootherStep(Mathf.Clamp01(t * 2.2f)); // Locks on smoothly in first ~45% of flight
            activeWeight = lockEase;

            float sweepAngle = Mathf.Lerp(arrowCamStartAngle, arrowCamEndAngle, EaseInOutSine(t));
            // Position relative to the arrow's flight axis (sweeping around its side)
            Vector3 sideOffsetDir = Quaternion.Euler(0f, sweepAngle, 0f) * (-flightFwdXZ);
            Vector3 arrowCamPos = lastTrackedPos
                + sideOffsetDir * arrowCamDistance
                + Vector3.up * arrowCamHeightOffset;

            desiredPos = Vector3.Lerp(savedPosition, arrowCamPos, lockEase);

            // Look tightly at the arrow itself (slightly ahead of its shaft toward the tip)
            Vector3 aimPoint = lastTrackedPos + trackedVelocityDir * 0.45f;
            Quaternion arrowLookRot = Quaternion.LookRotation((aimPoint - desiredPos).normalized, Vector3.up);
            arrowLookRot *= Quaternion.Euler(0f, 0f, 3.2f * Mathf.Sin(t * Mathf.PI)); // Subtle cinema bank

            desiredRot = Quaternion.Slerp(savedRotation, arrowLookRot, lockEase);
            desiredFov = Mathf.Lerp(savedFOV, arrowCamFov, lockEase);
        }
        else if (elapsed < fireballFlightTime + arrowAftermathDuration)
        {
            // BEAT 2: Arrow lands! Camera pulls back smoothly to reveal the barrage raining down around it
            float t = Mathf.Clamp01((elapsed - fireballFlightTime) / arrowAftermathDuration);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.8f);
            activeWeight = 1f;

            Vector3 lastArrowCamPos = lastTrackedPos
                + (Quaternion.Euler(0f, arrowCamEndAngle, 0f) * (-flightFwdXZ)) * arrowCamDistance
                + Vector3.up * arrowCamHeightOffset;

            desiredPos = Vector3.Lerp(lastArrowCamPos, impactRevealPos, easeOut);
            Quaternion startRot = Quaternion.LookRotation(((focusCenter + Vector3.up * 0.6f) - lastArrowCamPos).normalized, Vector3.up);
            desiredRot = Quaternion.Slerp(startRot, impactRevealRot, easeOut);
            desiredFov = Mathf.Lerp(arrowCamFov, 44f, easeOut);
        }
        else
        {
            // BEAT 3: Smooth curved return glide to gameplay camera
            float exitT = SmootherStep((elapsed - (fireballFlightTime + arrowAftermathDuration)) / arrowExitDuration);
            activeWeight = 1f - exitT;

            Vector3 sideDir = Vector3.Cross(Vector3.up, impactDir).normalized;
            Vector3 midControl = Vector3.Lerp(impactRevealPos, savedPosition, 0.5f) + sideDir * 6f + Vector3.up * 5f;

            desiredPos = QuadraticBezier(impactRevealPos, midControl, savedPosition, exitT);
            desiredRot = Quaternion.Slerp(impactRevealRot, savedRotation, exitT);
            desiredFov = Mathf.Lerp(44f, savedFOV, exitT);
        }

        if (avoidObstacles && activeWeight > 0.01f)
        {
            desiredPos = ResolveCameraObstacles(desiredPos, Mathf.Clamp01(elapsed / totalSequenceDuration), dt);
        }

        // Tighter tracking during arrow flight so the arrow stays crisp in frame
        float posSmooth = (elapsed < fireballFlightTime) ? Mathf.Lerp(0.08f, 0.035f, activeWeight) : Mathf.Lerp(0.035f, 0.09f, activeWeight);
        currentDampedPos = Vector3.SmoothDamp(currentDampedPos, desiredPos, ref posVelocity, posSmooth, Mathf.Infinity, dt);
        currentDampedRot = Quaternion.Slerp(currentDampedRot, desiredRot, 1f - Mathf.Exp(-18f * dt));
        currentFov = Mathf.Lerp(currentFov, desiredFov, 1f - Mathf.Exp(-12f * dt));

        Vector3 shake = swipeController != null ? swipeController.CurrentShakeOffset * 1.1f : Vector3.zero;
        transform.position = currentDampedPos + shake;
        transform.rotation = currentDampedRot;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = currentFov;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = Mathf.Lerp(savedTimeScale, slowMotionTimeScale, activeWeight);
        }
    }

    private void UpdateReinforcementCartCinematic(float dt)
    {
        if (trackedProjectile != null)
        {
            prevTrackedPos = lastTrackedPos;
            lastTrackedPos = trackedProjectile.position;
            Vector3 delta = lastTrackedPos - prevTrackedPos;
            if (delta.sqrMagnitude > 0.0001f)
            {
                Vector3 flatDir = new Vector3(delta.x, 0f, delta.z);
                if (flatDir.sqrMagnitude > 0.0001f)
                {
                    trackedVelocityDir = Vector3.Slerp(trackedVelocityDir, flatDir.normalized, 1f - Mathf.Exp(-10f * dt));
                }
            }
        }

        // Fallback arrival trigger if cart reached destination without explicit notify
        if (!cartArrived && Vector3.Distance(lastTrackedPos, focusCenter) < 1.5f)
        {
            cartArrived = true;
            cartArrivalTime = elapsed;
            totalSequenceDuration = cartArrivalTime + cartDismountHoldDuration + cartExitDuration;
        }

        Vector3 cartFwd = trackedVelocityDir.sqrMagnitude > 0.01f ? trackedVelocityDir.normalized : Vector3.right;
        // Pick the side closest to the gameplay camera so the camera doesn't cross over the cart
        Vector3 rightSide = Vector3.Cross(Vector3.up, cartFwd).normalized;
        if (Vector3.Dot(rightSide, baseApproachDir) < 0f) rightSide = -rightSide;

        Vector3 desiredPos;
        Quaternion desiredRot;
        float desiredFov;
        float activeWeight;

        float dismountStart = cartArrived ? cartArrivalTime : totalSequenceDuration;
        float exitStart = dismountStart + cartDismountHoldDuration;

        // Pose at the end of the dismount sweep for smooth exit
        Vector3 endSweepDir = (Quaternion.Euler(0f, 82f, 0f) * (-cartFwd * 0.65f + rightSide * 0.75f)).normalized;
        Vector3 arrivalCamPos = lastTrackedPos + endSweepDir * cartDismountDistance + Vector3.up * cartDismountHeight;
        Quaternion arrivalLookRot = Quaternion.LookRotation(((lastTrackedPos + Vector3.up * 1.1f) - arrivalCamPos).normalized, Vector3.up);

        if (!cartArrived)
        {
            // BEAT 1: Low Rear-Wheel Chase — tucked right behind the spinning rear wheel looking past the wagon & horse
            float entryT = SmootherStep(Mathf.Clamp01(elapsed / 0.6f));
            activeWeight = entryT;

            float creepT = EaseInOutSine(Mathf.Clamp01(elapsed / 2.2f));
            float backDist = Mathf.Lerp(cartRearDistance, cartRearDistance - cartCreepForward, creepT);

            Vector3 trackPos = lastTrackedPos
                - cartFwd * backDist
                + rightSide * cartSideDistance
                + Vector3.up * cartTrackHeight;

            // Subtle wheel-rumble & gallop cadence
            trackPos.y += Mathf.Sin(elapsed * 16f) * 0.045f * entryT;

            desiredPos = Vector3.Lerp(savedPosition, trackPos, entryT);

            // Look past the rear wheel & seated soldiers toward the galloping horse ahead
            Vector3 lookTarget = lastTrackedPos + cartFwd * 2.4f + Vector3.up * 1.15f;
            Quaternion trackRot = Quaternion.LookRotation((lookTarget - desiredPos).normalized, Vector3.up);
            trackRot *= Quaternion.Euler(0f, 0f, -3.0f * entryT);

            desiredRot = Quaternion.Slerp(savedRotation, trackRot, entryT);
            desiredFov = Mathf.Lerp(savedFOV, cartTrackFov, entryT);
        }
        else if (elapsed < exitStart)
        {
            // BEAT 2: Cart brakes! Camera sweeps forward around the side wheel to frame the front-quarter dismount
            float t = Mathf.Clamp01((elapsed - dismountStart) / cartDismountHoldDuration);
            float ease = EaseInOutSine(t);
            activeWeight = 1f;

            Vector3 startRel = (-cartFwd * (cartRearDistance - cartCreepForward)) + (rightSide * cartSideDistance);
            Vector3 rotatedRel = Quaternion.Slerp(
                Quaternion.LookRotation(startRel.normalized, Vector3.up),
                Quaternion.LookRotation(endSweepDir, Vector3.up),
                ease) * Vector3.forward;

            float dist = Mathf.Lerp(startRel.magnitude, cartDismountDistance, ease);
            float height = Mathf.Lerp(cartTrackHeight, cartDismountHeight, ease);

            desiredPos = lastTrackedPos + rotatedRel * dist + Vector3.up * height;

            Vector3 lookTarget = lastTrackedPos + Vector3.up * 1.1f;
            desiredRot = Quaternion.LookRotation((lookTarget - desiredPos).normalized, Vector3.up);
            desiredFov = Mathf.Lerp(cartTrackFov, 36f, ease);
        }
        else
        {
            // BEAT 3: Smooth curved crane-out back to gameplay camera
            float exitT = SmootherStep((elapsed - exitStart) / cartExitDuration);
            activeWeight = 1f - exitT;

            Vector3 midControl = Vector3.Lerp(arrivalCamPos, savedPosition, 0.5f) + rightSide * 6f + Vector3.up * 6f;
            desiredPos = QuadraticBezier(arrivalCamPos, midControl, savedPosition, exitT);
            desiredRot = Quaternion.Slerp(arrivalLookRot, savedRotation, exitT);
            desiredFov = Mathf.Lerp(36f, savedFOV, exitT);
        }

        if (avoidObstacles && activeWeight > 0.01f)
        {
            desiredPos = ResolveCameraObstacles(desiredPos, Mathf.Clamp01(elapsed / totalSequenceDuration), dt);
        }

        float smoothTime = !cartArrived ? 0.06f : Mathf.Lerp(0.035f, 0.1f, activeWeight);
        currentDampedPos = Vector3.SmoothDamp(currentDampedPos, desiredPos, ref posVelocity, smoothTime, Mathf.Infinity, dt);
        currentDampedRot = Quaternion.Slerp(currentDampedRot, desiredRot, 1f - Mathf.Exp(-12f * dt));
        currentFov = Mathf.Lerp(currentFov, desiredFov, 1f - Mathf.Exp(-10f * dt));

        Vector3 shake = swipeController != null ? swipeController.CurrentShakeOffset : Vector3.zero;
        transform.position = currentDampedPos + shake;
        transform.rotation = currentDampedRot;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = currentFov;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = Mathf.Lerp(savedTimeScale, slowMotionTimeScale, activeWeight);
        }
    }

    private void UpdateWoodLogCinematic(float dt)
    {
        if (trackedProjectile != null)
        {
            lastTrackedPos = trackedProjectile.position;
        }

        if (!cartArrived && elapsed >= fireballFlightTime)
        {
            cartArrived = true;
            cartArrivalTime = elapsed;
        }

        Vector3 logFwd = trackedVelocityDir.sqrMagnitude > 0.01f ? trackedVelocityDir.normalized : Vector3.right;
        // Define camera-relative left/right so left-to-right always matches screen left -> right
        Vector3 logRight = Vector3.Cross(Vector3.up, logFwd).normalized;

        Vector3 desiredPos;
        Quaternion desiredRot;
        float desiredFov;
        float activeWeight;

        float aftermathStart = cartArrived ? cartArrivalTime : fireballFlightTime;
        float exitStart = aftermathStart + logAftermathDuration;

        // Final third-person pose on the right side
        Vector3 endChasePos = lastTrackedPos - logFwd * logFollowBackDistance + logRight * logRightEndOffset + Vector3.up * logTrackHeight;
        Quaternion endChaseRot = Quaternion.LookRotation(((lastTrackedPos + logFwd * 5.0f + Vector3.up * 0.8f) - endChasePos).normalized, Vector3.up);

        if (!cartArrived)
        {
            // BEAT 1: Third-Person Behind-the-Log Chase sweeping smoothly from Left to Right
            float rollT = Mathf.Clamp01(elapsed / fireballFlightTime);
            float entryT = SmootherStep(Mathf.Clamp01(elapsed / 0.45f));
            activeWeight = entryT;

            float lateralOffset = Mathf.Lerp(logLeftStartOffset, logRightEndOffset, EaseInOutSine(rollT));
            Vector3 trackPos = lastTrackedPos
                - logFwd * logFollowBackDistance
                + logRight * lateralOffset
                + Vector3.up * logTrackHeight;

            desiredPos = Vector3.Lerp(savedPosition, trackPos, entryT);

            // Look over/ahead of the log down its crushing path
            Vector3 lookTarget = lastTrackedPos + logFwd * 5.0f + Vector3.up * 0.8f;
            Quaternion thirdPersonRot = Quaternion.LookRotation((lookTarget - desiredPos).normalized, Vector3.up);

            desiredRot = Quaternion.Slerp(savedRotation, thirdPersonRot, entryT);
            desiredFov = Mathf.Lerp(savedFOV, logCloseFov, entryT);
        }
        else if (elapsed < exitStart)
        {
            // BEAT 2: Brief settle at the end of the roll
            activeWeight = 1f;
            desiredPos = endChasePos;
            desiredRot = endChaseRot;
            desiredFov = logCloseFov;
        }
        else
        {
            // BEAT 3: Smooth curved return back to gameplay camera
            float exitT = SmootherStep((elapsed - exitStart) / logExitDuration);
            activeWeight = 1f - exitT;

            Vector3 midControl = Vector3.Lerp(endChasePos, savedPosition, 0.5f) + logRight * 5f + Vector3.up * 5f;
            desiredPos = QuadraticBezier(endChasePos, midControl, savedPosition, exitT);
            desiredRot = Quaternion.Slerp(endChaseRot, savedRotation, exitT);
            desiredFov = Mathf.Lerp(logCloseFov, savedFOV, exitT);
        }

        if (avoidObstacles && activeWeight > 0.01f)
        {
            desiredPos = ResolveCameraObstacles(desiredPos, Mathf.Clamp01(elapsed / totalSequenceDuration), dt);
        }

        float smoothTime = !cartArrived ? 0.055f : Mathf.Lerp(0.035f, 0.09f, activeWeight);
        currentDampedPos = Vector3.SmoothDamp(currentDampedPos, desiredPos, ref posVelocity, smoothTime, Mathf.Infinity, dt);
        currentDampedRot = Quaternion.Slerp(currentDampedRot, desiredRot, 1f - Mathf.Exp(-14f * dt));
        currentFov = Mathf.Lerp(currentFov, desiredFov, 1f - Mathf.Exp(-12f * dt));

        transform.position = currentDampedPos;
        transform.rotation = currentDampedRot;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = currentFov;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = Mathf.Lerp(savedTimeScale, slowMotionTimeScale, activeWeight);
        }
    }

    private void FinishCinematic()
    {
        isPlaying = false;
        transform.position = savedPosition;
        transform.rotation = savedRotation;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = savedFOV;
        }

        if (modifiedTimeScale)
        {
            Time.timeScale = savedTimeScale;
            modifiedTimeScale = false;
        }

        if (swipeController != null)
        {
            swipeController.IsCinematicOverride = false;
        }
        CameraSwipeController.SuppressSwipe = false;
    }

    private void RestoreStateImmediate()
    {
        if (!isPlaying) return;
        FinishCinematic();
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return -0.5f * (Mathf.Cos(Mathf.PI * t) - 1f);
    }

    [ContextMenu("Test Arrow Shower Cinematic")]
    private void TestArrowShowerCinematic()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CinematicCameraController] Enter Play Mode to test the Arrow Shower cinematic.");
            return;
        }

        Vector3 testCenter = transform.position + transform.forward * 45f;
        testCenter.y = 0f;
        PlayCinematic(CinematicStyle.ArrowShower, testCenter, 2.4f, GameModeManager.LocalTeamId);
    }

    [ContextMenu("Test Fire Arrows Cinematic")]
    private void TestFireArrowsCinematic()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CinematicCameraController] Enter Play Mode to test the Fire Arrows cinematic.");
            return;
        }

        Vector3 testCenter = transform.position + transform.forward * 45f;
        testCenter.y = 0f;
        PlayCinematic(CinematicStyle.FireArrows, testCenter, 2.4f, GameModeManager.LocalTeamId);
    }

    [ContextMenu("Test Big Fireball Cinematic")]
    private void TestBigFireballCinematic()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CinematicCameraController] Enter Play Mode to test the Big Fireball cinematic.");
            return;
        }

        Vector3 testCenter = transform.position + transform.forward * 45f;
        testCenter.y = 0f;
        PlayBigFireballCinematic(null, testCenter, 1.0f, GameModeManager.LocalTeamId);
    }
}
