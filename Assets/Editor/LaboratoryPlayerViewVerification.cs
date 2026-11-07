using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bounded Play smoke check for presentation only; never saves a runtime scene.</summary>
[InitializeOnLoad]
public static class LaboratoryPlayerViewVerification
{
    private const string Flag = "LaboratoryPlayerViewVerification.Running";
    private static int ticks;
    private static string Output => Path.GetFullPath("Validation/PlayerViewVerification");

    static LaboratoryPlayerViewVerification() { EditorApplication.update += Tick; }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("[PlayerViewVerification] PASS " + message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || ++ticks < 60) return;
        SessionState.SetBool(Flag, false);
        try
        {
            var view = UnityEngine.Object.FindObjectOfType<LaboratoryPlayerView>();
            var rig = UnityEngine.Object.FindObjectOfType<OVRCameraRig>();
            Require(view != null && rig != null, "Original OVR rig and presentation adapter present");
            Require(!view.XRActive, "Desktop smoke test runs without active headset");
            var camera = rig.centerEyeAnchor.GetComponent<Camera>();
            Require(camera.enabled && camera.targetTexture == null && camera.targetDisplay == 0,
                "CenterEyeAnchor outputs Display 1, not a preview RenderTexture");
            Require(!rig.usePerEyeCameras && camera.stereoTargetEye == StereoTargetEyeMask.Both,
                "Original stereo camera configuration preserved");
            Require(Vector3.Distance(rig.transform.position, new Vector3(0, 1.65f, 0)) < .01f,
                "Player starts at hall centre at eye level");
            var room = GameObject.Find("Objects/Room");
            var roomBounds = room.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToArray();
            var bounds = roomBounds[0];
            foreach (var b in roomBounds) bounds.Encapsulate(b);
            Require(bounds.Contains(camera.transform.position), "Player eye lies inside room bounds");
            Require(!Physics.OverlapSphere(camera.transform.position, .1f).Any(c => !c.isTrigger),
                "Player eye does not overlap a solid collider");
            var canvases = new[] { "Canvas", "GasDesktopCanvas", "TransformerDesktopCanvas" }
                .Select(n => GameObject.Find(n).GetComponent<Canvas>()).ToArray();
            Require(canvases.All(c => !c.enabled), "All three desktop overlays hidden by default");
            var world = GameObject.Find("FlatUnityCanvas/Unity Canvas").GetComponent<Canvas>();
            Require(world.enabled && world.renderMode == RenderMode.WorldSpace, "World-space interaction canvas retained");
            Require(GameObject.Find("ButtonMenu") != null, "Three-dimensional parameter buttons retained");
            Require(Camera.allCameras.Count(c => c.enabled && c.targetTexture == null && c.targetDisplay == 0) == 1,
                "One enabled final display camera");
            Capture(camera, "default-player.png");
            view.SetDebugVisible(true);
            Require(canvases.All(c => c.enabled && c.GetComponent<GraphicRaycaster>().enabled),
                "Debug entry restores rendering and UI input");
            var gas = UnityEngine.Object.FindObjectOfType<IsothermalGasExperiment>();
            var transformer = UnityEngine.Object.FindObjectOfType<TransformerExperiment>();
            Require(!gas.desktopPanel.activeSelf && !transformer.desktopPanel.activeSelf,
                "Large gas and transformer panels start closed");
            gas.TogglePanel(); Require(gas.desktopPanel.activeSelf, "Gas debug panel opens");
            gas.TogglePanel(); Require(!gas.desktopPanel.activeSelf, "Gas debug panel closes");
            transformer.TogglePanel(); Require(transformer.desktopPanel.activeSelf, "Transformer debug panel opens");
            transformer.TogglePanel(); Require(!transformer.desktopPanel.activeSelf, "Transformer debug panel closes");
            view.SetDebugVisible(false);
            Require(canvases.All(c => !c.enabled && !c.GetComponent<GraphicRaycaster>().enabled),
                "Debug exit hides overlays and removes invisible click blockers");
            var rotation = rig.transform.rotation;
            foreach (var target in new[] { "GasIsothermalLab", "TransformerLab" })
            {
                var station = GameObject.Find("Objects/" + target).transform;
                rig.transform.rotation = Quaternion.LookRotation(new Vector3(station.position.x, 0, station.position.z));
                Capture(camera, target + "-player.png");
            }
            rig.transform.rotation = rotation;
            File.WriteAllText(Path.Combine(Output, "result.txt"), "PASS: desktop Play smoke check; no VR hardware tested.\n" +
                "Camera: " + camera.name + " at " + camera.transform.position + "\nRoom bounds: " + bounds);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(Path.Combine(Output, "result.txt"), "FAIL: " + error);
            EditorApplication.Exit(1);
        }
    }

    private static void Capture(Camera camera, string name)
    {
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
