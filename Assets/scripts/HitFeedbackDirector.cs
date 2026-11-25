using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes hits on towers and castles feel powerful:
///  - Camera shake scaled by how much of the building's health the hit took.
///  - A quick "flinch" squash on the building when it is hit.
///  - Crumbling: stone chunks break off at 75%, 50% and 25% health.
///  - Destruction: hit-stop (brief slow-mo), heavy shake, a debris burst and the building
///    tilting and sinking into the ground.
/// Subscribes to PeasantObjectiveTarget events, so no building prefab needs changing.
/// Created automatically in any scene that has a GameManager.
/// </summary>
public class HitFeedbackDirector : MonoBehaviour
{
    [SerializeField] private float maxHitShake = 0.6f;
    [SerializeField] private float destroyShake = 1.4f;
    [SerializeField] private float hitStopScale = 0.08f;
    [SerializeField] private float hitStopSeconds = 0.14f;
    [SerializeField] private float castleSlowMoSeconds = 0.9f;
    [SerializeField] private float[] crumbleThresholds = { 0.75f, 0.5f, 0.25f };

    private class Tracked
    {
        public PeasantObjectiveTarget target;
        public int nextThreshold;
        public float lastFlinchTime;
        public Vector3 baseScale;
        public Material material;
        public float height = 6f;
    }

    private readonly Dictionary<PeasantObjectiveTarget, Tracked> tracked = new Dictionary<PeasantObjectiveTarget, Tracked>();
    private bool hitStopRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    private static void TryCreate()
    {
        if (FindFirstObjectByType<HitFeedbackDirector>() != null) return;
        if (FindFirstObjectByType<GameManager>() == null) return;
        new GameObject("[HitFeedbackDirector]").AddComponent<HitFeedbackDirector>();
    }

    private void Start()
    {
        StartCoroutine(ScanLoop());
    }

    // Towers can appear mid-match (archer tower powerup), so rescan every couple of seconds.
    private IEnumerator ScanLoop()
    {
        var wait = new WaitForSecondsRealtime(2f);
        while (true)
        {
            foreach (var t in FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None))
                if (t != null && !t.IsDestroyed && !tracked.ContainsKey(t)) Track(t);
            yield return wait;
        }
    }

    private void Track(PeasantObjectiveTarget t)
    {
        var info = new Tracked { target = t, baseScale = t.transform.localScale };
        // Use the biggest mesh (the building itself, not a health bar or flag) for chunk material and height.
        MeshRenderer rend = null;
        float best = 0f;
        foreach (var mr in t.GetComponentsInChildren<MeshRenderer>())
        {
            float v = mr.bounds.size.sqrMagnitude;
            if (v > best) { best = v; rend = mr; }
        }
        if (rend != null)
        {
            info.material = rend.sharedMaterial;
            info.height = Mathf.Clamp(rend.bounds.size.y, 2f, 20f);
        }
        // Skip thresholds already passed (e.g. a recaptured building).
        float ratio = Ratio(t);
        while (info.nextThreshold < crumbleThresholds.Length && ratio <= crumbleThresholds[info.nextThreshold]) info.nextThreshold++;

        tracked[t] = info;
        t.OnDamaged += (amount, attacker) => OnHit(info, amount);
        t.OnDestroyedEvent += () => OnDestroyed(info);
    }

    private static float Ratio(PeasantObjectiveTarget t) => t.MaxHealth > 0f ? t.CurrentHealth / t.MaxHealth : 1f;

    private bool IsCastle(PeasantObjectiveTarget t) =>
        t.ObjectiveName == "Castle" || t.name.ToLowerInvariant().Contains("castle");

    private void OnHit(Tracked info, float amount)
    {
        var t = info.target;
        if (t == null) return;

        // Shake: a 10% chunk of health gives a full shake, chip damage gives a light tremble.
        float frac = Mathf.Clamp01(amount / Mathf.Max(1f, t.MaxHealth) * 10f);
        if (frac > 0.15f) Shake(Mathf.Lerp(0.12f, maxHitShake, frac), 0.2f + 0.15f * frac);

        // Flinch (throttled so rapid arrows don't make it jitter).
        if (Time.unscaledTime - info.lastFlinchTime > 0.18f)
        {
            info.lastFlinchTime = Time.unscaledTime;
            t.transform.DOKill(true);
            t.transform.localScale = info.baseScale;
            t.transform.DOPunchScale(new Vector3(0.06f, -0.08f, 0.06f) * Mathf.Lerp(0.5f, 1.5f, frac), 0.22f, 8, 0.6f);
        }

        // Crumble chunks at each threshold.
        float ratio = Ratio(t);
        while (info.nextThreshold < crumbleThresholds.Length && ratio <= crumbleThresholds[info.nextThreshold])
        {
            info.nextThreshold++;
            SpawnDebris(info, 6, 0.6f);
            Shake(0.5f, 0.3f);
        }
    }

    private void OnDestroyed(Tracked info)
    {
        var t = info.target;
        if (t == null) return;
        bool castle = IsCastle(t);

        Shake(castle ? destroyShake * 1.4f : destroyShake, castle ? 0.8f : 0.55f);
        StartCoroutine(HitStop(castle ? castleSlowMoSeconds : hitStopSeconds, castle ? 0.25f : hitStopScale));
        SpawnDebris(info, castle ? 22 : 14, 1.1f);

        // Buildings are reused, so never move or rotate them: only a second wave of rubble and a rumble.
        t.transform.DOKill(true);
        t.transform.localScale = info.baseScale;
        DOVirtual.DelayedCall(0.35f, () => SpawnDebris(info, castle ? 12 : 8, 0.8f));
        Shake(0.35f, 1.4f);

        // Keep the entry until the delayed rubble has used it, then drop it.
        DOVirtual.DelayedCall(0.5f, () => tracked.Remove(t));
    }

    private void SpawnDebris(Tracked info, int count, float sizeScale)
    {
        if (info.target == null) return;
        Vector3 origin = info.target.transform.position;
        for (int i = 0; i < count; i++)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(chunk.GetComponent<Collider>());
            var r = chunk.GetComponent<MeshRenderer>();
            if (info.material != null) r.sharedMaterial = info.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform c = chunk.transform;
            c.position = origin + Vector3.up * Random.Range(info.height * 0.3f, info.height * 0.9f) + Random.insideUnitSphere * 0.8f;
            c.rotation = Random.rotation;
            float s = Random.Range(0.25f, 0.6f) * sizeScale;
            c.localScale = Vector3.one * s;

            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f) * sizeScale;
            Vector3 land = new Vector3(origin.x + dir.x, origin.y + 0.1f, origin.z + dir.y);
            float dur = Random.Range(0.55f, 0.85f);
            DOTween.Sequence().SetTarget(c)
                .Append(c.DOJump(land, Random.Range(1.5f, 3.5f), 1, dur).SetEase(Ease.Linear))
                .Join(c.DORotate(Random.insideUnitSphere * 540f, dur, RotateMode.FastBeyond360))
                .AppendInterval(Random.Range(0.8f, 1.6f))
                .Append(c.DOScale(0f, 0.35f).SetEase(Ease.InBack))
                .OnComplete(() => { if (chunk != null) Destroy(chunk); })
                .OnKill(() => { if (chunk != null) Destroy(chunk); });
        }
    }

    /// <summary>Global camera shake strength (1 = original, user asked for less).</summary>
    public static float ShakeStrength = 0.35f;

    private static void Shake(float intensity, float duration)
    {
        if (CameraSwipeController.Instance != null)
            CameraSwipeController.Instance.TriggerShake(intensity * ShakeStrength, duration * 0.75f);
    }

    private IEnumerator HitStop(float seconds, float scale)
    {
        if (hitStopRunning || Time.timeScale < 0.99f) yield break; // don't fight a pause menu
        hitStopRunning = true;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(seconds);
        if (Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = 1f;
        hitStopRunning = false;
    }
}
