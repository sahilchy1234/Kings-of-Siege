using UnityEngine;

/// <summary>
/// Add this component to an enemy castle, building, or other attackable objective.
/// The peasant controller targets these only after no living enemy
/// PeasantUnitController units remain on the battlefield.
///
/// Set Team ID to match the owning side. Optional Target Point should be an
/// empty child positioned where units should approach/strike the structure.
/// </summary>
public class PeasantObjectiveTarget : MonoBehaviour, IDamageable
{

    public PeasantUnitController _archer;
    [Header("Objective")]
    [SerializeField] private int teamId = 1;
    [SerializeField] private string objectiveName = "Castle";
    [Header("Health & Health Bar")]
    [SerializeField, Min(1f)] private float maxHealth = 500f;
    [SerializeField] private Transform healthBarCube;
    [SerializeField] private JuicyUnitHealthBar juicyHealthBar;
    [SerializeField] private Vector3 healthBarScaleAxis = Vector3.right;
    [SerializeField] private bool hideHealthBarOnDeath = true;
    [SerializeField] private bool autoColorByTeam = true;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private bool destroyGameObjectOnDeath = false;
    [SerializeField, Min(0f)] private float destroyDelay = 2f;
    public GameObject effectExplosion;

    private float currentHealth;
    private bool isDestroyed;
    private bool isShieldImmune;
    private Vector3 initialHealthBarScale;
    private Vector3 initialHealthBarLocalPos;

    public int TeamId => teamId;
    public string ObjectiveName => objectiveName;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => isDestroyed;
    public bool IsShieldImmune
    {
        get => isShieldImmune;
        set => isShieldImmune = value;
    }
    public Transform TargetPoint => targetPoint != null ? targetPoint : transform;

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float, PeasantUnitController> OnDamaged;
    public event System.Action OnDestroyedEvent;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        EnsureJuicyHealthBar();

        if (healthBarCube != null)
        {
            initialHealthBarScale = healthBarCube.localScale;
            initialHealthBarLocalPos = healthBarCube.localPosition;
            ApplyTeamHealthBarColor();
            UpdateHealthBar();
        }

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.RegisterObjective(this);
        }
    }

    private void EnsureJuicyHealthBar()
    {
        if (juicyHealthBar == null)
        {
            juicyHealthBar = GetComponentInChildren<JuicyUnitHealthBar>(true);
            if (juicyHealthBar == null)
            {
                Transform hbParent = healthBarCube != null ? healthBarCube.parent : transform.Find("healthbar");
                if (hbParent != null)
                {
                    juicyHealthBar = hbParent.GetComponent<JuicyUnitHealthBar>();
                    if (juicyHealthBar == null)
                        juicyHealthBar = hbParent.gameObject.AddComponent<JuicyUnitHealthBar>();
                }
            }
        }

        if (juicyHealthBar != null)
        {
            juicyHealthBar.SetupObjective(transform, teamId, maxHealth);
        }
    }

    public void ApplyTeamHealthBarColor()
    {
        if (juicyHealthBar != null)
        {
            juicyHealthBar.ApplyTeamTheming(teamId);
        }

        if (!autoColorByTeam || healthBarCube == null) return;

        int localTeam = GameModeManager.LocalTeamId;
        bool isFriendly = (teamId == localTeam);

        Renderer hbRenderer = healthBarCube.GetComponent<Renderer>();
        if (hbRenderer != null && hbRenderer.material != null)
        {
            hbRenderer.material.color = isFriendly ? Color.blue : Color.red;
        }
    }

    private void UpdateHealthBar()
    {
        if (juicyHealthBar != null)
        {
            juicyHealthBar.SetHealth(currentHealth, maxHealth);
        }

        if (healthBarCube == null) return;

        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        Vector3 newScale = initialHealthBarScale;
        Vector3 newPos = initialHealthBarLocalPos;

        if (healthBarScaleAxis.x > 0f)
        {
            newScale.x = initialHealthBarScale.x * healthPercent;
            // Shift position so the left edge stays fixed
            newPos.x = initialHealthBarLocalPos.x - (initialHealthBarScale.x * (1f - healthPercent) * 0.5f);
        }
        if (healthBarScaleAxis.y > 0f)
        {
            newScale.y = initialHealthBarScale.y * healthPercent;
            newPos.y = initialHealthBarLocalPos.y - (initialHealthBarScale.y * (1f - healthPercent) * 0.5f);
        }
        if (healthBarScaleAxis.z > 0f)
        {
            newScale.z = initialHealthBarScale.z * healthPercent;
            newPos.z = initialHealthBarLocalPos.z - (initialHealthBarScale.z * (1f - healthPercent) * 0.5f);
        }

        healthBarCube.localScale = newScale;
        healthBarCube.localPosition = newPos;
    }

    private void OnDestroy()
    {
        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.UnregisterObjective(this);
        }
    }

    public virtual void SetHealthNetworked(float newHealth)
    {
        currentHealth = Mathf.Max(0f, newHealth);
        UpdateHealthBar();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f && !isDestroyed)
        {
            DestroyObjective();
        }
    }

    public virtual void TakeDamage(float amount, PeasantUnitController attacker)
    {
        if (isDestroyed || amount <= 0f)
            return;

        if (isShieldImmune)
            return;

        // In multiplayer, only MasterClient executes authoritative damage calculations
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar();
        if (juicyHealthBar != null)
        {
            juicyHealthBar.OnTakeDamage(amount);
        }
        OnDamaged?.Invoke(amount, attacker);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f){
            DestroyObjective();

            if(_archer != null){
                _archer.DieNetworked();
            }
        }
    }

    public virtual void DestroyObjective()
    {
        if (isDestroyed)
            return;

            if(effectExplosion!=null){
                effectExplosion.gameObject.SetActive(true);
            }

        isDestroyed = true;
        OnDestroyedEvent?.Invoke();

        if (juicyHealthBar != null)
        {
            juicyHealthBar.PlayDestruction();
        }

        if (healthBarCube != null)
        {
            if (hideHealthBarOnDeath)
                healthBarCube.gameObject.SetActive(false);
            else
                UpdateHealthBar();
        }

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.UnregisterObjective(this);
        }

        // Disable colliders so units stop treating the destroyed structure as solid.
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
            col.enabled = false;

        if (destroyGameObjectOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    /// <summary>
    /// Resets this objective under new team ownership after being captured by enemies.
    /// </summary>
    public void ResetObjectiveForTeam(int newTeam, float newHp)
    {
        this.teamId = newTeam;
        this.maxHealth = Mathf.Max(1f, newHp);
        this.currentHealth = this.maxHealth;
        this.isDestroyed = false;

        if (healthBarCube != null)
        {
            healthBarCube.gameObject.SetActive(true);
            ApplyTeamHealthBarColor();
            UpdateHealthBar();
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
            col.enabled = true;

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.UnregisterObjective(this);
            BattleAIManager.Instance.RegisterObjective(this);
        }
    }
}
