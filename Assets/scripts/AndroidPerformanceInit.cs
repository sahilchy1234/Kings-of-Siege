using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Attached to any persistent GameObject (e.g. a "GameManager" in the first scene).
/// Runs once at startup and applies Android-specific performance settings that cannot
/// be set in the editor without affecting PC/editor play.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AndroidPerformanceInit : MonoBehaviour
{
    [Header("Frame Rate")]
    [Tooltip("Target FPS on Android. Set to 60 for smooth gameplay on capable devices.")]
    [SerializeField] private int targetFrameRate = 60;

    [Header("URP Pipeline Override")]
    [Tooltip("URP pipeline asset to use on Android. Assign URP-Performant here.")]
    [SerializeField] private UniversalRenderPipelineAsset performantPipelineAsset;

    [Header("Physics")]
    [Tooltip("Physics steps per second on Android. 30 saves ~40% CPU vs the default 50.")]
    [SerializeField] private int physicsStepsPerSecond = 30;

    /// <summary>
    /// Runs before the first scene loads, so every scene (including MainMenu, which has
    /// no AndroidPerformanceInit component) gets the 60 FPS target. Without this Android
    /// falls back to Unity's default 30 FPS cap.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyGlobalFrameRate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#endif
    }

    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Apply();
#endif
    }

    private void Apply()
    {
        // --- Frame rate ---
        // Turn off vSync so Application.targetFrameRate is respected.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        // Keep the screen on during battle so Android doesn't dim mid-game.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // --- Physics ---
        // Reduce fixed timestep to match the target frame rate so physics doesn't
        // run more often than we render (saves significant CPU).
        Time.fixedDeltaTime = 1f / physicsStepsPerSecond;

        // --- URP pipeline ---
        // Switch to the Performant URP asset (HDR off, main-light shadows off,
        // additional lights off) for lower GPU load on mobile tiles.
        if (performantPipelineAsset != null)
        {
            GraphicsSettings.defaultRenderPipeline = performantPipelineAsset;
            QualitySettings.renderPipeline        = performantPipelineAsset;
        }

        // --- LOD ---
        // Slightly more aggressive LOD switching so distant units use fewer triangles.
        // Does not change how close-up units look.
        QualitySettings.lodBias = 0.6f;

        // --- Texture streaming ---
        QualitySettings.streamingMipmapsActive = true;
        QualitySettings.streamingMipmapsMemoryBudget = 256f;

        Debug.Log($"[AndroidPerformanceInit] Applied: {targetFrameRate} FPS target, {physicsStepsPerSecond} Hz physics.");
    }
}
