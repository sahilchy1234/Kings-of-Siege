using UnityEngine;

/// <summary>
/// Allows friendly archer units to walk into the tower base and occupy the tower platform,
/// using the tower as their elevated combat station / fortress base to attack enemies.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TowerCombatController))]
public class TowerGarrisonZone : MonoBehaviour
{
    [Header("Garrison Settings")]
    [Tooltip("Radius around the tower base where archers can enter and occupy.")]
    [SerializeField, Min(1f)] private float garrisonRadius = 8.0f;

    [Tooltip("Radius to guide nearby unengaged archers to claim or garrison this tower when vacant.")]
    [SerializeField, Min(1f)] private float attractRadius = 35f;

    [Tooltip("If true, automatically scans for nearby eligible archer units to occupy if empty.")]
    [SerializeField] private bool autoOccupyNearbyArchers = true;

    [Tooltip("Interval between scans in seconds.")]
    [SerializeField, Range(0.1f, 2f)] private float scanInterval = 0.25f;

    private TowerCombatController towerCombat;
    private float nextScanTime;

    private void Awake()
    {
        towerCombat = GetComponent<TowerCombatController>();
    }

    private void Update()
    {
        if (towerCombat == null || towerCombat.IsDestroyed)
        {
            enabled = false;
            return;
        }

        if (towerCombat.IsOccupied || !autoOccupyNearbyArchers || Time.time < nextScanTime) return;

        nextScanTime = Time.time + scanInterval;
        ScanAndOccupy();
    }

    private void ScanAndOccupy()
    {
        // 1. Immediate Garrison Check (within garrisonRadius)
        Collider[] hits = Physics.OverlapSphere(transform.position, garrisonRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            PeasantUnitController puc = hits[i].GetComponentInParent<PeasantUnitController>();
            if (puc != null && !puc.IsDead)
            {
                // If neutral (teamId < 0), ANY archer (Team 0 or Team 1) can occupy!
                // If owned (teamId >= 0), only friendly archers of that team can occupy.
                bool isEligibleTeam = (towerCombat.TeamId < 0) || (puc.TeamId == towerCombat.TeamId);
                if (isEligibleTeam)
                {
                    // STRICT GUARD: ONLY genuine foot archers can occupy towers — NEVER ballistas, siege engines, or melee units!
                    if (puc.IsEligibleTowerArcher && !puc.IsBallista)
                    {
                        if (towerCombat.TryOccupyTower(puc))
                        {
                            return;
                        }
                    }
                }
            }
        }

        // 2. Attract nearby unengaged archer towards this tower if it is vacant or neutral
        if (attractRadius > garrisonRadius)
        {
            Collider[] attractHits = Physics.OverlapSphere(transform.position, attractRadius);
            for (int i = 0; i < attractHits.Length; i++)
            {
                PeasantUnitController puc = attractHits[i].GetComponentInParent<PeasantUnitController>();
                if (puc != null && !puc.IsDead)
                {
                    bool isEligibleTeam = (towerCombat.TeamId < 0) || (puc.TeamId == towerCombat.TeamId);
                    if (isEligibleTeam)
                    {
                        // STRICT GUARD: ONLY genuine foot archers can be guided to garrison towers — NEVER ballistas or melee!
                        if (puc.IsEligibleTowerArcher && !puc.IsBallista)
                        {
                            // If archer is not already targeting this tower, redirect them to garrison it!
                            if (puc.CurrentTarget != transform && puc.CurrentTarget != towerCombat.transform)
                            {
                                puc.SetTarget(transform);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, garrisonRadius);
    }
}
