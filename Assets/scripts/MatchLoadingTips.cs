using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// One-line gameplay tips shown while a match is loading (after matchmaking, before the battlefield appears).
/// Attach <see cref="MatchLoadingTipRotator"/> to any TMP_Text to cycle through the tips with a soft fade.
/// </summary>
public static class MatchLoadingTips
{
    public static readonly string[] Tips =
    {
        "Fences stall rushes. Place one in front of an incoming push.",
        "Save energy for the archer tower before a big push.",
        "Stone shower breaks up clustered enemies.",
        "Rolling wood clears a lane of weak units.",
        "Reinforcement carts bring fresh troops mid-fight.",
        "Arrow showers work best on tight groups.",
        "Big fireballs punish clumped units.",
        "Capture neutral towers to garrison archers on the wall.",
        "An empty energy bar is a lost push. Keep it spending.",
        "Spend energy in bursts, not one unit at a time."
    };

    private static int lastIndex = -1;

    /// <summary>Random tip that never repeats the previous one.</summary>
    public static string NextTip()
    {
        if (Tips.Length == 0) return string.Empty;
        if (Tips.Length == 1) return Tips[0];
        int idx;
        do
        {
            idx = Random.Range(0, Tips.Length);
        } while (idx == lastIndex);
        lastIndex = idx;
        return Tips[idx];
    }
}

/// <summary>Cycles <see cref="MatchLoadingTips"/> on a TMP_Text with a fade-out / fade-in.</summary>
[RequireComponent(typeof(TMP_Text))]
public class MatchLoadingTipRotator : MonoBehaviour
{
    [Tooltip("Seconds each tip stays on screen.")]
    [SerializeField] private float holdSeconds = 3.5f;
    [Tooltip("Seconds for each fade.")]
    [SerializeField] private float fadeSeconds = 0.35f;

    private TMP_Text label;
    private Coroutine routine;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        routine = StartCoroutine(RotateRoutine());
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator RotateRoutine()
    {
        Color c = label.color;
        label.text = MatchLoadingTips.NextTip();
        while (true)
        {
            yield return Fade(c.a, 1f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f);
            label.text = MatchLoadingTips.NextTip();
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        Color c = label.color;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeSeconds));
            label.color = c;
            yield return null;
        }
        c.a = to;
        label.color = c;
    }
}
