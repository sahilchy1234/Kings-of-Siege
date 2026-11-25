using UnityEngine;

/// <summary>
/// Automatically swaps materials between our meshes and opponent meshes
/// based on the local player / camera perspective:
/// 
/// Supports multiple material groups (e.g. Group 1 for Buildings/Roofs, 
/// Group 2 for Flags/Details/Accents, plus optional additional groups)
/// so you can assign different materials to different kinds of meshes.
/// 
/// When local player is Host / Normal View (Team 0):
/// - 'ourMeshes' receive 'oursMaterial' (Blue).
/// - 'opponentMeshes' receive 'opponentMaterial' (Red).
/// 
/// When camera changes and local player is the Opponent (Team 1 / Flipped View):
/// - 'opponentMeshes' receive 'oursMaterial' (Blue) -> our home castle becomes Blue.
/// - 'ourMeshes' receive 'opponentMaterial' (Red) -> enemy castle becomes Red.
/// 
/// Team identification is 100% automatic - no manual team ID configuration required!
/// </summary>
[DisallowMultipleComponent]
public class TeamMaterialSwapper : MonoBehaviour
{
    [System.Serializable]
    public struct TeamMaterialGroup
    {
        [Tooltip("Optional label describing this group (e.g., 'Flags', 'Roofs', 'Shields').")]
        public string groupName;

        [Tooltip("Material used for OUR side.")]
        public Material oursMaterial;

        [Tooltip("Material used for the OPPONENT side.")]
        public Material opponentMaterial;

        [Tooltip("Meshes belonging to side A (Team 0).")]
        public Renderer[] ourMeshes;

        [Tooltip("Meshes belonging to side B (Team 1).")]
        public Renderer[] opponentMeshes;

        [Tooltip("Material slot index to replace on renderers (default is 0).")]
        public int materialIndex;
    }

    [Header("Material Group 1 (Primary)")]
    [Tooltip("Material used for OUR side (e.g. Blue roof/armor).")]
    [SerializeField] private Material oursMaterial;

    [Tooltip("Material used for the OPPONENT side (e.g. Red roof/armor).")]
    [SerializeField] private Material opponentMaterial;

    [Tooltip("Meshes belonging to side A (Team 0 / Castle A side).")]
    [SerializeField] private Renderer[] ourMeshes;

    [Tooltip("Meshes belonging to side B (Team 1 / Castle B / Opponent side).")]
    [SerializeField] private Renderer[] opponentMeshes;

    [Tooltip("Material slot index to replace on group 1 renderers (default 0).")]
    [SerializeField] private int materialIndex = 0;

    [Header("Material Group 2 (Secondary / Duplicate)")]
    [Tooltip("Second material used for OUR side (e.g. Blue banners/accents/wood).")]
    [SerializeField] private Material oursMaterial2;

    [Tooltip("Second material used for the OPPONENT side (e.g. Red banners/accents/wood).")]
    [SerializeField] private Material opponentMaterial2;

    [Tooltip("Second set of meshes belonging to side A (Team 0 / Castle A side).")]
    [SerializeField] private Renderer[] ourMeshes2;

    [Tooltip("Second set of meshes belonging to side B (Team 1 / Castle B / Opponent side).")]
    [SerializeField] private Renderer[] opponentMeshes2;

    [Tooltip("Material slot index to replace on group 2 renderers (default 0).")]
    [SerializeField] private int materialIndex2 = 0;

    [Header("Additional Material Groups (Optional)")]
    [Tooltip("Add any extra material swap groups here if you have 3 or more material types.")]
    [SerializeField] private TeamMaterialGroup[] additionalGroups;

    private int lastAppliedTeamId = -999;
    private bool lastAppliedFlippedView = false;

    // Group 1 Properties
    public Material OursMaterial { get => oursMaterial; set => oursMaterial = value; }
    public Material OpponentMaterial { get => opponentMaterial; set => opponentMaterial = value; }
    public Renderer[] OurMeshes { get => ourMeshes; set => ourMeshes = value; }
    public Renderer[] OpponentMeshes { get => opponentMeshes; set => opponentMeshes = value; }
    public int MaterialIndex { get => materialIndex; set => materialIndex = value; }

    // Group 2 Properties
    public Material OursMaterial2 { get => oursMaterial2; set => oursMaterial2 = value; }
    public Material OpponentMaterial2 { get => opponentMaterial2; set => opponentMaterial2 = value; }
    public Renderer[] OurMeshes2 { get => ourMeshes2; set => ourMeshes2 = value; }
    public Renderer[] OpponentMeshes2 { get => opponentMeshes2; set => opponentMeshes2 = value; }
    public int MaterialIndex2 { get => materialIndex2; set => materialIndex2 = value; }

    // Additional Groups Property
    public TeamMaterialGroup[] AdditionalGroups { get => additionalGroups; set => additionalGroups = value; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInBattleScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "BattleScene")
        {
            if (FindFirstObjectByType<TeamMaterialSwapper>() == null)
            {
                GameObject go = new GameObject("[TeamMaterialSwapper]");
                go.AddComponent<TeamMaterialSwapper>();
                Debug.Log("[TeamMaterialSwapper] Auto-spawned runtime swapper in BattleScene.");
            }
        }
    }

    private void Awake()
    {
        AutoDiscoverReferencesIfMissing();
    }

    private void Start()
    {
        AutoDiscoverReferencesIfMissing();
        ApplyTeamMaterial();
    }

    private void Update()
    {
        int currentTeamId = GameModeManager.LocalTeamId;
        bool isFlipped = CameraSwipeController.Instance != null && CameraSwipeController.Instance.IsFlippedView;

        if (currentTeamId != lastAppliedTeamId || isFlipped != lastAppliedFlippedView)
        {
            ApplyTeamMaterial();
        }
    }

    public void AutoDiscoverReferencesIfMissing()
    {
        if (oursMaterial == null)
        {
            oursMaterial = RuntimeAssets.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Buildings/TT_RTS_buildings_blueB.mat");
        }
        if (opponentMaterial == null)
        {
            opponentMaterial = RuntimeAssets.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Buildings/TT_RTS_buildings_red.mat");
        }

        if ((ourMeshes == null || ourMeshes.Length == 0) || (opponentMeshes == null || opponentMeshes.Length == 0))
        {
            PeasantObjectiveTarget[] targets = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
            System.Collections.Generic.List<Renderer> team0List = new System.Collections.Generic.List<Renderer>();
            System.Collections.Generic.List<Renderer> team1List = new System.Collections.Generic.List<Renderer>();

            foreach (var t in targets)
            {
                if (t == null) continue;
                Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    // Skip health bar cubes
                    if (r.name.ToLower().Contains("health") || r.name.ToLower().Contains("bar")) continue;

                    // Skip character units (such as TT_Archers standing on the tower) so their character textures are preserved
                    if (r.GetComponentInParent<PeasantUnitController>() != null) continue;

                    if (t.TeamId == 0) team0List.Add(r);
                    else if (t.TeamId == 1) team1List.Add(r);
                }
            }

            if ((ourMeshes == null || ourMeshes.Length == 0) && team0List.Count > 0)
            {
                ourMeshes = team0List.ToArray();
            }
            if ((opponentMeshes == null || opponentMeshes.Length == 0) && team1List.Count > 0)
            {
                opponentMeshes = team1List.ToArray();
            }
        }
    }

    /// <summary>
    /// Automatically detects who the local player is and applies the materials:
    /// - Normal player (Team 0): ourMeshes = oursMaterial (Blue), opponentMeshes = opponentMaterial (Red).
    /// - Opponent player (Team 1 / Flipped): opponentMeshes = oursMaterial (Blue), ourMeshes = opponentMaterial (Red).
    /// </summary>
    [ContextMenu("Apply Team Materials")]
    public void ApplyTeamMaterial()
    {
        int localTeam = GameModeManager.LocalTeamId;
        bool isFlipped = CameraSwipeController.Instance != null && CameraSwipeController.Instance.IsFlippedView;
        bool isOpponentPerspective = (localTeam == 1) || isFlipped;

        lastAppliedTeamId = localTeam;
        lastAppliedFlippedView = isFlipped;

        // Group 1:
        Material materialForOurMeshes = isOpponentPerspective ? opponentMaterial : oursMaterial;
        Material materialForOpponentMeshes = isOpponentPerspective ? oursMaterial : opponentMaterial;
        ApplyMaterialAtIndex(ourMeshes, materialForOurMeshes, materialIndex);
        ApplyMaterialAtIndex(opponentMeshes, materialForOpponentMeshes, materialIndex);

        // Group 2 (Duplicated Set):
        Material materialForOurMeshes2 = isOpponentPerspective ? opponentMaterial2 : oursMaterial2;
        Material materialForOpponentMeshes2 = isOpponentPerspective ? oursMaterial2 : opponentMaterial2;
        ApplyMaterialAtIndex(ourMeshes2, materialForOurMeshes2, materialIndex2);
        ApplyMaterialAtIndex(opponentMeshes2, materialForOpponentMeshes2, materialIndex2);

        // Additional Groups (if configured):
        if (additionalGroups != null)
        {
            for (int i = 0; i < additionalGroups.Length; i++)
            {
                var group = additionalGroups[i];
                Material matOur = isOpponentPerspective ? group.opponentMaterial : group.oursMaterial;
                Material matOpp = isOpponentPerspective ? group.oursMaterial : group.opponentMaterial;
                ApplyMaterialAtIndex(group.ourMeshes, matOur, group.materialIndex);
                ApplyMaterialAtIndex(group.opponentMeshes, matOpp, group.materialIndex);
            }
        }
    }

    public void ApplyMaterialAtIndex(Renderer[] renderers, Material targetMat, int targetIndex = 0)
    {
        if (renderers == null || targetMat == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Material[] mats = r.materials;
            if (mats != null && targetIndex >= 0 && targetIndex < mats.Length)
            {
                mats[targetIndex] = targetMat;
                r.materials = mats;
            }
        }
    }

    private void ApplyMaterialToIndexZero(Renderer[] renderers, Material targetMat)
    {
        ApplyMaterialAtIndex(renderers, targetMat, 0);
    }
}
