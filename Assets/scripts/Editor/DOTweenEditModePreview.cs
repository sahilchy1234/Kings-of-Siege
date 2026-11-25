using UnityEngine;
using UnityEditor;
using DG.Tweening;

/// <summary>
/// Enables full 60 FPS DOTween animation playback directly inside the Unity Editor Scene & Game views
/// WITHOUT having to enter Play Mode.
/// </summary>
public static class DOTweenEditModePreview
{
    private static double lastTime;
    private static bool isRunning = false;
    private static float activeTimer = 0f;

    /// <summary>
    /// Starts driving DOTween.ManualUpdate from EditorApplication.update.
    /// </summary>
    public static void StartPreview(float duration = 12f)
    {
        if (Application.isPlaying) return;

        if (DOTween.instance == null)
        {
            DOTween.Init();
        }

        lastTime = EditorApplication.timeSinceStartup;
        activeTimer = duration;

        if (!isRunning)
        {
            isRunning = true;
            EditorApplication.update += OnEditorUpdate;
        }
    }

    public static void StopPreview()
    {
        if (isRunning)
        {
            isRunning = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void OnEditorUpdate()
    {
        if (Application.isPlaying)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;

        if (dt <= 0.0001f) return;
        if (dt > 0.08f) dt = 0.08f; // Clamp large hitch

        activeTimer -= dt;
        if (activeTimer <= 0f)
        {
            StopPreview();
            return;
        }

        // Ticks all manual tweens
        DOTween.ManualUpdate(dt, dt);

        // Repaint Scene View and Game View so animation is rendered smoothly
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}

