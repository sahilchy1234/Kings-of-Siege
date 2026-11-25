#if UNITY_ANDROID && !UNITY_EDITOR
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opt-in Android performance probe (inactive unless perfprobe.on exists in persistentDataPath). Logs a one-line summary to logcat every
/// two seconds (tag "Unity", prefix "[PerfProbe]"): FPS, CPU main/render and GPU frame
/// times, draw calls, SetPass calls, triangles and GC allocations per frame.
/// Counters that need a development build (draw calls etc.) read as 0 in release builds;
/// the FrameTimingManager CPU/GPU times work in both.
/// </summary>
public sealed class PerfProbe : MonoBehaviour
{
    const float Interval = 2f;

    ProfilerRecorder mainThread, renderThread, gpu, drawCalls, setPass, batches, tris, gcAlloc, gcCount, scripts, physics, anim, ui;
    readonly FrameTiming[] timings = new FrameTiming[1];
    float timer; int frames;
    double cpuMainSum, cpuRenderSum, gpuSum; int timingSamples;
    float worstFrame;
    readonly StringBuilder sb = new StringBuilder(256);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Off unless a marker file exists in the app's external files folder. Push it with:
        // adb shell touch /sdcard/Android/data/<package>/files/perfprobe.on
        if (!System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "perfprobe.on")))
            return;
        var go = new GameObject("[PerfProbe]");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<PerfProbe>();
    }

    void OnEnable()
    {
        mainThread   = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Thread", 15);
        gpu          = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 15);
        drawCalls    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        setPass      = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        batches      = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        tris         = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        gcAlloc      = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        gcCount      = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        scripts      = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate");
        physics      = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");
        anim         = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd");
        ui           = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "UGUI.Rendering.UpdateBatches");
    }

    void OnDisable()
    {
        mainThread.Dispose(); renderThread.Dispose(); gpu.Dispose(); drawCalls.Dispose(); setPass.Dispose();
        batches.Dispose(); tris.Dispose(); gcAlloc.Dispose(); gcCount.Dispose(); scripts.Dispose();
        physics.Dispose(); anim.Dispose(); ui.Dispose();
    }

    static double Ms(ProfilerRecorder r) => r.Valid ? r.LastValue * 1e-6 : -1;

    void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            cpuMainSum += timings[0].cpuMainThreadFrameTime;
            cpuRenderSum += timings[0].cpuRenderThreadFrameTime;
            gpuSum += timings[0].gpuFrameTime;
            timingSamples++;
        }

        frames++;
        timer += Time.unscaledDeltaTime;
        if (Time.unscaledDeltaTime > worstFrame) worstFrame = Time.unscaledDeltaTime;
        if (timer < Interval) return;

        int n = Mathf.Max(1, timingSamples);
        sb.Clear();
        sb.Append("[PerfProbe] scene=").Append(SceneManager.GetActiveScene().name)
          .Append(" fps=").Append((frames / timer).ToString("F1"))
          .Append(" worst=").Append((worstFrame * 1000f).ToString("F1")).Append("ms")
          .Append(" ftm_main=").Append((cpuMainSum / n).ToString("F2"))
          .Append(" ftm_render=").Append((cpuRenderSum / n).ToString("F2"))
          .Append(" ftm_gpu=").Append((gpuSum / n).ToString("F2"))
          .Append(" rec_main=").Append(Ms(mainThread).ToString("F2"))
          .Append(" rec_render=").Append(Ms(renderThread).ToString("F2"))
          .Append(" rec_gpu=").Append(Ms(gpu).ToString("F2"))
          .Append(" scripts=").Append(Ms(scripts).ToString("F2"))
          .Append(" physics=").Append(Ms(physics).ToString("F2"))
          .Append(" anim=").Append(Ms(anim).ToString("F2"))
          .Append(" uiBatch=").Append(Ms(ui).ToString("F2"))
          .Append(" draws=").Append(drawCalls.LastValue)
          .Append(" setpass=").Append(setPass.LastValue)
          .Append(" batches=").Append(batches.LastValue)
          .Append(" tris=").Append(tris.LastValue)
          .Append(" gcBytes=").Append(gcAlloc.LastValue)
          .Append(" gcCount=").Append(gcCount.LastValue)
          .Append(" res=").Append(Screen.width).Append('x').Append(Screen.height)
          .Append(" scale=").Append(((UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)?.renderScale ?? -1f).ToString("F2"));
        Debug.Log(sb.ToString());

        timer = 0; frames = 0; worstFrame = 0;
        cpuMainSum = cpuRenderSum = gpuSum = 0; timingSamples = 0;
    }
}
#endif
