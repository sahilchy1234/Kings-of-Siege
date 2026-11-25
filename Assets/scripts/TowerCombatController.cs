using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Comprehensive Defensive Tower Controller for towerour and towerenemy.
/// Manages stationed archer aiming, ballistic parabolic arrow launches,
/// network synchronization via Photon PUN2, target acquisition,
/// world-space health bar integration, and rich DOTween impact & collapse juice.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PeasantObjectiveTarget))]
public class TowerCombatController : MonoBehaviour
{
    private static readonly List<TowerCombatController> allTowers = new List<TowerCombatController>();

    [Header("Network & Identity")]
    [Tooltip("Unique index for network replication (0, 1 for our towers; 2, 3 for enemy towers).")]
    [SerializeField] private int towerIndex = 0;
    [SerializeField] private int teamId = 0; // 0 = Player/Our, 1 = Enemy/Opponent
    [SerializeField] private string towerDisplayName = "Defense Watchtower";

    [Header("Combat Stats")]
    [SerializeField, Min(100f)] private float maxHealth = 650f;
    [SerializeField, Min(5f)] private float attackRange = 26f;
    [SerializeField, Min(1f)] private float damagePerShot = 28f;
    [SerializeField, Min(0.2f)] private float attackCooldown = 1.35f;
    [SerializeField, Min(0.05f)] private float bowReleaseDelay = 0.22f;

    [Header("Visual & Spatial Setup")]
    [Tooltip("Height where the archer stands atop the tower.")]
    [SerializeField] private float platformHeightOffset = 7.05f;
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private GameObject healthBarPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip destructionSound;

    [Header("VFX Prefabs")]
    [Tooltip("Blood VFX prefab spawned when arrow strikes players/troops.")]
    [SerializeField] private GameObject bloodHitVfxPrefab;
    [Tooltip("Machine Hit VFX prefab spawned when arrow strikes siege machines (wood/sparks effect).")]
    [SerializeField] private GameObject machineHitVfxPrefab;
    [Tooltip("Dust VFX prefab spawned when arrow strikes buildings/obstacles/ground.")]
    [SerializeField] private GameObject dustHitVfxPrefab;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private GameObject destructionVfxPrefab;
    [SerializeField] private GameObject shootMuzzleVfxPrefab;

    [Header("Capture & Conquest")]
    [Tooltip("If true, when enemies destroy this tower, they capture and occupy it with a cinematic animation instead of permanently destroying it.")]
    [SerializeField] private bool allowCaptureOnDestroy = true;
    [SerializeField] private Material team0ArcherMaterial;
    [SerializeField] private Material team1ArcherMaterial;
    [SerializeField] private AudioClip captureSound;
    [SerializeField] private GameObject captureVfxPrefab;

    [Header("Autonomous Defense (Preset Towers)")]
    [Tooltip("If true, this tower attacks autonomously by itself without needing a stationed archer (used on base defense towers: towerour and towerenemy).")]
    [SerializeField] private bool attacksAutonomously = false;

    [Header("Archer Animations (Wall Towers Only)")]
    [Tooltip("If true, archers atop this tower will play rich combat animations (idle, bow draw, attack, hit flinch, and death). Enabled exclusively on wall towers.")]
    [SerializeField] private bool enableArcherAnimations = false;
    [SerializeField] private RuntimeAnimatorController archerAnimatorController;

    [Header("Arrow Launch Point")]
    [Tooltip("Explicit launch point transform. You can move this child GameObject in the Scene view or edit its position in Inspector to modify exactly where arrows launch from!")]
    [SerializeField] private Transform arrowLaunchPoint;
    [Tooltip("Custom local offset applied when calculating launch point if arrowLaunchPoint is not assigned.")]
    [SerializeField] private Vector3 launchOffset = new Vector3(0f, 8.5f, 0f);

    [Header("References")]
    [SerializeField] private PeasantObjectiveTarget objectiveTarget;
    [SerializeField] private TowerHealthBar healthBar;
    [SerializeField] private Transform stationedArcherTransform;
    [SerializeField] private Animator archerAnimator;
    [SerializeField] private TowerWorldBanner worldBanner;

    private AudioSource audioSource;
    private CapsuleCollider towerCollider;
    private NavMeshObstacle navObstacle;
    private Transform currentTarget;
    private float nextFireTime;
    private bool isDestroyed = false;
    private Tween recoilTween;
    private Tween shakeTween;
    private Vector3 initialScale = Vector3.one;
    private bool isOccupationInProgress = false;
    private bool isCapturing = false;
    private PeasantUnitController lastAttacker;

    public int TowerIndex => towerIndex;
    public int TeamId => teamId;
    public float MaxHealth => maxHealth;
    public float AttackRange => attackRange;
    public bool IsDestroyed => isDestroyed;
    public bool AttacksAutonomously => attacksAutonomously;
    public bool CanAttack => !isDestroyed && (attacksAutonomously || IsOccupied);
    public float PlatformHeightOffset => platformHeightOffset;
    public AudioClip CaptureSound => captureSound;
    public GameObject CaptureVfxPrefab => captureVfxPrefab;
    public TowerWorldBanner WorldBanner => worldBanner;

    public void SetWorldBanner(TowerWorldBanner banner)
    {
        worldBanner = banner;
    }

    public Transform ArrowLaunchPoint
    {
        get => arrowLaunchPoint;
        set => arrowLaunchPoint = value;
    }

    public Vector3 LaunchOffset
    {
        get => launchOffset;
        set => launchOffset = value;
    }

    /// <summary>
    /// Returns the exact world position where arrows should launch from.
    /// Prioritizes the explicit arrowLaunchPoint transform if assigned,
    /// falls back to stationed archer position, or calculated height offset.
    /// </summary>
    public Vector3 GetArrowLaunchPosition()
    {
        if (arrowLaunchPoint != null)
        {
            return arrowLaunchPoint.position;
        }

        Transform autoFound = transform.Find("ArrowLaunchPoint");
        if (autoFound != null)
        {
            arrowLaunchPoint = autoFound;
            return arrowLaunchPoint.position;
        }

        if (stationedArcherTransform != null)
        {
            return stationedArcherTransform.position + Vector3.up * 1.1f + stationedArcherTransform.forward * 0.45f;
        }

        return transform.position + Vector3.up * (platformHeightOffset + 1f);
    }

    public bool IsOccupied
    {
        get
        {
            if (isDestroyed || attacksAutonomously) return false;
            if (stationedArcherTransform == null)
            {
                Transform archer = transform.Find("StationedArcher");
                if (archer != null)
                {
                    archer.gameObject.SetActive(true);
                    stationedArcherTransform = archer;
                    archerAnimator = stationedArcherTransform.GetComponentInChildren<Animator>();
                }
            }
            return stationedArcherTransform != null && stationedArcherTransform.gameObject.activeSelf;
        }
    }

    public static void ReceiveNetworkTowerFire(int index, Vector3 targetPos, int targetNetId)
    {
        for (int i = 0; i < allTowers.Count; i++)
        {
            if (allTowers[i] != null && allTowers[i].towerIndex == index)
            {
                allTowers[i].ExecuteProxyFire(targetPos, targetNetId);
                return;
            }
        }
    }

    public static void ReceiveNetworkTowerCaptured(int index, int newTeam)
    {
        for (int i = 0; i < allTowers.Count; i++)
        {
            if (allTowers[i] != null && allTowers[i].towerIndex == index)
            {
                allTowers[i].ExecuteCapture(newTeam, null, isNetworkProxy: true);
                return;
            }
        }
    }

    private void Awake()
    {
        if (!allTowers.Contains(this)) allTowers.Add(this);

        initialScale = transform.localScale;
        objectiveTarget = GetComponent<PeasantObjectiveTarget>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.65f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 5f;
        audioSource.maxDistance = 60f;

        LoadDefaultAssetsIfNull();
        SetupColliderAndObstacle();
        SetupStationedArcher();

        if (arrowLaunchPoint == null)
        {
            Transform foundPoint = transform.Find("ArrowLaunchPoint");
            if (foundPoint != null) arrowLaunchPoint = foundPoint;
        }
    }

    private void Start()
    {
        SetupObjectiveTarget();
        SetupStationedArcher();
        SetupHealthBar();

        if (objectiveTarget != null)
        {
            objectiveTarget.OnDamaged += HandleDamageTaken;
            objectiveTarget.OnHealthChanged += HandleHealthChanged;
            objectiveTarget.OnDestroyedEvent += HandleDestroyed;
        }
    }

    private void OnDestroy()
    {
        allTowers.Remove(this);

        if (objectiveTarget != null)
        {
            objectiveTarget.OnDamaged -= HandleDamageTaken;
            objectiveTarget.OnHealthChanged -= HandleHealthChanged;
            objectiveTarget.OnDestroyedEvent -= HandleDestroyed;
        }

        recoilTween?.Kill();
        shakeTween?.Kill();
    }

    private void LoadDefaultAssetsIfNull()
    {
        if (shootSound == null)
        {
            shootSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowrelease.mp3");
        }
        if (hitSound == null)
        {
            hitSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowhit.mp3");
        }
        if (destructionSound == null)
        {
            destructionSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/dragon-studio-hammer-smash-effect-382731.mp3");
        }
        if (bloodHitVfxPrefab == null)
        {
            bloodHitVfxPrefab = Resources.Load<GameObject>("Effects/ArrowHit_Blood");
            if (bloodHitVfxPrefab == null)
                bloodHitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
        }
        if (machineHitVfxPrefab == null)
        {
            machineHitVfxPrefab = Resources.Load<GameObject>("Effects/ArrowHit_Wood");
            if (machineHitVfxPrefab == null)
                machineHitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Wood_01.prefab");
            if (machineHitVfxPrefab == null)
                machineHitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Metal_01.prefab");
            if (machineHitVfxPrefab == null)
                machineHitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Sparks_01.prefab");
        }
        if (dustHitVfxPrefab == null)
        {
            dustHitVfxPrefab = Resources.Load<GameObject>("Effects/ArrowHit_Dust");
            if (dustHitVfxPrefab == null)
                dustHitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Dust_Small_01.prefab");
        }
        if (hitVfxPrefab == null)
        {
            hitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab");
            if (hitVfxPrefab == null)
                hitVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab");
        }
        if (destructionVfxPrefab == null)
        {
            destructionVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion.prefab");
            if (destructionVfxPrefab == null)
                destructionVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab");
        }
        if (archerPrefab == null)
        {
            archerPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/prefabs/characters/TT_Archer.prefab");
            if (archerPrefab == null)
                archerPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs/TT_Archer.prefab");
            if (archerPrefab == null)
                archerPrefab = Resources.Load<GameObject>("Powerups/TT_Archer");
        }
        if (arrowPrefab == null)
        {
            arrowPrefab = Resources.Load<GameObject>("Powerups/Arrow_B");
        }
        if (team0ArcherMaterial == null)
        {
            team0ArcherMaterial = RuntimeAssets.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_blue.mat");
        }
        if (team1ArcherMaterial == null)
        {
            team1ArcherMaterial = RuntimeAssets.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_red.mat");
        }
        if (archerAnimatorController == null)
        {
            archerAnimatorController = RuntimeAssets.LoadAssetAtPath<RuntimeAnimatorController>("Assets/AnimationController/archer.controller");
        }
        if (captureVfxPrefab == null)
        {
            captureVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_LevelUp_01.prefab");
        }
        // No fireworks on tower capture (removed on request). If the scene/setup tool assigned the
        // fireworks prefab as the capture effect, use the level-up burst instead.
        if (captureVfxPrefab != null && captureVfxPrefab.name.Contains("Firework"))
        {
            captureVfxPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_LevelUp_01.prefab");
        }
        if (captureSound == null)
        {
            captureSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/nhlk-revolution-108386.mp3");
            if (captureSound == null)
                captureSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/audioatlant-total-war-epic-action-cinematic-trailer-version-2-513669.mp3");
        }
        if (shootSound == null) shootSound = Resources.Load<AudioClip>("Sounds/arrowrelease");
        if (hitSound == null) hitSound = Resources.Load<AudioClip>("Sounds/arrowhit");
        if (arrowPrefab == null) arrowPrefab = Resources.Load<GameObject>("Powerups/Arrow_B");
    }

    public void PlayArcherAnimation(string animType)
    {
        if (!enableArcherAnimations) return;

        // Auto-recover archerAnimator if reference was lost or newly stationed
        if (archerAnimator == null && stationedArcherTransform != null)
        {
            archerAnimator = stationedArcherTransform.GetComponentInChildren<Animator>();
        }

        if (archerAnimator == null) return;

        // Ensure runtime animator controller is attached
        if (archerAnimator.runtimeAnimatorController == null && archerAnimatorController != null)
        {
            archerAnimator.runtimeAnimatorController = archerAnimatorController;
        }

        if (archerAnimator.runtimeAnimatorController == null) return;
        archerAnimator.enabled = true;
        archerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        switch (animType.ToLower())
        {
            case "idle":
                // Guarantee the running flag and triggers are fully cleared so animator stays in idle
                archerAnimator.SetBool("running", false);
                archerAnimator.SetFloat("speed", 0f);
                archerAnimator.ResetTrigger("attack");
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.CrossFadeInFixedTime("infantry_01_idle", 0.1f, 0, 0f);
                break;

            case "attack":
                archerAnimator.SetBool("running", false);
                archerAnimator.SetFloat("speed", 0f);
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.ResetTrigger("attack");
                archerAnimator.SetTrigger("attack");
                // Immediately restart the full attack bow draw from frame 0
                archerAnimator.Play("attack", 0, 0f);
                break;

            case "run":
                archerAnimator.SetBool("running", true);
                archerAnimator.SetFloat("speed", 1f);
                archerAnimator.CrossFadeInFixedTime("run", 0.08f, 0, 0f);
                break;

            case "hurt":
            case "damage":
                archerAnimator.SetBool("running", false);
                archerAnimator.ResetTrigger("attack");
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.SetTrigger("hurt");
                archerAnimator.Play("hurt", 0, 0f);
                break;

            case "death":
                archerAnimator.SetBool("running", false);
                archerAnimator.SetFloat("speed", 0f);
                archerAnimator.ResetTrigger("attack");
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.SetTrigger("death");
                archerAnimator.Play("death", 0, 0f);
                break;
        }
    }

    public void ConfigureTower(int index, int team, string displayName, float hp)
    {
        this.towerIndex = index;
        this.teamId = team;
        this.towerDisplayName = displayName;
        this.maxHealth = hp;

        SetupObjectiveTarget();
        if (healthBar != null)
        {
            healthBar.Setup(transform, teamId, maxHealth);
        }
    }

    /// <summary>
    /// Mounts and stations an archer unit atop this tower platform in a cinematic acrobatic leap animation,
    /// making the tower actively occupied as their base. Destroyed towers can NEVER be occupied.
    /// </summary>
    public bool TryOccupyTower(PeasantUnitController archerUnit)
    {
        // 1. Strict guard: autonomous base defense towers attack by themselves and cannot be occupied
        if (attacksAutonomously) return false;

        // If destroyed, in-progress, or already occupied, NEVER allow occupation!
        if (isDestroyed)
        {
            Debug.LogWarning($"[TowerCombatController] Cannot occupy '{towerDisplayName}' because it is fully destroyed!");
            return false;
        }

        if (isOccupationInProgress || IsOccupied) return false;
        if (archerUnit == null || archerUnit.IsDead) return false;

        // STRICT GUARD: ONLY genuine foot archers can occupy/station on the tower!
        // Ballistas (even with UnitType Archer), siege engines, and melee units can NEVER occupy a tower!
        if (archerUnit.IsBallista || !archerUnit.IsEligibleTowerArcher)
        {
            return false;
        }

        // If neutral (teamId < 0), ANY living archer (Team 0 or Team 1) can occupy and claim the tower!
        // If already owned (teamId >= 0), only friendly archers of that team can occupy.
        if (teamId >= 0 && archerUnit.TeamId != teamId) return false;

        StartCoroutine(OccupySequenceRoutine(archerUnit));
        return true;
    }

    private IEnumerator OccupySequenceRoutine(PeasantUnitController archerUnit)
    {
        if (isDestroyed || archerUnit == null) yield break;
        isOccupationInProgress = true;
        int unitTeam = archerUnit.TeamId;

        // 1. Disengage unit controls so it stops roaming, chasing, or attacking
        archerUnit.SetRunningAnimation(false);
        archerUnit.enabled = false;
        var agent = archerUnit.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        var col = archerUnit.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var anim = archerUnit.GetComponentInChildren<Animator>();

        // 2. Turn archer to face the tower base
        Vector3 towerBaseDir = transform.position - archerUnit.transform.position;
        towerBaseDir.y = 0f;
        if (towerBaseDir.sqrMagnitude > 0.01f)
        {
            archerUnit.transform.rotation = Quaternion.LookRotation(towerBaseDir.normalized, Vector3.up);
        }

        // 3. Anticipation crouch / windup before the leap
        if (anim != null && enableArcherAnimations) anim.CrossFade("run", 0.08f);
        archerUnit.transform.DOPunchScale(new Vector3(0.12f, -0.2f, 0.12f), 0.18f, 6, 0.4f);
        yield return new WaitForSeconds(0.18f);

        // Guard check if tower was destroyed while winding up
        if (isDestroyed || archerUnit == null)
        {
            isOccupationInProgress = false;
            yield break;
        }

        // 4. Acrobatic Parabolic Leap Ascension
        Vector3 platformPos = transform.position + Vector3.up * platformHeightOffset;
        Vector3 battlefieldTarget = (unitTeam == 0) ? new Vector3(131.31f, platformPos.y, -48.65f) : new Vector3(98.45f, platformPos.y, -39.92f);
        Vector3 battlefieldDir = battlefieldTarget - platformPos;
        battlefieldDir.y = 0f;
        Quaternion targetRotation = (battlefieldDir.sqrMagnitude > 0.01f)
            ? Quaternion.LookRotation(battlefieldDir.normalized, Vector3.up)
            : transform.rotation;

        float leapDuration = 0.65f;
        float leapHeight = Mathf.Clamp(platformHeightOffset * 0.35f, 2.0f, 4.0f);

        // Audio whoosh / jump launch
        if (shootSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(1.15f, 1.3f);
            KoSAudio.PlayOneShot(audioSource, shootSound, 0.6f, "tower_shoot");
        }

        // Mid-air leap animation
        if (anim != null && enableArcherAnimations) anim.CrossFade("attack", 0.1f);

        // DOTween DOJump parabolic curve to the top platform
        archerUnit.transform.DOJump(platformPos, leapHeight, 1, leapDuration).SetEase(Ease.OutQuad);
        archerUnit.transform.DORotateQuaternion(targetRotation, leapDuration * 0.9f).SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(leapDuration);

        // Guard check if tower was destroyed mid-air
        if (isDestroyed || archerUnit == null)
        {
            isOccupationInProgress = false;
            yield break;
        }

        // 5. Landing Touchdown Bounce & Polish
        if (hitVfxPrefab != null)
        {
            Instantiate(hitVfxPrefab, platformPos, Quaternion.identity);
        }

        // Landing squash and stretch
        archerUnit.transform.DOPunchScale(new Vector3(0.2f, -0.25f, 0.2f), 0.25f, 7, 0.5f);

        // Landing impact audio
        if (hitSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(1.0f, 1.2f);
            KoSAudio.PlayOneShot(audioSource, hitSound, 0.5f, "tower_land");
        }

        // Capture fanfare audio
        AudioClip fanfareClip = (captureSound != null) ? captureSound : shootSound;
        if (fanfareClip != null && audioSource != null)
        {
            audioSource.pitch = (unitTeam == 0) ? 1.35f : 0.85f;
            KoSAudio.PlayOneShot(audioSource, fanfareClip, 0.85f, "tower_capture");
        }

        if (captureVfxPrefab != null)
        {
            Instantiate(captureVfxPrefab, platformPos + Vector3.up * 1f, Quaternion.identity);
        }

        // 6. Finalize Stationed Archer Hierarchy & Components
        GameObject archerGo = archerUnit.gameObject;
        Transform archerTr = archerGo.transform;

        archerTr.SetParent(transform, true);
        archerTr.position = platformPos;
        archerTr.rotation = targetRotation;
        archerGo.name = "StationedArcher";

        if (agent != null) Destroy(agent);
        if (col != null) Destroy(col);
        archerUnit.SetRunningAnimation(false);
        Destroy(archerUnit);

        // Standardize world scale
        Vector3 parentLossy = transform.lossyScale;
        float safeX = Mathf.Abs(parentLossy.x) > 0.001f ? parentLossy.x : 1f;
        float safeY = Mathf.Abs(parentLossy.y) > 0.001f ? parentLossy.y : 1f;
        float safeZ = Mathf.Abs(parentLossy.z) > 0.001f ? parentLossy.z : 1f;
        archerTr.localScale = new Vector3(1.05f / safeX, 1.05f / safeY, 1.05f / safeZ);

        stationedArcherTransform = archerTr;
        archerAnimator = archerGo.GetComponentInChildren<Animator>();

        // Purge any leftover mobile unit health bars from stationed archer; tower uses TowerHealthBar
        CleanupStationedArcherHealthBars(archerGo);

        // Ensure archer uniform matches claiming team
        ApplyStationedArcherMaterial(archerGo, unitTeam);

        if (archerAnimator != null)
        {
            archerAnimator.SetBool("running", false);
            archerAnimator.SetFloat("speed", 0f);
            archerAnimator.ResetTrigger("attack");
            archerAnimator.ResetTrigger("hurt");
            archerAnimator.ResetTrigger("death");

            if (enableArcherAnimations)
            {
                if (archerAnimator.runtimeAnimatorController == null && archerAnimatorController != null)
                    archerAnimator.runtimeAnimatorController = archerAnimatorController;
                archerAnimator.enabled = true;
                PlayArcherAnimation("idle");
            }
            else
            {
                archerAnimator.enabled = false;
            }
        }

        // 7. Claim Tower Ownership if previously neutral or different team
        if (this.teamId != unitTeam)
        {
            this.teamId = unitTeam;
            this.towerDisplayName = (unitTeam == 0) ? "Our Wall Tower" : "Enemy Wall Tower";

            if (objectiveTarget != null)
            {
                objectiveTarget.ResetObjectiveForTeam(unitTeam, maxHealth);
            }

            if (healthBar != null)
            {
                healthBar.ResetHealthBar(unitTeam, maxHealth);
            }

            if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer)
            {
                NetworkBattleBridge.Instance.BroadcastTowerCaptured(towerIndex, unitTeam);
            }
        }

        isOccupationInProgress = false;
        Debug.Log($"<color=green>[TowerCombatController] Tower '{towerDisplayName}' successfully CLAIMED and OCCUPIED by Team {unitTeam}!</color>");

        // Trigger rich acquisition celebration, VFX & DOTween UI (NO destruction prefab!)
        TriggerAcquisitionFeedback(unitTeam);
    }

    private void SetupObjectiveTarget()
    {
        if (objectiveTarget == null) objectiveTarget = GetComponent<PeasantObjectiveTarget>();
        if (objectiveTarget != null)
        {
            // Sync values to objective target
            var fields = typeof(PeasantObjectiveTarget).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "teamId") f.SetValue(objectiveTarget, teamId);
                else if (f.Name == "objectiveName") f.SetValue(objectiveTarget, towerDisplayName);
                else if (f.Name == "maxHealth") f.SetValue(objectiveTarget, maxHealth);
                else if (f.Name == "autoColorByTeam") f.SetValue(objectiveTarget, false);
            }
        }
    }

    private void SetupColliderAndObstacle()
    {
        towerCollider = GetComponent<CapsuleCollider>();
        if (towerCollider == null)
        {
            towerCollider = gameObject.AddComponent<CapsuleCollider>();
            towerCollider.direction = 2; // Z-axis aligns with world Y for imported FBX
            towerCollider.radius = 2.0f;
            towerCollider.height = 7.5f;
            towerCollider.center = new Vector3(0f, 0f, 3.5f);
        }

        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null)
        {
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();
            navObstacle.shape = NavMeshObstacleShape.Capsule;
            navObstacle.radius = 2.0f;
            navObstacle.height = 7.5f;
            navObstacle.center = new Vector3(0f, 0f, 3.5f);
            navObstacle.carving = true;
        }
    }

    private void SetupStationedArcher()
    {
        if (attacksAutonomously)
        {
            // Autonomous base defense towers attack by themselves - NO stationed archer!
            Transform existingArcher = transform.Find("StationedArcher");
            if (existingArcher != null)
            {
                Destroy(existingArcher.gameObject);
            }
            stationedArcherTransform = null;
            archerAnimator = null;
            return;
        }

        if (teamId < 0)
        {
            // Neutral towers start completely empty - no archer until occupied!
            Transform existingArcher = transform.Find("StationedArcher");
            if (existingArcher != null)
            {
                Destroy(existingArcher.gameObject);
            }
            stationedArcherTransform = null;
            archerAnimator = null;
            return;
        }

        Transform existing = transform.Find("StationedArcher");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            stationedArcherTransform = existing;
            CleanupStationedArcherHealthBars(existing.gameObject);
            ApplyStationedArcherMaterial(existing.gameObject, teamId);

            archerAnimator = stationedArcherTransform.GetComponentInChildren<Animator>();
            if (archerAnimator != null)
            {
                archerAnimator.SetBool("running", false);
                archerAnimator.SetFloat("speed", 0f);
                archerAnimator.ResetTrigger("attack");
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.ResetTrigger("death");

                if (enableArcherAnimations)
                {
                    if (archerAnimator.runtimeAnimatorController == null && archerAnimatorController != null)
                        archerAnimator.runtimeAnimatorController = archerAnimatorController;
                    archerAnimator.enabled = true;
                    PlayArcherAnimation("idle");
                }
                else
                {
                    archerAnimator.enabled = false;
                }
            }
            return;
        }

        if (archerPrefab != null)
        {
            Vector3 spawnWorldPos = transform.position + Vector3.up * platformHeightOffset;
            GameObject archerInstance = Instantiate(archerPrefab, spawnWorldPos, transform.rotation, transform);
            archerInstance.transform.localScale = new Vector3(0.77f, 0.77f, 0.77f);
            archerInstance.name = "StationedArcher";
            archerInstance.SetActive(true);
            stationedArcherTransform = archerInstance.transform;

            // Remove any leftover mobile peasant components from the stationed archer visual
            PeasantUnitController puc = archerInstance.GetComponent<PeasantUnitController>();
            if (puc != null)
            {
                puc.SetRunningAnimation(false);
                Destroy(puc);
            }
            NavMeshAgent nma = archerInstance.GetComponent<NavMeshAgent>();
            if (nma != null) Destroy(nma);
            Collider c = archerInstance.GetComponent<Collider>();
            if (c != null) Destroy(c);

            // Clean position on top platform
            stationedArcherTransform.position = spawnWorldPos;

            // Normalize scale so archer is standard world size (~1.05m) regardless of tower's lossyScale
            Vector3 parentLossy = transform.lossyScale;
            float safeX = Mathf.Abs(parentLossy.x) > 0.001f ? parentLossy.x : 1f;
            float safeY = Mathf.Abs(parentLossy.y) > 0.001f ? parentLossy.y : 1f;
            float safeZ = Mathf.Abs(parentLossy.z) > 0.001f ? parentLossy.z : 1f;
            stationedArcherTransform.localScale = new Vector3(1.05f / safeX, 1.05f / safeY, 1.05f / safeZ);

            // Strip stray mobile health bars from stationed archer; tower uses TowerHealthBar
            CleanupStationedArcherHealthBars(archerInstance);
            ApplyStationedArcherMaterial(archerInstance, teamId);

            archerAnimator = archerInstance.GetComponentInChildren<Animator>();
            if (archerAnimator != null)
            {
                archerAnimator.SetBool("running", false);
                archerAnimator.SetFloat("speed", 0f);
                archerAnimator.ResetTrigger("attack");
                archerAnimator.ResetTrigger("hurt");
                archerAnimator.ResetTrigger("death");

                if (enableArcherAnimations)
                {
                    if (archerAnimator.runtimeAnimatorController == null && archerAnimatorController != null)
                        archerAnimator.runtimeAnimatorController = archerAnimatorController;
                    archerAnimator.enabled = true;
                    PlayArcherAnimation("idle");
                }
                else
                {
                    archerAnimator.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Purges any stray unit health bars or UI canvases from stationed archers.
    /// The tower itself owns and renders its dedicated TowerHealthBar.
    /// </summary>
    public static void CleanupStationedArcherHealthBars(GameObject archerGo)
    {
        if (archerGo == null) return;

        JuicyUnitHealthBar[] unitBars = archerGo.GetComponentsInChildren<JuicyUnitHealthBar>(true);
        for (int i = 0; i < unitBars.Length; i++)
        {
            if (unitBars[i] != null) Destroy(unitBars[i].gameObject);
        }

        for (int i = archerGo.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = archerGo.transform.GetChild(i);
            if (child.name.Equals("healthbar", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("UICanvas", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith("heathBar", System.StringComparison.OrdinalIgnoreCase))
            {
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// Applies team uniform material to stationed archer to visually match owning team.
    /// </summary>
    public void ApplyStationedArcherMaterial(GameObject archerGo, int archerTeam)
    {
        if (archerGo == null) return;
        Material targetMat = (archerTeam == 0) ? team0ArcherMaterial : (archerTeam == 1 ? team1ArcherMaterial : null);
        if (targetMat != null)
        {
            Renderer[] rends = archerGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                rends[i].sharedMaterial = targetMat;
            }
        }
    }

    private void SetupHealthBar()
    {
        if (healthBar == null) healthBar = GetComponentInChildren<TowerHealthBar>();
        if (healthBar == null)
        {
            GameObject hbGo = new GameObject("TowerHealthBar");
            hbGo.transform.SetParent(transform, false);
            healthBar = hbGo.AddComponent<TowerHealthBar>();
        }

        healthBar.Setup(transform, teamId, maxHealth);
    }

    private void Update()
    {
        if (isDestroyed || !CanAttack) return;

        // In multiplayer, combat targeting and firing decision is authoritative on MasterClient
        bool isMultiplayer = NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer;
        if (isMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // Target tracking & firing loop
        UpdateTargetingAndAim();

        if (currentTarget != null && Time.time >= nextFireTime)
        {
            StartCoroutine(ShootSequenceRoutine(currentTarget));
            nextFireTime = Time.time + attackCooldown;
        }
    }

    private void UpdateTargetingAndAim()
    {
        // Check if current target is still valid
        if (currentTarget != null)
        {
            if (!IsTargetValidAndAlive(currentTarget))
            {
                currentTarget = null;
            }
            else if (Vector3.Distance(transform.position, currentTarget.position) > attackRange + 1.5f)
            {
                currentTarget = null;
            }
        }

        // Acquire new target if none
        if (currentTarget == null)
        {
            currentTarget = AcquireBestTarget();
        }

        // Aim archer towards target
        if (currentTarget != null && stationedArcherTransform != null)
        {
            Vector3 aimDir = currentTarget.position - stationedArcherTransform.position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
                stationedArcherTransform.rotation = Quaternion.Slerp(stationedArcherTransform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }
    }

    private Transform AcquireBestTarget()
    {
        Transform bestTarget = null;
        float closestDistSq = attackRange * attackRange;
        Vector3 myPos = transform.position;

        // 1. Prioritize living enemy units
        if (BattleAIManager.Instance != null)
        {
            var units = BattleAIManager.Instance.GetAllLivingUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead || u.TeamId == teamId) continue;

                float dSq = (u.transform.position - myPos).sqrMagnitude;
                if (dSq <= closestDistSq)
                {
                    closestDistSq = dSq;
                    bestTarget = u.transform;
                }
            }
        }

        if (bestTarget != null) return bestTarget;

        // 2. Fallback: attack living enemy objectives / towers in range
        if (BattleAIManager.Instance != null)
        {
            var objectives = BattleAIManager.Instance.GetAllActiveObjectives();
            for (int i = 0; i < objectives.Count; i++)
            {
                var obj = objectives[i];
                if (obj == null || obj.IsDestroyed || obj.TeamId == teamId || obj.TeamId < 0) continue;

                float dSq = (obj.TargetPoint.position - myPos).sqrMagnitude;
                if (dSq <= closestDistSq)
                {
                    closestDistSq = dSq;
                    bestTarget = obj.TargetPoint;
                }
            }
        }

        if (bestTarget != null) return bestTarget;

        // 3. Fallback: direct physics sphere scan for living enemy units in range
        Collider[] hits = Physics.OverlapSphere(myPos, attackRange);
        for (int i = 0; i < hits.Length; i++)
        {
            PeasantUnitController u = hits[i].GetComponentInParent<PeasantUnitController>();
            if (u != null && !u.IsDead && u.TeamId != teamId && u.TeamId >= 0 && !u.CannotBeAttackedByPeasants)
            {
                float dSq = (u.transform.position - myPos).sqrMagnitude;
                if (dSq <= closestDistSq)
                {
                    closestDistSq = dSq;
                    bestTarget = u.transform;
                }
            }
        }

        return bestTarget;
    }

    private bool IsTargetValidAndAlive(Transform t)
    {
        if (t == null) return false;
        PeasantUnitController puc = t.GetComponentInParent<PeasantUnitController>();
        if (puc != null) return !puc.IsDead;

        PeasantObjectiveTarget pot = t.GetComponentInParent<PeasantObjectiveTarget>();
        if (pot != null) return !pot.IsDestroyed;

        return false;
    }

    private IEnumerator ShootSequenceRoutine(Transform target)
    {
        if (target == null) yield break;

        // Archer bow draw animation
        PlayArcherAnimation("attack");

        // Keep tower solid and stationary during attacks (no squeezing, squashing, or moving)
        recoilTween?.Kill();
        transform.localScale = initialScale;

        yield return new WaitForSeconds(bowReleaseDelay);

        if (isDestroyed || target == null) yield break;

        Vector3 spawnPos = GetArrowLaunchPosition();

        Vector3 aimPoint = target.position + Vector3.up * 0.9f;

        // Play Bow Release Audio with juicy pitch variance
        if (shootSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.06f);
            KoSAudio.PlayOneShot(audioSource, shootSound, 0.85f, "tower_shoot");
        }

        // Launch Ballistic Parabolic Arrow
        LaunchArrow(spawnPos, target, aimPoint, damagePerShot, proxyOnly: false);

        // Multiplayer replication
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer)
        {
            int targetNetId = -1;
            PeasantUnitController puc = target.GetComponentInParent<PeasantUnitController>();
            if (puc != null) targetNetId = NetworkBattleBridge.Instance.GetUnitNetId(puc);

            NetworkBattleBridge.Instance.BroadcastTowerFired(towerIndex, aimPoint, targetNetId);
        }
    }

    public void ExecuteProxyFire(Vector3 targetPos, int targetNetId)
    {
        if (isDestroyed) return;

        // Aim archer to target
        if (stationedArcherTransform != null)
        {
            Vector3 dir = targetPos - stationedArcherTransform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                stationedArcherTransform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        PlayArcherAnimation("attack");

        if (shootSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.06f);
            KoSAudio.PlayOneShot(audioSource, shootSound, 0.8f, "tower_shoot");
        }

        Vector3 spawnPos = GetArrowLaunchPosition();

        LaunchArrow(spawnPos, null, targetPos, 0f, proxyOnly: true);
    }

    private void LaunchArrow(Vector3 spawnPos, Transform target, Vector3 targetCoord, float damageAmt, bool proxyOnly)
    {
        GameObject arrowObj;
        if (arrowPrefab != null)
        {
            arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowObj.transform.position = spawnPos;
            arrowObj.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
        }

        TowerArrowProjectile projectile = arrowObj.GetComponent<TowerArrowProjectile>();
        if (projectile == null) projectile = arrowObj.AddComponent<TowerArrowProjectile>();

        projectile.Launch(target, targetCoord, damageAmt, teamId, proxyOnly, hitVfxPrefab, hitSound, bloodHitVfxPrefab, dustHitVfxPrefab, machineHitVfxPrefab);
    }

    private void HandleDamageTaken(float amount, PeasantUnitController attacker)
    {
        if (isDestroyed && !isCapturing) return;
        if (attacker != null) lastAttacker = attacker;

        // Tower remains stationary when taking damage (no shaking or moving)
        shakeTween?.Kill();

        // Hit Sound
        if (hitSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            KoSAudio.PlayOneShot(audioSource, hitSound, 0.7f, "structure_hit");
        }

        // Archer flinch
        PlayArcherAnimation("hurt");

        if (healthBar != null)
        {
            healthBar.OnTakeDamage(amount);
        }
    }

    private void HandleHealthChanged(float newHp, float maxHp)
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(newHp, maxHp);
        }
    }



    public GameObject AfterObject;

    public bool flag = false;
    private void HandleDestroyed()
    {
        if (isDestroyed && !isCapturing) return;

        // If capture is enabled, the attacking enemy team conquers and occupies the tower with cool acquisition effects!
        if (allowCaptureOnDestroy)
        {
            // Ensure tower mesh stays intact and visible, NO destruction prefab!
            if (AfterObject != null) AfterObject.SetActive(false);
            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (AfterObject == null || !rends[i].transform.IsChildOf(AfterObject.transform))
                    rends[i].enabled = true;
            }

            int newTeam = (lastAttacker != null && lastAttacker.TeamId >= 0) ? lastAttacker.TeamId : ((teamId == 0) ? 1 : 0);
            ExecuteCapture(newTeam, lastAttacker, isNetworkProxy: false);
            return;
        }

        // ONLY for true permanent destruction (when allowCaptureOnDestroy is false):
        if (flag && AfterObject != null)
        {
            Renderer myRend = GetComponent<Renderer>();
            if (myRend != null) myRend.enabled = false;
            AfterObject.SetActive(true);
        }

        isDestroyed = true;
        isOccupationInProgress = false;
        StopAllCoroutines();

        recoilTween?.Kill();
        shakeTween?.Kill();

        // 1. Permanently remove garrison zone so NO ONE can ever occupy a destroyed tower
        TowerGarrisonZone garrisonZone = GetComponent<TowerGarrisonZone>();
        if (garrisonZone != null)
        {
            garrisonZone.enabled = false;
            Destroy(garrisonZone);
        }

        // 2. Audio Feedback
        KoSAudio.PlayClipAtPoint(destructionSound, transform.position, 1.0f, "tower_destroyed");
        KoSAudio.Play("rubble", transform.position);

        // 3. Explosion VFX
        if (destructionVfxPrefab != null)
        {
            Instantiate(destructionVfxPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
        }

        // 4. Disable all Colliders & NavMeshObstacle so troops path through freely
        Collider[] allCols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < allCols.Length; i++)
        {
            if (allCols[i] != null) allCols[i].enabled = false;
        }

        if (navObstacle != null) navObstacle.enabled = false;

        // 5. Archer death animation
        PlayArcherAnimation("death");

        // 6. Health Bar death animation
        if (healthBar != null)
        {
            healthBar.PlayDestruction();
        }

        // 7. Dramatic DOTween Destruction Collapse: Full Sink Below Ground & Deactivation
        // Sequence collapseSeq = DOTween.Sequence();
        // // Step A: Violent earthquake rumble
        // collapseSeq.Append(transform.DOShakePosition(0.6f, 0.35f, 25));
        // // Step B: Sinks fully into the earth
        // float sinkDepth = Mathf.Max(platformHeightOffset + 3.5f, 10f);
        // collapseSeq.Join(transform.DOMoveY(transform.position.y - sinkDepth, 1.8f).SetEase(Ease.InCubic));
        // collapseSeq.Join(transform.DORotate(transform.eulerAngles + new Vector3(8f, 0f, 4f), 1.8f).SetEase(Ease.OutQuad));
        // collapseSeq.OnComplete(() =>
        // {
        //     // Tower is fully destroyed - deactivate completely so it cannot linger or be interacted with
        //     gameObject.SetActive(false);
        // });
    }

    public void ExecuteCapture(int newTeam, PeasantUnitController capturingAttacker, bool isNetworkProxy = false)
    {
        if (isCapturing) return;
        StartCoroutine(CaptureSequenceRoutine(newTeam, capturingAttacker, isNetworkProxy));
    }

    private IEnumerator CaptureSequenceRoutine(int newTeam, PeasantUnitController capturingAttacker, bool isNetworkProxy)
    {
        isCapturing = true;
        isDestroyed = true;
        currentTarget = null;

        // Broadcast to multiplayer peers if we are MasterClient
        if (!isNetworkProxy && NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer)
        {
            NetworkBattleBridge.Instance.BroadcastTowerCaptured(towerIndex, newTeam);
        }

        // 1. Defeat the current defending archer (if any)
        if (archerAnimator != null)
        {
            PlayArcherAnimation("death");
        }

        // Structural tremor from losing defending archer
        shakeTween?.Kill();
        shakeTween = transform.DOShakePosition(0.25f, 0.15f, 12);

        yield return new WaitForSeconds(0.35f);

        // Remove defeated archer visual
        if (stationedArcherTransform != null)
        {
            Destroy(stationedArcherTransform.gameObject);
            stationedArcherTransform = null;
            archerAnimator = null;
        }

        // 2. Prepare the new Capturing Archer
        Vector3 homeBasePos = (newTeam == 0) ? new Vector3(65.94f, transform.position.y, 9.84f) : new Vector3(158.97f, transform.position.y, -86.53f);
        Vector3 approachDir = (homeBasePos - transform.position).normalized;
        approachDir.y = 0;
        Vector3 baseSpawnPos = transform.position + approachDir * 3.5f;

        GameObject capturingArcherGo = null;
        bool isExistingUnit = false;

        // If the attacking conqueror was a living archer nearby, let them leap up to claim the tower!
        // ONLY genuine foot archers can leap up — NEVER a ballista, siege engine, or melee unit!
        if (capturingAttacker != null && !capturingAttacker.IsDead && capturingAttacker.TeamId == newTeam 
            && capturingAttacker.IsEligibleTowerArcher && !capturingAttacker.IsBallista
            && Vector3.Distance(capturingAttacker.transform.position, transform.position) < 18f)
        {
            capturingArcherGo = capturingAttacker.gameObject;
            isExistingUnit = true;

            capturingAttacker.SetRunningAnimation(false);
            capturingAttacker.enabled = false;
            var agent = capturingAttacker.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }
            var col = capturingAttacker.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
        else if (archerPrefab != null)
        {
            capturingArcherGo = Instantiate(archerPrefab, baseSpawnPos, Quaternion.LookRotation(-approachDir, Vector3.up));
            capturingArcherGo.transform.localScale = new Vector3(0.77f, 0.77f, 0.77f);
            capturingArcherGo.name = "CapturingArcher";

            // Strip mobile peasant scripts
            PeasantUnitController puc = capturingArcherGo.GetComponent<PeasantUnitController>();
            if (puc != null) Destroy(puc);
            NavMeshAgent nma = capturingArcherGo.GetComponent<NavMeshAgent>();
            if (nma != null) Destroy(nma);
            Collider c = capturingArcherGo.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        if (capturingArcherGo == null)
        {
            isCapturing = false;
            isDestroyed = false;
            yield break;
        }

        // Apply capturing team uniform material
        Material targetMat = (newTeam == 0) ? team0ArcherMaterial : team1ArcherMaterial;
        if (targetMat != null)
        {
            Renderer[] rends = capturingArcherGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                rends[i].sharedMaterial = targetMat;
            }
        }

        Animator captAnim = capturingArcherGo.GetComponentInChildren<Animator>();

        // 3. Play the Nice Occupation Animation (Anticipation -> Parabolic Leap -> Touchdown)
        Vector3 toTower = transform.position - capturingArcherGo.transform.position;
        toTower.y = 0;
        if (toTower.sqrMagnitude > 0.01f)
        {
            capturingArcherGo.transform.rotation = Quaternion.LookRotation(toTower.normalized, Vector3.up);
        }

        // Crouch anticipation before jump
        if (captAnim != null && enableArcherAnimations)
        {
            if (captAnim.runtimeAnimatorController == null && archerAnimatorController != null)
                captAnim.runtimeAnimatorController = archerAnimatorController;
            captAnim.enabled = true;
            captAnim.CrossFade("run", 0.08f);
        }
        else if (captAnim != null && !enableArcherAnimations)
        {
            captAnim.enabled = false;
        }
        capturingArcherGo.transform.DOPunchScale(new Vector3(0.12f, -0.2f, 0.12f), 0.18f, 6, 0.4f);
        yield return new WaitForSeconds(0.18f);

        // Ballistic parabolic leap up to the platform
        Vector3 platformPos = transform.position + Vector3.up * platformHeightOffset;
        Vector3 enemyLookTarget = (newTeam == 0) ? new Vector3(131.31f, platformPos.y, -48.65f) : new Vector3(98.45f, platformPos.y, -39.92f);
        Vector3 aimDir = enemyLookTarget - platformPos;
        aimDir.y = 0;
        Quaternion aimRot = (aimDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(aimDir.normalized, Vector3.up) : transform.rotation;

        float leapDuration = 0.65f;
        float leapHeight = Mathf.Clamp(platformHeightOffset * 0.35f, 2.0f, 4.0f);

        // Launch whoosh audio
        if (shootSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(1.15f, 1.3f);
            KoSAudio.PlayOneShot(audioSource, shootSound, 0.6f, "tower_shoot");
        }

        if (captAnim != null && enableArcherAnimations) captAnim.CrossFade("attack", 0.1f);

        capturingArcherGo.transform.DOJump(platformPos, leapHeight, 1, leapDuration).SetEase(Ease.OutQuad);
        capturingArcherGo.transform.DORotateQuaternion(aimRot, leapDuration * 0.9f).SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(leapDuration);

        // 4. Touchdown & Landing Bounce
        if (hitVfxPrefab != null)
        {
            Instantiate(hitVfxPrefab, platformPos, Quaternion.identity);
        }

        capturingArcherGo.transform.DOPunchScale(new Vector3(0.2f, -0.25f, 0.2f), 0.25f, 7, 0.5f);

        // Landing impact audio
        if (hitSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(1.0f, 1.2f);
            KoSAudio.PlayOneShot(audioSource, hitSound, 0.5f, "tower_land");
        }

        // Capture fanfare chime
        AudioClip fanfareClip = (captureSound != null) ? captureSound : shootSound;
        if (fanfareClip != null && audioSource != null)
        {
            audioSource.pitch = (newTeam == 0) ? 1.35f : 0.85f;
            KoSAudio.PlayOneShot(audioSource, fanfareClip, 0.85f, "tower_capture");
        }

        if (captureVfxPrefab != null)
        {
            Instantiate(captureVfxPrefab, platformPos + Vector3.up * 1f, Quaternion.identity);
        }

        // 5. Finalize Capturing Archer Hierarchy
        capturingArcherGo.transform.SetParent(transform, true);
        capturingArcherGo.transform.position = platformPos;
        capturingArcherGo.transform.rotation = aimRot;
        capturingArcherGo.name = "StationedArcher";

        if (isExistingUnit)
        {
            var ag = capturingArcherGo.GetComponent<NavMeshAgent>();
            if (ag != null) Destroy(ag);
            var c = capturingArcherGo.GetComponent<Collider>();
            if (c != null) Destroy(c);
            var puc = capturingArcherGo.GetComponent<PeasantUnitController>();
            if (puc != null)
            {
                puc.SetRunningAnimation(false);
                Destroy(puc);
            }
        }

        // Standardize world scale
        Vector3 parentLossy = transform.lossyScale;
        float safeX = Mathf.Abs(parentLossy.x) > 0.001f ? parentLossy.x : 1f;
        float safeY = Mathf.Abs(parentLossy.y) > 0.001f ? parentLossy.y : 1f;
        float safeZ = Mathf.Abs(parentLossy.z) > 0.001f ? parentLossy.z : 1f;
        capturingArcherGo.transform.localScale = new Vector3(1.05f / safeX, 1.05f / safeY, 1.05f / safeZ);

        stationedArcherTransform = capturingArcherGo.transform;
        archerAnimator = capturingArcherGo.GetComponentInChildren<Animator>();

        // Strip stray mobile health bars from stationed archer; tower uses TowerHealthBar
        CleanupStationedArcherHealthBars(capturingArcherGo);
        ApplyStationedArcherMaterial(capturingArcherGo, newTeam);

        if (archerAnimator != null)
        {
            archerAnimator.SetBool("running", false);
            archerAnimator.SetFloat("speed", 0f);
            archerAnimator.ResetTrigger("attack");
            archerAnimator.ResetTrigger("hurt");
            archerAnimator.ResetTrigger("death");

            if (enableArcherAnimations)
            {
                if (archerAnimator.runtimeAnimatorController == null && archerAnimatorController != null)
                    archerAnimator.runtimeAnimatorController = archerAnimatorController;
                archerAnimator.enabled = true;
                PlayArcherAnimation("idle");
            }
            else
            {
                archerAnimator.enabled = false;
            }
        }

        // 6. Update Tower Ownership & Re-activate Defense
        this.teamId = newTeam;
        this.towerDisplayName = (newTeam == 0) ? "Our Wall Tower (Captured)" : "Enemy Wall Tower (Captured)";

        if (objectiveTarget != null)
        {
            objectiveTarget.ResetObjectiveForTeam(newTeam, maxHealth);
        }

        if (healthBar != null)
        {
            healthBar.ResetHealthBar(newTeam, maxHealth);
        }

        if (towerCollider != null) towerCollider.enabled = true;
        if (navObstacle != null) navObstacle.enabled = true;

        isDestroyed = false;
        isCapturing = false;
        isOccupationInProgress = false;

        Debug.Log($"<color=cyan>[TowerCombatController] Tower '{towerDisplayName}' (Index {towerIndex}) successfully CONQUERED and OCCUPIED by Team {newTeam}!</color>");

        // Trigger rich acquisition celebration, VFX & DOTween UI (NO destruction prefab!)
        TriggerAcquisitionFeedback(newTeam);
    }

    /// <summary>
    /// Displays rich acquisition celebration when a tower is occupied or conquered:
    /// - Guarantees NO destruction prefab is visible and tower mesh is intact.
    /// - Plays tower physical power-up punch scale with DOTween.
    /// - Triggers procedural expanding ground shockwave ring with DOTween.
    /// - Spawns celebration particles (level-up ascending light beam / fireworks).
    /// - Plays triumphant fanfare audio.
    /// - Spawns 3D World-Space Floating Billboard Banner and Screen HUD toast using DOTween.
    /// </summary>
    public void TriggerAcquisitionFeedback(int acquiringTeam)
    {
        // 1. Ensure NO destruction prefab is active and all tower renderers are visible
        if (AfterObject != null)
        {
            AfterObject.SetActive(false);
        }
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (AfterObject == null || !allRenderers[i].transform.IsChildOf(AfterObject.transform))
            {
                allRenderers[i].enabled = true;
            }
        }

        // 2. Delegate to TowerAcquisitionUI for rich DOTween animations & UI
        TowerAcquisitionUI.Show(this, acquiringTeam);
    }

    [ContextMenu("Test Acquisition (Team 0 - Player)")]
    public void TestAcquisitionPlayer()
    {
        TriggerAcquisitionFeedback(0);
    }

    [ContextMenu("Test Acquisition (Team 1 - Enemy)")]
    public void TestAcquisitionEnemy()
    {
        TriggerAcquisitionFeedback(1);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = (teamId == 0) ? new Color(0.2f, 0.8f, 1f, 0.95f) : (teamId == 1 ? new Color(1f, 0.25f, 0.25f, 0.95f) : Color.yellow);
        Vector3 launchPos = GetArrowLaunchPosition();
        Gizmos.DrawWireSphere(launchPos, 0.45f);
        Gizmos.DrawSphere(launchPos, 0.2f);
        Gizmos.DrawLine(transform.position, launchPos);
        Gizmos.DrawRay(launchPos, transform.forward * 2.0f);
    }
}
