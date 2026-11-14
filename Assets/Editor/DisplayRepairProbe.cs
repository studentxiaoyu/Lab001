using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

[InitializeOnLoad]
internal static class DisplayRepairProbe
{
    const string Dir = "Validation/DisplayRepair";
    static double next;
    static DisplayRepairProbe() { EditorApplication.update += Tick; next = EditorApplication.timeSinceStartup + 5; }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        string request = Dir + "/request.txt";
        if (!File.Exists(request)) return;
        string label = File.ReadAllText(request).Trim(); File.Delete(request);
        try { Snapshot(label); } catch (Exception ex) { Directory.CreateDirectory(Dir); File.WriteAllText(Dir + "/probe-error.txt", ex.ToString()); }
    }
    static void Snapshot(string label)
    {
        Directory.CreateDirectory(Dir);
        var b = new StringBuilder();
        b.AppendLine("Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + " Play=" + EditorApplication.isPlaying);
        b.AppendLine("Pipeline=" + GraphicsSettings.currentRenderPipeline + " Quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in cameras) b.AppendLine($"CAM {PathOf(c.transform)} enabled={c.isActiveAndEnabled} pos={c.transform.position} rot={c.transform.eulerAngles} mask={c.cullingMask} rt={c.targetTexture} depth={c.depth} near={c.nearClipPlane} far={c.farClipPlane} fov={c.fieldOfView}");
        foreach (var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.scene.IsValid()) b.AppendLine($"TEXT {PathOf(t.transform)} active={t.isActiveAndEnabled} text={t.text.Replace('\n',' ')} font={t.font} size={t.fontSize} scale={t.transform.lossyScale} pos={t.transform.position} rot={t.transform.eulerAngles} bounds={t.bounds} material={t.fontSharedMaterial} shader={t.fontSharedMaterial?.shader} supported={t.fontSharedMaterial?.shader?.isSupported}");
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.gameObject.scene.IsValid() && (PathOf(r.transform).Contains("DataBoard") || r.sharedMaterials.Any(m => m && (!m.shader || !m.shader.isSupported))))
                b.AppendLine($"RENDERER {PathOf(r.transform)} active={r.gameObject.activeInHierarchy} enabled={r.enabled} bounds={r.bounds} materials=" + string.Join(";", r.sharedMaterials.Select(m => m ? m.name + ":" + m.shader?.name + ":supported=" + m.shader?.isSupported : "null")));
        foreach(var id in AssetDatabase.FindAssets("t:TMP_FontAsset", new[]{"Assets/ArtRes/Fonts"})) {
            var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(id));
            b.AppendLine($"FONT {f.name} mode={f.atlasPopulationMode} source={f.sourceFontFile} chars={f.characterTable.Count} readable={f.atlasTexture.isReadable}");
        }
        File.WriteAllText(Dir + "/" + label + ".txt", b.ToString());
        var eye = cameras.FirstOrDefault(c => c.name == "CenterEyeAnchor");
        if (eye) Capture(eye, Dir + "/" + label + "-eye.png");
        var titles = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.name == "TextTitle").ToArray();
        for(int i=0; i<titles.Length; i++) {
            var title = titles[i].transform; var go = new GameObject("temporary display inspection");
            try { var c=go.AddComponent<Camera>(); c.orthographic=true; c.orthographicSize=1.1f; c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=new Color(.15f,.15f,.15f); c.nearClipPlane=.01f; c.farClipPlane=30;
                var target = title.position-title.up*.5f; c.transform.SetPositionAndRotation(target-title.forward*4,Quaternion.LookRotation(title.forward,title.up));
                Capture(c,Dir+"/"+label+"-board-"+i+".png");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
    static void Capture(Camera c,string path) {
        var previous=c.targetTexture; var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1400,900,24); var t=new Texture2D(1400,900,TextureFormat.RGB24,false);
        try { c.targetTexture=rt; c.Render(); RenderTexture.active=rt; t.ReadPixels(new Rect(0,0,1400,900),0,0); t.Apply(); File.WriteAllBytes(path,t.EncodeToPNG()); }
        finally { c.targetTexture=previous; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(t); }
    }
}
