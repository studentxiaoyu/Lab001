#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
internal static class ShapesUrpRepairProbe {
    static readonly double started;
    static int samples, beginCount, endCount, maxTotal, maxActive, maxPending, errors;
    static double nextSample;
    static readonly System.Text.StringBuilder rows = new System.Text.StringBuilder();
    static ShapesUrpRepairProbe() {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
        Application.logMessageReceived += Log;
        rows.AppendLine("seconds,total,active,pendingCommands,beginCameras,endCameras");
    }
    static void Begin(ScriptableRenderContext ctx, Camera cam) { beginCount++; }
    static void End(ScriptableRenderContext ctx, Camera cam) { endCount++; }
    static void Log(string message, string trace, LogType type) {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
            (message.Contains("Text element allocation cap") || trace.Contains("Shapes."))) errors++;
    }
    static int Pending() {
        var field = typeof(Shapes.DrawCommand).GetField("cBuffersRendering", BindingFlags.Static | BindingFlags.NonPublic);
        var dict = field.GetValue(null) as IDictionary;
        int count = 0;
        if (dict != null) foreach (ICollection list in dict.Values) count += list.Count;
        return count;
    }
    static void Tick() {
        double elapsed = EditorApplication.timeSinceStartup - started;
        if (elapsed < 2 || elapsed < nextSample) return;
        nextSample = elapsed + 0.5;
        int total = Shapes.ShapesTextPool.InstanceElementCount;
        int active = Shapes.ShapesTextPool.InstanceElementCountActive;
        int pending = Pending();
        samples++;
        maxTotal = Math.Max(maxTotal, total); maxActive = Math.Max(maxActive, active); maxPending = Math.Max(maxPending, pending);
        rows.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1},{2},{3},{4},{5}", elapsed,total,active,pending,beginCount,endCount));
        if (elapsed < 22) return;
        EditorApplication.update -= Tick;
        RenderPipelineManager.beginCameraRendering -= Begin;
        RenderPipelineManager.endCameraRendering -= End;
        Application.logMessageReceived -= Log;
        var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
        string dir = Path.Combine(Application.dataPath, "../Validation/ShapesURPRepair");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"editor-pool-samples.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(dir,"editor-verification.txt"),
            "Editor-only observation; no player build or VR acceptance.\n" +
            "Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + "\n" +
            "Playing=" + EditorApplication.isPlaying + "\n" +
            "CompatibilityMode=" + (settings != null && settings.enableRenderCompatibilityMode) + "\n" +
            "Samples=" + samples + "\nBeginCameraCallbacks=" + beginCount + "\nEndCameraCallbacks=" + endCount +
            "\nMaxTotal=" + maxTotal + "\nMaxActive=" + maxActive + "\nMaxPendingCommands=" + maxPending +
            "\nFinalTotal=" + total + "\nFinalActive=" + active + "\nFinalPendingCommands=" + pending + "\nNewShapesErrors=" + errors + "\n");
        Debug.Log("[Shapes URP repair] Editor pool observation saved to Validation/ShapesURPRepair.");
    }
}
#endif
